using Godot;
using System;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
    /// <summary>
    /// Manages sub-regions within biomes to create varied terrain and features within each biome type.
    /// </summary>
    public class BiomeSubRegions
    {
        // Sub-region types for ForestLands biome
        public enum ForestLandsSubRegion
        {
            Plains,     // Open areas with sparse trees
            Forest,     // Standard forest with medium tree density
            Mountains   // Mountainous areas with hills and rock formations
        }

        // Sub-region types for Desert biome
        public enum DesertSubRegion
        {
            Dunes,      // Sandy dunes with minimal vegetation
            Rocky,      // Rocky desert with more stone and rock formations
            Oasis       // Small areas with water and more vegetation
        }

        // Sub-region types for Tundra biome
        public enum TundraSubRegion
        {
            Snowy,      // Flat snowy plains
            Frozen,     // Areas with ice formations
            Alpine      // Mountainous tundra with rocky outcrops
        }

        // Sub-region types for Islands biome
        public enum IslandsSubRegion
        {
            Beach,      // Sandy beaches around the edges
            Jungle,     // Dense vegetation in the center
            Lagoon      // Shallow water areas with coral
        }

        // Static noise generators for sub-biome determination
        private static FastNoiseLite _subRegionNoise;
        private static FastNoiseLite _warpNoise;
        private static int _seed;
        private static bool _initialized = false;

        // Initialize the noise generators
        public static void Initialize(int seed)
        {
            _seed = seed;

            // Main sub-region noise
            _subRegionNoise = new FastNoiseLite();
            _subRegionNoise.Seed = seed + 2000;
            _subRegionNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _subRegionNoise.Frequency = 0.004f; // Medium frequency for medium-sized areas
            _subRegionNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _subRegionNoise.FractalOctaves = 2;
            _subRegionNoise.FractalLacunarity = 2.0f;
            _subRegionNoise.FractalGain = 0.5f;

            // Domain warp noise for more natural boundaries
            _warpNoise = new FastNoiseLite();
            _warpNoise.Seed = seed + 2500;
            _warpNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
            _warpNoise.Frequency = 0.003f;
            _warpNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _warpNoise.FractalOctaves = 2;
            _warpNoise.FractalLacunarity = 1.8f;
            _warpNoise.FractalGain = 0.4f;

            _initialized = true;
        }

        // Get the sub-region for a position within the ForestLands biome
        public static ForestLandsSubRegion GetForestLandsSubRegion(int worldX, int worldZ)
        {
            EnsureInitialized();

            // Apply domain warping for more natural boundaries
            float warpX = _warpNoise.GetNoise2D(worldX * 0.008f, worldZ * 0.008f) * 80.0f;
            float warpZ = _warpNoise.GetNoise2D((worldX + 500) * 0.008f, (worldZ + 500) * 0.008f) * 80.0f;

            // Get noise value with warping
            float noiseValue = _subRegionNoise.GetNoise2D(worldX + warpX, worldZ + warpZ);

            // Convert from [-1, 1] to [0, 1]
            noiseValue = (noiseValue + 1f) * 0.5f;

            // Divide into three equal ranges for the three sub-regions
            if (noiseValue < 0.333f)
                return ForestLandsSubRegion.Plains;
            else if (noiseValue < 0.667f)
                return ForestLandsSubRegion.Forest;
            else
                return ForestLandsSubRegion.Mountains;
        }

        // Get the sub-region for a position within the Desert biome
        public static DesertSubRegion GetDesertSubRegion(int worldX, int worldZ)
        {
            EnsureInitialized();

            // Apply domain warping for more natural boundaries
            float warpX = _warpNoise.GetNoise2D(worldX * 0.008f, worldZ * 0.008f) * 80.0f;
            float warpZ = _warpNoise.GetNoise2D((worldX + 500) * 0.008f, (worldZ + 500) * 0.008f) * 80.0f;

            // Get noise value with warping
            float noiseValue = _subRegionNoise.GetNoise2D(worldX + warpX, worldZ + warpZ);

            // Convert from [-1, 1] to [0, 1]
            noiseValue = (noiseValue + 1f) * 0.5f;

            // Divide into sub-regions with different probabilities
            // Dunes: 70%, Rocky: 25%, Oasis: 5%
            if (noiseValue < 0.7f)
                return DesertSubRegion.Dunes;
            else if (noiseValue < 0.95f)
                return DesertSubRegion.Rocky;
            else
                return DesertSubRegion.Oasis;
        }

        // Get the sub-region for a position within the Tundra biome
        public static TundraSubRegion GetTundraSubRegion(int worldX, int worldZ)
        {
            EnsureInitialized();

            // Apply domain warping for more natural boundaries
            float warpX = _warpNoise.GetNoise2D(worldX * 0.008f, worldZ * 0.008f) * 80.0f;
            float warpZ = _warpNoise.GetNoise2D((worldX + 500) * 0.008f, (worldZ + 500) * 0.008f) * 80.0f;

            // Get noise value with warping
            float noiseValue = _subRegionNoise.GetNoise2D(worldX + warpX, worldZ + warpZ);

            // Convert from [-1, 1] to [0, 1]
            noiseValue = (noiseValue + 1f) * 0.5f;

            // Divide into sub-regions with different probabilities
            // Snowy: 60%, Frozen: 25%, Alpine: 15%
            if (noiseValue < 0.6f)
                return TundraSubRegion.Snowy;
            else if (noiseValue < 0.85f)
                return TundraSubRegion.Frozen;
            else
                return TundraSubRegion.Alpine;
        }

        // Get the sub-region for a position within the Islands biome
        public static IslandsSubRegion GetIslandsSubRegion(int worldX, int worldZ)
        {
            EnsureInitialized();

            // Apply domain warping for more natural boundaries
            float warpX = _warpNoise.GetNoise2D(worldX * 0.008f, worldZ * 0.008f) * 80.0f;
            float warpZ = _warpNoise.GetNoise2D((worldX + 500) * 0.008f, (worldZ + 500) * 0.008f) * 80.0f;

            // Get noise value with warping
            float noiseValue = _subRegionNoise.GetNoise2D(worldX + warpX, worldZ + warpZ);

            // Convert from [-1, 1] to [0, 1]
            noiseValue = (noiseValue + 1f) * 0.5f;

            // For Islands, we'll use a different approach based on distance from center
            // This creates concentric rings of different sub-regions
            // Beach on the outer edge, Jungle in the middle, Lagoon in specific areas
            
            // Secondary noise for lagoon placement
            float lagoonNoise = _warpNoise.GetNoise2D(worldX * 0.02f, worldZ * 0.02f);
            lagoonNoise = (lagoonNoise + 1f) * 0.5f;
            
            // If we're in a potential lagoon area
            if (lagoonNoise > 0.7f && noiseValue > 0.4f && noiseValue < 0.6f)
                return IslandsSubRegion.Lagoon;
            // If we're near the edge (beach)
            else if (noiseValue < 0.3f || noiseValue > 0.85f)
                return IslandsSubRegion.Beach;
            // Otherwise, jungle
            else
                return IslandsSubRegion.Jungle;
        }

        // Helper method to ensure noise is initialized
        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize(0); // Use default seed if not initialized yet
            }
        }
    }
}
