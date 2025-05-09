using Godot;
using System;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
    /// <summary>
    /// Generates different sub-regions within the Forest biome to create varied terrain and flora.
    /// </summary>
    public class ForestRegionGenerator
    {
        // Forest sub-region types
        public enum ForestSubRegion
        {
            ForestPlains,    // Open areas with sparse trees (similar to Plains biome)
            RegularForest,   // Standard forest with medium tree density
            ForestMountains  // Dense forest with hills and mountains
        }

        private FastNoiseLite _regionNoise;
        private FastNoiseLite _warpNoise;
        private int _seed;
        private static ForestRegionGenerator _instance;

        // Singleton instance
        public static ForestRegionGenerator Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ForestRegionGenerator();
                }
                return _instance;
            }
        }

        // Initialize with a specific seed
        public void Initialize(int seed)
        {
            _seed = seed;

            // Initialize region noise with even lower frequency for larger, more cohesive regions
            _regionNoise = new FastNoiseLite();
            _regionNoise.Seed = seed + 300;
            _regionNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _regionNoise.Frequency = 0.001f; // Reduced from 0.002f for even larger regions
            _regionNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _regionNoise.FractalOctaves = 2;
            _regionNoise.FractalLacunarity = 1.8f; // Reduced from 2.0f for smoother transitions
            _regionNoise.FractalGain = 0.5f;

            // Initialize warp noise for more natural region boundaries with smoother transitions
            _warpNoise = new FastNoiseLite();
            _warpNoise.Seed = seed + 400;
            _warpNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
            _warpNoise.Frequency = 0.003f; // Reduced from 0.004f for smoother warping
            _warpNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _warpNoise.FractalOctaves = 2; // Reduced from 3 for smoother transitions
            _warpNoise.FractalLacunarity = 1.8f; // Added explicit lacunarity for smoother transitions
            _warpNoise.FractalGain = 0.4f; // Added lower gain for less extreme warping
        }

        // Get the forest sub-region for a world position
        public ForestSubRegion GetForestSubRegion(int worldX, int worldZ)
        {
            // Apply reduced domain warping for smoother, more natural region boundaries
            float warpX = _warpNoise.GetNoise2D(worldX * 0.008f, worldZ * 0.008f) * 80.0f; // Reduced scale and multiplier
            float warpZ = _warpNoise.GetNoise2D((worldX + 500) * 0.008f, (worldZ + 500) * 0.008f) * 80.0f; // Reduced scale and multiplier

            // Get region noise with warped coordinates
            float regionValue = _regionNoise.GetNoise2D(worldX + warpX, worldZ + warpZ);

            // Convert from [-1, 1] to [0, 1]
            regionValue = (regionValue + 1f) * 0.5f;

            // Determine sub-region based on noise value
            // 30% ForestPlains, 40% RegularForest, 30% ForestMountains
            if (regionValue < 0.3f)
                return ForestSubRegion.ForestPlains;
            else if (regionValue < 0.7f)
                return ForestSubRegion.RegularForest;
            else
                return ForestSubRegion.ForestMountains;
        }

        // Get a blend factor for smooth transitions between sub-regions
        public float GetSubRegionBlendFactor(int worldX, int worldZ, ForestSubRegion targetRegion)
        {
            // Apply reduced domain warping for smoother, more natural region boundaries
            float warpX = _warpNoise.GetNoise2D(worldX * 0.008f, worldZ * 0.008f) * 80.0f; // Reduced scale and multiplier
            float warpZ = _warpNoise.GetNoise2D((worldX + 500) * 0.008f, (worldZ + 500) * 0.008f) * 80.0f; // Reduced scale and multiplier

            // Get region noise with warped coordinates
            float regionValue = _regionNoise.GetNoise2D(worldX + warpX, worldZ + warpZ);

            // Convert from [-1, 1] to [0, 1]
            regionValue = (regionValue + 1f) * 0.5f;

            // Calculate blend factor based on how far into the region we are
            float blendFactor = 0.0f;

            switch (targetRegion)
            {
                case ForestSubRegion.ForestPlains:
                    // ForestPlains: 0.0 - 0.3
                    if (regionValue < 0.2f)
                        blendFactor = 1.0f; // Fully ForestPlains
                    else if (regionValue < 0.4f)
                        blendFactor = 1.0f - ((regionValue - 0.2f) / 0.2f); // Blend to RegularForest
                    break;

                case ForestSubRegion.RegularForest:
                    // RegularForest: 0.3 - 0.7
                    if (regionValue < 0.2f)
                        blendFactor = regionValue / 0.2f; // Blend from ForestPlains
                    else if (regionValue < 0.6f)
                        blendFactor = 1.0f; // Fully RegularForest
                    else if (regionValue < 0.8f)
                        blendFactor = 1.0f - ((regionValue - 0.6f) / 0.2f); // Blend to ForestMountains
                    break;

                case ForestSubRegion.ForestMountains:
                    // ForestMountains: 0.7 - 1.0
                    if (regionValue < 0.6f)
                        blendFactor = 0.0f; // Not ForestMountains
                    else if (regionValue < 0.8f)
                        blendFactor = (regionValue - 0.6f) / 0.2f; // Blend from RegularForest
                    else
                        blendFactor = 1.0f; // Fully ForestMountains
                    break;
            }

            return Mathf.Clamp(blendFactor, 0.0f, 1.0f);
        }

        // Get tree density based on sub-region with strategic clearings and dense patches
        public float GetTreeDensity(int worldX, int worldZ)
        {
            ForestSubRegion subRegion = GetForestSubRegion(worldX, worldZ);

            // Base densities for each sub-region
            float plainsDensity = 0.003f;  // Very sparse trees
            float regularDensity = 0.01f;  // Medium tree density
            float mountainDensity = 0.02f; // Dense trees

            // Get blend factors for smooth transitions
            float plainsBlend = GetSubRegionBlendFactor(worldX, worldZ, ForestSubRegion.ForestPlains);
            float regularBlend = GetSubRegionBlendFactor(worldX, worldZ, ForestSubRegion.RegularForest);
            float mountainBlend = GetSubRegionBlendFactor(worldX, worldZ, ForestSubRegion.ForestMountains);

            // Calculate base density with smooth transitions
            float baseDensity =
                plainsDensity * plainsBlend +
                regularDensity * regularBlend +
                mountainDensity * mountainBlend;

            // Create strategic clearings and dense forest patches

            // 1. Large-scale forest distribution (creates major clearings and forest areas)
            float largeScaleNoise = _regionNoise.GetNoise2D(worldX * 0.005f, worldZ * 0.005f);
            largeScaleNoise = (largeScaleNoise + 1f) * 0.5f; // Convert to [0,1]

            // 2. Medium-scale forest distribution (creates medium-sized clearings and dense patches)
            float mediumScaleNoise = _regionNoise.GetNoise2D(worldX * 0.02f, worldZ * 0.02f);
            mediumScaleNoise = (mediumScaleNoise + 1f) * 0.5f; // Convert to [0,1]

            // 3. Small-scale forest distribution (creates small clearings and tree clusters)
            float smallScaleNoise = _warpNoise.GetNoise2D(worldX * 0.05f, worldZ * 0.05f);
            smallScaleNoise = (smallScaleNoise + 1f) * 0.5f; // Convert to [0,1]

            // Combine the different scales with different weights
            float forestDistribution =
                largeScaleNoise * 0.5f +  // Large-scale has the most influence
                mediumScaleNoise * 0.3f + // Medium-scale has moderate influence
                smallScaleNoise * 0.2f;   // Small-scale has the least influence

            // Apply a threshold curve to create more distinct clearings and dense areas
            // This creates a more binary distribution with some gradient at the edges
            float threshold = 0.4f; // Adjust this to control clearing size
            float curve = 8.0f;     // Adjust this to control edge sharpness

            // Apply sigmoid-like curve to create more distinct forest edges
            forestDistribution = 1.0f / (1.0f + Mathf.Exp(-curve * (forestDistribution - threshold)));

            // Apply the forest distribution to the base density
            // This will create areas with no trees (clearings) and areas with dense trees
            float finalDensity = baseDensity * forestDistribution * 2.0f; // Multiply by 2 to compensate for the reduction

            // Add smoother paths through forests (reduce tree density along certain noise curves)
            float pathNoise1 = _warpNoise.GetNoise2D(worldX * 0.007f + 500, worldZ * 0.007f + 500); // Reduced frequency for wider, smoother paths
            float pathNoise2 = _warpNoise.GetNoise2D(worldX * 0.007f - 500, worldZ * 0.007f - 500); // Reduced frequency for wider, smoother paths

            // If we're close to a path, reduce tree density (wider paths)
            if (Mathf.Abs(pathNoise1) < 0.15f || Mathf.Abs(pathNoise2) < 0.15f) // Increased from 0.1f for wider paths
            {
                // Calculate how close we are to the path center (0 = center, 1 = edge)
                float pathDistance1 = Mathf.Abs(pathNoise1) / 0.15f; // Adjusted for wider paths
                float pathDistance2 = Mathf.Abs(pathNoise2) / 0.15f; // Adjusted for wider paths
                float pathDistance = Mathf.Min(pathDistance1, pathDistance2);

                // Reduce density more at the center of the path
                finalDensity *= pathDistance;
            }

            return finalDensity;
        }
    }
}
