/// <summary>
/// Terrain category for a single hex tile. Kept deliberately coarse — only
/// the land/sea split is meaningful yet. Future passes will refine land into
/// biomes (forest, hills, desert, ...) and water into shallows/deep, but
/// every system that currently asks "can a unit walk here?" or "do I draw a
/// grass tuft?" only needs the binary answer below.
/// </summary>
public enum TerrainType
{
    Water,
    Land,
}

/// <summary>
/// The static, per-tile terrain layer for a map: a rectangular grid of
/// <see cref="TerrainType"/>. Independent of the renderable
/// <see cref="HexGrid"/> so the generator can produce one without touching
/// Unity, and so future systems (pathfinding, AI) can read it without going
/// through the renderer.
/// </summary>
public sealed class MapData
{
    public readonly int columns;
    public readonly int rows;

    readonly TerrainType[,] tiles;

    public MapData(int columns, int rows)
    {
        this.columns = columns;
        this.rows = rows;
        tiles = new TerrainType[columns, rows];
    }

    public TerrainType this[int col, int row]
    {
        get => tiles[col, row];
        set => tiles[col, row] = value;
    }

    public bool InBounds(int col, int row)
        => col >= 0 && col < columns && row >= 0 && row < rows;

    public bool IsLand(int col, int row)
        => InBounds(col, row) && tiles[col, row] == TerrainType.Land;

    public bool IsWater(int col, int row)
        => InBounds(col, row) && tiles[col, row] == TerrainType.Water;
}
