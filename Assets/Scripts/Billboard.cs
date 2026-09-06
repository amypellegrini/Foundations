using UnityEngine;

/// <summary>
/// Rotates this transform to face the active camera while keeping world up.
/// Civ-style sprite trick: under an ortho camera every sprite ends up
/// perpendicular to the view plane, so a side-view drawing reads correctly
/// from a tilted top-down camera.
/// </summary>
[ExecuteAlways]
public class Billboard : MonoBehaviour
{
    [Tooltip("Camera to face. Falls back to Camera.main if null.")]
    public Camera target;

    void LateUpdate()
    {
        Camera cam = target != null ? target : Camera.main;
        if (cam == null)
            return;

        Vector3 forward = cam.orthographic
            ? cam.transform.forward
            : (transform.position - cam.transform.position);

        if (forward.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}
