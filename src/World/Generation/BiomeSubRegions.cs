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

        // Get a blend factor for smooth transitions between ForestLands sub-regions
        public static float GetForestLandsBlendFactor(int worldX, int worldZ, ForestLandsSubRegion targetRegion)
        {
            EnsureInitialized();

            // Apply domain warping for more natural boundaries
            float warpX = _warpNoise.GetNoise2D(worldX * 0.008f, worldZ * 0.008f) * 80.0f;
            float warpZ = _warpNoise.GetNoise2D((worldX + 500) * 0.008f, (worldZ + 500) * 0.008f) * 80.0f;

            // Get noise value with warping
            float noiseValue = _subRegionNoise.GetNoise2D(worldX + warpX, worldZ + warpZ);

            // Convert from [-1, 1] to [0, 1]
            noiseValue = (noiseValue + 1f) * 0.5f;

            // Calculate blend factor based on how far into the region we are
            float blendFactor = 0.0f;
            float transitionWidth = 0.1f; // Width of the transition zone (10% of the range)

            switch (targetRegion)
            {
                case ForestLandsSubRegion.Plains:
                    // Plains: 0.0 - 0.333
                    if (noiseValue < 0.333f - transitionWidth)
                        blendFactor = 1.0f; // Fully Plains
                    else if (noiseValue < 0.333f + transitionWidth)
                        blendFactor = 1.0f - ((noiseValue - (0.333f - transitionWidth)) / (transitionWidth * 2)); // Blend to Forest
                    break;

                case ForestLandsSubRegion.Forest:
                    // Forest: 0.333 - 0.667
                    if (noiseValue < 0.333f - transitionWidth)
                        blendFactor = 0.0f; // Not Forest
                    else if (noiseValue < 0.333f + transitionWidth)
                        blendFactor = (noiseValue - (0.333f - transitionWidth)) / (transitionWidth * 2); // Blend from Plains
                    else if (noiseValue < 0.667f - transitionWidth)
                        blendFactor = 1.0f; // Fully Forest
                    else if (noiseValue < 0.667f + transitionWidth)
                        blendFactor = 1.0f - ((noiseValue - (0.667f - transitionWidth)) / (transitionWidth * 2)); // Blend to Mountains
                    break;

                case ForestLandsSubRegion.Mountains:
                    // Mountains: 0.667 - 1.0
                    if (noiseValue < 0.667f - transitionWidth)
                        blendFactor = 0.0f; // Not Mountains
                    else if (noiseValue < 0.667f + transitionWidth)
                        blendFactor = (noiseValue - (0.667f - transitionWidth)) / (transitionWidth * 2); // Blend from Forest
                    else
                        blendFactor = 1.0f; // Fully Mountains
                    break;
            }

            return Mathf.Clamp(blendFactor, 0.0f, 1.0f);
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

        // Get a blend factor for smooth transitions between Desert sub-regions
        public static float GetDesertBlendFactor(int worldX, int worldZ, DesertSubRegion targetRegion)
        {
            EnsureInitialized();

            // Apply domain warping for more natural boundaries
            float warpX = _warpNoise.GetNoise2D(worldX * 0.008f, worldZ * 0.008f) * 80.0f;
            float warpZ = _warpNoise.GetNoise2D((worldX + 500) * 0.008f, (worldZ + 500) * 0.008f) * 80.0f;

            // Get noise value with warping
            float noiseValue = _subRegionNoise.GetNoise2D(worldX + warpX, worldZ + warpZ);

            // Convert from [-1, 1] to [0, 1]
            noiseValue = (noiseValue + 1f) * 0.5f;

            // Calculate blend factor based on how far into the region we are
            float blendFactor = 0.0f;
            float transitionWidth = 0.1f; // Width of the transition zone (10% of the range)

            switch (targetRegion)
            {
                case DesertSubRegion.Dunes:
                    // Dunes: 0.0 - 0.7
                    if (noiseValue < 0.7f - transitionWidth)
                        blendFactor = 1.0f; // Fully Dunes
                    else if (noiseValue < 0.7f + transitionWidth)
                        blendFactor = 1.0f - ((noiseValue - (0.7f - transitionWidth)) / (transitionWidth * 2)); // Blend to Rocky
                    break;

                case DesertSubRegion.Rocky:
                    // Rocky: 0.7 - 0.95
                    if (noiseValue < 0.7f - transitionWidth)
                        blendFactor = 0.0f; // Not Rocky
                    else if (noiseValue < 0.7f + transitionWidth)
                        blendFactor = (noiseValue - (0.7f - transitionWidth)) / (transitionWidth * 2); // Blend from Dunes
                    else if (noiseValue < 0.95f - transitionWidth)
                        blendFactor = 1.0f; // Fully Rocky
                    else if (noiseValue < 0.95f + transitionWidth)
                        blendFactor = 1.0f - ((noiseValue - (0.95f - transitionWidth)) / (transitionWidth * 2)); // Blend to Oasis
                    break;

                case DesertSubRegion.Oasis:
                    // Oasis: 0.95 - 1.0
                    // For Oasis, we want sharp boundaries - an oasis should be a distinct area
                    if (noiseValue < 0.95f - transitionWidth * 0.5f) // Narrower transition for Oasis
                        blendFactor = 0.0f; // Not Oasis
                    else if (noiseValue < 0.95f)
                        blendFactor = (noiseValue - (0.95f - transitionWidth * 0.5f)) / (transitionWidth * 0.5f); // Sharper blend from Rocky
                    else
                        blendFactor = 1.0f; // Fully Oasis
                    break;
            }

            return Mathf.Clamp(blendFactor, 0.0f, 1.0f);
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

        // Get a blend factor for smooth transitions between Tundra sub-regions
        public static float GetTundraBlendFactor(int worldX, int worldZ, TundraSubRegion targetRegion)
        {
            EnsureInitialized();

            // Apply domain warping for more natural boundaries
            float warpX = _warpNoise.GetNoise2D(worldX * 0.008f, worldZ * 0.008f) * 80.0f;
            float warpZ = _warpNoise.GetNoise2D((worldX + 500) * 0.008f, (worldZ + 500) * 0.008f) * 80.0f;

            // Get noise value with warping
            float noiseValue = _subRegionNoise.GetNoise2D(worldX + warpX, worldZ + warpZ);

            // Convert from [-1, 1] to [0, 1]
            noiseValue = (noiseValue + 1f) * 0.5f;

            // Calculate blend factor based on how far into the region we are
            float blendFactor = 0.0f;
            float transitionWidth = 0.1f; // Width of the transition zone (10% of the range)

            switch (targetRegion)
            {
                case TundraSubRegion.Snowy:
                    // Snowy: 0.0 - 0.6
                    if (noiseValue < 0.6f - transitionWidth)
                        blendFactor = 1.0f; // Fully Snowy
                    else if (noiseValue < 0.6f + transitionWidth)
                        blendFactor = 1.0f - ((noiseValue - (0.6f - transitionWidth)) / (transitionWidth * 2)); // Blend to Frozen
                    break;

                case TundraSubRegion.Frozen:
                    // Frozen: 0.6 - 0.85
                    if (noiseValue < 0.6f - transitionWidth)
                        blendFactor = 0.0f; // Not Frozen
                    else if (noiseValue < 0.6f + transitionWidth)
                        blendFactor = (noiseValue - (0.6f - transitionWidth)) / (transitionWidth * 2); // Blend from Snowy
                    else if (noiseValue < 0.85f - transitionWidth)
                        blendFactor = 1.0f; // Fully Frozen
                    else if (noiseValue < 0.85f + transitionWidth)
                        blendFactor = 1.0f - ((noiseValue - (0.85f - transitionWidth)) / (transitionWidth * 2)); // Blend to Alpine
                    break;

                case TundraSubRegion.Alpine:
                    // Alpine: 0.85 - 1.0
                    if (noiseValue < 0.85f - transitionWidth)
                        blendFactor = 0.0f; // Not Alpine
                    else if (noiseValue < 0.85f + transitionWidth)
                        blendFactor = (noiseValue - (0.85f - transitionWidth)) / (transitionWidth * 2); // Blend from Frozen
                    else
                        blendFactor = 1.0f; // Fully Alpine
                    break;
            }

            return Mathf.Clamp(blendFactor, 0.0f, 1.0f);
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

        // Get a blend factor for smooth transitions between Islands sub-regions
        public static float GetIslandsBlendFactor(int worldX, int worldZ, IslandsSubRegion targetRegion)
        {
            EnsureInitialized();

            // Apply domain warping for more natural boundaries
            float warpX = _warpNoise.GetNoise2D(worldX * 0.008f, worldZ * 0.008f) * 80.0f;
            float warpZ = _warpNoise.GetNoise2D((worldX + 500) * 0.008f, (worldZ + 500) * 0.008f) * 80.0f;

            // Get noise value with warping
            float noiseValue = _subRegionNoise.GetNoise2D(worldX + warpX, worldZ + warpZ);

            // Convert from [-1, 1] to [0, 1]
            noiseValue = (noiseValue + 1f) * 0.5f;

            // Secondary noise for lagoon placement
            float lagoonNoise = _warpNoise.GetNoise2D(worldX * 0.02f, worldZ * 0.02f);
            lagoonNoise = (lagoonNoise + 1f) * 0.5f;

            // Calculate blend factor based on how far into the region we are
            float blendFactor = 0.0f;
            float transitionWidth = 0.05f; // Narrower transition width for islands (5% of the range)

            switch (targetRegion)
            {
                case IslandsSubRegion.Beach:
                    // Beach: noiseValue < 0.3f || noiseValue > 0.85f
                    if (noiseValue < 0.3f - transitionWidth || noiseValue > 0.85f + transitionWidth)
                        blendFactor = 1.0f; // Fully Beach
                    else if (noiseValue < 0.3f + transitionWidth)
                        blendFactor = 1.0f - ((noiseValue - (0.3f - transitionWidth)) / (transitionWidth * 2)); // Blend to Jungle
                    else if (noiseValue > 0.85f - transitionWidth)
                        blendFactor = (noiseValue - (0.85f - transitionWidth)) / (transitionWidth * 2); // Blend from Jungle
                    break;

                case IslandsSubRegion.Jungle:
                    // Jungle: 0.3f < noiseValue < 0.85f (except Lagoon areas)
                    if (noiseValue < 0.3f - transitionWidth || noiseValue > 0.85f + transitionWidth)
                        blendFactor = 0.0f; // Not Jungle
                    else if (noiseValue < 0.3f + transitionWidth)
                        blendFactor = (noiseValue - (0.3f - transitionWidth)) / (transitionWidth * 2); // Blend from Beach
                    else if (noiseValue > 0.85f - transitionWidth)
                        blendFactor = 1.0f - ((noiseValue - (0.85f - transitionWidth)) / (transitionWidth * 2)); // Blend to Beach
                    else if (lagoonNoise > 0.7f - transitionWidth && noiseValue > 0.4f - transitionWidth && noiseValue < 0.6f + transitionWidth)
                    {
                        // Near lagoon area
                        if (lagoonNoise > 0.7f + transitionWidth && noiseValue > 0.4f + transitionWidth && noiseValue < 0.6f - transitionWidth)
                            blendFactor = 0.0f; // Fully Lagoon area
                        else
                            blendFactor = 0.5f; // Transition area
                    }
                    else
                        blendFactor = 1.0f; // Fully Jungle
                    break;

                case IslandsSubRegion.Lagoon:
                    // Lagoon: lagoonNoise > 0.7f && noiseValue > 0.4f && noiseValue < 0.6f
                    // For Lagoon, we want a sharper boundary - a lagoon should be a distinct area
                    if (lagoonNoise > 0.7f + transitionWidth && noiseValue > 0.4f + transitionWidth && noiseValue < 0.6f - transitionWidth)
                        blendFactor = 1.0f; // Fully Lagoon
                    else if (lagoonNoise > 0.7f - transitionWidth && noiseValue > 0.4f - transitionWidth && noiseValue < 0.6f + transitionWidth)
                        blendFactor = 0.5f; // Transition area
                    else
                        blendFactor = 0.0f; // Not Lagoon
                    break;
            }

            return Mathf.Clamp(blendFactor, 0.0f, 1.0f);
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
