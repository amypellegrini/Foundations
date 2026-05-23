using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pans the camera across the hex map in response to arrow keys (or WASD),
/// edge-of-screen mouse pushes, and left-mouse-button drag, keeping the
/// camera flying parallel to the XZ plane. Movement directions are taken
/// from the camera's own forward/right with the Y axis flattened, so
/// panning still feels natural when the camera is tilted.
///
/// Left-click drag uses a "grab the world" model: the world point under
/// the cursor when the drag began stays under the cursor for the rest of
/// the drag, so dragging right visually moves the world right (the camera
/// moves left). The drag only activates once the cursor has moved a few
/// pixels from the press position, so plain clicks (handled by
/// <see cref="GameController"/>) aren't swallowed by a phantom drag.
///
/// Edge-scroll kicks in when the cursor sits within
/// <see cref="edgePanMargin"/> pixels of any window edge; it is suppressed
/// while a left-click drag is active so the two pan modes don't fight.
///
/// Mouse-wheel scroll zooms the camera in and out by moving along its own
/// forward axis, clamped to <see cref="minZoomHeight"/> /
/// <see cref="maxZoomHeight"/>.
///
/// If a <see cref="HexGrid"/> is assigned, the pan position is clamped so
/// the camera stays roughly over the map.
/// </summary>
public class CameraPan : MonoBehaviour
{
    [Tooltip("World units per second when fully held.")]
    public float panSpeed = 50f;

    [Tooltip("Optional grid used to clamp the camera to the map's footprint.")]
    public HexGrid grid;

    [Tooltip("Extra margin (in world units) added to each side of the clamp " +
             "region so the camera can drift slightly past the map edge.")]
    public float clampMargin = 4f;

    [Tooltip("Camera used for left-click drag raycasts. Defaults to " +
             "Camera.main if left unset.")]
    public Camera cam;

    [Tooltip("Distance from the window edge (in pixels) where mouse-edge " +
             "pan activates. Set to 0 to disable edge-scrolling.")]
    public float edgePanMargin = 20f;

    [Tooltip("World units the camera travels along its forward axis per " +
             "unit of scroll input (perspective mode). Positive scroll zooms in.")]
    public float zoomSpeed = 0.05f;

    [Tooltip("Minimum camera height (Y) when zoomed all the way in (perspective mode).")]
    public float minZoomHeight = 6f;

    [Tooltip("Maximum camera height (Y) when zoomed all the way out (perspective mode).")]
    public float maxZoomHeight = 30f;

    [Tooltip("World units of orthographicSize per unit of scroll input " +
             "(orthographic mode). Positive scroll zooms in (shrinks size).")]
    public float orthoZoomSpeed = 3f;

    [Tooltip("Minimum orthographicSize when zoomed all the way in (orthographic mode).")]
    public float minOrthoSize = 4f;

    [Tooltip("Maximum orthographicSize when zoomed all the way out (orthographic mode).")]
    public float maxOrthoSize = 5f;

    // Pixel distance the cursor must travel while the left button is held
    // before we treat the gesture as a camera drag rather than a click. Kept
    // in sync with the matching threshold in GameController so the two
    // never disagree near the boundary.
    public const float DragActivationPixels = 5f;

    // World-space point that was under the cursor when the left-mouse drag
    // activated. While the drag is active, we move the camera so this same
    // point stays under the cursor every frame.
    Vector3 dragAnchorWorld;
    // Screen position recorded on press; used to defer drag activation
    // until the cursor has moved past DragActivationPixels.
    Vector2 dragPressScreen;
    bool dragButtonHeld;
    bool dragging;

