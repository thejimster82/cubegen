using Godot;
using System;
using System.Collections.Generic;
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
                GD.Print("ColorVariationGenerator not initialized ");
                return baseColor;
            }

            // Get variation intensity for this voxel type
            if (!_variationIntensity.TryGetValue(voxelType, out float intensity))
            {
                intensity = 0.1f; // Use default if not found
            }

            // Get noise values at this position - use a single noise value for more consistent coloring
            float primaryNoise = _colorNoise.GetNoise2D(worldX, worldZ);
            float detailNoise = _detailNoise.GetNoise2D(worldX, worldZ) * 0.5f;

            // Calculate variation factor from noise - keep it within a smaller range
            // Map the noise from [-1,1] to [0,1] range
            float variationFactor = (primaryNoise + detailNoise + 2.0f) * 0.5f * intensity;

            // Instead of varying RGB channels independently (which creates rainbow effects),
            // use a more controlled approach that maintains the color's character

            // Extract HSV components from the base color
            baseColor.ToHsv(out float h, out float s, out float v);

            // Allow more significant hue variation (within ±15% of the original hue)
            // This creates more pronounced color variations while still keeping colors in the same family
            float hueVariation = ((_rNoise.GetNoise2D(worldX, worldZ) + 1.0f) * 0.15f) - 0.1f;
            h = Mathf.Wrap(h + hueVariation, 0.0f, 1.0f);

            // Vary saturation and value more dramatically
            // This creates much more noticeable variations while still maintaining some character of the original color
            s = Mathf.Clamp(s * (0.6f + variationFactor * 0.8f), 0.0f, 1.0f);
            v = Mathf.Clamp(v * (0.5f + variationFactor * 1.0f), 0.0f, 1.0f);

            // Convert back to RGB
            Color variedColor = Color.FromHsv(h, s, v);
            variedColor.A = baseColor.A;

            return variedColor;
        }
    }
}
