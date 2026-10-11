using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// A single game unit occupying one hex tile. Moves a fixed number of tiles
/// per turn along a queued path; the walk between tile centers follows a
/// smoothed curve with acceleration, braking, and an on-the-spot turn before
/// setting off, so an animated character riding on it can match its stride
/// to <see cref="CurrentSpeed"/>.
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

    [Tooltip("Whether this unit can found a city. Only the Founder should have " +
             "this set; founding consumes the unit.")]
    public bool canFoundCity;

    [Tooltip("Optional world-space label that displays the unit's name below the avatar.")]
    public TMP_Text nameLabel;

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

    [Tooltip("Cruising speed in world units per second.")]
    public float moveSpeed = 2.2f;

    [Tooltip("Speed gained per second when setting off, in world units/sec².")]
    public float acceleration = 4f;

    [Tooltip("Speed shed per second when arriving, in world units/sec².")]
    public float deceleration = 3f;

    [Tooltip("How fast the unit rotates to face its heading, in degrees/sec.")]
    public float turnSpeed = 360f;

    [Tooltip("If the route starts further than this many degrees from the unit's " +
             "facing, it turns on the spot before walking.")]
    public float turnInPlaceAngle = 35f;

    [Tooltip("How much of each half-segment is rounded off at a corner (0 = sharp " +
             "corners through tile centers, 1 = widest curve).")]
    [Range(0f, 1f)] public float cornerRounding = 0.85f;

    /// <summary>Tiles still available to spend this turn.</summary>
    public int MovesRemaining { get; private set; }

    /// <summary>
    /// True once this unit has spent its action for the current turn — either
    /// by exhausting its move budget, or by being explicitly skipped. Cleared
    /// each turn by <see cref="RefillMoves"/>.
    /// </summary>
    public bool HasActed => MovesRemaining == 0 || skipped;

    bool skipped;

    /// <summary>Current hex as offset coordinates.</summary>
    public Vector2Int Coord => new Vector2Int(col, row);

    /// <summary>True while the walk along the route is still playing.</summary>
    public bool IsMoving => routeActive;

    /// <summary>Current ground speed along the route, in world units/sec.</summary>
    public float CurrentSpeed => currentSpeed;

    /// <summary>Pending tiles still to be walked, ending at the destination.</summary>
    public IReadOnlyList<Vector2Int> Path => path;

    /// <summary>
    /// Canonical world position (X always in [0, WorldWidth) when wrapped).
    /// Slide animations operate here; the visible transform is derived from
    /// this each frame.
    /// </summary>
    public Vector3 CanonicalPosition => canonicalPos;

    readonly List<Vector2Int> path = new List<Vector2Int>();

    // Route being walked: the unit's start point followed by the tile centers
    // queued by MoveAlongPath (in continuous, possibly unwrapped coordinates).
    // It's sampled into a dense polyline with cumulative arc lengths so the
    // unit can be advanced by distance.
    readonly List<Vector3> routePoints = new List<Vector3>();
    readonly List<float> routePointDistance = new List<float>();
    readonly List<Vector3> curve = new List<Vector3>();
    readonly List<float> curveDistance = new List<float>();
    float routeDistance;
    float currentSpeed;
    bool routeActive;
    const int CornerSamples = 10;

    // Last enqueued slide target. Used to keep wrap-aware waypoint targets
    // continuous: a step from col W-1 to col 0 must enqueue the target at
    // canonical X + WorldWidth (one HexSize east), not at canonical X = 0
    // (a long slide back across the world).
    Vector3 lastQueuedTarget;

    Vector3 canonicalPos;

    void Awake()
    {
        MovesRemaining = movesPerTurn;
    }

    void Start()
    {
        MovesRemaining = movesPerTurn;
        SnapToHex();
        SyncNameLabel();
        // Default to hidden — GameController turns the label on for the
        // currently focused unit only.
        if (nameLabel != null)
            nameLabel.gameObject.SetActive(FindAnyObjectByType<GameController>()?.SelectedUnit == this);
    }

    /// <summary>
    /// Toggles the focus-only overlays for this unit. Right now that's just
    /// the in-world name label, which should only appear under the focused
    /// unit so the map stays uncluttered when many units are visible.
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (nameLabel != null)
            nameLabel.gameObject.SetActive(selected);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        SyncNameLabel();
    }
