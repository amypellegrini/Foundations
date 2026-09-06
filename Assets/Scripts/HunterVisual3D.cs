using UnityEngine;

/// <summary>Gentle walking motion applied only to the miniature, never the logical unit.</summary>
public class HunterVisual3D : MonoBehaviour
{
    public Unit unit;
    Vector3 last;
    void Start() { last = unit.transform.position; }
    void LateUpdate()
    {
        Vector3 direction = unit.transform.position - last;
        direction.y = 0;
        if (direction.sqrMagnitude > .000001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 12);
        transform.localPosition = Vector3.up * (unit.IsMoving ? Mathf.Sin(Time.time * 14) * .035f : 0);
        last = unit.transform.position;
    }
}
