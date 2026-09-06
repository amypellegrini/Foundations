using UnityEngine;

/// <summary>
/// One rectangular slice of the <see cref="HexGrid"/>, owning its own terrain
/// mesh plus the satellite renderers (borders, decorations, fog overlays)
/// that draw the tiles inside [<see cref="ColStart"/>, <see cref="ColEnd"/>)
/// × [<see cref="RowStart"/>, <see cref="RowEnd"/>).
///
/// The chunk is the unit of frustum culling for the world map: when the
/// culling driver decides a chunk is off-screen, it deactivates the chunk
/// GameObject and every satellite goes with it in one operation.
///
/// On a wrapping grid the chunk can also spawn lazy "ghost" copies of itself
/// at ±<see cref="HexGrid.WorldWidth"/> along X, so the seam between the
/// east and west edges of the world can be rendered continuously when the
/// camera straddles it. Ghosts share Mesh assets with the canonical chunk
/// — terrain, borders, fog overlays all reflect the same data — so the
/// extra memory is just the GameObject and renderer overhead, not mesh data.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexChunk : MonoBehaviour
{
    public int ChunkCol { get; private set; }
    public int ChunkRow { get; private set; }
    public int ColStart { get; private set; }
    public int ColEnd { get; private set; }
    public int RowStart { get; private set; }
    public int RowEnd { get; private set; }

    /// <summary>The grid that owns this chunk. Needed for wrap math when
    /// spawning ghost instances.</summary>
    public HexGrid Grid { get; private set; }

    /// <summary>World-space XZ bounds of the chunk's tiles. Y is flattened to
    /// a thin slab so 2D culling against the camera's ground-view rectangle
    /// works with a plain <c>Bounds.Intersects</c> check.</summary>
    public Bounds WorldBounds { get; private set; }

    /// <summary>Border mesh container for the tiles in this chunk, if borders
    /// are enabled at the grid level. Owned by <see cref="HexGrid"/>.</summary>
    public MeshFilter BorderFilter { get; set; }

    /// <summary>Number of tiles covered by this chunk (it may be smaller than
    /// the configured chunk size at the right/bottom edges of the map).</summary>
    public int TileCount => (ColEnd - ColStart) * (RowEnd - RowStart);

    GameObject westGhost;
    GameObject eastGhost;

    public void Initialize(int chunkCol, int chunkRow,
                           int colStart, int colEnd,
                           int rowStart, int rowEnd,
                           Bounds worldBounds, HexGrid grid)
    {
        ChunkCol = chunkCol;
        ChunkRow = chunkRow;
        ColStart = colStart;
        ColEnd = colEnd;
        RowStart = rowStart;
        RowEnd = rowEnd;
        WorldBounds = worldBounds;
        Grid = grid;
    }

    /// <summary>True if the offset coordinate falls inside this chunk.</summary>
    public bool Contains(int col, int row)
        => col >= ColStart && col < ColEnd && row >= RowStart && row < RowEnd;

    /// <summary>
    /// Activates this chunk's east (direction = +1) or west (-1) ghost,
    /// creating it the first time it's needed by cloning the canonical
    /// subtree. Cloned MeshFilters share Mesh references with the canonical
    /// so the ghost stays in sync with fog and border updates automatically.
    /// </summary>
    public GameObject SetGhostActive(int direction, bool active)
    {
        if (direction == 0) return gameObject;

        ref GameObject slot = ref direction < 0 ? ref westGhost : ref eastGhost;

        if (slot == null)
        {
            if (!active) return null;        // don't bother building an inactive ghost
            slot = BuildGhost(direction);
            if (slot == null) return null;
        }

        if (slot.activeSelf != active) slot.SetActive(active);
        return slot;
    }

    GameObject BuildGhost(int direction)
    {
        if (Grid == null)
        {
            Debug.LogError("HexChunk: cannot build ghost without a grid reference.", this);
            return null;
        }

        // Instantiate clones the entire GameObject subtree (terrain, borders,
        // fog overlay GameObjects, anything else parented under this chunk),
        // and Mesh references on the cloned MeshFilters share with the
        // original — exactly what we want so the ghost stays in lockstep
        // with fog/border updates without extra bookkeeping.
        var ghost = Instantiate(gameObject, transform.parent);
        ghost.name = $"{name}_Ghost{(direction > 0 ? "East" : "West")}";
        ghost.transform.localPosition = transform.localPosition
            + new Vector3(direction * Grid.WorldWidth, 0f, 0f);

        // The clone carries its own HexChunk component (a side effect of
        // Instantiate). Strip it so culling treats the ghost as a passive
        // shell rather than a second canonical chunk.
        var dup = ghost.GetComponent<HexChunk>();
        if (dup != null) Destroy(dup);

        // Instantiate does not copy MaterialPropertyBlocks set via
        // SetPropertyBlock — they are per-renderer runtime state. Walk
        // both subtrees in lockstep and re-apply each canonical renderer's
        // property block to its ghost counterpart, so per-layer fog alphas
        // (and any future MPB tweaks) survive the clone.
        CopyMaterialPropertyBlocks(transform, ghost.transform);

        ghost.SetActive(false);
        return ghost;
    }

    static void CopyMaterialPropertyBlocks(Transform source, Transform clone)
    {
        var srcRenderer = source.GetComponent<Renderer>();
        var dstRenderer = clone.GetComponent<Renderer>();
        if (srcRenderer != null && dstRenderer != null && srcRenderer.HasPropertyBlock())
        {
            var mpb = new MaterialPropertyBlock();
            srcRenderer.GetPropertyBlock(mpb);
            dstRenderer.SetPropertyBlock(mpb);
        }

        int count = Mathf.Min(source.childCount, clone.childCount);
        for (int i = 0; i < count; i++)
            CopyMaterialPropertyBlocks(source.GetChild(i), clone.GetChild(i));
    }
}
