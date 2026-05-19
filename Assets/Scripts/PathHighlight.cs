using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws a unit's projected movement path as a polyline through hex tile
/// centers, using a <see cref="LineRenderer"/>.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PathHighlight : MonoBehaviour
{
    [Tooltip("Thickness of the route line.")]
    public float lineWidth = 0.12f;

    [Tooltip("Height above the tile plane, to avoid z-fighting with the grid.")]
    public float heightOffset = 0.06f;

    [Tooltip("Material asset for the route line. A material asset (not a runtime " +
             "Shader.Find) keeps the shader in player builds.")]
    public Material lineMaterial;

    LineRenderer line;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = lineWidth;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.startColor = line.endColor = Color.white;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (lineMaterial != null)
            line.sharedMaterial = lineMaterial;
        Hide();
    }

    /// <summary>Draws the route through the given world-space points.</summary>
    public void Show(IList<Vector3> points)
    {
        if (points == null || points.Count < 2)
        {
            Hide();
            return;
        }
        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            line.SetPosition(i, points[i] + Vector3.up * heightOffset);
        line.enabled = true;
    }

    /// <summary>Hides the route line.</summary>
    public void Hide()
    {
        line.enabled = false;
    }
}
