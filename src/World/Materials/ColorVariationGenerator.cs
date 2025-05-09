using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent; // Added for ConcurrentDictionary
using CubeGen.World.Common;

namespace CubeGen.World.Materials
{
    /// <summary>
    /// Generates color variations for voxels to create natural-looking gradients across terrain
    /// </summary>
    public static class ColorVariationGenerator
    {
        // Noise generators for different variation patterns
        private static FastNoiseLite _colorNoise;
        private static FastNoiseLite _detailNoise;
        private static FastNoiseLite _rNoise;
        private static FastNoiseLite _gNoise;
        private static FastNoiseLite _bNoise;

        private static bool _initialized = false;

        // Variation intensity by voxel type (how much the color can vary)
        private static Dictionary<VoxelType, float> _variationIntensity = new Dictionary<VoxelType, float>();

        // Seed for the noise generators
        private static int _seed = 0;

        /// <summary>
        /// Initialize the color variation generator with a specific seed
        /// </summary>
        public static void Initialize(int seed)
        {
            _seed = seed;

            // Initialize primary color noise (large-scale variations)
            _colorNoise = new FastNoiseLite();
            _colorNoise.Seed = seed;
            _colorNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _colorNoise.Frequency = 0.008f; // Increased frequency for more noticeable gradients
            _colorNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _colorNoise.FractalOctaves = 3; // More octaves for more complex patterns
            _colorNoise.FractalLacunarity = 2.2f; // Increased lacunarity for more variation between scales
            _colorNoise.FractalGain = 0.65f; // Higher gain for more dramatic effect

            // Initialize detail noise (smaller variations)
            _detailNoise = new FastNoiseLite();
            _detailNoise.Seed = seed + 1000;
            _detailNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _detailNoise.Frequency = 0.03f; // Higher frequency for more dramatic local variations
            _detailNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _detailNoise.FractalOctaves = 3; // More octaves for more complex patterns
            _detailNoise.FractalGain = 0.6f; // Higher gain for more dramatic effect

            // Initialize R channel noise (used for hue variation)
            _rNoise = new FastNoiseLite();
            _rNoise.Seed = seed + 2000;
            _rNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _rNoise.Frequency = 0.012f; // Increased frequency for more dramatic hue variations
            _rNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _rNoise.FractalOctaves = 3; // More octaves for more complex patterns
            _rNoise.FractalGain = 0.6f; // Higher gain for more dramatic effect

            // Initialize G channel noise
            _gNoise = new FastNoiseLite();
            _gNoise.Seed = seed + 3000;
            _gNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _gNoise.Frequency = 0.01f;
            _gNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _gNoise.FractalOctaves = 1;
            _gNoise.FractalGain = 0.3f;

            // Initialize B channel noise
            _bNoise = new FastNoiseLite();
            _bNoise.Seed = seed + 4000;
            _bNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _bNoise.Frequency = 0.01f;
            _bNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _bNoise.FractalOctaves = 1;
            _bNoise.FractalGain = 0.3f;

            // Set up variation intensity for different voxel types
            InitializeVariationIntensity();

            _initialized = true;

            GD.Print("ColorVariationGenerator initialized with seed: " + seed);
        }

        /// <summary>
        /// Set up how much each voxel type's color can vary
        /// </summary>
        private static void InitializeVariationIntensity()
        {
            // Default to a high variation for all types
            foreach (VoxelType type in Enum.GetValues(typeof(VoxelType)))
            {
                _variationIntensity.Add(type, 1.0f);
            }

            // Set specific variation intensities for natural materials
            // Using high values for more dramatic variations
            _variationIntensity[VoxelType.Grass] = 1.2f;      // 120% variation for grass
            _variationIntensity[VoxelType.Dirt] = 1.0f;       // 100% variation for dirt
            _variationIntensity[VoxelType.Stone] = 0.9f;      // 90% variation for stone
            _variationIntensity[VoxelType.Sand] = 0.5f;       // 100% variation for sand
            _variationIntensity[VoxelType.Wood] = 0.9f;       // 90% variation for wood
            _variationIntensity[VoxelType.Leaves] = 1.2f;     // 120% variation for leaves

            // Less variation for manufactured/uniform materials but still significant
            _variationIntensity[VoxelType.Bedrock] = 0.8f;    // 80% variation for bedrock
            _variationIntensity[VoxelType.Cloud] = 0.9f;      // 90% variation for clouds

            // Water can have significant variation
            _variationIntensity[VoxelType.Water] = 0.9f;      // 90% variation for water

            // Decorations can have extreme variation
            _variationIntensity[VoxelType.TallGrass] = 1f;  // 130% variation for tall grass
            _variationIntensity[VoxelType.Flower] = 1.2f;     // 120% variation for flowers
            _variationIntensity[VoxelType.Mushroom] = 1.2f;   // 120% variation for mushrooms

            // Additional voxel types
            _variationIntensity[VoxelType.Snow] = 0.9f;       // 90% variation for snow
            _variationIntensity[VoxelType.Cactus] = 1.0f;     // 100% variation for cactus
            _variationIntensity[VoxelType.IceBlock] = 0.8f;   // 80% variation for ice
            _variationIntensity[VoxelType.SnowLeaves] = 1.0f; // 100% variation for snow-covered leaves
        }