    void Update()
    {
        Vector3 position = transform.position;

        // Scroll-wheel zoom runs first so the drag anchor below raycasts
        // from the post-zoom camera pose and stays consistent for the
        // rest of the frame.
        position = ApplyScrollZoom(position);

        // Left-click drag pan ("grab the world"): the world point grabbed
        // at drag activation stays under the cursor for the rest of the drag.
        position = ApplyDragPan(position);

        // Edge-of-screen pan and keyboard pan compose on top of any drag
        // delta this frame. Edge-pan is suppressed while dragging so a
        // drag that brushes the window border doesn't get a free boost.
        if (!dragging)
            position = ApplyEdgePan(position);
        position = ApplyKeyboardPan(position);

        // Cylindrical grids let the camera roam unbounded in X. Re-wrap
        // it into [0, WorldWidth) at the end of each frame so coordinates
        // don't drift forever, and shift any active drag anchor by the
        // same amount so the world-grab math stays consistent.
        if (grid != null && grid.wrap && grid.WorldWidth > 0f)
        {
            float W = grid.WorldWidth;
            float wrappedX = position.x - Mathf.Floor(position.x / W) * W;
            float shift = wrappedX - position.x;
            if (shift != 0f)
            {
                position.x = wrappedX;
                if (dragging) dragAnchorWorld.x += shift;
            }
        }

        transform.position = position;
    }

    Vector3 ApplyScrollZoom(Vector3 position)
    {
        float scroll = ReadScroll();
        if (Mathf.Approximately(scroll, 0f))
            return position;

        Camera c = cam != null ? cam : Camera.main;
        if (c != null && c.orthographic)
        {
            // Orthographic mode: zoom by shrinking/growing orthographicSize.
            // The camera doesn't physically move — pan position is unchanged.
            float size = c.orthographicSize - scroll * orthoZoomSpeed;
            c.orthographicSize = Mathf.Clamp(size,
                Mathf.Min(minOrthoSize, maxOrthoSize),
                Mathf.Max(minOrthoSize, maxOrthoSize));
            return position;
        }

        Vector3 forward = transform.forward;
        if (forward.sqrMagnitude < 0.0001f)
            return position;

        Vector3 candidate = position + forward * (scroll * zoomSpeed);

        // Clamp Y to the allowed zoom range. If the height would cross a
        // limit, snap to it and recompute the matching XZ displacement
        // along forward so the camera continues moving toward whatever it
        // was looking at instead of stalling against the ceiling.
        float minY = Mathf.Min(minZoomHeight, maxZoomHeight);
        float maxY = Mathf.Max(minZoomHeight, maxZoomHeight);
        if (candidate.y < minY || candidate.y > maxY)
        {
            float targetY = Mathf.Clamp(candidate.y, minY, maxY);
            if (Mathf.Abs(forward.y) > 0.0001f)
            {
                float t = (targetY - position.y) / forward.y;
                candidate = position + forward * t;
            }
            else
            {
                candidate.y = targetY;
            }
        }

        if (grid != null)
            candidate = ClampToGrid(position, candidate);

        return candidate;
    }

    Vector3 ApplyKeyboardPan(Vector3 position)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return position;

