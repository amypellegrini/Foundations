using UnityEngine;

/// <summary>
/// Disables off-screen chunks of the world map. Each frame the camera's
/// view frustum is intersected with the ground plane (y=0) and the
/// resulting rectangle is tested against each chunk's
/// <see cref="HexChunk.WorldBounds"/>. Chunks that don't overlap have their
/// GameObject deactivated, which suspends every renderer they own — terrain,
/// borders, decorations, fog overlays — in a single SetActive call.
///
/// When the grid wraps east–west, each chunk is also tested against a copy
/// of its bounds shifted by ±<see cref="HexGrid.WorldWidth"/>. If a copy
/// intersects the view, the corresponding ghost instance is activated so
/// the seam at the world edge renders continuously.
///
/// Relying on Unity's built-in MeshRenderer culling alone is not enough: at
/// real-world scale the world holds hundreds of chunks and potentially
/// hundreds of thousands of decoration GameObjects. The CPU cost of even an
/// "auto-culled" off-screen renderer is bounded but real; turning the whole
/// chunk subtree off is the cheapest way to keep it free.
/// </summary>
public class HexGridCulling : MonoBehaviour
{
    [Tooltip("Camera whose frustum drives chunk visibility. Falls back to Camera.main.")]
    public Camera cam;

    [Tooltip("Grid to cull. Defaults to the HexGrid on the same GameObject.")]
    public HexGrid grid;

    [Tooltip("Extra padding (world units) added around the visible rectangle, " +
             "so chunks just past the edge stay loaded and the camera doesn't " +
             "flash an empty band when panning at speed. Roughly one chunk " +
             "extent is a safe default.")]
    public float padding = 24f;

    [Tooltip("Used when the camera doesn't look at the ground (e.g. straight " +
             "down with no perspective on far chunks) — falls back to a square " +
             "of this radius around the camera's XZ position.")]
    public float fallbackRadius = 50f;

    [Tooltip("Frames to wait before allowing ghost (wrap) chunks to spawn. " +
             "Lets FogOfWar finish initializing its overlay children on the " +
             "canonical chunk first, so the ghost clones inherit fog meshes.")]
    [Min(0)]
    public int ghostWarmupFrames = 2;

    Bounds[,] cachedBounds;
    HexChunk[,] cachedChunks;
    int cachedChunkCols;
    int cachedChunkRows;
    int framesAlive;

    void Awake()
    {
        if (grid == null) grid = GetComponent<HexGrid>();
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || grid == null) return;

        framesAlive++;
        EnsureCache();
        if (cachedChunks == null) return;

        bool ghostsAllowed = grid.wrap && framesAlive > ghostWarmupFrames;
        float W = grid.WorldWidth;

        Bounds view = ComputeGroundViewBounds();
        for (int cc = 0; cc < cachedChunkCols; cc++)
        {
            for (int cr = 0; cr < cachedChunkRows; cr++)
            {
                var chunk = cachedChunks[cc, cr];
                if (chunk == null) continue;

                Bounds canonical = cachedBounds[cc, cr];
                bool showCanonical = view.Intersects(canonical);
                if (chunk.gameObject.activeSelf != showCanonical)
                    chunk.gameObject.SetActive(showCanonical);

                // Wrap ghosts: each chunk has a virtual copy at ±W. They're
                // only spawned the first time they're needed, so on a map
                // far from the seam none of them ever exist.
                if (ghostsAllowed)
                {
                    Bounds east = canonical;
                    east.center += new Vector3(W, 0f, 0f);
                    chunk.SetGhostActive(+1, view.Intersects(east));

                    Bounds west = canonical;
                    west.center -= new Vector3(W, 0f, 0f);
                    chunk.SetGhostActive(-1, view.Intersects(west));
                }
                else
                {
                    chunk.SetGhostActive(+1, false);
                    chunk.SetGhostActive(-1, false);
                }
            }
        }
    }

    void EnsureCache()
    {
        int cc = grid.ChunkColumns;
        int cr = grid.ChunkRows;
        if (cachedChunks != null && cachedChunkCols == cc && cachedChunkRows == cr)
            return;

        cachedChunkCols = cc;
        cachedChunkRows = cr;
        cachedChunks = new HexChunk[cc, cr];
        cachedBounds = new Bounds[cc, cr];
        foreach (var chunk in grid.Chunks)
        {
            if (chunk == null) continue;
            cachedChunks[chunk.ChunkCol, chunk.ChunkRow] = chunk;
            cachedBounds[chunk.ChunkCol, chunk.ChunkRow] = chunk.WorldBounds;
        }
    }

    // Compute the XZ-plane rectangle currently visible to the camera by
    // intersecting the four frustum-corner rays with y=0. Returns a thin
    // slab (Y extents = 1) so an axis-aligned Bounds.Intersects with the
    // chunk bounds reduces to a 2D test.
    Bounds ComputeGroundViewBounds()
    {
        // Sample the four corners of the camera's far plane in viewport
        // space; ScreenPointToRay handles orthographic and perspective
        // uniformly. Project each ray onto y=0 to get the ground footprint.
        Vector2[] corners =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
        };

        Plane ground = new Plane(Vector3.up, Vector3.zero);
        bool any = false;
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var v in corners)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(v.x, v.y, 0f));
            if (!ground.Raycast(ray, out float t)) continue;
            if (t < 0f) continue;  // looking away from the ground
            Vector3 p = ray.GetPoint(t);
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.z);
            maxZ = Mathf.Max(maxZ, p.z);
            any = true;
        }

        if (!any)
        {
            // Fallback: camera doesn't intersect the ground (looking up,
            // parallel, or below it). Use a square around the camera XZ.
            Vector3 p = cam.transform.position;
            minX = p.x - fallbackRadius;
            maxX = p.x + fallbackRadius;
            minZ = p.z - fallbackRadius;
            maxZ = p.z + fallbackRadius;
        }

        minX -= padding; maxX += padding;
        minZ -= padding; maxZ += padding;

        var b = new Bounds();
        b.SetMinMax(new Vector3(minX, -0.5f, minZ),
                    new Vector3(maxX, 0.5f, maxZ));
        return b;
    }
}
