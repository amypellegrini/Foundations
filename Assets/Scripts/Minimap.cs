using UnityEngine;

/// <summary>
/// HUD overlay that paints a small top-down map of the world in the
/// lower-left corner of the screen. Only tiles the player has already
/// discovered (Visible or Explored, per <see cref="FogOfWar"/>) show their
/// terrain — Unknown tiles render as the void colour, matching the
/// fog-of-war model. Friendly units are overlaid as bright dots.
///
/// Rendered through IMGUI (the same path as the rest of the HUD), so it
/// needs no Canvas/RawImage rig — drop the component on any GameObject
/// and wire <see cref="grid"/> + <see cref="fogOfWar"/>.
/// </summary>
public class Minimap : MonoBehaviour
{
    [Tooltip("Hex grid whose terrain is rendered.")]
    public HexGrid grid;

    [Tooltip("Fog-of-war source. Tiles outside its known set draw as the void colour.")]
    public FogOfWar fogOfWar;

    [Header("Layout")]
    [Tooltip("On-screen size of the minimap in pixels. Aspect is preserved by " +
             "fitting the map's column/row ratio inside this box.")]
    public Vector2 size = new Vector2(240f, 140f);

    [Tooltip("Pixel gap between the minimap and the lower-left corner of the screen.")]
    public float margin = 16f;

    [Tooltip("Width of the dark frame drawn around the minimap, in pixels.")]
    public float frameWidth = 2f;

    [Header("Tile colours")]
    [Tooltip("Unknown tiles — never seen, or fully decayed back from Explored.")]
    public Color unknownColor = new Color(0.04f, 0.04f, 0.06f, 0.85f);

    [Tooltip("Land tiles within line of sight this turn.")]
    public Color visibleLand = new Color(0.45f, 0.70f, 0.30f, 1f);

    [Tooltip("Water tiles within line of sight this turn.")]
    public Color visibleWater = new Color(0.20f, 0.40f, 0.65f, 1f);

    [Tooltip("Land tiles previously seen but currently out of sight.")]
    public Color exploredLand = new Color(0.27f, 0.40f, 0.20f, 1f);

    [Tooltip("Water tiles previously seen but currently out of sight.")]
    public Color exploredWater = new Color(0.14f, 0.24f, 0.38f, 1f);

    [Tooltip("Frame colour drawn behind the minimap.")]
    public Color frameColor = new Color(0f, 0f, 0f, 0.85f);

    [Header("Units")]
    [Tooltip("Colour for the unit dot drawn on top of the terrain texture.")]
    public Color unitColor = new Color(1f, 0.85f, 0.20f, 1f);

    [Tooltip("Diameter of the unit dot, in screen pixels. 0 hides unit markers.")]
    public float unitDotSize = 6f;

    Texture2D texture;
    Texture2D unitTex;
    Texture2D frameTex;
    int lastRevision = -1;
    int lastColumns;
    int lastRows;

    void OnEnable()
    {
        lastRevision = -1; // force a repaint on the next OnGUI pass
    }

    void OnDisable()
    {
        if (texture != null) { Destroy(texture); texture = null; }
        if (unitTex != null) { Destroy(unitTex); unitTex = null; }
        if (frameTex != null) { Destroy(frameTex); frameTex = null; }
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (grid == null) return;

        EnsureTextures();
        RebuildIfDirty();
        if (texture == null) return;

        // Fit the minimap rect to the map's aspect inside the configured size.
        float mapAspect = (float)grid.columns / Mathf.Max(1, grid.rows);
        float w = size.x;
        float h = size.y;
        if (w / h > mapAspect) w = h * mapAspect;
        else h = w / mapAspect;

        float x = margin;
        float y = Screen.height - h - margin;

        var frame = new Rect(x - frameWidth, y - frameWidth,
                             w + frameWidth * 2f, h + frameWidth * 2f);
        GUI.DrawTexture(frame, frameTex);

        var rect = new Rect(x, y, w, h);
        // The texture is stored with row 0 at v=0 (south). On screen, lower
        // Y is the bottom of the minimap — which is south on our world map —
        // so DrawTexture's default (no Y flip) lines up correctly.
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);

        if (unitDotSize > 0f)
            DrawUnits(rect);
    }

    void EnsureTextures()
    {
        if (grid.columns != lastColumns || grid.rows != lastRows)
        {
            if (texture != null) Destroy(texture);
            texture = null;
            lastColumns = grid.columns;
            lastRows = grid.rows;
        }

        if (texture == null)
        {
            texture = new Texture2D(grid.columns, grid.rows, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "MinimapTexture",
            };
            lastRevision = -1;
        }

        if (unitTex == null)
        {
            unitTex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "MinimapUnitTex" };
            unitTex.SetPixel(0, 0, Color.white);
            unitTex.Apply();
        }

        if (frameTex == null)
        {
            frameTex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "MinimapFrameTex" };
            frameTex.SetPixel(0, 0, frameColor);
            frameTex.Apply();
        }
    }

    void RebuildIfDirty()
    {
        int rev = fogOfWar != null ? fogOfWar.StateRevision : 0;
        // When there's no fog-of-war source the texture only needs to be
        // painted once — every tile resolves to its raw terrain colour. We
        // still take that path when rev == 0 (overlay not yet initialized).
        if (rev == lastRevision && lastRevision != -1)
            return;
        lastRevision = rev;

        var pixels = texture.GetPixels32();
        int cols = grid.columns;
        int rows = grid.rows;
        for (int r = 0; r < rows; r++)
        {
            int rowOffset = r * cols;
            for (int c = 0; c < cols; c++)
            {
                pixels[rowOffset + c] = (Color32)ColorFor(c, r);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false);
    }

    Color ColorFor(int col, int row)
    {
        var vis = fogOfWar != null ? fogOfWar.GetVisibility(col, row) : TileVisibility.Visible;
        if (vis == TileVisibility.Unknown)
            return unknownColor;

        bool land = grid.IsLand(col, row);
        if (vis == TileVisibility.Visible)
            return land ? visibleLand : visibleWater;
        return land ? exploredLand : exploredWater;
    }

    void DrawUnits(Rect rect)
    {
        var units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        if (units == null || units.Length == 0) return;

        float cellW = rect.width / Mathf.Max(1, grid.columns);
        float cellH = rect.height / Mathf.Max(1, grid.rows);
        float halfDot = unitDotSize * 0.5f;

        var prev = GUI.color;
        GUI.color = unitColor;
        foreach (var unit in units)
        {
            if (unit == null) continue;
            int col = unit.col;
            int r = unit.row;
            // Track mid-slide position so the dot tracks the unit smoothly,
            // matching how FogOfWar resolves a unit's current tile.
            if (grid.TryWorldToHex(unit.transform.position, out int wc, out int wr))
            {
                col = wc;
                r = wr;
            }
            // Don't reveal hidden units (e.g. future AI units) — only draw
            // the marker when the tile under the unit is at least Explored.
            if (fogOfWar != null && fogOfWar.GetVisibility(col, r) == TileVisibility.Unknown)
                continue;

            float px = rect.x + (col + 0.5f) * cellW;
            float py = rect.y + rect.height - (r + 0.5f) * cellH;
            GUI.DrawTexture(new Rect(px - halfDot, py - halfDot, unitDotSize, unitDotSize), unitTex);
        }
        GUI.color = prev;
    }
}
