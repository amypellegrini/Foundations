using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-tile visibility tier as seen by the local player.
/// </summary>
public enum TileVisibility
{
    /// <summary>Never been seen. Rendered as opaque shadow.</summary>
    Unknown,

    /// <summary>Seen at some point, not currently visible. Rendered as
    /// dimmed ground without grid borders or decorations.</summary>
    Explored,

    /// <summary>Within line of sight of a friendly unit this turn.
    /// Rendered with no overlay — full color, borders, and decor.</summary>
    Visible,
}

/// <summary>
/// Three-tier fog of war (Unknown / Explored / Visible) computed from the
/// union of all friendly units' sight radii. Renders the Unknown and
/// Explored tiers as two overlay meshes layered above the hex grid, and
/// drives <see cref="HexGrid.RebuildBorders"/> and
/// <see cref="HexDecorator.ApplyVisibility"/> so that grid lines and props
/// only show on Visible tiles. See <c>specs/06-fog-of-war.md</c>.
/// </summary>
[RequireComponent(typeof(HexGrid))]
public class FogOfWar : MonoBehaviour
{
    [Header("Overlay materials")]
    [Tooltip("Material asset for the near-opaque overlay covering Unknown tiles.")]
    public Material unknownMaterial;

    [Tooltip("Material asset for the translucent overlay covering Explored tiles.")]
    public Material exploredMaterial;

    [Header("Geometry")]
    [Tooltip("World units the fog overlay sits above the ground plane. " +
             "Must exceed the grid's borderHeight so the overlay occludes borders.")]
    public float heightOffset = 0.04f;

    [Tooltip("Optional decorator whose props are hidden outside Visible tiles. " +
             "Auto-resolved from the same GameObject when left unset.")]
    public HexDecorator decorator;

    HexGrid grid;
    TileVisibility[,] state;
    Mesh unknownMesh;
    Mesh exploredMesh;
    int lastUnitsHash;
    bool initialized;

    void Awake()
    {
        grid = GetComponent<HexGrid>();
        if (decorator == null) decorator = GetComponent<HexDecorator>();
    }

    void OnEnable()
    {
        // Defer initial mesh build to the first LateUpdate so HexGrid and
        // HexDecorator have completed their own Start() work first.
        initialized = false;
    }

    void LateUpdate()
    {
        if (!initialized)
        {
            state = new TileVisibility[grid.columns, grid.rows];
            CreateOverlayChild("FogUnknown", unknownMaterial, out unknownMesh);
            CreateOverlayChild("FogExplored", exploredMaterial, out exploredMesh);
            Recompute(force: true);
            initialized = true;
            return;
        }

        int hash = ComputeUnitsHash();
        if (hash != lastUnitsHash)
        {
            lastUnitsHash = hash;
            Recompute(force: false);
        }
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

            // Sight radius is small; brute-force the bounding box rather
            // than introducing a separate hex iterator.
            int minCol = Mathf.Max(0, uc - u.sightRadius);
            int maxCol = Mathf.Min(grid.columns - 1, uc + u.sightRadius);
            int minRow = Mathf.Max(0, ur - u.sightRadius);
            int maxRow = Mathf.Min(grid.rows - 1, ur + u.sightRadius);
            for (int c = minCol; c <= maxCol; c++)
                for (int r = minRow; r <= maxRow; r++)
                    if (HexGrid.HexDistance(uc, ur, c, r) <= u.sightRadius)
                        nextVisible.Add(new Vector2Int(c, r));
        }

        bool changed = force;
        for (int c = 0; c < grid.columns; c++)
        {
            for (int r = 0; r < grid.rows; r++)
            {
                TileVisibility was = state[c, r];
                TileVisibility now;
                if (nextVisible.Contains(new Vector2Int(c, r)))
                    now = TileVisibility.Visible;
                else if (was == TileVisibility.Unknown)
                    now = TileVisibility.Unknown;
                else
                    now = TileVisibility.Explored;
                if (now != was)
                {
                    state[c, r] = now;
                    changed = true;
                }
            }
        }

        if (!changed)
            return;

        BuildOverlayMesh(unknownMesh, TileVisibility.Unknown);
        BuildOverlayMesh(exploredMesh, TileVisibility.Explored);

        grid.RebuildBorders((c, r) => state[c, r] == TileVisibility.Visible);

        if (decorator != null)
            decorator.ApplyVisibility((c, r) => state[c, r] == TileVisibility.Visible);
    }

    void CreateOverlayChild(string name, Material mat, out Mesh mesh)
    {
        Transform existing = transform.Find(name);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
        }

        mesh = new Mesh
        {
            name = name + "Mesh",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
        };
        go.GetComponent<MeshFilter>().sharedMesh = mesh;

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        if (mat == null)
            Debug.LogError($"FogOfWar: {name} material is not assigned.", this);
    }

    void BuildOverlayMesh(Mesh target, TileVisibility match)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        Vector3 lift = Vector3.up * heightOffset;
        float horizSpacing = grid.HorizontalSpacing;
        float vertSpacing = grid.VerticalSpacing;

        for (int col = 0; col < grid.columns; col++)
        {
            for (int row = 0; row < grid.rows; row++)
            {
                if (state[col, row] != match)
                    continue;
                float x = col * horizSpacing;
                float z = row * vertSpacing + (col % 2 == 1 ? vertSpacing * 0.5f : 0f);
                AddHexFan(verts, tris, new Vector3(x, 0f, z) + lift, grid.hexSize);
            }
        }

        target.Clear();
        if (verts.Count > 0)
        {
            target.SetVertices(verts);
            target.SetTriangles(tris, 0);
            target.RecalculateBounds();
        }
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
