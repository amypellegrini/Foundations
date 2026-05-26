using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-tile visibility tier as seen by the local player.
/// </summary>
public enum TileVisibility
{
    /// <summary>Never been seen, or fully decayed. Rendered as opaque shadow.</summary>
    Unknown,

    /// <summary>Seen at some point, currently out of sight. Rendered as a
    /// dimmed overlay whose opacity grows with the freshness step
    /// (older memory = darker). After <c>decayTurns</c> ticks the tile
    /// reverts to Unknown.</summary>
    Explored,

    /// <summary>Within line of sight of a friendly unit this turn.
    /// Rendered with no overlay — full color, borders, and decor.</summary>
    Visible,
}

/// <summary>
/// Three-tier fog of war (Unknown / Explored / Visible) computed from the
/// union of all friendly units' sight radii, with Explored tiles fading
/// back to Unknown over <see cref="decayTurns"/> turn ticks. Overlay meshes
/// are split per <see cref="HexChunk"/> so fog is culled with the chunk it
/// belongs to. Drives <see cref="HexGrid.RebuildBorders"/> and
/// <see cref="HexDecorator.ApplyVisibility"/> so that grid lines and props
/// only show on Visible tiles. See <c>specs/06-fog-of-war.md</c>.
/// </summary>
[RequireComponent(typeof(HexGrid))]
public class FogOfWar : MonoBehaviour
{
    [Tooltip("Shared base material asset for every fog overlay layer. " +
             "Per-layer alpha is overridden via MaterialPropertyBlock.")]
    public Material fogMaterial;

    [Header("Decay")]
    [Tooltip("Turn ticks an Explored tile takes to fully decay back to Unknown. " +
             "10 = 10 percentage points per turn.")]
    [Range(1, 100)]
    public int decayTurns = 10;

    [Tooltip("Overlay alpha drawn on a tile that just became Explored (freshest memory).")]
    [Range(0f, 1f)]
    public float exploredAlpha = 0.55f;

    [Tooltip("Overlay alpha drawn on Unknown tiles (and the asymptote of decaying Explored).")]
    [Range(0f, 1f)]
    public float unknownAlpha = 0.95f;

    [Header("Geometry")]
    [Tooltip("World units the fog overlay sits above the ground plane. " +
             "Must exceed the grid's borderHeight so the overlay occludes borders.")]
    public float heightOffset = 0.04f;

    [Tooltip("Optional decorator whose props are hidden outside Visible tiles. " +
             "Auto-resolved from the same GameObject when left unset.")]
    public HexDecorator decorator;

    HexGrid grid;
    TileVisibility[,] state;

    // Steps elapsed since a tile became Explored. 0 = freshly Explored
    // (drawn at exploredAlpha). decayTurns = fully decayed (transitions
    // to Unknown). Only meaningful while state[c,r] == Explored.
    int[,] freshnessStep;

    // Per-chunk overlay meshes: explored layers (one per decay step) and
    // one unknown layer, parented under each chunk's GameObject so they
    // ride the chunk's culling on/off in one operation.
    readonly Dictionary<HexChunk, Mesh[]> chunkExploredMeshes =
        new Dictionary<HexChunk, Mesh[]>();
    readonly Dictionary<HexChunk, Mesh> chunkUnknownMeshes =
        new Dictionary<HexChunk, Mesh>();

