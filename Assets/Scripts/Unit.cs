using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A single game unit occupying one hex tile, rendered as a sphere. Moves a
/// fixed number of tiles per turn along a queued path; movement is animated as
/// a slide between tile centers.
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

    readonly List<Vector2Int> path = new List<Vector2Int>();
    readonly Queue<Vector3> waypoints = new Queue<Vector3>();

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
        Vector3 facing = target - transform.position;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.0001f)
        {
            Quaternion want = Quaternion.LookRotation(facing);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, want, turnSpeed * Time.deltaTime);
        }

        transform.position = Vector3.MoveTowards(
            transform.position, target, moveSpeed * Time.deltaTime);

        if ((transform.position - target).sqrMagnitude < 0.0001f)
        {
            transform.position = target;
            waypoints.Dequeue();
        }
    }

    /// <summary>Positions the unit on top of its hex tile.</summary>
    public void SnapToHex()
    {
        if (grid == null)
        {
            Debug.LogError("Unit: grid is not assigned.", this);
            return;
        }
        transform.position = grid.HexCenter(col, row) + Vector3.up * heightOffset;
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
            waypoints.Enqueue(grid.HexCenter(col, row) + Vector3.up * heightOffset);
        }
        MovesRemaining -= steps;
        return steps;
    }

    /// <summary>Restores the unit's move budget at the start of a new turn.</summary>
    public void RefillMoves()
    {
        MovesRemaining = movesPerTurn;
    }
}
