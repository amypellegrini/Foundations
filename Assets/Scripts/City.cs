using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// A founded city occupying one hex tile. For this slice the city is just a
/// flat magenta hex laid over its tile plus a world-space name label below
/// it; richer settlement systems come later (see SPEC.md §3.2).
///
/// The magenta look reuses the grid's land material with a per-renderer
/// <see cref="MaterialPropertyBlock"/> colour override, so no extra material
/// asset is needed and the shader is guaranteed to ship in player builds.
///
/// Like <see cref="Unit"/>, the city's visible transform is shifted each
/// LateUpdate to the east–west wrap copy nearest the active camera, so the
/// marker and label always appear in the world copy the player is viewing.
/// </summary>
public class City : MonoBehaviour
{
    public HexGrid grid;
    public int col;
    public int row;
    public string cityName;

    /// <summary>The population a city is founded with.</summary>
    public const int StartingPopulation = 100;

    /// <summary>
    /// How many tiles out from the city center its territory reaches. A
    /// boundary ring is drawn around every tile within this hex distance.
    /// </summary>
    public const int TerritoryRadius = 3;

    /// <summary>
    /// Current number of inhabitants. A freshly founded city starts at
    /// <see cref="StartingPopulation"/>; growth systems come later (SPEC.md §3.2).
    /// </summary>
    public int population = StartingPopulation;

    /// <summary>
    /// How many in-radius turns must accrue for the population to grow by one.
    /// </summary>
    public const int TurnsPerGrowth = 3;

    /// <summary>
    /// Elapsed in-radius turns counted toward the next +1. Transient: it is
    /// reset to 0 the moment no Hunter is in the city's radius (see
    /// <see cref="ResetGrowth"/>), so progress is never saved across a leave.
    /// Always in the range [0, <see cref="TurnsPerGrowth"/>).
    /// </summary>
    public int growthProgress;

    /// <summary>
    /// Whether growth is currently accruing — true after the most recent turn
    /// tick found a Hunter in radius, false otherwise. Drives both the panel's
    /// idle/active readout and the visibility of the "+1" tile indicator.
    /// </summary>
    public bool IsAccruing { get; private set; }

    /// <summary>The city's hex as offset coordinates.</summary>
    public Vector2Int Coord => new Vector2Int(col, row);

    TMP_Text label;
    GameObject growthLabel;

    /// <summary>
    /// Builds the city's visuals on the given tile. <paramref name="tileMaterial"/>
    /// is the shared land material (recoloured magenta via a property block);
    /// <paramref name="labelTemplate"/> is an existing world-space label (the
    /// founder's name label) whose font and styling the city label copies so
    /// the two read consistently.
    /// </summary>
    public void Initialize(HexGrid grid, int col, int row, string cityName,
        Material tileMaterial, TMP_Text labelTemplate, float tileLift, float labelDrop)
    {
        this.grid = grid;
        this.col = col;
        this.row = row;
        this.cityName = cityName;

        BuildTile(tileMaterial, tileLift);
        BuildTerritory(tileMaterial, tileLift);
        BuildLabel(labelTemplate, tileLift, labelDrop);
        BuildGrowthLabel(labelTemplate, tileLift, labelDrop);
        SyncDisplay();
    }

    /// <summary>
    /// Records one turn with at least one Hunter in radius. Advances the
    /// transient progress counter; on every <see cref="TurnsPerGrowth"/>th
    /// accrued turn the population grows by exactly one and the counter rolls
    /// back toward zero for the next cycle. Returns true on the turn a +1 lands.
    /// </summary>
    public bool AccrueGrowth()
    {
        IsAccruing = true;
        growthProgress++;
        bool grew = false;
        if (growthProgress >= TurnsPerGrowth)
        {
            population++;
            growthProgress = 0;
            grew = true;
        }
        UpdateGrowthLabel();
        return grew;
    }

    /// <summary>
    /// Clears transient growth state — called on any turn with no Hunter in
    /// radius. Progress toward the next +1 is discarded (not saved), so a
    /// Hunter that re-enters later starts counting again from zero.
    /// </summary>
    public void ResetGrowth()
    {
        IsAccruing = false;
        growthProgress = 0;
        UpdateGrowthLabel();
    }

