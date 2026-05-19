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

    /// <summary>Current turn number, starting at 1.</summary>
    public int Turn { get; private set; } = 1;

    /// <summary>The unit currently under the player's control, if any.</summary>
    public Unit SelectedUnit => selectedUnit;

    Unit selectedUnit;
    bool wasMoving;
    GUIStyle hudStyle;

    void Awake()
    {
        if (cam == null)
            cam = Camera.main;
    }

    void Update()
    {
        HandleClick();
        HandleSpace();

        // While a unit slides, glue its outline to the tile it is passing over.
        if (selectedUnit != null && selectedUnit.IsMoving &&
            grid.TryWorldToHex(selectedUnit.transform.position, out int c, out int r))
        {
            selectionHighlight.ShowAt(grid.HexCenter(c, r));
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
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

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
            var points = new List<Vector3>(selectedUnit.Path.Count + 1);
            points.Add(grid.HexCenter(selectedUnit.col, selectedUnit.row));
            foreach (var tile in selectedUnit.Path)
                points.Add(grid.HexCenter(tile.x, tile.y));
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
