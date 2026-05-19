using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a flat-top hex grid as a single procedural mesh on the XZ plane.
/// First visible slice of the Foundations map: just the tiles, nothing else.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexGrid : MonoBehaviour
{
    [Header("Grid dimensions (offset coordinates)")]
    public int columns = 12;
    public int rows = 8;

    [Header("Hex geometry")]
    [Tooltip("Distance from a hex center to one of its corners.")]
    public float hexSize = 1f;

    [Tooltip("Fraction each hex is shrunk to leave a visible gap between tiles.")]
    [Range(0f, 0.2f)]
    public float gap = 0.05f;

    [Header("Appearance")]
    public Color tileColor = new Color(0.45f, 0.55f, 0.38f);

    void Start()
    {
        BuildGrid();
    }

    void BuildGrid()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        float drawSize = hexSize * (1f - gap);

        // Flat-top hex spacing: columns step horizontally, every odd column
        // is nudged half a row down so the hexes interlock.
        float horizSpacing = hexSize * 1.5f;
        float vertSpacing = hexSize * Mathf.Sqrt(3f);

        for (int col = 0; col < columns; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                float x = col * horizSpacing;
                float z = row * vertSpacing + (col % 2 == 1 ? vertSpacing * 0.5f : 0f);
                AddHex(vertices, triangles, new Vector3(x, 0f, z), drawSize);
            }
        }

        var mesh = new Mesh { name = "HexGrid" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;

        // Unlit keeps tile color predictable — the hexes are flat, so lighting
        // would only flatten the color anyway.
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader) { name = "HexTile" };
        material.SetColor("_BaseColor", tileColor);
        GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    /// <summary>
    /// Adds one flat-top hexagon (a triangle fan around its center) lying
    /// on the XZ plane, wound so the face points up (+Y).
    /// </summary>
    static void AddHex(List<Vector3> verts, List<int> tris, Vector3 center, float size)
    {
        int centerIndex = verts.Count;
        verts.Add(center);

        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i);
            verts.Add(center + new Vector3(Mathf.Cos(angle) * size, 0f, Mathf.Sin(angle) * size));
        }

        for (int i = 0; i < 6; i++)
        {
            tris.Add(centerIndex);
            tris.Add(centerIndex + 1 + (i + 1) % 6);
            tris.Add(centerIndex + 1 + i);
        }
    }
}
