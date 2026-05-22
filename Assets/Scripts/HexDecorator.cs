using UnityEngine;

/// <summary>
/// Scatters small decorative props (grass tufts, etc.) across every tile of
/// the attached <see cref="HexGrid"/> at runtime, using a deterministic
/// per-tile seed so the same layout reappears across plays.
/// </summary>
[RequireComponent(typeof(HexGrid))]
public class HexDecorator : MonoBehaviour
{
    [Header("Props")]
    [Tooltip("Prefabs scattered across each hex. One is picked at random per instance.")]
    public GameObject[] grassPrefabs;

    [Header("Density (per hex)")]
    [Range(0, 30)] public int minPerHex = 3;
    [Range(0, 30)] public int maxPerHex = 5;

    [Header("Placement")]
    [Tooltip("Tufts are placed within this fraction of the hex radius from the center. " +
             "Keep <= 0.866 to stay inside the inscribed circle of a flat-top hex.")]
    [Range(0.1f, 0.866f)] public float radiusFraction = 0.75f;

    [Tooltip("Random uniform scale multiplier applied on top of the prefab's own scale.")]
    [Range(0.1f, 2f)] public float minScale = 0.7f;
    [Range(0.1f, 2f)] public float maxScale = 1.1f;

    [Header("Determinism")]
    [Tooltip("Same seed = same scatter pattern across plays.")]
    public int seed = 1337;

    void Start()
    {
        if (grassPrefabs == null || grassPrefabs.Length == 0)
        {
            Debug.LogWarning("HexDecorator: no prefabs assigned, nothing to scatter.", this);
            return;
        }

        var grid = GetComponent<HexGrid>();
        var parent = new GameObject("Decorations").transform;
        parent.SetParent(transform, worldPositionStays: false);

        for (int col = 0; col < grid.columns; col++)
            for (int row = 0; row < grid.rows; row++)
                ScatterOnHex(grid, col, row, parent);
    }

    void ScatterOnHex(HexGrid grid, int col, int row, Transform parent)
    {
        // Hashed seed so each tile draws from an independent sequence and
        // changing the grid size doesn't shuffle every tile's layout.
        int hexSeed = seed ^ (col * 73856093) ^ (row * 19349663);
        var rng = new System.Random(hexSeed);

        Vector3 center = grid.HexCenter(col, row);
        int count = rng.Next(minPerHex, maxPerHex + 1);

        for (int i = 0; i < count; i++)
        {
            // Uniform disk sample: sqrt(u) gives equal area per radial band.
            float angle = (float)(rng.NextDouble() * 2.0 * System.Math.PI);
            float radius = (float)System.Math.Sqrt(rng.NextDouble())
                           * radiusFraction * grid.hexSize;
            Vector3 pos = center + new Vector3(
                Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            float scale = Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());
            float rotY = (float)(rng.NextDouble() * 360.0);

            var prefab = grassPrefabs[rng.Next(grassPrefabs.Length)];
            var tuft = Instantiate(prefab, pos, Quaternion.Euler(0f, rotY, 0f), parent);
            tuft.transform.localScale *= scale;
        }
    }
}
