using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
    /// <summary>
    /// Generates environmental features like hills, mountains, rivers, and lakes
    /// based on biome types and terrain characteristics.
    /// </summary>
    public class EnvironmentalFeatureGenerator
    {
        // Noise generators for different features
        private FastNoiseLite _hillNoise;
        private FastNoiseLite _mountainNoise;
        private FastNoiseLite _riverNoise;
        private FastNoiseLite _featurePlacementNoise;

        // Feature probability by biome type
        private Dictionary<BiomeType, Dictionary<FeatureType, float>> _featureProbabilities;

        // Seed for random generation
        private int _seed;

        // Feature scales and parameters
        private float _hillScale = 0.01f;
        private float _mountainScale = 0.008f;
        private float _riverScale = 0.005f;

        // Constructor
        public EnvironmentalFeatureGenerator(int seed)
        {
            _seed = seed;
            InitializeNoise();
            InitializeFeatureProbabilities();
        }

        // Initialize noise generators
        private void InitializeNoise()
        {
            // Hill noise - gentle, rolling
            _hillNoise = new FastNoiseLite();
            _hillNoise.Seed = _seed + 100;
            _hillNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _hillNoise.Frequency = _hillScale;
            _hillNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _hillNoise.FractalOctaves = 3;
            _hillNoise.FractalLacunarity = 2.0f;
            _hillNoise.FractalGain = 0.5f;

            // Mountain noise - more dramatic, ridged
            _mountainNoise = new FastNoiseLite();
            _mountainNoise.Seed = _seed + 200;
            _mountainNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _mountainNoise.Frequency = _mountainScale;
            _mountainNoise.FractalType = FastNoiseLite.FractalTypeEnum.Ridged;
            _mountainNoise.FractalOctaves = 4;
            _mountainNoise.FractalLacunarity = 2.2f;
            _mountainNoise.FractalGain = 0.6f;

            // River noise - for determining river paths
            _riverNoise = new FastNoiseLite();
            _riverNoise.Seed = _seed + 300;
            _riverNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _riverNoise.Frequency = _riverScale;
            _riverNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _riverNoise.FractalOctaves = 2;

            // Feature placement noise - for determining where features should be placed
            _featurePlacementNoise = new FastNoiseLite();
            _featurePlacementNoise.Seed = _seed + 400;
            _featurePlacementNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _featurePlacementNoise.Frequency = 0.006f; // Increased from 0.003f for more frequent features
        }

        // Initialize feature probabilities by biome
        private void InitializeFeatureProbabilities()
        {
            _featureProbabilities = new Dictionary<BiomeType, Dictionary<FeatureType, float>>();

            // Base probabilities that are more balanced across biomes
            // This ensures features can span across biome boundaries more naturally

            // Plains biome - increased density of all features
            var plainsProbabilities = new Dictionary<FeatureType, float>
            {
                { FeatureType.Hill, 0.7f },       // Increased from 0.5f
                { FeatureType.Mountain, 0.4f },   // Increased from 0.2f
                { FeatureType.River, 0.5f },      // Increased from 0.3f
                { FeatureType.Creek, 0.5f },      // Increased from 0.3f
                { FeatureType.Lake, 0.5f }        // Increased from 0.3f
            };
            _featureProbabilities[BiomeType.Plains] = plainsProbabilities;

            // Forest biome - increased density of all features
            var forestProbabilities = new Dictionary<FeatureType, float>
            {
                { FeatureType.Hill, 0.7f },       // Increased from 0.5f
                { FeatureType.Mountain, 0.4f },   // Increased from 0.2f
                { FeatureType.River, 0.5f },      // Increased from 0.3f
                { FeatureType.Creek, 0.5f },      // Increased from 0.3f
                { FeatureType.Lake, 0.5f }        // Increased from 0.3f
            };
            _featureProbabilities[BiomeType.Forest] = forestProbabilities;

            // Desert biome - increased density of all features, but still less water than other biomes
            var desertProbabilities = new Dictionary<FeatureType, float>
            {
                { FeatureType.Hill, 0.7f },       // Increased from 0.5f
                { FeatureType.Mountain, 0.4f },   // Increased from 0.2f
                { FeatureType.River, 0.4f },      // Increased from 0.2f
                { FeatureType.Creek, 0.4f },      // Increased from 0.2f
                { FeatureType.Lake, 0.4f }        // Increased from 0.2f
            };
            _featureProbabilities[BiomeType.Desert] = desertProbabilities;

            // Mountains biome - increased density of all features, with even more mountains
            var mountainsProbabilities = new Dictionary<FeatureType, float>
            {
                { FeatureType.Hill, 0.7f },       // Increased from 0.5f
                { FeatureType.Mountain, 0.6f },   // Increased from 0.4f
                { FeatureType.River, 0.5f },      // Increased from 0.3f
                { FeatureType.Creek, 0.5f },      // Increased from 0.3f
                { FeatureType.Lake, 0.5f }        // Increased from 0.3f
            };
            _featureProbabilities[BiomeType.Mountains] = mountainsProbabilities;

            // Tundra biome - increased density of all features
            var tundraProbabilities = new Dictionary<FeatureType, float>
            {
                { FeatureType.Hill, 0.7f },       // Increased from 0.5f
                { FeatureType.Mountain, 0.4f },   // Increased from 0.2f
                { FeatureType.River, 0.5f },      // Increased from 0.3f
                { FeatureType.Creek, 0.5f },      // Increased from 0.3f
                { FeatureType.Lake, 0.5f }        // Increased from 0.3f
            };
            _featureProbabilities[BiomeType.Tundra] = tundraProbabilities;
        }

        /// <summary>
        /// Modifies the terrain height based on environmental features at the given position.
        /// </summary>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldZ">World Z coordinate</param>
        /// <param name="baseHeight">Base height from the regular terrain generator</param>
        /// <param name="biomeType">Biome type at this position</param>
        /// <param name="maxHeight">Maximum possible height</param>
        /// <returns>Modified height value</returns>
        public int ModifyTerrainHeight(int worldX, int worldZ, int baseHeight, BiomeType biomeType, int maxHeight)
        {
            // Get feature probabilities for this biome
            var probabilities = _featureProbabilities[biomeType];

            // Calculate feature influences
            float hillInfluence = CalculateHillInfluence(worldX, worldZ, biomeType);
            float mountainInfluence = CalculateMountainInfluence(worldX, worldZ, biomeType);
            float riverInfluence = CalculateRiverInfluence(worldX, worldZ, biomeType);
            float lakeInfluence = CalculateLakeInfluence(worldX, worldZ, biomeType);

            // Apply hill and mountain influences to increase height
            float heightModifier = 0;

            // Hills add gentle elevation - increased height
            heightModifier += hillInfluence * 25; // Increased from 15 to 25 blocks of height

            // Mountains add more dramatic elevation - increased height
            heightModifier += mountainInfluence * 60; // Increased from 40 to 60 blocks of height

            // Rivers and lakes subtract from height to create depressions
            if (riverInfluence > 0.6f) // Reduced from 0.7f to match ShouldPlaceWater
            {
                // Calculate river depth based on width (wider rivers are deeper)
                float riverWidth = 1.0f - riverInfluence;
                float riverDepth = 4 + (riverWidth * 6); // Increased from 3-8 to 4-10 blocks deep

                // Subtract from height to create the river bed
                heightModifier -= riverDepth;

                // Ensure we don't go below minimum river depth
                int minRiverHeight = 5;
                if (baseHeight + heightModifier < minRiverHeight)
                {
                    heightModifier = minRiverHeight - baseHeight;
                }
            }

            // Lakes create larger depressions
            if (lakeInfluence > 0.7f) // Reduced from 0.8f to match ShouldPlaceWater
            {
                // Calculate lake depth
                float lakeDepth = 6 + (lakeInfluence - 0.7f) * 12; // Increased from 5-7 to 6-12 blocks deep

                // Subtract from height to create the lake bed
                heightModifier -= lakeDepth;

                // Ensure we don't go below minimum lake depth
                int minLakeHeight = 5;
                if (baseHeight + heightModifier < minLakeHeight)
                {
                    heightModifier = minLakeHeight - baseHeight;
                }
            }

            // Calculate final height
            int modifiedHeight = baseHeight + Mathf.RoundToInt(heightModifier);

            // Clamp to valid range
            modifiedHeight = Mathf.Clamp(modifiedHeight, 1, maxHeight - 1);

            return modifiedHeight;
        }

        /// <summary>
        /// Determines if a water voxel should be placed at the given position.
        /// </summary>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldZ">World Z coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="terrainHeight">Terrain height at this position</param>
        /// <param name="biomeType">Biome type at this position</param>
        /// <returns>True if water should be placed, false otherwise</returns>
        public bool ShouldPlaceWater(int worldX, int worldZ, int y, int terrainHeight, BiomeType biomeType)
        {
            // Check for rivers - lowered threshold for more water
            float riverInfluence = CalculateRiverInfluence(worldX, worldZ, biomeType);
            if (riverInfluence > 0.6f && y <= terrainHeight) // Reduced from 0.7f
            {
                return true;
            }

            // Check for lakes - lowered threshold for more water
            float lakeInfluence = CalculateLakeInfluence(worldX, worldZ, biomeType);
            if (lakeInfluence > 0.7f && y <= terrainHeight) // Reduced from 0.8f
            {
                return true;
            }

            return false;
        }

        // Calculate hill influence at a position
        private float CalculateHillInfluence(int worldX, int worldZ, BiomeType biomeType)
        {
            // Get base probability for this biome
            float baseProbability = _featureProbabilities[biomeType][FeatureType.Hill];

            // Get noise value for hill placement
            float placementNoise = _featurePlacementNoise.GetNoise2D(worldX * 0.01f, worldZ * 0.01f);
            placementNoise = (placementNoise + 1) * 0.5f; // Convert to 0-1 range

            // Only place hills where the placement noise exceeds the threshold
            if (placementNoise < 1 - baseProbability)
            {
                return 0;
            }

            // Calculate hill shape using hill noise
            float hillNoise = _hillNoise.GetNoise2D(worldX, worldZ);
            hillNoise = (hillNoise + 1) * 0.5f; // Convert to 0-1 range

                // Instead of biome-specific modifiers, use a consistent approach for hills
            // This ensures hills can span across biome boundaries without height discontinuities

            // We'll still use the biome probability to determine if hills should be present
            // but the actual hill shape will be consistent across biomes
            hillNoise *= 0.85f;

            return hillNoise;
        }

        // Calculate mountain influence at a position
        private float CalculateMountainInfluence(int worldX, int worldZ, BiomeType biomeType)
        {
            // Get base probability for this biome
            float baseProbability = _featureProbabilities[biomeType][FeatureType.Mountain];

            // Get noise value for mountain placement
            float placementNoise = _featurePlacementNoise.GetNoise2D(worldX * 0.005f, worldZ * 0.005f);
            placementNoise = (placementNoise + 1) * 0.5f; // Convert to 0-1 range

            // Only place mountains where the placement noise exceeds the threshold
            if (placementNoise < 1 - baseProbability)
            {
                return 0;
            }

            // Calculate mountain shape using mountain noise
            float mountainNoise = _mountainNoise.GetNoise2D(worldX, worldZ);
            mountainNoise = (mountainNoise + 1) * 0.5f; // Convert to 0-1 range

            // Use a consistent approach for mountains across all biomes
            // This ensures mountains can span across biome boundaries without height discontinuities

            // We'll still use the biome probability to determine if mountains should be present
            // but the actual mountain shape will be consistent across biomes
            mountainNoise *= 1.0f;

            return mountainNoise;
        }

        // Calculate river influence at a position
        private float CalculateRiverInfluence(int worldX, int worldZ, BiomeType biomeType)
        {
            // Get base probability for this biome
            float baseProbability = _featureProbabilities[biomeType][FeatureType.River];

            // Rivers are more complex - we want them to flow naturally
            // Use domain warping to create winding river paths
            float warpX = _riverNoise.GetNoise2D(worldX * 0.01f, worldZ * 0.01f) * 100;
            float warpZ = _riverNoise.GetNoise2D(worldX * 0.01f + 100, worldZ * 0.01f + 100) * 100;

            // Sample noise at warped coordinates for river path
            float riverNoise = _riverNoise.GetNoise2D((worldX + warpX) * 0.02f, (worldZ + warpZ) * 0.02f);

            // Convert to 0-1 range and invert (rivers form in low areas of noise)
            riverNoise = 1 - ((riverNoise + 1) * 0.5f);

            // Apply threshold based on biome probability
            float threshold = 1 - (baseProbability * 0.5f); // Increased from 0.3f for more rivers

            // River width factor - higher values make wider rivers
            float widthFactor = 0.08f; // Increased from 0.05f for wider rivers

            // Calculate river value - higher means more likely to be a river
            float riverValue = 0;
            if (riverNoise > threshold)
            {
                // Calculate how far above threshold (0-1 range)
                float thresholdDelta = (riverNoise - threshold) / (1 - threshold);

                // Apply sigmoid function to create defined river banks
                riverValue = 1.0f / (1.0f + Mathf.Exp(-(thresholdDelta - 0.5f) / widthFactor));
            }

            // Use a consistent approach for rivers across all biomes
            // This ensures rivers can span across biome boundaries without discontinuities

            // We'll still use the biome probability to determine if rivers should be present
            // but the actual river shape will be consistent across biomes
            riverValue *= 1.0f;

            return riverValue;
        }

        // Calculate creek influence at a position (smaller rivers)
        private float CalculateCreekInfluence(int worldX, int worldZ, BiomeType biomeType)
        {
            // Get base probability for this biome
            float baseProbability = _featureProbabilities[biomeType][FeatureType.Creek];

            // Similar to rivers but with different parameters for smaller creeks
            float warpX = _riverNoise.GetNoise2D(worldX * 0.02f + 50, worldZ * 0.02f + 50) * 50;
            float warpZ = _riverNoise.GetNoise2D(worldX * 0.02f + 150, worldZ * 0.02f + 150) * 50;

            // Sample noise at warped coordinates for creek path
            float creekNoise = _riverNoise.GetNoise2D((worldX + warpX) * 0.04f, (worldZ + warpZ) * 0.04f);

            // Convert to 0-1 range and invert (creeks form in low areas of noise)
            creekNoise = 1 - ((creekNoise + 1) * 0.5f);

            // Apply threshold based on biome probability
            float threshold = 1 - (baseProbability * 0.4f); // Increased from 0.2f for more creeks

            // Creek width factor - higher values make wider creeks
            float widthFactor = 0.05f; // Increased from 0.03f, still narrower than rivers

            // Calculate creek value - higher means more likely to be a creek
            float creekValue = 0;
            if (creekNoise > threshold)
            {
                // Calculate how far above threshold (0-1 range)
                float thresholdDelta = (creekNoise - threshold) / (1 - threshold);

                // Apply sigmoid function to create defined creek banks
                creekValue = 1.0f / (1.0f + Mathf.Exp(-(thresholdDelta - 0.5f) / widthFactor));
            }

            // Use a consistent approach for creeks across all biomes
            // This ensures creeks can span across biome boundaries without discontinuities

            // We'll still use the biome probability to determine if creeks should be present
            // but the actual creek shape will be consistent across biomes
            creekValue *= 1.0f;

            return creekValue;
        }

        // Calculate lake influence at a position
        private float CalculateLakeInfluence(int worldX, int worldZ, BiomeType biomeType)
        {
            // Get base probability for this biome
            float baseProbability = _featureProbabilities[biomeType][FeatureType.Lake];

            // Get noise value for lake placement
            float placementNoise = _featurePlacementNoise.GetNoise2D(worldX * 0.003f + 200, worldZ * 0.003f + 200);
            placementNoise = (placementNoise + 1) * 0.5f; // Convert to 0-1 range

            // Only place lakes where the placement noise exceeds the threshold
            if (placementNoise < 1 - baseProbability)
            {
                return 0;
            }

            // Calculate lake shape using a different noise
            float lakeNoise = _hillNoise.GetNoise2D(worldX * 0.02f + 300, worldZ * 0.02f + 300);
            lakeNoise = (lakeNoise + 1) * 0.5f; // Convert to 0-1 range

            // Lakes form in circular/oval depressions
            // Calculate distance from lake center
            float centerX = Mathf.Round(worldX / 100.0f) * 100.0f;
            float centerZ = Mathf.Round(worldZ / 100.0f) * 100.0f;

            // Add some variation to lake center
            centerX += _hillNoise.GetNoise2D(centerX, centerZ) * 30;
            centerZ += _hillNoise.GetNoise2D(centerX + 100, centerZ + 100) * 30;

            // Calculate distance from center
            float distX = worldX - centerX;
            float distZ = worldZ - centerZ;

            // Create oval lakes by varying the x and z scales - increased size
            float xScale = 50 + _hillNoise.GetNoise2D(centerX, centerZ + 200) * 30; // Increased from 30+20
            float zScale = 50 + _hillNoise.GetNoise2D(centerX + 200, centerZ) * 30; // Increased from 30+20

            // Normalize distance
            float normalizedDist = Mathf.Sqrt((distX * distX) / (xScale * xScale) + (distZ * distZ) / (zScale * zScale));

            // Lake value decreases with distance from center
            float lakeValue = 1.0f - normalizedDist;

            // Apply threshold for lake edge
            if (lakeValue < 0)
            {
                lakeValue = 0;
            }

            // Use a consistent approach for lakes across all biomes
            // This ensures lakes can span across biome boundaries without discontinuities

            // We'll still use the biome probability to determine if lakes should be present
            // but the actual lake shape will be consistent across biomes
            lakeValue *= 1.0f;

            return lakeValue;
        }
    }

    // Enum for different types of environmental features
    public enum FeatureType
    {
        Hill,
        Mountain,
        River,
        Creek,
        Lake
    }
}
