using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Listens for left-mouse clicks, raycasts into the scene, and highlights the
/// hex tile under a clicked unit.
/// </summary>
public class SelectionController : MonoBehaviour
{
    [Tooltip("Camera used to cast click rays. Falls back to Camera.main.")]
    public Camera cam;

    [Tooltip("Highlight overlay moved onto the selected unit's tile.")]
    public HexHighlight highlight;

    void Awake()
    {
        if (cam == null)
            cam = Camera.main;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            TrySelectAt(mouse.position.ReadValue());
    }

    /// <summary>
    /// Raycasts at the given screen point; if it hits a unit, highlights that
    /// unit's hex tile and returns true.
    /// </summary>
    public bool TrySelectAt(Vector2 screenPoint)
    {
        if (cam == null || highlight == null)
            return false;

        Ray ray = cam.ScreenPointToRay(screenPoint);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var unit = hit.collider.GetComponentInParent<Unit>();
            if (unit != null)
            {
                highlight.ShowAt(unit);
                return true;
            }
        }
        return false;
    }
}