    int lastUnitsHash;
    bool initialized;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        grid = GetComponent<HexGrid>();
        if (decorator == null) decorator = GetComponent<HexDecorator>();
    }

    void OnEnable()
    {
        // Defer initial mesh build to the first LateUpdate so HexGrid and
        // HexDecorator have completed their own setup first.
        initialized = false;
    }

    void LateUpdate()
    {
        if (!initialized)
        {
            InitializeOverlay();
            initialized = true;
            Recompute(force: true);
            return;
        }

        int hash = ComputeUnitsHash();
        if (hash != lastUnitsHash)
        {
            lastUnitsHash = hash;
            Recompute(force: false);
        }
    }

    void InitializeOverlay()
    {
        state = new TileVisibility[grid.columns, grid.rows];
        freshnessStep = new int[grid.columns, grid.rows];

        // Wipe any layer GameObjects left over from a previous version of
        // this component (the old non-chunked design parented Fog* directly
        // under HexGrid).
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name.StartsWith("Fog"))
                DestroyImmediate(child.gameObject);
        }

        if (fogMaterial == null)
        {
            Debug.LogError("FogOfWar: fogMaterial is not assigned.", this);
            return;
        }

        chunkExploredMeshes.Clear();
        chunkUnknownMeshes.Clear();
        foreach (var chunk in grid.Chunks)
        {
            var container = new GameObject("Fog");
            container.transform.SetParent(chunk.transform, worldPositionStays: false);

            var exploredMeshes = new Mesh[decayTurns];
            for (int i = 0; i < decayTurns; i++)
            {
                float t = (float)i / decayTurns;
                float alpha = Mathf.Lerp(exploredAlpha, unknownAlpha, t);
                exploredMeshes[i] = CreateOverlayLayer(container.transform,
                    $"FogExplored_{i:00}", alpha);
            }
            chunkExploredMeshes[chunk] = exploredMeshes;
            chunkUnknownMeshes[chunk] = CreateOverlayLayer(container.transform,
                "FogUnknown", unknownAlpha);
        }
    }

    Mesh CreateOverlayLayer(Transform parent, string name, float alpha)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, worldPositionStays: false);

        var mesh = new Mesh
        {
            name = name + "Mesh",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
        };
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = fogMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var mpb = new MaterialPropertyBlock();
        var c = new Color(0f, 0f, 0f, alpha);
        mpb.SetColor(BaseColorId, c);
        mpb.SetColor(ColorId, c);
        mr.SetPropertyBlock(mpb);

        return mesh;
    }

    int ComputeUnitsHash()
    {
        int h = 17;
        foreach (var u in FindObjectsByType<Unit>(FindObjectsSortMode.None))
        {
            // Hash on the tile under the unit's current world position so the
            // reveal tracks slide animations instead of jumping at the start.
            int c = u.col, r = u.row;
            if (grid.TryWorldToHex(u.transform.position, out int wc, out int wr))
            {
                c = wc;
                r = wr;
            }
            h = h * 31 + c * 1000 + r + u.sightRadius * 1_000_000;
        }
        return h;
    }

    void Recompute(bool force)
    {
        var nextVisible = new HashSet<Vector2Int>();
        foreach (var u in FindObjectsByType<Unit>(FindObjectsSortMode.None))
        {
            int uc = u.col, ur = u.row;
            if (grid.TryWorldToHex(u.transform.position, out int wc, out int wr))
            {
                uc = wc;
                ur = wr;
            }

            // Sweep the (2r+1)-by-(2r+1) bounding square of the unit's sight
            // radius, wrapping the column into the canonical range when wrap
            // is on — that's what lets a unit on the seam reveal tiles on
            // both sides. Rows clamp because the poles don't wrap.
            int minRow = Mathf.Max(0, ur - u.sightRadius);
            int maxRow = Mathf.Min(grid.rows - 1, ur + u.sightRadius);
            for (int dc = -u.sightRadius; dc <= u.sightRadius; dc++)
            {
                int c = grid.WrapColumn(uc + dc);
                if (!grid.wrap && (c < 0 || c >= grid.columns)) continue;
                for (int r = minRow; r <= maxRow; r++)
                {
                    if (grid.Distance(uc, ur, c, r) <= u.sightRadius)
                        nextVisible.Add(new Vector2Int(c, r));
                }
            }
        }

        bool changed = force;
        for (int c = 0; c < grid.columns; c++)
        {
            for (int r = 0; r < grid.rows; r++)
            {
                TileVisibility was = state[c, r];
                bool visible = nextVisible.Contains(new Vector2Int(c, r));

                TileVisibility now;
                if (visible)
                {
                    now = TileVisibility.Visible;
                }
                else if (was == TileVisibility.Visible)
                {
                    // Just slipped out of sight — freshly Explored.
                    now = TileVisibility.Explored;
                    freshnessStep[c, r] = 0;
                }
                else if (was == TileVisibility.Explored)
                {
                    // Decay only ticks on turn advances, not on sight
                    // recomputes — preserve current freshness.
                    now = TileVisibility.Explored;
                }
                else
                {
                    now = TileVisibility.Unknown;
                }

                if (now != was)
                {
                    state[c, r] = now;
                    changed = true;
                }
            }
        }

        if (!changed)
            return;

        StateRevision++;
        RebuildOverlayMeshes();
        grid.RebuildBorders((c, r) => state[c, r] == TileVisibility.Visible);
        if (decorator != null)
            decorator.ApplyVisibility((c, r) => state[c, r] == TileVisibility.Visible);
    }

    /// <summary>
    /// The current visibility tier of a single tile. Returns
    /// <see cref="TileVisibility.Unknown"/> before the overlay has
    /// initialized or for out-of-bounds coordinates — callers (e.g. the
    /// minimap) treat that as "not discovered yet".
    /// </summary>
    public TileVisibility GetVisibility(int col, int row)
    {
        if (state == null || grid == null) return TileVisibility.Unknown;
        if (col < 0 || col >= grid.columns || row < 0 || row >= grid.rows)
            return TileVisibility.Unknown;
        return state[col, row];
    }

    /// <summary>
    /// Monotonically increases whenever the per-tile state changes — the
    /// minimap reads this to know when to repaint its texture without
    /// having to mirror the visibility array itself.
    /// </summary>
    public int StateRevision { get; private set; }

    /// <summary>
    /// Advance the freshness clock by one tick: every Explored tile loses
    /// 10% (one step) of its freshness; tiles whose freshness hits zero
    /// transition back to Unknown. Call once per turn from the game loop.
    /// </summary>
    public void OnTurnAdvance()
    {
        if (!initialized)
            return;

        bool changed = false;
        for (int c = 0; c < grid.columns; c++)
        {
            for (int r = 0; r < grid.rows; r++)
            {
                if (state[c, r] != TileVisibility.Explored)
                    continue;

                freshnessStep[c, r]++;
                if (freshnessStep[c, r] >= decayTurns)
                {
                    state[c, r] = TileVisibility.Unknown;
                    freshnessStep[c, r] = 0;
                }
                changed = true;
            }
        }

        // Only the fog meshes change on a decay tick — tier-level visibility
        // is the same, so borders and decor don't need to be rebuilt unless
        // a tile crossed Explored → Unknown. Rebuilding unconditionally is
        // cheap (only affected chunks emit any geometry) and keeps the code
        // simple.
        if (changed)
        {
            StateRevision++;
            RebuildOverlayMeshes();
        }
    }

    void RebuildOverlayMeshes()
    {
        // Per-chunk geometry buffers reused across chunks to avoid the
        // per-tile garbage from allocating fresh lists each iteration.
        var unknownVerts = new List<Vector3>();
        var unknownTris = new List<int>();
        var exploredVerts = new List<Vector3>[decayTurns];
        var exploredTris = new List<int>[decayTurns];
        for (int i = 0; i < decayTurns; i++)
        {
            exploredVerts[i] = new List<Vector3>();
            exploredTris[i] = new List<int>();
        }

        Vector3 lift = Vector3.up * heightOffset;
        float horizSpacing = grid.HorizontalSpacing;
        float vertSpacing = grid.VerticalSpacing;

        foreach (var chunk in grid.Chunks)
        {
            unknownVerts.Clear();
            unknownTris.Clear();
            for (int i = 0; i < decayTurns; i++)
            {
                exploredVerts[i].Clear();
                exploredTris[i].Clear();
            }

            for (int col = chunk.ColStart; col < chunk.ColEnd; col++)
            {
                for (int row = chunk.RowStart; row < chunk.RowEnd; row++)
                {
                    var s = state[col, row];
                    if (s == TileVisibility.Visible)
                        continue;

                    float x = col * horizSpacing;
                    float z = row * vertSpacing + (col % 2 == 1 ? vertSpacing * 0.5f : 0f);
                    Vector3 center = new Vector3(x, 0f, z) + lift;

                    if (s == TileVisibility.Unknown)
                    {
                        AddHexFan(unknownVerts, unknownTris, center, grid.hexSize);
                    }
                    else
                    {
                        int step = Mathf.Clamp(freshnessStep[col, row], 0, decayTurns - 1);
                        AddHexFan(exploredVerts[step], exploredTris[step], center, grid.hexSize);
                    }
                }
            }

            if (chunkUnknownMeshes.TryGetValue(chunk, out var unknownMesh))
                ApplyMesh(unknownMesh, unknownVerts, unknownTris);
            if (chunkExploredMeshes.TryGetValue(chunk, out var exploredMeshes))
            {
                for (int i = 0; i < decayTurns; i++)
                    ApplyMesh(exploredMeshes[i], exploredVerts[i], exploredTris[i]);
            }
        }
    }

    static void ApplyMesh(Mesh mesh, List<Vector3> verts, List<int> tris)
    {
        mesh.Clear();
        if (verts.Count == 0)
            return;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }

    static void AddHexFan(List<Vector3> verts, List<int> tris, Vector3 center, float radius)
    {
        int centerIdx = verts.Count;
        verts.Add(center);
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.Deg2Rad * 60f * i;
            verts.Add(center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        for (int i = 0; i < 6; i++)
        {
            tris.Add(centerIdx);
            tris.Add(centerIdx + 1 + (i + 1) % 6);
            tris.Add(centerIdx + 1 + i);
        }
    }
}
