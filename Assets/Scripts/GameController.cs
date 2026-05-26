using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drives the turn-based action loop. Each unit gets one action per turn —
/// either move (along a previously-clicked path) or skip. Pressing Space
/// commits the selected unit's action and auto-advances focus to the next
/// unit that still has an action available; once every unit is spent, the
/// next Space press starts the following turn. Clicking any unit at any
/// time overrides the auto-cycle, so the player can act in any order.
/// </summary>
public class GameController : MonoBehaviour
{
    [Tooltip("Camera used to cast click rays. Falls back to Camera.main.")]
    public Camera cam;

    [Tooltip("Camera pan controller. Used to auto-follow the selected unit " +
             "and re-center on it at the start of each turn. If left unset, " +
             "GameController grabs one off the cam GameObject.")]
    public CameraPan cameraPan;

    [Tooltip("The hex grid units move across.")]
    public HexGrid grid;

    [Tooltip("White outline marking the selected unit's tile.")]
    public HexHighlight selectionHighlight;

    [Tooltip("Route line showing the selected unit's projected path.")]
    public PathHighlight pathHighlight;

    [Tooltip("Fog-of-war system; ticked once per turn so Explored memory decays.")]
    public FogOfWar fogOfWar;

    /// <summary>Current turn number, starting at 1.</summary>
    public int Turn { get; private set; } = 1;

    /// <summary>The unit currently under the player's control, if any.</summary>
    public Unit SelectedUnit => selectedUnit;

    Unit selectedUnit;
    bool wasMoving;
    // True when AdvanceTurn fired with auto-walks still in flight, so the
    // turn-restart focus reset must be deferred until those slides finish.
    bool pendingTurnRestart;
    // Units whose queued paths still need to be auto-walked this turn.
    // Drained one entry at a time by LateUpdate so the slides play
    // sequentially — never two units sliding at once.
    readonly Queue<Unit> pendingAutoWalks = new Queue<Unit>();
    GUIStyle hudStyle;

    // Stable, ordered list of all units. Built in Awake so the turn cycle
    // has a deterministic ordering independent of Unity's object discovery
    // order. Sorted by unitName so "Founder" precedes "Hunter".
    readonly List<Unit> units = new List<Unit>();

    // Position of the cursor when the left mouse button went down. On
    // release, the click only commits if the cursor hasn't moved past the
    // shared drag threshold — that way a left-click drag handled by
    // CameraPan doesn't also trigger a unit selection or move order.
    Vector2 pressScreen;
    bool pressing;

    void Awake()
    {
        if (cam == null)
            cam = Camera.main;
        if (cameraPan == null && cam != null)
            cameraPan = cam.GetComponent<CameraPan>();

        // Place starting units before any Unit.Start fires (Awake on every
        // component runs before any Start), so SnapToHex picks up the
        // generated land tile instead of the scene's authored col/row.
        if (grid != null)
        {
            grid.EnsureBuilt();
            units.Clear();
            units.AddRange(FindObjectsByType<Unit>(FindObjectsSortMode.None));
            units.Sort((a, b) => string.Compare(a.unitName, b.unitName, StringComparison.Ordinal));
            PlaceFoundingParty();
        }
    }

    void Start()
    {
        // Open the game with the first unit selected so Space immediately
        // acts on something instead of doing nothing until a click.
        var first = FindNextUnit(null);
        if (first != null)
            Select(first);
    }