        // THREAD SAFETY: Use ConcurrentDictionary for thread-safe color caching
        private static ConcurrentDictionary<(int x, int z, VoxelType type), Color> _colorCache = new ConcurrentDictionary<(int x, int z, VoxelType type), Color>();
        private const int CACHE_SIZE_LIMIT = 10000; // Limit cache size to prevent memory issues
        private static readonly object _colorCacheLock = new object(); // Lock for cache clearing operations

        /// <summary>
        /// Apply color variation to a base color based on world position and voxel type
        /// </summary>
        /// <param name="baseColor">The original color from the material</param>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldY">World Y coordinate (not used in current implementation)</param>
        /// <param name="worldZ">World Z coordinate</param>
        /// <param name="voxelType">Type of voxel</param>
        /// <returns>A new color with variation applied</returns>
        public static Color GetVariedColor(Color baseColor, float worldX, float worldY, float worldZ, VoxelType voxelType)
        {
            if (!_initialized)
            {
                return baseColor;
            }

            // OPTIMIZATION: Round coordinates to reduce unique positions and increase cache hits
            int roundedX = Mathf.RoundToInt(worldX / 2) * 2;
            int roundedZ = Mathf.RoundToInt(worldZ / 2) * 2;

            // Create cache key
            var cacheKey = (roundedX, roundedZ, voxelType);

            // THREAD SAFETY: Use thread-safe TryGetValue
            if (_colorCache.TryGetValue(cacheKey, out Color cachedColor))
            {
                return cachedColor;
            }

            // THREAD SAFETY: Use lock for cache size check and clearing
            // This prevents multiple threads from clearing the cache simultaneously
            if (_colorCache.Count > CACHE_SIZE_LIMIT)
            {
                lock (_colorCacheLock)
                {
                    // Double-check inside lock to avoid multiple clears
                    if (_colorCache.Count > CACHE_SIZE_LIMIT)
                    {
                        // Create a new dictionary instead of clearing
                        // This is safer for concurrent access
                        _colorCache = new ConcurrentDictionary<(int x, int z, VoxelType type), Color>();
                    }
                }
            }

            // OPTIMIZATION: Simplified color variation - just use one noise sample
            // Get variation intensity for this voxel type
            if (!_variationIntensity.TryGetValue(voxelType, out float intensity))
            {
                intensity = 0.1f; // Use default if not found
            }

            // Use a single noise sample for variation
            float noise = _colorNoise.GetNoise2D(roundedX, roundedZ);

            // Map noise from [-1,1] to [0.8,1.2] range for a subtle 20% variation
            float variationFactor = 1.0f + (noise * 0.2f * intensity);

            // Apply simple RGB scaling for variation
            Color variedColor = new Color(
                Mathf.Clamp(baseColor.R * variationFactor, 0.0f, 1.0f),
                Mathf.Clamp(baseColor.G * variationFactor, 0.0f, 1.0f),
                Mathf.Clamp(baseColor.B * variationFactor, 0.0f, 1.0f),
                baseColor.A
            );

            // THREAD SAFETY: Use thread-safe GetOrAdd to avoid race conditions
            // This ensures only one thread can add a value for a given key
            return _colorCache.GetOrAdd(cacheKey, _ => variedColor);
        }
    }
}