    void BuildTile(Material tileMaterial, float lift)
    {
        var go = new GameObject("Tile");
        go.transform.SetParent(transform, worldPositionStays: false);
        go.transform.localPosition = Vector3.up * lift;

        var verts = new Vector3[7];
        var tris = new int[18];
        verts[0] = Vector3.zero;
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.Deg2Rad * 60f * i;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * grid.hexSize, 0f, Mathf.Sin(a) * grid.hexSize);
        }
        for (int i = 0; i < 6; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = 1 + (i + 1) % 6;
            tris[i * 3 + 2] = 1 + i;
        }

        var mesh = new Mesh { name = "CityHex" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        if (tileMaterial != null)
            mr.sharedMaterial = tileMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", Color.magenta);
        mpb.SetColor("_Color", Color.magenta);
        mr.SetPropertyBlock(mpb);
    }

    /// <summary>
    /// Outlines the city's territory: a ring drawn along the outer edges of
    /// every tile within <see cref="TerritoryRadius"/> hexes of the center.
    /// Reuses the land material recoloured magenta (a property block, so no
    /// extra asset) and rides at the same height as the city marker — above
    /// the fog overlay — so the player can always see their own city's
    /// borders, even where the tiles beyond them are still unexplored.
    /// </summary>
    void BuildTerritory(Material tileMaterial, float lift)
    {
        Vector3 cityLocal = GridLocalCenter(col, row);

        // Count how many in-radius tiles each hex edge belongs to, keyed by
        // its (quantized) midpoint. Interior edges are shared by two tiles and
        // counted twice; a boundary edge touches exactly one — those are the
        // ring. Quantizing absorbs the tiny float differences between the same
        // corner computed from two adjacent centers.
        var edgeCount = new Dictionary<Vector2Int, int>();
        var edgeEnds = new Dictionary<Vector2Int, (Vector3 a, Vector3 b)>();

        for (int dc = -TerritoryRadius; dc <= TerritoryRadius; dc++)
        {
            for (int dr = -TerritoryRadius; dr <= TerritoryRadius; dr++)
            {
                int c = col + dc;
                int r = row + dr;
                if (HexGrid.HexDistance(col, row, c, r) > TerritoryRadius) continue;
                // Clip at the poles (rows don't wrap); columns wrap freely.
                if (!grid.InBounds(grid.WrapColumn(c), r)) continue;

                Vector3 center = GridLocalCenter(c, r) - cityLocal + Vector3.up * lift;
                Vector3[] corners = HexGrid.HexCorners(center, grid.hexSize);
                for (int i = 0; i < 6; i++)
                {
                    Vector3 a = corners[i];
                    Vector3 b = corners[(i + 1) % 6];
                    Vector3 mid = (a + b) * 0.5f;
                    var key = new Vector2Int(
                        Mathf.RoundToInt(mid.x * 1000f),
                        Mathf.RoundToInt(mid.z * 1000f));
                    if (edgeCount.TryGetValue(key, out int n))
                    {
                        edgeCount[key] = n + 1;
                    }
                    else
                    {
                        edgeCount[key] = 1;
                        edgeEnds[key] = (a, b);
                    }
                }
            }
        }

        var verts = new List<Vector3>();
        var tris = new List<int>();
        float halfW = grid.borderWidth * 0.5f;
        foreach (var kv in edgeCount)
        {
            if (kv.Value != 1) continue; // interior edge — skip
            var (a, b) = edgeEnds[kv.Key];
            AddRingQuad(verts, tris, a, b, halfW);
        }

        var go = new GameObject("Territory");
        go.transform.SetParent(transform, worldPositionStays: false);

        var mesh = new Mesh
        {
            name = "CityTerritory",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
        };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        if (tileMaterial != null)
            mr.sharedMaterial = tileMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", Color.magenta);
        mpb.SetColor("_Color", Color.magenta);
        mr.SetPropertyBlock(mpb);
    }

    /// <summary>
    /// The grid-local center of a tile, using the same flat-top, odd-column
    /// offset layout as <see cref="HexGrid"/> but without applying the grid's
    /// transform — coordinates here are relative to the city's own origin.
    /// </summary>
    Vector3 GridLocalCenter(int c, int r)
    {
        float x = c * grid.HorizontalSpacing;
        float z = r * grid.VerticalSpacing + ((c & 1) == 1 ? grid.VerticalSpacing * 0.5f : 0f);
        return new Vector3(x, 0f, z);
    }

    // A thin quad along the edge a -> b, widened perpendicular on the XZ plane.
    // Mirrors HexGrid's border quads so the ring reads as the same kind of line.
    static void AddRingQuad(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, float halfW)
    {
        Vector3 edge = b - a;
        Vector3 perp = new Vector3(-edge.z, 0f, edge.x).normalized * halfW;

        int baseIdx = verts.Count;
        verts.Add(a + perp);
        verts.Add(b + perp);
        verts.Add(b - perp);
        verts.Add(a - perp);

        tris.Add(baseIdx);
        tris.Add(baseIdx + 1);
        tris.Add(baseIdx + 2);
        tris.Add(baseIdx);
        tris.Add(baseIdx + 2);
        tris.Add(baseIdx + 3);
    }

    void BuildLabel(TMP_Text template, float tileLift, float drop)
    {
        var go = new GameObject("Name");
        go.transform.SetParent(transform, worldPositionStays: false);
        // Sit just above the tile (so fog doesn't occlude it) and offset south
        // so it reads as a caption below the marker under the tilted camera.
        go.transform.localPosition = new Vector3(0f, tileLift + 0.01f, -drop);

        label = go.AddComponent<TextMeshPro>();
        if (template != null)
        {
            if (template.font != null) label.font = template.font;
            if (template.fontSharedMaterial != null)
                label.fontSharedMaterial = template.fontSharedMaterial;
            label.fontSize = template.fontSize;
            label.color = template.color;
            label.rectTransform.sizeDelta = template.rectTransform.sizeDelta;
            label.rectTransform.pivot = template.rectTransform.pivot;
            go.transform.localScale = template.rectTransform.localScale;
        }
        else
        {
            label.fontSize = 8f;
            label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(8f, 2f);
            go.transform.localScale = Vector3.one * 0.25f;
        }
        label.alignment = TextAlignmentOptions.Center;
        label.text = cityName;

        go.AddComponent<Billboard>();
    }

    /// <summary>
    /// Builds the floating "+1" growth indicator above the city marker, reusing
    /// the same world-space label styling as the name caption. Starts hidden;
    /// <see cref="UpdateGrowthLabel"/> toggles it with <see cref="IsAccruing"/>.
    /// Offset north (positive Z) so it reads above the marker, opposite the
    /// name caption which sits to the south.
    /// </summary>
    void BuildGrowthLabel(TMP_Text template, float tileLift, float drop)
    {
        growthLabel = new GameObject("GrowthIndicator");
        growthLabel.transform.SetParent(transform, worldPositionStays: false);
        growthLabel.transform.localPosition = new Vector3(0f, tileLift + 0.01f, drop);

        var text = growthLabel.AddComponent<TextMeshPro>();
        if (template != null)
        {
            if (template.font != null) text.font = template.font;
            if (template.fontSharedMaterial != null)
                text.fontSharedMaterial = template.fontSharedMaterial;
            text.fontSize = template.fontSize;
            text.rectTransform.sizeDelta = template.rectTransform.sizeDelta;
            text.rectTransform.pivot = template.rectTransform.pivot;
            growthLabel.transform.localScale = template.rectTransform.localScale;
        }
        else
        {
            text.fontSize = 8f;
            text.rectTransform.sizeDelta = new Vector2(8f, 2f);
            growthLabel.transform.localScale = Vector3.one * 0.25f;
        }
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.green;
        text.text = "+1";

        growthLabel.AddComponent<Billboard>();
        growthLabel.SetActive(false);
    }

    /// <summary>
    /// Shows the "+1" indicator while growth is accruing and hides it otherwise.
    /// </summary>
    void UpdateGrowthLabel()
    {
        if (growthLabel != null)
            growthLabel.SetActive(IsAccruing);
    }

    void LateUpdate()
    {
        SyncDisplay();
    }

    void SyncDisplay()
    {
        if (grid == null) return;
        Vector3 center = grid.HexCenter(col, row);
        if (grid.wrap)
        {
            Camera cam = Camera.main;
            if (cam != null)
                center = grid.WrapToCamera(center, cam.transform.position.x);
        }
        transform.position = center;
    }
}
