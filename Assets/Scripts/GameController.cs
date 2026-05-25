using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drives the turn-based movement loop:
/// click a unit to select it, click a tile to project a path, then press
/// Space to spend the unit's moves. When a unit is out of moves, Space
/// advances to the next turn; units already following a path then move
/// automatically, so one tap per turn carries them along their route.
/// </summary>
public class GameController : MonoBehaviour
{
    [Tooltip("Camera used to cast click rays. Falls back to Camera.main.")]
    public Camera cam;

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
    GUIStyle hudStyle;

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

        // Place starting units before any Unit.Start fires (Awake on every
        // component runs before any Start), so SnapToHex picks up the
        // generated land tile instead of the scene's authored col/row.
        if (grid != null)
        {
            grid.EnsureBuilt();
            ScatterUnitsOnLand();
        }
    }

    void ScatterUnitsOnLand()
    {
        if (grid.MapData == null) return;

        var landTiles = new List<Vector2Int>();
        for (int c = 0; c < grid.columns; c++)
            for (int r = 0; r < grid.rows; r++)
                if (grid.MapData[c, r] == TerrainType.Land)
                    landTiles.Add(new Vector2Int(c, r));
        if (landTiles.Count == 0) return;

        Vector2Int firstPlaced = default;
        bool any = false;
        foreach (var unit in FindObjectsByType<Unit>(FindObjectsSortMode.None))
        {
            var tile = landTiles[Random.Range(0, landTiles.Count)];
            unit.col = tile.x;
            unit.row = tile.y;
            if (!any) { firstPlaced = tile; any = true; }
        }

        // Slide the camera over to the first unit's tile so the player opens
        // on their Founder instead of looking at unexplored ocean somewhere
        // else on the map.
        if (any && cam != null)
        {
            Vector3 target = grid.HexCenter(firstPlaced.x, firstPlaced.y);
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
        bool moving = selectedUnit != null && selectedUnit.IsMoving;
        if (wasMoving && !moving)
            RefreshHighlights();   // slide just finished — settle the overlays
        wasMoving = moving;
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

    // --- Game actions (also called by verification tooling) -------------

    /// <summary>Selects a unit and shows its overlays.</summary>
    public void Select(Unit unit)
    {
        selectedUnit = unit;
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
    /// The Space-bar action: move the selected unit along its path if it has
    /// both moves and a path left, otherwise advance to the next turn.
    /// </summary>
    public void Step()
    {
        if (selectedUnit == null || selectedUnit.IsMoving)
            return;

        if (selectedUnit.MovesRemaining > 0 && selectedUnit.Path.Count > 0)
        {
            selectedUnit.MoveAlongPath();
            RefreshHighlights();
        }
        else
        {
            AdvanceTurn();
        }
    }

    void AdvanceTurn()
    {
        Turn++;
        // A new turn refills moves; a unit already following a path spends
        // them straight away, so the player taps once per turn, not twice.
        foreach (var unit in FindObjectsByType<Unit>(FindObjectsSortMode.None))
        {
            unit.RefillMoves();
            if (unit.Path.Count > 0)
                unit.MoveAlongPath();
        }
        if (fogOfWar != null)
            fogOfWar.OnTurnAdvance();
        RefreshHighlights();
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

        GUILayout.BeginArea(new Rect(16, 12, 560, 180));
        GUILayout.Label($"Turn {Turn}", hudStyle);
        if (selectedUnit != null)
        {
            GUILayout.Label(
                $"{selectedUnit.unitName} — moves {selectedUnit.MovesRemaining}/{selectedUnit.movesPerTurn}"
                + $"  ·  path: {selectedUnit.Path.Count} tile(s)", hudStyle);
        }
        else
        {
            GUILayout.Label("Click the unit to select it.", hudStyle);
        }
        GUILayout.Label("Click a tile to set a destination  ·  Space to move / advance turn", hudStyle);
        GUILayout.EndArea();
    }
}
