using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pans the camera across the hex map in response to arrow keys (or WASD)
/// and right-mouse-button drag, keeping the camera flying parallel to the
/// XZ plane. Movement directions are taken from the camera's own
/// forward/right with the Y axis flattened, so panning still feels natural
/// when the camera is tilted.
///
/// Right-click drag uses a "grab the world" model: the world point under
/// the cursor when the drag started stays under the cursor for the rest of
/// the drag, so dragging right visually moves the world right (the camera
/// moves left).
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

    [Tooltip("Camera used for right-click drag raycasts. Defaults to " +
             "Camera.main if left unset.")]
    public Camera cam;

    // World-space point that was under the cursor when the right-mouse drag
    // started. While the drag is active, we move the camera so this same
    // point stays under the cursor every frame.
    Vector3 dragAnchorWorld;
    bool dragging;

    void Update()
    {
        Vector3 position = transform.position;

        // Right-click drag pan ("grab the world"): the world point grabbed
        // at mouse-down stays under the cursor for the rest of the drag.
        position = ApplyDragPan(position);

        // Keyboard pan composes on top of any drag delta this frame.
        position = ApplyKeyboardPan(position);

        transform.position = position;
    }

    Vector3 ApplyKeyboardPan(Vector3 position)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return position;

        Vector2 input = ReadInput(keyboard);
        if (input.sqrMagnitude < 0.0001f)
            return position;

        // Flatten camera basis so vertical pitch doesn't make panning sluggish.
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 delta = (right * input.x + forward * input.y) * panSpeed * Time.deltaTime;
        Vector3 next = position + delta;

        if (grid != null)
            next = ClampToGrid(position, next);

        return next;
    }

    Vector3 ApplyDragPan(Vector3 position)
    {
        var mouse = Mouse.current;
        if (mouse == null)
            return position;

        var rmb = mouse.rightButton;
        if (!rmb.isPressed)
        {
            dragging = false;
            return position;
        }

        Camera c = cam != null ? cam : Camera.main;
        if (c == null)
            return position;

        Vector2 screen = mouse.position.ReadValue();

        if (rmb.wasPressedThisFrame || !dragging)
        {
            // Start of drag: remember the world point under the cursor.
            if (TryRaycastGround(c, screen, out Vector3 anchor))
            {
                dragAnchorWorld = anchor;
                dragging = true;
            }
            return position;
        }

        // Continuing drag: figure out where the cursor currently maps to
        // on the ground plane *assuming the camera hadn't moved*, then
        // translate the camera so the original anchor lands there instead.
        if (!TryRaycastGround(c, screen, out Vector3 current))
            return position;

        Vector3 delta = dragAnchorWorld - current;
        delta.y = 0f;
        Vector3 next = position + delta;

        if (grid != null)
            next = ClampToGrid(position, next);

        return next;
    }

    // Raycast the cursor onto the y = position.y plane (the plane the
    // camera flies parallel to is the XZ plane, but we anchor the drag at
    // y = 0 so both the start and continuation samples agree).
    static bool TryRaycastGround(Camera c, Vector2 screen, out Vector3 hit)
    {
        Ray ray = c.ScreenPointToRay(screen);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float enter))
        {
            hit = ray.GetPoint(enter);
            return true;
        }
        hit = default;
        return false;
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
