using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A single game unit occupying one hex tile, rendered as a sphere. Moves a
/// fixed number of tiles per turn along a queued path; movement is animated as
/// a slide between tile centers.
///
/// On a wrapping grid the unit's logical position lives in canonical world
/// coordinates (X in [0, WorldWidth)). Its visible transform is shifted
/// each LateUpdate to the wrap copy nearest the active camera, so the
/// player always sees it in the world copy they're looking at. Mid-slide,
/// the slide animation runs in continuous world coordinates that may
/// briefly exceed the canonical range — once the path empties the canonical
/// position is re-wrapped, and the display offset re-snaps invisibly
/// (because the unit and the chunks it's near both shift by the same W).
/// </summary>
[RequireComponent(typeof(Collider))]
public class Unit : MonoBehaviour
{
    [Tooltip("Hex grid this unit lives on.")]
    public HexGrid grid;

    [Header("Identity")]
    public string unitName = "Founder";

    [Header("Hex position (offset coordinates)")]
    public int col;
    public int row;

    [Header("Vision")]
    [Tooltip("Tiles around this unit revealed by line of sight. Founder = 2.")]
    public int sightRadius = 2;

    [Header("Movement")]
    [Tooltip("Tiles this unit may move each turn.")]
    public int movesPerTurn = 2;

    [Tooltip("Height of the unit's center above the tile plane.")]
    public float heightOffset = 0.5f;

    [Tooltip("Slide speed in world units per second.")]
    public float moveSpeed = 4f;

    [Tooltip("How fast the unit rotates to face its next waypoint, in degrees/sec.")]
    public float turnSpeed = 720f;

    /// <summary>Tiles still available to spend this turn.</summary>
    public int MovesRemaining { get; private set; }

    /// <summary>Current hex as offset coordinates.</summary>
    public Vector2Int Coord => new Vector2Int(col, row);

    /// <summary>True while the slide animation is still playing.</summary>
    public bool IsMoving => waypoints.Count > 0;

    /// <summary>Pending tiles still to be walked, ending at the destination.</summary>
    public IReadOnlyList<Vector2Int> Path => path;

    /// <summary>
    /// Canonical world position (X always in [0, WorldWidth) when wrapped).
    /// Slide animations operate here; the visible transform is derived from
    /// this each frame.
    /// </summary>
    public Vector3 CanonicalPosition => canonicalPos;

    readonly List<Vector2Int> path = new List<Vector2Int>();
    readonly Queue<Vector3> waypoints = new Queue<Vector3>();

    // Last enqueued slide target. Used to keep wrap-aware waypoint targets
    // continuous: a step from col W-1 to col 0 must enqueue the target at
    // canonical X + WorldWidth (one HexSize east), not at canonical X = 0
    // (a long slide back across the world).
    Vector3 lastQueuedTarget;

    Vector3 canonicalPos;

    Animator animator;
    static readonly int IsMovingParam = Animator.StringToHash("IsMoving");

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        MovesRemaining = movesPerTurn;
        SnapToHex();
    }

    void Update()
    {
        bool moving = waypoints.Count > 0;
        if (animator != null)
            animator.SetBool(IsMovingParam, moving);

        if (!moving)
            return;

        Vector3 target = waypoints.Peek();

        // Face the next waypoint on the XZ plane before stepping toward it.
        Vector3 facing = target - canonicalPos;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.0001f)
        {
            Quaternion want = Quaternion.LookRotation(facing);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, want, turnSpeed * Time.deltaTime);
        }

        canonicalPos = Vector3.MoveTowards(
            canonicalPos, target, moveSpeed * Time.deltaTime);

        if ((canonicalPos - target).sqrMagnitude < 0.0001f)
        {
            canonicalPos = target;
            waypoints.Dequeue();

            // End of the queued chain — re-wrap canonical X into [0, W) so
            // the logical truth doesn't drift further each circumnavigation.
            // The display reshuffles to the same screen pixel automatically
            // because the wrap-to-camera offset jumps by exactly W at the
            // same moment.
            if (waypoints.Count == 0 && grid != null && grid.wrap && grid.WorldWidth > 0f)
            {
                float W = grid.WorldWidth;
                canonicalPos.x -= Mathf.Floor(canonicalPos.x / W) * W;
                lastQueuedTarget = canonicalPos;
            }
        }
    }

    void LateUpdate()
    {
        SyncDisplay();
    }

    /// <summary>Positions the unit on top of its hex tile.</summary>
    public void SnapToHex()
    {
        if (grid == null)
        {
            Debug.LogError("Unit: grid is not assigned.", this);
            return;
        }
        canonicalPos = grid.HexCenter(col, row) + Vector3.up * heightOffset;
        lastQueuedTarget = canonicalPos;
        SyncDisplay();
    }

    /// <summary>
    /// Replaces the unit's pending path. Tiles must exclude the unit's current
    /// tile and lead to the destination, as produced by <see cref="HexGrid.FindPath"/>.
    /// </summary>
    public void SetPath(List<Vector2Int> newPath)
    {
        path.Clear();
        if (newPath != null)
            path.AddRange(newPath);
    }

    /// <summary>
    /// Walks up to <see cref="MovesRemaining"/> tiles along the pending path,
    /// consuming move points and queueing the slide animation. The logical
    /// position jumps to the destination immediately. Returns the tiles moved.
    /// On a wrapping grid the queued slide target is offset by ±WorldWidth
    /// as needed so the unit slides into the adjacent tile rather than back
    /// across the whole world to a far canonical copy.
    /// </summary>
    public int MoveAlongPath()
    {
        int steps = Mathf.Min(MovesRemaining, path.Count);
        for (int i = 0; i < steps; i++)
        {
            Vector2Int tile = path[0];
            path.RemoveAt(0);
            col = tile.x;
            row = tile.y;
            Vector3 target = grid.HexCenter(col, row) + Vector3.up * heightOffset;
            if (grid.wrap)
            {
                float W = grid.WorldWidth;
                float dx = target.x - lastQueuedTarget.x;
                while (dx > W * 0.5f) { target.x -= W; dx -= W; }
                while (dx < -W * 0.5f) { target.x += W; dx += W; }
            }
            waypoints.Enqueue(target);
            lastQueuedTarget = target;
        }
        MovesRemaining -= steps;
        return steps;
    }

    /// <summary>Restores the unit's move budget at the start of a new turn.</summary>
    public void RefillMoves()
    {
        MovesRemaining = movesPerTurn;
    }

    void SyncDisplay()
    {
        Vector3 display = canonicalPos;
        if (grid != null && grid.wrap)
        {
            Camera cam = Camera.main;
            if (cam != null)
                display = grid.WrapToCamera(display, cam.transform.position.x);
        }
        transform.position = display;
    }
}
