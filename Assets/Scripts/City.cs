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
    /// Current number of inhabitants. A freshly founded city starts at
    /// <see cref="StartingPopulation"/>; growth systems come later (SPEC.md §3.2).
    /// </summary>
    public int population = StartingPopulation;

    /// <summary>The city's hex as offset coordinates.</summary>
    public Vector2Int Coord => new Vector2Int(col, row);

    TMP_Text label;

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
        BuildLabel(labelTemplate, tileLift, labelDrop);
        SyncDisplay();
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
