using UnityEngine;

/// <summary>
/// A single game unit occupying one hex tile. For this first slice it is just
/// a sphere marker; clicking it highlights the tile it stands on.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Unit : MonoBehaviour
{
    [Tooltip("Hex grid this unit lives on.")]
    public HexGrid grid;

    [Header("Hex position (offset coordinates)")]
    public int col;
    public int row;

    [Tooltip("Height of the unit's center above the tile plane.")]
    public float heightOffset = 0.5f;

    void Start()
    {
        SnapToHex();
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
}