#endif

    void SyncNameLabel()
    {
        if (nameLabel != null && nameLabel.text != unitName)
            nameLabel.text = unitName;
    }

    void Update()
    {
        if (!routeActive)
            return;

        float dt = Time.deltaTime;
        float remaining = RouteLength - routeDistance;

        // Face along the curve. From a standstill, turn on the spot first
        // rather than drifting off sideways.
        Vector3 heading = Tangent(routeDistance);
        bool aligned = true;
        if (heading.sqrMagnitude > 0.0001f)
        {
            Quaternion want = Quaternion.LookRotation(heading);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);
            aligned = currentSpeed > 0.05f ||
                      Quaternion.Angle(transform.rotation, want) <= turnInPlaceAngle;
        }

        // Accelerate toward cruising speed, but never faster than the speed
        // from which the unit can still brake to a stop at the destination.
        // A small floor keeps the last centimeters from taking forever.
        float brake = Mathf.Sqrt(2f * deceleration * Mathf.Max(remaining, 0f));
        float target = aligned ? Mathf.Min(moveSpeed, Mathf.Max(brake, 0.25f)) : 0f;
        currentSpeed = target < currentSpeed
            ? target
            : Mathf.MoveTowards(currentSpeed, target, acceleration * dt);

        routeDistance = Mathf.Min(routeDistance + currentSpeed * dt, RouteLength);
        canonicalPos = Sample(routeDistance);

        if (routeDistance >= RouteLength - 0.0001f)
        {
            canonicalPos = routePoints[routePoints.Count - 1];
            routeActive = false;
            currentSpeed = 0f;
            routePoints.Clear();

            // End of the queued chain — re-wrap canonical X into [0, W) so
            // the logical truth doesn't drift further each circumnavigation.
            // The display reshuffles to the same screen pixel automatically
            // because the wrap-to-camera offset jumps by exactly W at the
            // same moment.
            if (grid != null && grid.wrap && grid.WorldWidth > 0f)
            {
                float W = grid.WorldWidth;
                canonicalPos.x -= Mathf.Floor(canonicalPos.x / W) * W;
            }
            lastQueuedTarget = canonicalPos;
        }
    }

    float RouteLength => curveDistance.Count > 0 ? curveDistance[curveDistance.Count - 1] : 0f;

    /// <summary>
    /// Appends tile centers to the route. If the unit is already walking, the
    /// route restarts from where it stands through the tile centers it hasn't
    /// reached yet, keeping its current speed so it doesn't stutter.
    /// </summary>
    void ExtendRoute(List<Vector3> targets)
    {
        var points = new List<Vector3> { canonicalPos };
        if (routeActive)
        {
            for (int i = 1; i < routePoints.Count; i++)
                if (routePointDistance[i] > routeDistance + 0.01f)
                    points.Add(routePoints[i]);
        }
        else
        {
            currentSpeed = 0f;
        }
        points.AddRange(targets);

        routePoints.Clear();
        routePoints.AddRange(points);
        BuildCurve();
        routeDistance = 0f;
        routeActive = RouteLength > 0.0001f;
        if (!routeActive)
            routePoints.Clear();
    }

    /// <summary>
    /// Samples the route into a polyline: straight runs between tile centers
    /// with each interior corner replaced by a quadratic Bézier, so the unit
    /// sweeps around bends instead of pivoting on the spot.
    /// </summary>
    void BuildCurve()
    {
        curve.Clear();
        curveDistance.Clear();
        routePointDistance.Clear();

        int n = routePoints.Count;
        AddCurvePoint(routePoints[0]);
        routePointDistance.Add(0f);
        for (int i = 1; i < n; i++)
        {
            Vector3 corner = routePoints[i];
            if (i == n - 1)
            {
                AddCurvePoint(corner);
                routePointDistance.Add(RouteLength);
                break;
            }
            float r = cornerRounding * 0.5f;
            Vector3 enter = Vector3.Lerp(corner, routePoints[i - 1], r);
            Vector3 exit = Vector3.Lerp(corner, routePoints[i + 1], r);
            AddCurvePoint(enter);
            for (int k = 1; k <= CornerSamples; k++)
            {
                float t = k / (float)CornerSamples;
                float u = 1f - t;
                AddCurvePoint(u * u * enter + 2f * u * t * corner + t * t * exit);
                if (k == CornerSamples / 2)
                    routePointDistance.Add(RouteLength);
            }
        }
    }

    void AddCurvePoint(Vector3 p)
    {
        if (curve.Count == 0)
        {
            curve.Add(p);
            curveDistance.Add(0f);
            return;
        }
        float d = Vector3.Distance(curve[curve.Count - 1], p);
        if (d < 0.00001f) return;
        curve.Add(p);
        curveDistance.Add(curveDistance[curveDistance.Count - 1] + d);
    }

    int SegmentAt(float distance)
    {
        int lo = 0, hi = curveDistance.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (curveDistance[mid] <= distance) lo = mid; else hi = mid;
        }
        return lo;
    }

    Vector3 Sample(float distance)
    {
        if (curve.Count == 1) return curve[0];
        int i = SegmentAt(distance);
        float span = curveDistance[i + 1] - curveDistance[i];
        return Vector3.Lerp(curve[i], curve[i + 1], (distance - curveDistance[i]) / span);
    }

    /// <summary>Horizontal direction of travel at the given distance.</summary>
    Vector3 Tangent(float distance)
    {
        if (curve.Count < 2) return Vector3.zero;
        // Look slightly ahead so the facing anticipates bends; at the very
        // end, keep the direction of the final segment.
        Vector3 a = Sample(distance);
        Vector3 b = Sample(Mathf.Min(distance + 0.15f, RouteLength));
        Vector3 d = b - a;
        if (d.sqrMagnitude < 0.000001f)
            d = curve[curve.Count - 1] - curve[curve.Count - 2];
        d.y = 0f;
        return d.normalized;
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
        routeActive = false;
        routePoints.Clear();
        currentSpeed = 0f;
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
    /// consuming move points and queueing the walk. The logical position
    /// jumps to the destination immediately. Returns the tiles moved.
    /// On a wrapping grid the queued target is offset by ±WorldWidth as
    /// needed so the unit walks into the adjacent tile rather than back
    /// across the whole world to a far canonical copy.
    /// </summary>
    public int MoveAlongPath()
    {
        int steps = Mathf.Min(MovesRemaining, path.Count);
        var targets = new List<Vector3>(steps);
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
            targets.Add(target);
            lastQueuedTarget = target;
        }
        if (targets.Count > 0)
            ExtendRoute(targets);
        MovesRemaining -= steps;
        return steps;
    }

    /// <summary>Restores the unit's move budget at the start of a new turn.</summary>
    public void RefillMoves()
    {
        MovesRemaining = movesPerTurn;
        skipped = false;
    }

    /// <summary>
    /// Forfeits any remaining moves for this turn. The unit is treated as
    /// having spent its action and the turn cycle should move past it.
    /// </summary>
    public void Skip()
    {
        skipped = true;
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
