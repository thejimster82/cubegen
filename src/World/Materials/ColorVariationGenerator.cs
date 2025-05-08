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
            _colorNoise.Frequency = 0.005f; // Very low frequency for broader, more noticeable gradients
            _colorNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _colorNoise.FractalOctaves = 3;
            _colorNoise.FractalLacunarity = 2.0f;
            _colorNoise.FractalGain = 0.6f; // Increased gain for more pronounced effect

            // Initialize detail noise (smaller variations)
            _detailNoise = new FastNoiseLite();
            _detailNoise.Seed = seed + 1000;
            _detailNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _detailNoise.Frequency = 0.03f; // Adjusted for better balance with primary noise
            _detailNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _detailNoise.FractalOctaves = 2;
            _detailNoise.FractalGain = 0.5f;

                        // Initialize detail noise (smaller variations)
            _rNoise = new FastNoiseLite();
            _rNoise.Seed = seed + 2000;
            _rNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _rNoise.Frequency = 0.03f; // Adjusted for better balance with primary noise
            _rNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _rNoise.FractalOctaves = 2;
            _rNoise.FractalGain = 0.5f;

                        // Initialize detail noise (smaller variations)
            _gNoise = new FastNoiseLite();
            _gNoise.Seed = seed + 3000;
            _gNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _gNoise.Frequency = 0.03f; // Adjusted for better balance with primary noise
            _gNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _gNoise.FractalOctaves = 2;
            _gNoise.FractalGain = 0.5f;

                        // Initialize detail noise (smaller variations)
            _bNoise = new FastNoiseLite();
            _bNoise.Seed = seed + 4000;
            _bNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _bNoise.Frequency = 0.03f; // Adjusted for better balance with primary noise
            _bNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _bNoise.FractalOctaves = 2;
            _bNoise.FractalGain = 0.5f;

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
            // Default to moderate variation for all types
            foreach (VoxelType type in Enum.GetValues(typeof(VoxelType)))
            {
                _variationIntensity.Add(type, 0.5f);
            }

            // Set specific variation intensities for natural materials
            _variationIntensity[VoxelType.Grass] = 0.45f;     // 45% variation for grass (increased from 35%)
            _variationIntensity[VoxelType.Dirt] = 0.4f;       // 40% variation for dirt (increased from 30%)
            _variationIntensity[VoxelType.Stone] = 0.35f;     // 35% variation for stone (increased from 25%)
            _variationIntensity[VoxelType.Sand] = 0.4f;       // 40% variation for sand (increased from 30%)
            _variationIntensity[VoxelType.Wood] = 0.3f;       // 30% variation for wood (increased from 20%)
            _variationIntensity[VoxelType.Leaves] = 0.45f;    // 45% variation for leaves (increased from 35%)

            // Less variation for manufactured/uniform materials
            _variationIntensity[VoxelType.Bedrock] = 0.25f;   // 25% variation for bedrock (increased from 15%)
            _variationIntensity[VoxelType.Cloud] = 0.3f;      // 30% variation for clouds (increased from 20%)

            // Water can have some variation
            _variationIntensity[VoxelType.Water] = 0.3f;      // 30% variation for water (increased from 20%)

            // Decorations can have more variation
            _variationIntensity[VoxelType.TallGrass] = 0.5f;  // 50% variation for tall grass (increased from 40%)
            _variationIntensity[VoxelType.Flower] = 0.4f;     // 40% variation for flowers (increased from 30%)
            _variationIntensity[VoxelType.Mushroom] = 0.4f;   // 40% variation for mushrooms (increased from 30%)
        }

        /// <summary>
        /// Apply color variation to a base color based on world position and voxel type
        /// </summary>
        /// <param name="baseColor">The original color from the material</param>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldY">World Y coordinate</param>
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
            float intensity = 0.1f; // Default
            if (_variationIntensity.ContainsKey(voxelType))
            {
                intensity = _variationIntensity[voxelType];
            }

            // Get noise values at this position
            float primaryNoise = _colorNoise.GetNoise2D(worldX, worldZ) * 3;
            // Uncomment to use detail noise for additional variation
            float detailNoise = _detailNoise.GetNoise2D(worldX, worldZ) * 3;
            float rNoise = _rNoise.GetNoise2D(worldX, worldZ) * 3;
            float gNoise = _gNoise.GetNoise2D(worldX, worldZ) * 3;
            float bNoise = _bNoise.GetNoise2D(worldX, worldZ) * 3;

            // Calculate variation factor from noise
            float variationFactor = primaryNoise * intensity + detailNoise * intensity;

            // Apply variation to each color channel with different multipliers
            // These different multipliers create more natural-looking variations by
            // shifting the color balance rather than just darkening/lightening
            float r = Mathf.Clamp(baseColor.R * (1.0f - variationFactor * rNoise), 0.0f, 1.0f);
            float g = Mathf.Clamp(baseColor.G * (1.0f - variationFactor * gNoise), 0.0f, 1.0f);
            float b = Mathf.Clamp(baseColor.B * (1.0f - variationFactor * bNoise), 0.0f, 1.0f);

            // Create new color with variation
            return new Color(r, g, b, baseColor.A);
        }
    }
}