    /// <summary>
    /// Drops the founding party — every starting unit — onto a contiguous
    /// cluster of land tiles so they begin the game next to each other. The
    /// first unit lands on the anchor tile; the remaining units fill in
    /// adjacent land neighbors. Falls back gracefully if the chosen anchor
    /// doesn't have enough adjacent land for everyone.
    /// </summary>
    void PlaceFoundingParty()
    {
        if (grid.MapData == null || units.Count == 0) return;

        // Anchor candidates: land tiles with at least one land neighbor, so
        // there's always room for a Hunter (or any second unit) next door.
        var candidates = new List<Vector2Int>();
        for (int c = 0; c < grid.columns; c++)
        {
            for (int r = 0; r < grid.rows; r++)
            {
                if (grid.MapData[c, r] != TerrainType.Land) continue;
                foreach (var n in grid.Neighbors(c, r))
                {
                    if (grid.IsLand(n.x, n.y))
                    {
                        candidates.Add(new Vector2Int(c, r));
                        break;
                    }
                }
            }
        }
        if (candidates.Count == 0) return;

        var anchor = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        // Place the first unit on the anchor.
        units[0].col = anchor.x;
        units[0].row = anchor.y;

        // Spread the rest across adjacent land tiles. For a 2-unit founding
        // party this just places the second unit next to the first; the
        // loop is here so the routine still works if more starting units
        // are added later.
        var occupied = new HashSet<Vector2Int> { anchor };
        for (int i = 1; i < units.Count; i++)
        {
            Vector2Int? spot = null;
            foreach (var n in grid.Neighbors(units[i - 1].col, units[i - 1].row))
            {
                if (occupied.Contains(n)) continue;
                if (!grid.IsLand(n.x, n.y)) continue;
                spot = n;
                break;
            }
            // If the previous unit had no free land neighbors, fall back to
            // the anchor's neighbors.
            if (spot == null)
            {
                foreach (var n in grid.Neighbors(anchor.x, anchor.y))
                {
                    if (occupied.Contains(n)) continue;
                    if (!grid.IsLand(n.x, n.y)) continue;
                    spot = n;
                    break;
                }
            }
            if (spot == null) break;
            units[i].col = spot.Value.x;
            units[i].row = spot.Value.y;
            occupied.Add(spot.Value);
        }

        // Slide the camera over to the founding party so the player opens
        // on their units instead of looking at unexplored ocean somewhere
        // else on the map.
        if (cam != null)
        {
            Vector3 target = grid.HexCenter(anchor.x, anchor.y);
            Vector3 p = cam.transform.position;
            Vector3 fwd = cam.transform.forward;
            // Compensate for a tilted camera: its ground look-at point sits
            // offset from its transform position along forward. Without this,
            // a tilted-down camera lands so its look-at is far from the unit.
            float tx = 0f, tz = 0f;
            if (p.y > 0f && fwd.y < -0.0001f)
            {
                float t = -p.y / fwd.y;
                tx = fwd.x * t;
                tz = fwd.z * t;
            }
            cam.transform.position = new Vector3(target.x - tx, p.y, target.z - tz);
        }
    }

    void Update()
    {
        HandleClick();
        HandleSpace();
        HandleTab();

        // While a unit slides, glue its outline to the tile it is passing over.
        if (selectedUnit != null && selectedUnit.IsMoving &&
            grid.TryWorldToHex(selectedUnit.transform.position, out int c, out int r))
        {
            Vector3 center = grid.HexCenter(c, r);
            // Match the wrap copy the unit is currently displayed in, so the
            // outline doesn't snap back to canonical X when the camera is
            // looking at the unit through a ghost.
            center = grid.WrapToCamera(center, selectedUnit.transform.position.x);
            selectionHighlight.ShowAt(center);
        }
    }

    void LateUpdate()
    {
        // Watch every unit. Focus transitions must wait for any slide to
        // finish, and the auto-walk queue runs strictly one unit at a time.
        bool moving = AnyUnitMoving();
        if (wasMoving && !moving)
            RefreshHighlights();

        // All focus transitions are gated on "nothing is animating" so the
        // selection outline, camera follow, and HUD don't jump to the next
        // unit while the previous one is still mid-slide.
        if (!moving)
        {
            // Drain queued auto-walks first. Each pop starts its own slide
            // and re-enters LateUpdate's wait loop until completion, so
            // units never animate simultaneously.
            if (pendingAutoWalks.Count > 0)
            {
                StartNextAutoWalk();
            }
            else if (pendingTurnRestart)
            {
                pendingTurnRestart = false;
                ApplyTurnRestart();
            }
            else if (selectedUnit != null && selectedUnit.HasActed)
            {
                AdvanceFocusOrTurn();
            }
        }

        wasMoving = moving;
    }

