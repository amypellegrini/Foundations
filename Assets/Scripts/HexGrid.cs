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
    [Tooltip("Material for the tiles. This must be a project material asset, not " +
             "one created at runtime — referencing an asset is what keeps its " +
             "shader (and variants) included in player builds.")]
    public Material tileMaterial;

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

        if (tileMaterial != null)
            GetComponent<MeshRenderer>().sharedMaterial = tileMaterial;
        else
            Debug.LogError("HexGrid: tileMaterial is not assigned.", this);
    }

    // --- Public query API: lets units and overlays sit on the grid -------

    /// <summary>Horizontal distance between adjacent hex columns.</summary>
    public float HorizontalSpacing => hexSize * 1.5f;

    /// <summary>Vertical distance between adjacent hex rows.</summary>
    public float VerticalSpacing => hexSize * Mathf.Sqrt(3f);

    /// <summary>World-space center of the hex at the given offset coordinates.</summary>
    public Vector3 HexCenter(int col, int row)
    {
        float x = col * HorizontalSpacing;
        float z = row * VerticalSpacing + (col % 2 == 1 ? VerticalSpacing * 0.5f : 0f);
        return transform.TransformPoint(new Vector3(x, 0f, z));
    }

    /// <summary>
    /// The six flat-top corner positions of a hexagon, ordered counter-clockwise.
    /// Matches the winding used by <see cref="AddHex"/>.
    /// </summary>
    public static Vector3[] HexCorners(Vector3 center, float radius)
    {
        var corners = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i);
            corners[i] = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }
        return corners;
    }

    /// <summary>True if the given offset coordinates lie on the grid.</summary>
    public bool InBounds(int col, int row)
        => col >= 0 && col < columns && row >= 0 && row < rows;

    static readonly Vector2Int[] EvenColumnNeighbors =
    {
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 0), new Vector2Int(1, -1),
        new Vector2Int(-1, 0), new Vector2Int(-1, -1),
    };

    static readonly Vector2Int[] OddColumnNeighbors =
    {
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, 0),
        new Vector2Int(-1, 1), new Vector2Int(-1, 0),
    };

    /// <summary>
    /// The in-bounds hex neighbors of a tile. Uses an offset table for this
    /// grid's flat-top layout, where odd columns are nudged half a row in +Z.
    /// </summary>
    public IEnumerable<Vector2Int> Neighbors(int col, int row)
    {
        var table = (col % 2 == 0) ? EvenColumnNeighbors : OddColumnNeighbors;
        foreach (var d in table)
        {
            int c = col + d.x;
            int r = row + d.y;
            if (InBounds(c, r))
                yield return new Vector2Int(c, r);
        }
    }

    /// <summary>
    /// Finds the hex containing a world-space point by nearest center.
    /// Returns false if the point lies outside the grid.
    /// </summary>
    public bool TryWorldToHex(Vector3 world, out int col, out int row)
    {
        col = 0;
        row = 0;
        float best = float.MaxValue;
        for (int c = 0; c < columns; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                Vector3 center = HexCenter(c, r);
                float dx = world.x - center.x;
                float dz = world.z - center.z;
                float d = dx * dx + dz * dz;
                if (d < best)
                {
                    best = d;
                    col = c;
                    row = r;
                }
            }
        }
        // Any point inside a hex is at most hexSize from its center.
        return best <= hexSize * hexSize;
    }

    /// <summary>
    /// Breadth-first shortest path between two tiles over uniform-cost hexes.
    /// The result is the sequence of tiles to step onto, excluding the start
    /// and ending at the goal; empty if the goal equals the start or is
    /// unreachable.
    /// </summary>
    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        var path = new List<Vector2Int>();
        if (start == goal || !InBounds(start.x, start.y) || !InBounds(goal.x, goal.y))
            return path;

        var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
        var frontier = new Queue<Vector2Int>();
        frontier.Enqueue(start);

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            if (current == goal)
                break;
            foreach (var next in Neighbors(current.x, current.y))
            {
                if (cameFrom.ContainsKey(next))
                    continue;
                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(goal))
            return path;

        for (Vector2Int node = goal; node != start; node = cameFrom[node])
            path.Add(node);
        path.Reverse();
        return path;
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