        return ApplyAxisPan(position, ReadInput(keyboard));
    }

    Vector3 ApplyEdgePan(Vector3 position)
    {
        if (edgePanMargin <= 0f)
            return position;

        if (!ReadMousePosition(out Vector2 screen))
            return position;

        float w = Screen.width;
        float h = Screen.height;

        // Cursor outside the game window — some platforms keep reporting
        // a stale position, so don't pan when the user has clearly moved
        // focus away.
        if (screen.x < 0f || screen.x > w || screen.y < 0f || screen.y > h)
            return position;

        float x = 0f, y = 0f;
        if (screen.x <= edgePanMargin) x = -1f;
        else if (screen.x >= w - edgePanMargin) x = 1f;
        if (screen.y <= edgePanMargin) y = -1f;
        else if (screen.y >= h - edgePanMargin) y = 1f;

        return ApplyAxisPan(position, new Vector2(x, y));
    }

    Vector3 ApplyAxisPan(Vector3 position, Vector2 input)
    {
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
        if (!ReadLeftMouse(out Vector2 screen, out bool pressedThisFrame))
        {
            dragButtonHeld = false;
            dragging = false;
            return position;
        }

        Camera c = cam != null ? cam : Camera.main;
        if (c == null)
            return position;

        if (pressedThisFrame || !dragButtonHeld)
        {
            // Button just went down — record where, but don't anchor or
            // move the camera yet. We wait for the cursor to travel beyond
            // DragActivationPixels so a plain click can't accidentally
            // start a drag.
            dragPressScreen = screen;
            dragButtonHeld = true;
            dragging = false;
            return position;
        }

        if (!dragging)
        {
            float sqr = (screen - dragPressScreen).sqrMagnitude;
            if (sqr < DragActivationPixels * DragActivationPixels)
                return position;

            // Crossed the activation threshold: anchor at the *current*
            // cursor position (not the press position) so the first moved
            // frame doesn't snap the camera by the threshold distance.
            if (!TryRaycastGround(c, screen, out Vector3 anchor))
                return position;

            dragAnchorWorld = anchor;
            dragging = true;
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

    // Read the left mouse button from whichever input pipeline reports it.
    // The new Input System (Mouse.current) reads from a lower-level source
    // that doesn't see clicks synthesized by macOS accessibility tools like
    // BestMouseKeys; the legacy Input API does, so we fall back to it when
    // both backends are enabled (Project Settings -> Active Input Handling
    // = "Both").
    static bool ReadLeftMouse(out Vector2 screen, out bool pressedThisFrame)
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            screen = mouse.position.ReadValue();
            pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
            return true;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        if (UnityEngine.Input.GetMouseButton(0))
        {
            screen = UnityEngine.Input.mousePosition;
            pressedThisFrame = UnityEngine.Input.GetMouseButtonDown(0);
            return true;
        }
#endif

        screen = default;
        pressedThisFrame = false;
        return false;
    }

    // Read the current cursor position from whichever input pipeline is
    // available, matching the dual-backend handling used for the left
    // mouse button above.
    static bool ReadMousePosition(out Vector2 screen)
    {
        var mouse = Mouse.current;
        if (mouse != null)
        {
            screen = mouse.position.ReadValue();
            return true;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        Vector3 legacy = UnityEngine.Input.mousePosition;
        screen = new Vector2(legacy.x, legacy.y);
        return true;
#else
        screen = default;
        return false;
#endif
    }

    // Read the vertical scroll delta from whichever input pipeline reports
    // it. Same dual-backend fallback as the mouse-button helpers above.
    static float ReadScroll()
    {
        var mouse = Mouse.current;
        if (mouse != null)
        {
            float y = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(y, 0f))
                return y;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.Input.mouseScrollDelta.y;
#else
        return 0f;
#endif
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
        float minZ = Mathf.Min(a.z, b.z) - clampMargin;
        float maxZ = Mathf.Max(a.z, b.z) + clampMargin;

        // Clamp the camera's ground look-at point — not the transform — into
        // the map bounds. With a tilted camera the transform sits offset
        // along XZ from what's actually being viewed, so clamping the
        // transform itself locks the view well short of the far edges
        // (especially at full zoom-in). Subtracting the offset converts the
        // bounds from look-at space into camera-position space.
        Vector3 offset = GroundLookAtOffset(next);
        next.z = SoftClamp(current.z, next.z, minZ - offset.z, maxZ - offset.z);

        // Only the N-S poles are hard edges on a cylindrical grid; with
        // wrap on, the camera roams free in X (re-wrapped at the end of
        // Update). When wrap is off, fall back to the original X clamp.
        if (!grid.wrap)
        {
            float minX = Mathf.Min(a.x, b.x) - clampMargin;
            float maxX = Mathf.Max(a.x, b.x) + clampMargin;
            next.x = SoftClamp(current.x, next.x, minX - offset.x, maxX - offset.x);
        }
        return next;
    }

    // XZ offset from the given camera position to where its forward ray
    // intersects the y = 0 ground plane. Zero when the camera is at/below
    // the ground or pointing parallel to / away from it.
    Vector3 GroundLookAtOffset(Vector3 candidatePos)
    {
        Vector3 forward = transform.forward;
        if (candidatePos.y <= 0f || forward.y >= -0.0001f)
            return Vector3.zero;
        float t = -candidatePos.y / forward.y;
        return new Vector3(forward.x * t, 0f, forward.z * t);
    }

    // Clamp `next` into [min,max], but if `current` is already outside the
    // range, allow `next` to stay equally far out — only block movement that
    // pushes further away. Prevents an off-bounds starting position from
    // snapping inward on the first input frame.
    static float SoftClamp(float current, float next, float min, float max)
        => Mathf.Clamp(next, Mathf.Min(min, current), Mathf.Max(max, current));
}
