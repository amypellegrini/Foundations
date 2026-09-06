using UnityEngine;

/// <summary>
/// A white hex-shaped outline that snaps onto any hex tile to highlight it.
/// Drawn with a <see cref="LineRenderer"/> so it needs no extra mesh geometry.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class HexHighlight : MonoBehaviour
{
    [Tooltip("Radius of the outline. Match the grid's hex size to hug the tile.")]
    public float radius = 1f;

    [Tooltip("Height above the tile plane, to avoid z-fighting with the grid.")]
    public float heightOffset = 0.02f;

    [Tooltip("Thickness of the outline.")]
    public float lineWidth = 0.08f;

    [Tooltip("Material asset for the outline. A material asset (not a runtime " +
             "Shader.Find) keeps the shader in player builds.")]
    public Material borderMaterial;

    LineRenderer line;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.loop = true;
        line.positionCount = 6;
        line.useWorldSpace = true;
        line.widthMultiplier = lineWidth;
        line.numCornerVertices = 0;
        line.numCapVertices = 0;
        line.startColor = line.endColor = Color.white;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (borderMaterial != null)
            line.sharedMaterial = borderMaterial;
        Hide();
    }

    /// <summary>Snaps the outline onto the tile occupied by the given unit.
    /// On a wrapping grid the outline tracks the wrap copy currently visible
    /// to the camera, matching where the unit is actually drawn.</summary>
    public void ShowAt(Unit unit)
    {
        if (unit == null || unit.grid == null) return;
        Vector3 center = unit.grid.HexCenter(unit.col, unit.row);
        Camera cam = Camera.main;
        if (cam != null)
            center = unit.grid.WrapToCamera(center, cam.transform.position.x);
        ShowAt(center);
    }

    /// <summary>Snaps the outline onto the hex tile at the given world center.</summary>
    public void ShowAt(Vector3 hexCenter)
    {
        Vector3 center = hexCenter + Vector3.up * heightOffset;
        line.SetPositions(HexGrid.HexCorners(center, radius));
        line.enabled = true;
    }

    /// <summary>Hides the outline.</summary>
    public void Hide()
    {
        line.enabled = false;
    }
}
