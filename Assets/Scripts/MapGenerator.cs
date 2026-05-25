using UnityEngine;

/// <summary>
/// Procedural land/sea generator. Drives the binary terrain layer the
/// <see cref="HexGrid"/> needs to know about: which tiles are continents and
/// which are ocean. Future expansion (biomes, rivers, mountains, resources)
/// will layer on top of the same Perlin field this produces.
///
/// Algorithm: classic fractal-noise heightmap, thresholded at
/// <see cref="seaLevel"/> to split land from water. A polar falloff pulls
/// the top and bottom rows toward water so continents tend to sit in
/// temperate latitudes and the world reads as having north/south oceans
/// rather than continents that run off the edge of the map. When the
/// <see cref="HexGrid"/> wraps east–west, the noise is sampled with a
/// lerp-across-the-seam trick so coastlines are continuous across the wrap.
/// </summary>
[RequireComponent(typeof(HexGrid))]
public class MapGenerator : MonoBehaviour
{
    [Header("Noise shape")]
    [Tooltip("World units per noise feature. Bigger = fewer, larger continents.")]
    public float noiseScale = 80f;

    [Tooltip("FBM octaves. More = jaggier coastlines, more small islands.")]
    [Range(1, 6)] public int octaves = 4;

    [Tooltip("Amplitude falloff per octave. 0.5 is the textbook value.")]
    [Range(0f, 1f)] public float persistence = 0.5f;

    [Header("Land / sea balance")]
    [Tooltip("Height threshold separating land from water. Higher = more ocean. " +
             "Perlin clusters near 0.5 so small changes around here have outsized " +
             "effect on the land fraction.")]
    [Range(0f, 1f)] public float seaLevel = 0.5f;

    [Header("Polar oceans")]
    [Tooltip("How aggressively to push the top and bottom rows toward water. " +
             "0 = no bias, 1 = the very edge is guaranteed ocean.")]
    [Range(0f, 1f)] public float polarFalloff = 0.6f;

    [Tooltip("Fraction of map height over which the polar bias ramps from full " +
             "strength at the edge to none in the interior. 0.2 = ~20% of rows " +
             "near each pole are biased.")]
    [Range(0.01f, 0.5f)] public float polarBand = 0.18f;

    [Header("Determinism")]
    [Tooltip("0 = pick a fresh random seed each play. Any other value = repeatable.")]
    public int seed = 0;

    /// <summary>
    /// Builds a <see cref="MapData"/> sized to the grid. <paramref name="horizSpacing"/>
    /// and <paramref name="vertSpacing"/> are the world-space hex spacings so the
    /// noise samples in continuous world coordinates (continents have a real-world
    /// size regardless of how many tiles fit in them). <paramref name="wrap"/>
    /// enables the seamless-noise branch so the world wraps cleanly E–W.
    /// </summary>
    public MapData Generate(int columns, int rows,
                            float horizSpacing, float vertSpacing,
                            bool wrap)
    {
        var data = new MapData(columns, rows);
        int effectiveSeed = seed == 0 ? Random.Range(1, int.MaxValue) : seed;
        var rng = new System.Random(effectiveSeed);
        Vector2 offset = new Vector2(
            (float)(rng.NextDouble() * 1000.0),
            (float)(rng.NextDouble() * 1000.0));

        float worldWidth = columns * horizSpacing;

        for (int col = 0; col < columns; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                float x = col * horizSpacing;
                float y = row * vertSpacing + ((col & 1) == 1 ? vertSpacing * 0.5f : 0f);

                float h = wrap
                    ? FbmSeamless(x, y, worldWidth, noiseScale, octaves, persistence, offset)
                    : Fbm(x, y, noiseScale, octaves, persistence, offset);

                h *= LatitudeMultiplier(row, rows);

                data[col, row] = h > seaLevel ? TerrainType.Land : TerrainType.Water;
            }
        }

        return data;
    }

    // Goes from 1 in the middle of the map down to (1 - polarFalloff) right at
    // row 0 and row (rows-1). Multiplying the noise by this drags edge tiles
    // beneath sea level so they tend to be ocean.
    float LatitudeMultiplier(int row, int rows)
    {
        if (rows <= 1) return 1f;
        float v = (float)row / (rows - 1);                 // [0, 1]
        float distFromEdge = Mathf.Min(v, 1f - v);          // [0, 0.5]
        // GLSL-style smoothstep across the band: 0 right at the pole, 1 once
        // we're at least polarBand fraction of the map away from it. Note:
        // Unity's Mathf.SmoothStep(a, b, t) is a smoothed lerp from a to b
        // — *not* the threshold smoothstep — which is why we inline the math.
        float t = Mathf.Clamp01(distFromEdge / Mathf.Max(0.0001f, polarBand));
        t = t * t * (3f - 2f * t);
        return Mathf.Lerp(1f - polarFalloff, 1f, t);
    }

    static float Fbm(float x, float y, float baseScale, int octaves,
                     float persistence, Vector2 offset)
    {
        float sum = 0f;
        float amp = 1f;
        float ampSum = 0f;
        float freq = 1f / Mathf.Max(0.0001f, baseScale);
        for (int i = 0; i < octaves; i++)
        {
            // Per-octave offsets are arbitrary irrational-ish constants — they
            // just decorrelate octaves so the lower octaves don't all peak at
            // the same point.
            sum += Mathf.PerlinNoise(
                (x + offset.x + i * 17.31f) * freq,
                (y + offset.y + i * 23.79f) * freq) * amp;
            ampSum += amp;
            amp *= persistence;
            freq *= 2f;
        }
        return ampSum > 0f ? sum / ampSum : 0f;
    }

    // Seamless E-W noise: f(0) == f(W). Lerping between two FBM samples — one
    // anchored at the canonical x and one shifted by -W — using x/W as the
    // blend factor makes the function repeat with period W.
    static float FbmSeamless(float x, float y, float worldWidth, float baseScale,
                             int octaves, float persistence, Vector2 offset)
    {
        if (worldWidth <= 0f)
            return Fbm(x, y, baseScale, octaves, persistence, offset);
        float a = Fbm(x, y, baseScale, octaves, persistence, offset);
        float b = Fbm(x - worldWidth, y, baseScale, octaves, persistence, offset);
        float t = Mathf.Clamp01(x / worldWidth);
        return Mathf.Lerp(a, b, t);
    }
}
