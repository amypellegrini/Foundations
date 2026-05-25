using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A flat-top hex grid on the XZ plane. The tile geometry is split into a
/// rectangular array of <see cref="HexChunk"/>s, each with its own terrain
/// mesh, border mesh, and (via collaborators) decorations and fog overlays.
/// Chunking is the unit of frustum culling so the camera only pays for the
/// slice of the world it can actually see.
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

    [Header("Chunking")]
    [Tooltip("Tiles per chunk along each axis. Chunks are the granularity at " +
             "which the world is culled against the camera frustum.")]
    [Min(1)]
    public int chunkSize = 16;

    [Header("Topology")]
    [Tooltip("Wrap the world east–west, so a unit walking off the east edge " +
             "arrives at the west edge of the same row. Requires an even " +
             "number of columns: with an odd column count the odd/even " +
             "neighbor parity flips at the seam and adjacency breaks.")]
    public bool wrap = true;

    [Header("Appearance")]
    [Tooltip("Material for land tiles. This must be a project material asset, not " +
             "one created at runtime — referencing an asset is what keeps its " +
             "shader (and variants) included in player builds.")]
    public Material tileMaterial;

    [Tooltip("Material for water tiles. Only used when a MapGenerator is attached " +
             "and produces water tiles; otherwise every tile renders with tileMaterial.")]
    public Material waterMaterial;

    [Tooltip("World units that map to one full repeat of the tile texture. " +
             "Smaller = more visible texture detail per hex. Has no effect on " +
             "an untextured material.")]
    public float textureWorldScale = 3f;

    [Header("Borders")]
    [Tooltip("Material for the hex border overlay mesh. Leave unset to skip " +
             "drawing borders.")]
    public Material borderMaterial;

    [Tooltip("World-space thickness of each hex edge.")]
    public float borderWidth = 0.02f;

    [Tooltip("How far above the tile mesh the border quads sit, in world units. " +
             "Just enough to avoid z-fighting with the textured ground.")]
    public float borderHeight = 0.02f;

    HexChunk[,] chunks;
    Transform chunksRoot;
    bool isBuilt;

    /// <summary>
    /// The static terrain layer (land/water per tile). Null when no
    /// <see cref="MapGenerator"/> is attached, in which case every tile is
    /// treated as land. Generated during <see cref="EnsureBuilt"/>.
    /// </summary>
    public MapData MapData { get; private set; }

    void Awake()
    {
        // Build in Awake (not Start) so collaborators that read the chunk
        // layout in Start — HexDecorator, FogOfWar — see a fully built grid.
        EnsureBuilt();
    }

    /// <summary>
    /// Idempotent build entry point. Generates the map (if a
    /// <see cref="MapGenerator"/> is attached) and constructs the chunk
    /// hierarchy. Safe for collaborators to call from their own Awake when
    /// they need to read map data before HexGrid's Awake has run.
    /// </summary>
    public void EnsureBuilt()
    {
        if (isBuilt) return;
        isBuilt = true;

        if (wrap && (columns & 1) != 0)
        {
            Debug.LogWarning(
                $"HexGrid: wrap=true requires an even column count; got {columns}. " +
                "Odd/even neighbor parity will not match across the seam.", this);
        }

        var generator = GetComponent<MapGenerator>();
        if (generator != null)
            MapData = generator.Generate(columns, rows, HorizontalSpacing, VerticalSpacing, wrap);

        BuildChunks();
    }

    /// <summary>
    /// True if the tile at the given offset coordinates is land. Returns true
    /// for any in-bounds tile when there is no <see cref="MapData"/> — i.e.,
    /// the all-land default used when no <see cref="MapGenerator"/> is
    /// attached. Out-of-bounds tiles return false.
    /// </summary>
    public bool IsLand(int col, int row)
    {
        if (!InBounds(col, row)) return false;
        if (MapData == null) return true;
        return MapData[col, row] == TerrainType.Land;
    }

    // --- Public query API: lets units, overlays, culling sit on the grid -

    /// <summary>Horizontal distance between adjacent hex columns.</summary>
    public float HorizontalSpacing => hexSize * 1.5f;

    /// <summary>Vertical distance between adjacent hex rows.</summary>
    public float VerticalSpacing => hexSize * Mathf.Sqrt(3f);

    /// <summary>World-space width of the full grid along X — i.e., the period
    /// of the cylindrical world when <see cref="wrap"/> is on.</summary>
    public float WorldWidth => columns * HorizontalSpacing;

    /// <summary>
    /// Maps any (possibly negative or oversized) column index into the
    /// canonical range [0, <see cref="columns"/>) when <see cref="wrap"/> is
    /// on, otherwise returns the input unchanged.
    /// </summary>
    public int WrapColumn(int col)
    {
        if (!wrap || columns <= 0) return col;
        int c = col % columns;
        return c < 0 ? c + columns : c;
    }

    /// <summary>
    /// Shifts a canonical world position by ±<see cref="WorldWidth"/> so it
    /// lands in the half-world centered on <paramref name="cameraX"/> —
    /// i.e., the wrap copy the camera is currently looking at. Used by
    /// units, highlights, and path lines so they appear in the same world
    /// copy as the chunks the camera is rendering.
    /// </summary>
    public Vector3 WrapToCamera(Vector3 canonical, float cameraX)
    {
        if (!wrap || WorldWidth <= 0f) return canonical;
        float W = WorldWidth;
        float dx = cameraX - canonical.x;
        if (dx > W * 0.5f) canonical.x += W;
        else if (dx < -W * 0.5f) canonical.x -= W;
        return canonical;
    }

    /// <summary>Number of chunk columns covering the grid.</summary>
    public int ChunkColumns => Mathf.Max(1, Mathf.CeilToInt((float)columns / chunkSize));

    /// <summary>Number of chunk rows covering the grid.</summary>
    public int ChunkRows => Mathf.Max(1, Mathf.CeilToInt((float)rows / chunkSize));

    /// <summary>All chunks in row-major order, or empty before initialization.</summary>
    public IEnumerable<HexChunk> Chunks
    {
        get
        {
            if (chunks == null) yield break;
            for (int cc = 0; cc < ChunkColumns; cc++)
                for (int cr = 0; cr < ChunkRows; cr++)
                    yield return chunks[cc, cr];
        }
    }

    /// <summary>The chunk owning the tile at (col, row), or null off-grid.</summary>
    public HexChunk GetChunk(int col, int row)
    {
        if (chunks == null || !InBounds(col, row)) return null;
        return chunks[col / chunkSize, row / chunkSize];
    }

    /// <summary>
    /// World-space center of the hex at the given offset coordinates. The
    /// column is wrapped into the canonical range first when
    /// <see cref="wrap"/> is on, so HexCenter(columns, row) and HexCenter(0,
    /// row) return the same point — the canonical copy of the world.
    /// </summary>
    public Vector3 HexCenter(int col, int row)
    {
        int c = WrapColumn(col);
        float x = c * HorizontalSpacing;
        float z = row * VerticalSpacing + ((c & 1) == 1 ? VerticalSpacing * 0.5f : 0f);
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

    /// <summary>
    /// True if the given offset coordinates lie on the grid. Columns are
    /// always considered valid when <see cref="wrap"/> is on — they wrap
    /// into the canonical range. Rows always clamp; the north and south
    /// edges of the world are hard poles.
    /// </summary>
    public bool InBounds(int col, int row)
    {
        if (row < 0 || row >= rows) return false;
        return wrap || (col >= 0 && col < columns);
    }

    /// <summary>
    /// Hex distance (in tiles) between two offset coordinates, ignoring wrap.
    /// Uses the standard cube-coordinate conversion for odd-column offset
    /// layouts.
    /// </summary>
    public static int HexDistance(int colA, int rowA, int colB, int rowB)
    {
        int ax = colA;
        int az = rowA - (colA - (colA & 1)) / 2;
        int ay = -ax - az;
        int bx = colB;
        int bz = rowB - (colB - (colB & 1)) / 2;
        int by = -bx - bz;
        return (Mathf.Abs(ax - bx) + Mathf.Abs(ay - by) + Mathf.Abs(az - bz)) / 2;
    }

    /// <summary>
    /// Hex distance (in tiles) between two offset coordinates, taking the
    /// world's E–W wrap into account when <see cref="wrap"/> is on. Picks
    /// the shorter of the direct and wrap-around distances.
    /// </summary>
    public int Distance(int colA, int rowA, int colB, int rowB)
    {
        int direct = HexDistance(colA, rowA, colB, rowB);
        if (!wrap || columns <= 0) return direct;
        int east = HexDistance(colA, rowA, colB + columns, rowB);
        int west = HexDistance(colA, rowA, colB - columns, rowB);
        return Mathf.Min(direct, Mathf.Min(east, west));
    }

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
    /// grid's flat-top layout, where odd columns are nudged half a row in
    /// +Z. When <see cref="wrap"/> is on, neighbor columns wrap into the
    /// canonical range so adjacency crosses the E–W seam.
    /// </summary>
    public IEnumerable<Vector2Int> Neighbors(int col, int row)
    {
        int wc = WrapColumn(col);
        var table = ((wc & 1) == 0) ? EvenColumnNeighbors : OddColumnNeighbors;
        foreach (var d in table)
        {
            int c = WrapColumn(wc + d.x);
            int r = row + d.y;
            if (InBounds(c, r))
                yield return new Vector2Int(c, r);
        }
    }

    /// <summary>
    /// Finds the hex containing a world-space point. Computes a candidate
    /// column/row in constant time from the hex spacing, then snaps to the
    /// nearest neighboring center to handle the odd-column offset and the
    /// hexagonal corners. When <see cref="wrap"/> is on, the input point's
    /// X is reduced into the canonical world before lookup, so a point
    /// rendered in a ghost copy still resolves to its canonical tile.
    /// Returns false if the point lies outside the grid (off the row range).
    /// </summary>
    public bool TryWorldToHex(Vector3 world, out int col, out int row)
    {
        Vector3 local = transform.InverseTransformPoint(world);

        if (wrap && WorldWidth > 0f)
        {
            // Reduce X into [0, WorldWidth) so points in an east/west ghost
            // copy of the world map back to the canonical tile.
            float W = WorldWidth;
            local.x -= Mathf.Floor(local.x / W) * W;
        }

        float h = HorizontalSpacing;
        float v = VerticalSpacing;

        int approxCol = h > 0f ? Mathf.RoundToInt(local.x / h) : 0;
        float rowOffset = (approxCol & 1) == 1 ? v * 0.5f : 0f;
        int approxRow = v > 0f ? Mathf.RoundToInt((local.z - rowOffset) / v) : 0;

        col = 0;
        row = 0;
        float best = float.MaxValue;
        for (int dc = -1; dc <= 1; dc++)
        {
            for (int dr = -1; dr <= 1; dr++)
            {
                int c = approxCol + dc;
                int r = approxRow + dr;
                if (!InBounds(c, r))
                    continue;
                int cw = WrapColumn(c);
                // Compare in local space against the canonical center so wrap
                // candidates don't compete with the world-space ghost copies.
                float cx = cw * h;
                float cz = r * v + ((cw & 1) == 1 ? v * 0.5f : 0f);
                float dx = local.x - cx;
                float dz = local.z - cz;
                float d = dx * dx + dz * dz;
                if (d < best)
                {
                    best = d;
                    col = cw;
                    row = r;
                }
            }
        }
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
    /// Rebuilds every chunk's border overlay. <paramref name="include"/>, if
    /// non-null, decides which tiles should draw borders this pass — used by
    /// the fog-of-war system to hide grid lines on tiles the player cannot
    /// currently see.
    /// </summary>
    public void RebuildBorders(System.Func<int, int, bool> include)
    {
        if (chunks == null) return;
        for (int cc = 0; cc < ChunkColumns; cc++)
            for (int cr = 0; cr < ChunkRows; cr++)
                BuildBorderMesh(chunks[cc, cr], include);
    }

    // --- Chunk construction ---------------------------------------------

    void BuildChunks()
    {
        // Tear down any chunks from a previous build in this scene instance.
        Transform existing = transform.Find("Chunks");
        if (existing != null) DestroyImmediate(existing.gameObject);

        chunksRoot = new GameObject("Chunks").transform;
        chunksRoot.SetParent(transform, worldPositionStays: false);

        if (tileMaterial == null)
            Debug.LogError("HexGrid: tileMaterial is not assigned.", this);

        int cc = ChunkColumns;
        int cr = ChunkRows;
        chunks = new HexChunk[cc, cr];
        for (int x = 0; x < cc; x++)
            for (int y = 0; y < cr; y++)
                chunks[x, y] = BuildChunk(x, y);
    }

    HexChunk BuildChunk(int chunkCol, int chunkRow)
    {
        int colStart = chunkCol * chunkSize;
        int colEnd = Mathf.Min(colStart + chunkSize, columns);
        int rowStart = chunkRow * chunkSize;
        int rowEnd = Mathf.Min(rowStart + chunkSize, rows);

        var go = new GameObject($"Chunk_{chunkCol:00}_{chunkRow:00}");
        go.transform.SetParent(chunksRoot, worldPositionStays: false);

        Mesh landMesh = BuildTerrainMesh(colStart, colEnd, rowStart, rowEnd, TerrainType.Land);
        go.AddComponent<MeshFilter>().sharedMesh = landMesh;
        var mr = go.AddComponent<MeshRenderer>();
        if (tileMaterial != null) mr.sharedMaterial = tileMaterial;

        var chunk = go.AddComponent<HexChunk>();
        Bounds bounds = ChunkWorldBounds(colStart, colEnd, rowStart, rowEnd);
        chunk.Initialize(chunkCol, chunkRow, colStart, colEnd, rowStart, rowEnd, bounds, this);

        // Water rides as a sibling mesh under the same chunk GameObject. This
        // keeps it inside the chunk's frustum-culling unit (one SetActive
        // toggles both layers) and means ghost copies for E-W wrap inherit
        // the water layer for free, the same way they inherit borders.
        if (MapData != null && waterMaterial != null)
        {
            Mesh waterMesh = BuildTerrainMesh(colStart, colEnd, rowStart, rowEnd, TerrainType.Water);
            if (waterMesh.vertexCount > 0)
            {
                var wgo = new GameObject("Water");
                wgo.transform.SetParent(go.transform, worldPositionStays: false);
                wgo.AddComponent<MeshFilter>().sharedMesh = waterMesh;
                wgo.AddComponent<MeshRenderer>().sharedMaterial = waterMaterial;
            }
        }

        if (borderMaterial != null)
        {
            var bgo = new GameObject("Borders");
            bgo.transform.SetParent(go.transform, worldPositionStays: false);
            var bmf = bgo.AddComponent<MeshFilter>();
            bmf.sharedMesh = new Mesh
            {
                name = $"ChunkBorders_{chunkCol:00}_{chunkRow:00}",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            };
            var bmr = bgo.AddComponent<MeshRenderer>();
            bmr.sharedMaterial = borderMaterial;
            chunk.BorderFilter = bmf;
            BuildBorderMesh(chunk, include: null);
        }

        return chunk;
    }

    Mesh BuildTerrainMesh(int colStart, int colEnd, int rowStart, int rowEnd,
                          TerrainType terrain)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();

        float horizSpacing = HorizontalSpacing;
        float vertSpacing = VerticalSpacing;
        float uvScale = textureWorldScale > 0f ? 1f / textureWorldScale : 0f;

        for (int col = colStart; col < colEnd; col++)
        {
            for (int row = rowStart; row < rowEnd; row++)
            {
                // When MapData is null every tile is land (the default
                // before a generator is wired in), so the water pass emits
                // nothing and the land pass emits everything.
                TerrainType tileTerrain = MapData != null ? MapData[col, row] : TerrainType.Land;
                if (tileTerrain != terrain) continue;

                float x = col * horizSpacing;
                float z = row * vertSpacing + (col % 2 == 1 ? vertSpacing * 0.5f : 0f);
                AddHex(verts, tris, uvs, new Vector3(x, 0f, z), hexSize, uvScale);
            }
        }

        var mesh = new Mesh
        {
            name = $"Chunk{terrain}_{colStart}_{rowStart}",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
        };
        if (verts.Count > 0)
        {
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }
        return mesh;
    }

    void BuildBorderMesh(HexChunk chunk, System.Func<int, int, bool> include)
    {
        if (chunk == null || chunk.BorderFilter == null) return;

        var verts = new List<Vector3>();
        var tris = new List<int>();
        float halfW = borderWidth * 0.5f;
        Vector3 lift = Vector3.up * borderHeight;
        float horizSpacing = HorizontalSpacing;
        float vertSpacing = VerticalSpacing;

        for (int col = chunk.ColStart; col < chunk.ColEnd; col++)
        {
            for (int row = chunk.RowStart; row < chunk.RowEnd; row++)
            {
                // Borders are a land-only overlay: hex grid lines drawn on
                // the water make the ocean look like a tiled board.
                if (MapData != null && MapData[col, row] != TerrainType.Land)
                    continue;
                if (include != null && !include(col, row))
                    continue;
                float x = col * horizSpacing;
                float z = row * vertSpacing + (col % 2 == 1 ? vertSpacing * 0.5f : 0f);
                Vector3[] corners = HexCorners(new Vector3(x, 0f, z), hexSize);
                for (int i = 0; i < 6; i++)
                    AddBorderQuad(verts, tris, corners[i], corners[(i + 1) % 6], halfW, lift);
            }
        }

        Mesh mesh = chunk.BorderFilter.sharedMesh;
        mesh.Clear();
        if (verts.Count > 0)
        {
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }

    // Compute the chunk's world-space bounds from its tile centers, padded
    // by hexSize so corners sit comfortably inside. Y is flattened to a thin
    // slab so a 2D ground-plane intersection test ignores camera height.
    Bounds ChunkWorldBounds(int colStart, int colEnd, int rowStart, int rowEnd)
    {
        Vector3 a = HexCenter(colStart, rowStart);
        Vector3 b = HexCenter(colEnd - 1, rowEnd - 1);
        float minX = Mathf.Min(a.x, b.x) - hexSize;
        float maxX = Mathf.Max(a.x, b.x) + hexSize;
        float minZ = Mathf.Min(a.z, b.z) - hexSize;
        float maxZ = Mathf.Max(a.z, b.z) + hexSize;
        var bounds = new Bounds();
        bounds.SetMinMax(new Vector3(minX, -0.5f, minZ),
                         new Vector3(maxX, 0.5f, maxZ));
        return bounds;
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
