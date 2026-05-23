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

    [Header("Appearance")]
    [Tooltip("Material for the tiles. This must be a project material asset, not " +
             "one created at runtime — referencing an asset is what keeps its " +
             "shader (and variants) included in player builds.")]
    public Material tileMaterial;

    [Tooltip("World units that map to one full repeat of the tile texture. " +
             "Smaller = more visible texture detail per hex. Has no effect on " +
             "an untextured material.")]
    public float textureWorldScale = 3f;

    [Header("Borders")]
    [Tooltip("Material for the hex border overlay mesh. Leave unset to skip " +
             "drawing borders.")]
    public Material borderMaterial;

    [Tooltip("World-space thickness of each hex edge.")]
    public float borderWidth = 0.04f;

    [Tooltip("How far above the tile mesh the border quads sit, in world units. " +
             "Just enough to avoid z-fighting with the textured ground.")]
    public float borderHeight = 0.02f;

    void Start()
    {
        BuildGrid();
        BuildBorders();
    }

    void BuildGrid()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uvs = new List<Vector2>();

        // Flat-top hex spacing: columns step horizontally, every odd column
        // is nudged half a row down so the hexes interlock.
        float horizSpacing = hexSize * 1.5f;
        float vertSpacing = hexSize * Mathf.Sqrt(3f);

        // World-position planar UVs let a tileable texture flow continuously
        // across hex boundaries instead of restarting per tile.
        float uvScale = textureWorldScale > 0f ? 1f / textureWorldScale : 0f;

        for (int col = 0; col < columns; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                float x = col * horizSpacing;
                float z = row * vertSpacing + (col % 2 == 1 ? vertSpacing * 0.5f : 0f);
                AddHex(vertices, triangles, uvs, new Vector3(x, 0f, z), hexSize, uvScale);
            }
        }

        var mesh = new Mesh { name = "HexGrid" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
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
    /// Builds a second, overlay mesh containing thin quads along every hex
    /// edge, drawn slightly above the ground plane with
    /// <see cref="borderMaterial"/>. Shared edges between neighboring hexes
    /// are drawn twice — the overdraw is cheap and the alternative (edge
    /// deduplication) isn't worth the complexity for a static grid.
    /// </summary>
    void BuildBorders()
    {
        Transform existing = transform.Find("HexBorders");
        if (borderMaterial == null)
        {
            if (existing != null) DestroyImmediate(existing.gameObject);
            return;
        }

        var verts = new List<Vector3>();
        var tris = new List<int>();
        float halfW = borderWidth * 0.5f;
        Vector3 lift = Vector3.up * borderHeight;
        float horizSpacing = hexSize * 1.5f;
        float vertSpacing = hexSize * Mathf.Sqrt(3f);

        for (int col = 0; col < columns; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                float x = col * horizSpacing;
                float z = row * vertSpacing + (col % 2 == 1 ? vertSpacing * 0.5f : 0f);
                Vector3[] corners = HexCorners(new Vector3(x, 0f, z), hexSize);
                for (int i = 0; i < 6; i++)
                    AddBorderQuad(verts, tris, corners[i], corners[(i + 1) % 6], halfW, lift);
            }
        }

        var mesh = new Mesh { name = "HexBorders" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Transform t = existing;
        if (t == null)
        {
            var go = new GameObject("HexBorders");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            t = go.transform;
        }
        t.GetComponent<MeshFilter>().sharedMesh = mesh;
        t.GetComponent<MeshRenderer>().sharedMaterial = borderMaterial;
    }

    // Builds a thin rectangular quad along the edge a -> b, offset by
    // halfW perpendicular to the edge on the XZ plane, and lifted by `lift`
    // above the ground so it sits proud of the textured tiles.
    static void AddBorderQuad(List<Vector3> verts, List<int> tris,
                              Vector3 a, Vector3 b, float halfW, Vector3 lift)
    {
        Vector3 edge = b - a;
        Vector3 perp = new Vector3(-edge.z, 0f, edge.x).normalized * halfW;

        int baseIdx = verts.Count;
        verts.Add(a + perp + lift);
        verts.Add(b + perp + lift);
        verts.Add(b - perp + lift);
        verts.Add(a - perp + lift);

        tris.Add(baseIdx);
        tris.Add(baseIdx + 1);
        tris.Add(baseIdx + 2);
        tris.Add(baseIdx);
        tris.Add(baseIdx + 2);
        tris.Add(baseIdx + 3);
    }

    /// <summary>
    /// Adds one flat-top hexagon (a triangle fan around its center) lying
    /// on the XZ plane, wound so the face points up (+Y). UVs are emitted
    /// from each vertex's world XZ scaled by <paramref name="uvScale"/>, so
    /// a tileable texture wraps continuously across neighboring hexes.
    /// </summary>
    static void AddHex(List<Vector3> verts, List<int> tris, List<Vector2> uvs,
                       Vector3 center, float size, float uvScale)
    {
        int centerIndex = verts.Count;
        verts.Add(center);
        uvs.Add(new Vector2(center.x * uvScale, center.z * uvScale));

        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i);
            Vector3 corner = center + new Vector3(
                Mathf.Cos(angle) * size, 0f, Mathf.Sin(angle) * size);
            verts.Add(corner);
            uvs.Add(new Vector2(corner.x * uvScale, corner.z * uvScale));
        }

        for (int i = 0; i < 6; i++)
        {
            tris.Add(centerIndex);
            tris.Add(centerIndex + 1 + (i + 1) % 6);
            tris.Add(centerIndex + 1 + i);
        }
    }
}
