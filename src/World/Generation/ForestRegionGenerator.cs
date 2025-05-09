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
        // Removed unused _seed field
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
            // Removed assignment to unused _seed field

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
            // 45% ForestPlains, 35% RegularForest, 20% ForestMountains
            // Increased plains regions from 30% to 45%
            if (regionValue < 0.45f)
                return ForestSubRegion.ForestPlains;
            else if (regionValue < 0.8f)
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
                    // ForestPlains: 0.0 - 0.45
                    if (regionValue < 0.35f)
                        blendFactor = 1.0f; // Fully ForestPlains
                    else if (regionValue < 0.55f)
                        blendFactor = 1.0f - ((regionValue - 0.35f) / 0.2f); // Blend to RegularForest
                    break;

                case ForestSubRegion.RegularForest:
                    // RegularForest: 0.45 - 0.8
                    if (regionValue < 0.35f)
                        blendFactor = regionValue / 0.35f; // Blend from ForestPlains
                    else if (regionValue < 0.7f)
                        blendFactor = 1.0f; // Fully RegularForest
                    else if (regionValue < 0.9f)
                        blendFactor = 1.0f - ((regionValue - 0.7f) / 0.2f); // Blend to ForestMountains
                    break;

                case ForestSubRegion.ForestMountains:
                    // ForestMountains: 0.8 - 1.0
                    if (regionValue < 0.7f)
                        blendFactor = 0.0f; // Not ForestMountains
                    else if (regionValue < 0.9f)
                        blendFactor = (regionValue - 0.7f) / 0.2f; // Blend from RegularForest
                    else
                        blendFactor = 1.0f; // Fully ForestMountains
                    break;
            }

            return Mathf.Clamp(blendFactor, 0.0f, 1.0f);
        }

        // Get tree density based on sub-region with strategic clearings and dense patches
        public float GetTreeDensity(int worldX, int worldZ)
        {
            // Get the forest sub-region for this position
            ForestSubRegion subRegion = GetForestSubRegion(worldX, worldZ);

            // Base densities for each sub-region
            float plainsDensity = 0.0015f; // Extremely sparse trees (reduced from 0.003f)
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

            // Adjust threshold based on sub-region to create more clearings in plains areas
            float threshold;
            float curve;

            if (subRegion == ForestSubRegion.ForestPlains)
            {
                threshold = 0.6f; // Higher threshold for plains (more clearings) (increased from 0.4f)
                curve = 10.0f;    // Sharper transitions for plains (increased from 8.0f)
            }
            else if (subRegion == ForestSubRegion.RegularForest)
            {
                threshold = 0.4f; // Standard threshold for regular forest
                curve = 8.0f;     // Standard curve for regular forest
            }
            else // ForestMountains
            {
                threshold = 0.3f; // Lower threshold for mountains (fewer clearings)
                curve = 6.0f;     // Smoother transitions for mountains
            }

            // Apply sigmoid-like curve to create more distinct forest edges
            forestDistribution = 1.0f / (1.0f + Mathf.Exp(-curve * (forestDistribution - threshold)));

            // Apply the forest distribution to the base density
            // This will create areas with no trees (clearings) and areas with dense trees
            float finalDensity = baseDensity * forestDistribution * 2.0f; // Multiply by 2 to compensate for the reduction

            // Add smoother paths through forests (reduce tree density along certain noise curves)
            float pathNoise1 = _warpNoise.GetNoise2D(worldX * 0.007f + 500, worldZ * 0.007f + 500); // Reduced frequency for wider, smoother paths
            float pathNoise2 = _warpNoise.GetNoise2D(worldX * 0.007f - 500, worldZ * 0.007f - 500); // Reduced frequency for wider, smoother paths

            // Additional path noise for plains areas to create more open spaces
            float plainsPathNoise = _warpNoise.GetNoise2D(worldX * 0.005f + 1000, worldZ * 0.005f + 1000);

            // Path width threshold - wider in plains areas
            float pathThreshold;
            if (subRegion == ForestSubRegion.ForestPlains)
            {
                pathThreshold = 0.25f; // Much wider paths in plains (increased from 0.15f)
            }
            else if (subRegion == ForestSubRegion.RegularForest)
            {
                pathThreshold = 0.18f; // Slightly wider paths in regular forest (increased from 0.15f)
            }
            else // ForestMountains
            {
                pathThreshold = 0.15f; // Standard path width in mountains
            }

            // If we're close to a path, reduce tree density (wider paths)
            if (Mathf.Abs(pathNoise1) < pathThreshold ||
                Mathf.Abs(pathNoise2) < pathThreshold ||
                (subRegion == ForestSubRegion.ForestPlains && Mathf.Abs(plainsPathNoise) < 0.3f)) // Extra wide clearings in plains
            {
                // Calculate how close we are to the path center (0 = center, 1 = edge)
                float pathDistance1 = Mathf.Abs(pathNoise1) / pathThreshold;
                float pathDistance2 = Mathf.Abs(pathNoise2) / pathThreshold;
                float pathDistance3 = (subRegion == ForestSubRegion.ForestPlains) ? Mathf.Abs(plainsPathNoise) / 0.3f : 1.0f;
                // Mathf.Min only takes 2 arguments, so we need to chain them
                float pathDistance = Mathf.Min(Mathf.Min(pathDistance1, pathDistance2), pathDistance3);

                // Reduce density more at the center of the path
                // In plains areas, create completely clear paths (no trees at center)
                if (subRegion == ForestSubRegion.ForestPlains && pathDistance < 0.3f)
                {
                    finalDensity = 0.0f; // No trees at all in the center of plains paths
                }
                else
                {
                    finalDensity *= pathDistance;
                }
            }

            return finalDensity;
        }
    }
}
