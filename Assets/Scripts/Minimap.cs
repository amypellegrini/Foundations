using System.Collections.Generic;
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
             "fitting the discovered region's column/row ratio inside this box.")]
    public Vector2 size = new Vector2(240f, 140f);

    [Tooltip("Pixel gap between the minimap and the lower-left corner of the screen.")]
    public float margin = 16f;

    [Tooltip("Width of the dark frame drawn around the minimap, in pixels.")]
    public float frameWidth = 2f;

    [Tooltip("Tiles of breathing room drawn around the discovered region. " +
             "These pad tiles still render as Unknown so the player sees a " +
             "thin frontier ring around their explored area.")]
    [Min(0)]
    public int padding = 1;

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
    // Bounding box of the discovered (Visible + Explored) region, in tile
    // coordinates. Width counts columns starting at colStart and walking east
    // with wrap; row range is always clamped to [0, rows).
    int bboxColStart;
    int bboxColCount;
    int bboxRowStart;
    int bboxRowCount;

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

        EnsureAuxTextures();
        RebuildIfDirty();
        if (texture == null || bboxColCount == 0 || bboxRowCount == 0) return;

        // Fit the minimap rect to the bbox's aspect inside the configured size.
        float bboxAspect = (float)bboxColCount / bboxRowCount;
        float w = size.x;
        float h = size.y;
        if (w / h > bboxAspect) w = h * bboxAspect;
        else h = w / bboxAspect;

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

    void EnsureAuxTextures()
    {
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
        bool gridChanged = grid.columns != lastColumns || grid.rows != lastRows;
        // When there's no fog-of-war source the texture only needs to be
        // painted once — every tile resolves to its raw terrain colour. We
        // still take that path when rev == 0 (overlay not yet initialized).
        if (!gridChanged && rev == lastRevision && lastRevision != -1)
            return;
        lastRevision = rev;
        lastColumns = grid.columns;
        lastRows = grid.rows;

        ComputeBoundingBox();
        if (bboxColCount == 0 || bboxRowCount == 0)
        {
            if (texture != null) { Destroy(texture); texture = null; }
            return;
        }

        if (texture == null || texture.width != bboxColCount || texture.height != bboxRowCount)
        {
            if (texture != null) Destroy(texture);
            texture = new Texture2D(bboxColCount, bboxRowCount, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "MinimapTexture",
            };
        }

        var pixels = texture.GetPixels32();
        for (int y = 0; y < bboxRowCount; y++)
        {
            int row = bboxRowStart + y;
            int rowOffset = y * bboxColCount;
            for (int x = 0; x < bboxColCount; x++)
            {
                int col = grid.WrapColumn(bboxColStart + x);
                pixels[rowOffset + x] = (Color32)ColorFor(col, row);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false);
    }

    /// <summary>
    /// Computes the tightest bounding box around tiles the player has at
    /// least Explored, expanded by <see cref="padding"/> tiles. On a
    /// wrapping grid the bbox is allowed to cross the E–W seam: the
    /// algorithm finds the largest circular gap in the set of discovered
    /// columns and covers everything except that gap.
    /// </summary>
    void ComputeBoundingBox()
    {
        int cols = grid.columns;
        int rows = grid.rows;

        // Gather row min/max and the set of discovered columns in one pass.
        int rowMin = int.MaxValue, rowMax = int.MinValue;
        var discoveredCols = new HashSet<int>();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (IsDiscovered(c, r))
                {
                    if (r < rowMin) rowMin = r;
                    if (r > rowMax) rowMax = r;
                    discoveredCols.Add(c);
                }
            }
        }

        if (discoveredCols.Count == 0)
        {
            // Nothing discovered yet — render nothing. (Game opens with the
            // founder's sight radius already revealing tiles, so this branch
            // is only hit on the very first frames before fog initializes.)
            bboxColStart = 0;
            bboxColCount = 0;
            bboxRowStart = 0;
            bboxRowCount = 0;
            return;
        }

        // Rows: clamp [rowMin-padding, rowMax+padding] to the world.
        int padRowMin = Mathf.Max(0, rowMin - padding);
        int padRowMax = Mathf.Min(rows - 1, rowMax + padding);
        bboxRowStart = padRowMin;
        bboxRowCount = padRowMax - padRowMin + 1;

        // Columns: find the wrap-aware column range covering every discovered
        // column. Without wrap it's just [min, max].
        var sorted = new List<int>(discoveredCols);
        sorted.Sort();

        int colStart, colCount;
        if (!grid.wrap)
        {
            colStart = sorted[0];
            colCount = sorted[sorted.Count - 1] - sorted[0] + 1;
        }
        else
        {
            // Find the biggest gap between consecutive discovered columns,
            // including the wrap-around gap between the last and first
            // entries. The bbox covers the world minus that gap.
            int n = sorted.Count;
            int bestGap = (cols - sorted[n - 1]) + sorted[0] - 1; // wrap gap
            int bestStart = sorted[0]; // start right after the wrap gap
            for (int i = 0; i < n - 1; i++)
            {
                int gap = sorted[i + 1] - sorted[i] - 1;
                if (gap > bestGap)
                {
                    bestGap = gap;
                    bestStart = sorted[i + 1];
                }
            }
            colStart = bestStart;
            colCount = cols - bestGap;
        }

        // Apply column padding. With wrap, pad both sides freely (capped at
        // the world width). Without wrap, clamp to [0, cols).
        if (grid.wrap)
        {
            colCount = Mathf.Min(cols, colCount + padding * 2);
            colStart = grid.WrapColumn(colStart - padding);
        }
        else
        {
            int padColStart = Mathf.Max(0, colStart - padding);
            int padColEnd = Mathf.Min(cols - 1, colStart + colCount - 1 + padding);
            colStart = padColStart;
            colCount = padColEnd - padColStart + 1;
        }

        bboxColStart = colStart;
        bboxColCount = colCount;
    }

    bool IsDiscovered(int col, int row)
    {
        if (fogOfWar == null) return true;
        return fogOfWar.GetVisibility(col, row) != TileVisibility.Unknown;
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

        float cellW = rect.width / Mathf.Max(1, bboxColCount);
        float cellH = rect.height / Mathf.Max(1, bboxRowCount);
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

            // Map the unit's tile into the bbox's local column space, taking
            // the wrap into account so a unit on the seam still lands inside
            // a bbox that crosses the seam.
            int localCol = col - bboxColStart;
            if (grid.wrap)
            {
                int cols = grid.columns;
                localCol = ((localCol % cols) + cols) % cols;
            }
            if (localCol < 0 || localCol >= bboxColCount) continue;

            int localRow = r - bboxRowStart;
            if (localRow < 0 || localRow >= bboxRowCount) continue;

            float px = rect.x + (localCol + 0.5f) * cellW;
            float py = rect.y + rect.height - (localRow + 0.5f) * cellH;
            GUI.DrawTexture(new Rect(px - halfDot, py - halfDot, unitDotSize, unitDotSize), unitTex);
        }
        GUI.color = prev;
    }
}