    bool AnyUnitMoving()
    {
        foreach (var u in units)
            if (u != null && u.IsMoving) return true;
        return false;
    }

    // --- Input ----------------------------------------------------------

    void HandleClick()
    {
        var mouse = Mouse.current;
        if (mouse == null)
            return;

        // Record the press, but don't commit anything until release. Firing
        // on press would (a) be triggered by BestMouseKeys' numpad-0
        // drag-start, and (b) collide with the left-click drag the camera
        // owns. We fall through without returning so a synthesized click
        // that reports press+release in the same frame (e.g. BestMouseKeys
        // numpad-5) still commits this frame.
        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressScreen = mouse.position.ReadValue();
            pressing = true;
        }

        if (!mouse.leftButton.wasReleasedThisFrame || !pressing)
            return;

        pressing = false;

        Vector2 releaseScreen = mouse.position.ReadValue();
        // Same threshold CameraPan uses to activate a drag — if the cursor
        // moved at least that far, this gesture was a pan, not a click.
        float sqr = (releaseScreen - pressScreen).sqrMagnitude;
        if (sqr >= CameraPan.DragActivationPixels * CameraPan.DragActivationPixels)
            return;

        Ray ray = cam.ScreenPointToRay(releaseScreen);

        // A click on a unit selects it.
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var unit = hit.collider.GetComponentInParent<Unit>();
            if (unit != null)
            {
                Select(unit);
                return;
            }
        }

        // Otherwise a click on the ground sets the selected unit's destination.
        var ground = new Plane(Vector3.up, grid.transform.position);
        if (ground.Raycast(ray, out float dist))
        {
            Vector3 point = ray.GetPoint(dist);
            if (selectedUnit != null && grid.TryWorldToHex(point, out int c, out int r))
                OrderMoveTo(new Vector2Int(c, r));
        }
    }

    void HandleSpace()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            Step();
    }

    void HandleTab()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            CycleFocus();
    }

    // --- Game actions (also called by verification tooling) -------------

    /// <summary>Selects a unit and shows its overlays.</summary>
    public void Select(Unit unit)
    {
        if (selectedUnit != null && selectedUnit != unit)
            selectedUnit.SetSelected(false);
        selectedUnit = unit;
        if (unit != null)
            unit.SetSelected(true);
        if (cameraPan != null)
            cameraPan.followUnit = unit;
        RefreshHighlights();
    }

    /// <summary>Projects a path for the selected unit to the given tile.</summary>
    public void OrderMoveTo(Vector2Int target)
    {
        if (selectedUnit == null)
            return;
        selectedUnit.SetPath(grid.FindPath(selectedUnit.Coord, target));
        RefreshHighlights();
    }

    /// <summary>
    /// The Space-bar action. Commits the selected unit's action for this
    /// turn: walk along its queued path if one is pending, otherwise skip.
    /// Once the unit has no further action this turn, focus jumps to the
    /// next unit that still has one — or, if none remain, the turn rolls
    /// over.
    /// </summary>
    public void Step()
    {
        if (units.Count == 0) return;

        if (selectedUnit == null)
        {
            var next = FindNextUnit(null);
            if (next != null) Select(next);
            else AdvanceTurn();
            return;
        }

        // Block every action — move, skip, cycle — while anything is
        // sliding, so the player can't kick off a second move on top of
        // an in-flight one or an auto-walk in the queue.
        if (AnyUnitMoving())
            return;

        if (!selectedUnit.HasActed)
        {
            if (selectedUnit.MovesRemaining > 0 && selectedUnit.Path.Count > 0)
            {
                // Move action: walk along the queued path. Focus only
                // advances once the unit has actually consumed every
                // move; partial moves keep the unit selected so the
                // player can click another destination and continue.
                // Hand-off itself lands in LateUpdate once the slide
                // finishes, which keeps the camera follow tidy.
                selectedUnit.MoveAlongPath();
                RefreshHighlights();
            }
            else
            {
                // Skip action: this unit forfeits its remaining moves and
                // focus advances to the next unit in the cycle.
                selectedUnit.Skip();
                AdvanceFocusOrTurn();
            }
        }
        else
        {
            // Already spent — Space cycles past it.
            AdvanceFocusOrTurn();
        }
    }

    /// <summary>
    /// Cycle focus to the next unit in the ordered list regardless of
    /// whether it has acted this turn — TAB is a pure review/selection
    /// helper, separate from the Space action loop. Blocks while anything
    /// is animating so a cycle can't interrupt a slide.
    /// </summary>
    public void CycleFocus()
    {
        if (units.Count == 0) return;
        if (AnyUnitMoving()) return;

        var next = FindNextAnyUnit(selectedUnit);
        if (next == null || next == selectedUnit) return;

        Select(next);
        if (cameraPan != null)
            cameraPan.CenterOn(next.transform.position);
    }

    /// <summary>
    /// The next non-null unit after <paramref name="after"/> in the ordered
    /// list, wrapping around. Unlike <see cref="FindNextUnit"/> this ignores
    /// <c>HasActed</c> so TAB can revisit any unit at any time.
    /// </summary>
    Unit FindNextAnyUnit(Unit after)
    {
        if (units.Count == 0) return null;
        int startIndex = after != null ? units.IndexOf(after) : -1;
        for (int step = 1; step <= units.Count; step++)
        {
            int idx = (startIndex + step) % units.Count;
            if (idx < 0) idx += units.Count;
            var u = units[idx];
            if (u != null) return u;
        }
        return null;
    }

    /// <summary>
    /// Move the player to the next unit with an action available, or roll
    /// the turn over if every unit has acted.
    /// </summary>
    void AdvanceFocusOrTurn()
    {
        var next = FindNextUnit(selectedUnit);
        if (next != null)
            Select(next);
        else
            AdvanceTurn();
    }

    /// <summary>
    /// The next unit after <paramref name="after"/> in cycle order that
    /// still has an action available this turn, or null if every unit is
    /// spent. Wraps around the list and skips the starting unit itself.
    /// </summary>
    Unit FindNextUnit(Unit after)
    {
        if (units.Count == 0) return null;
        int startIndex = after != null ? units.IndexOf(after) : -1;
        for (int step = 1; step <= units.Count; step++)
        {
            int idx = (startIndex + step) % units.Count;
            if (idx < 0) idx += units.Count;
            var u = units[idx];
            if (u != null && !u.HasActed)
                return u;
        }
        return null;
    }

    void AdvanceTurn()
    {
        Turn++;
        // Refill moves for everyone. Units that still hold a path from the
        // previous turn don't walk it straight away — instead each one is
        // queued for a sequential auto-walk so the slides play one unit at
        // a time, in their own focus cycle.
        foreach (var unit in units)
        {
            if (unit == null) continue;
            unit.RefillMoves();
        }
        if (fogOfWar != null)
            fogOfWar.OnTurnAdvance();

        pendingAutoWalks.Clear();
        foreach (var unit in units)
        {
            if (unit != null && unit.Path.Count > 0)
                pendingAutoWalks.Enqueue(unit);
        }

        if (pendingAutoWalks.Count > 0)
        {
            // Defer the post-walk focus reset until the whole queue has
            // drained. Start the first one now; LateUpdate kicks off the
            // rest in order, each only after the previous slide ends.
            pendingTurnRestart = true;
            StartNextAutoWalk();
        }
        else
        {
            ApplyTurnRestart();
        }
        RefreshHighlights();
    }

    /// <summary>
    /// Pop the next queued auto-walk, focus on the unit, and kick off its
    /// slide. Skips entries whose pending path has since been drained.
    /// </summary>
    void StartNextAutoWalk()
    {
        while (pendingAutoWalks.Count > 0)
        {
            var unit = pendingAutoWalks.Dequeue();
            if (unit == null) continue;
            if (unit.Path.Count == 0 || unit.MovesRemaining == 0) continue;

            // Focus moves to the unit that's about to walk so the player
            // can see what's happening — camera centers and the HUD
            // marker tracks it.
            Select(unit);
            if (cameraPan != null)
                cameraPan.CenterOn(unit.transform.position);
            unit.MoveAlongPath();
            RefreshHighlights();
            return;
        }
    }

    /// <summary>
    /// Restart the per-turn cycle on the first un-acted unit, re-centering
    /// the camera so the player doesn't scroll-hunt after spending the
    /// previous turn elsewhere on the map.
    /// </summary>
    void ApplyTurnRestart()
    {
        var first = FindNextUnit(null);
        if (first == null) return;
        Select(first);
        if (cameraPan != null)
            cameraPan.CenterOn(first.transform.position);
    }

    // --- Overlays --------------------------------------------------------

    void RefreshHighlights()
    {
        if (selectedUnit == null)
        {
            selectionHighlight.Hide();
            pathHighlight.Hide();
            return;
        }

        selectionHighlight.ShowAt(selectedUnit);

        if (!selectedUnit.IsMoving && selectedUnit.Path.Count > 0)
        {
            // Build the polyline in continuous world coordinates so a route
            // that crosses the E-W seam draws as a straight line east/west
            // through the ghost copies, not a sudden snap back across the
            // whole map. Each waypoint is offset by ±WorldWidth as needed
            // to stay close to the previous point.
            var points = new List<Vector3>(selectedUnit.Path.Count + 1);
            float cameraX = cam != null ? cam.transform.position.x : 0f;
            Vector3 prev = grid.HexCenter(selectedUnit.col, selectedUnit.row);
            prev = grid.WrapToCamera(prev, cameraX);
            points.Add(prev);
            foreach (var tile in selectedUnit.Path)
            {
                Vector3 p = grid.HexCenter(tile.x, tile.y);
                if (grid.wrap)
                {
                    float W = grid.WorldWidth;
                    float dx = p.x - prev.x;
                    while (dx > W * 0.5f) { p.x -= W; dx -= W; }
                    while (dx < -W * 0.5f) { p.x += W; dx += W; }
                }
                points.Add(p);
                prev = p;
            }
            pathHighlight.Show(points);
        }
        else
        {
            pathHighlight.Hide();
        }
    }

    // --- HUD -------------------------------------------------------------

    void OnGUI()
    {
        hudStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            normal = { textColor = Color.white },
        };

        GUILayout.BeginArea(new Rect(16, 12, 640, 220));
        GUILayout.Label($"Turn {Turn}", hudStyle);

        // Compact per-unit status line so the player can see who has acted
        // and who is still waiting. Selected unit gets a leading marker.
        foreach (var unit in units)
        {
            if (unit == null) continue;
            string marker = unit == selectedUnit ? "▶ " : "   ";
            string status = unit.HasActed ? "done" :
                $"{unit.MovesRemaining}/{unit.movesPerTurn} moves" +
                (unit.Path.Count > 0 ? $" · path {unit.Path.Count}" : "");
            GUILayout.Label($"{marker}{unit.unitName} — {status}", hudStyle);
        }

        if (selectedUnit == null)
            GUILayout.Label("Click a unit to select it.", hudStyle);
        GUILayout.Label("Click a tile to set a destination  ·  Space to move / skip / next turn  ·  Tab to cycle units", hudStyle);
        GUILayout.EndArea();
    }
}
