using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pans the camera across the hex map in response to arrow keys (or WASD),
/// keeping the camera flying parallel to the XZ plane. Movement directions
/// are taken from the camera's own forward/right with the Y axis flattened,
/// so panning still feels natural when the camera is tilted.
///
/// If a <see cref="HexGrid"/> is assigned, the pan position is clamped so
/// the camera stays roughly over the map.
/// </summary>
public class CameraPan : MonoBehaviour
{
    [Tooltip("World units per second when fully held.")]
    public float panSpeed = 12f;

    [Tooltip("Optional grid used to clamp the camera to the map's footprint.")]
    public HexGrid grid;

    [Tooltip("Extra margin (in world units) added to each side of the clamp " +
             "region so the camera can drift slightly past the map edge.")]
    public float clampMargin = 4f;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        Vector2 input = ReadInput(keyboard);
        if (input.sqrMagnitude < 0.0001f)
            return;

        // Flatten camera basis so vertical pitch doesn't make panning sluggish.
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 delta = (right * input.x + forward * input.y) * panSpeed * Time.deltaTime;
        Vector3 next = transform.position + delta;

        if (grid != null)
            next = ClampToGrid(transform.position, next);

        transform.position = next;
    }

    static Vector2 ReadInput(Keyboard k)
    {
        float x = 0f, y = 0f;
        if (k.leftArrowKey.isPressed || k.aKey.isPressed) x -= 1f;
        if (k.rightArrowKey.isPressed || k.dKey.isPressed) x += 1f;
        if (k.downArrowKey.isPressed || k.sKey.isPressed) y -= 1f;
        if (k.upArrowKey.isPressed || k.wKey.isPressed) y += 1f;
        return new Vector2(x, y);
    }

    Vector3 ClampToGrid(Vector3 current, Vector3 next)
    {
        Vector3 a = grid.HexCenter(0, 0);
        Vector3 b = grid.HexCenter(grid.columns - 1, grid.rows - 1);
        float minX = Mathf.Min(a.x, b.x) - clampMargin;
        float maxX = Mathf.Max(a.x, b.x) + clampMargin;
        float minZ = Mathf.Min(a.z, b.z) - clampMargin;
        float maxZ = Mathf.Max(a.z, b.z) + clampMargin;
        next.x = SoftClamp(current.x, next.x, minX, maxX);
        next.z = SoftClamp(current.z, next.z, minZ, maxZ);
        return next;
    }

    // Clamp `next` into [min,max], but if `current` is already outside the
    // range, allow `next` to stay equally far out — only block movement that
    // pushes further away. Prevents an off-bounds starting position from
    // snapping inward on the first input frame.
    static float SoftClamp(float current, float next, float min, float max)
        => Mathf.Clamp(next, Mathf.Min(min, current), Mathf.Max(max, current));
}
