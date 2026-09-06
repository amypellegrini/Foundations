using UnityEngine;

/// <summary>Serialized terrain heights shared by visuals, movement, and overlays.</summary>
public class Terrain3D : MonoBehaviour
{
    public int rows = 8;
    public float[] heights;
    public bool procedural;
    HexGrid grid;
    public float Height(int col, int row)
    {
        if (procedural)
        {
            if (grid == null) grid = GetComponent<HexGrid>();
            if (grid.MapData == null || !grid.IsLand(col,row)) return 0;
            return .18f + Mathf.Round(Mathf.PerlinNoise(grid.WrapColumn(col)*.19f,row*.19f)*3)*.08f;
        }
        int index = col * rows + row;
        return heights != null && index >= 0 && index < heights.Length ? heights[index] : 0;
    }
}
