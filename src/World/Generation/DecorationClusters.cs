using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
    public static class DecorationClusters
    {
        // OPTIMIZATION: Reduced decoration density for better performance
        private const float CLUSTER_RADIUS_MIN = 10.0f;  // Increased for larger, more sparse clusters
        private const float CLUSTER_RADIUS_MAX = 20.0f;  // Increased for larger, more sparse clusters
        private const int MIN_ITEMS_PER_CLUSTER = 2;     // Reduced minimum items per cluster
        private const int MAX_ITEMS_PER_CLUSTER = 5;     // Reduced maximum items per cluster

        // Cluster center probability (chance to start a new cluster)
        // Very low probability to create distinct, separated clusters
        private const float CLUSTER_CENTER_PROBABILITY = 0.0005f; // 0.05% chance - reduced by half

        // THREAD SAFETY: Use ConcurrentDictionary instead of Dictionary for thread safety
        private static ConcurrentDictionary<Vector2I, List<ClusterInfo>> _clusterCenters = new ConcurrentDictionary<Vector2I, List<ClusterInfo>>();

        // Lock object for synchronizing access to cluster lists
        private static readonly object _clusterLock = new object();

        // Noise generator for natural patterns
        private static FastNoiseLite _noise;

        // Static constructor to initialize noise
        static DecorationClusters()
        {
            // Initialize noise generator
            _noise = new FastNoiseLite();
            _noise.Seed = 12345; // Use a fixed seed for decoration patterns
            _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _noise.Frequency = 0.05f;
        }

        // Structure to hold cluster information
        private struct ClusterInfo
        {
            public Vector2I Position;    // World position of cluster center
            public float Radius;         // Radius of the cluster
            public VoxelType Type;       // Type of decoration in this cluster
            public int ItemCount;        // Number of items in this cluster
            public int ItemsPlaced;      // Number of items already placed

            public ClusterInfo(Vector2I position, float radius, VoxelType type, int itemCount)
            {
                Position = position;
                Radius = radius;
                Type = type;
                ItemCount = itemCount;
                ItemsPlaced = 0;
            }
        }

        // Structure to hold decoration placement information
        public struct DecorationPlacement
        {
            public VoxelType Type;       // Type of decoration
            public Vector2 Offset;       // Offset from center of voxel (range: -0.4 to 0.4)
            public float Rotation;       // Rotation in degrees
            public float Scale;          // Scale variation (0.8 to 1.2)

            public DecorationPlacement(VoxelType type, Vector2 offset, float rotation, float scale)
            {
                Type = type;
                Offset = offset;
                Rotation = rotation;
                Scale = scale;
            }
        }

        // Initialize clusters for a chunk
        public static void InitializeChunkClusters(Vector2I chunkPos, int chunkSize, Random random)
        {
            // Calculate world position of chunk corner
            Vector2I worldPos = new Vector2I(chunkPos.X * chunkSize, chunkPos.Y * chunkSize);

            // THREAD SAFETY: Use thread-safe operations for the ConcurrentDictionary
            // Create a new list for this chunk
            List<ClusterInfo> newClusterList = new List<ClusterInfo>();

            // Use GetOrAdd to safely add the new list if it doesn't exist
            _clusterCenters.GetOrAdd(chunkPos, newClusterList);

            // Get biome type for this chunk (use center of chunk for biome determination)
            int centerX = worldPos.X + chunkSize / 2;
            int centerZ = worldPos.Y + chunkSize / 2;
            BiomeType biomeType = WorldGenerator.GetBiomeType(centerX, centerZ);

            // Adjust cluster parameters based on biome
            float clusterProbability = GetClusterProbabilityForBiome(biomeType);

            // Use noise to create more natural cluster distribution
            float noiseScale = 0.05f;
            float noiseValue = _noise.GetNoise2D(chunkPos.X * noiseScale, chunkPos.Y * noiseScale);

            // Convert from [-1,1] to [0,1] range
            noiseValue = (noiseValue + 1.0f) * 0.5f;

            // Adjust the number of potential clusters based on noise
            float adjustedProbability = CLUSTER_CENTER_PROBABILITY * (0.7f + noiseValue * 0.6f);

            // Determine how many potential cluster centers to check
            int potentialCenters = (int)(chunkSize * chunkSize * adjustedProbability);

            // Ensure at least one potential center per chunk
            potentialCenters = Math.Max(1, potentialCenters);

            // Try to create clusters
            for (int i = 0; i < potentialCenters; i++)
            {
                // Use a different approach for positioning clusters
                // Instead of a uniform grid, use a more natural distribution

                // Use a different noise scale for each potential center
                float posNoiseX = _noise.GetNoise2D((chunkPos.X + i * 0.1f) * noiseScale, (chunkPos.Y + i * 0.2f) * noiseScale);
                float posNoiseZ = _noise.GetNoise2D((chunkPos.X + i * 0.3f) * noiseScale, (chunkPos.Y + i * 0.1f) * noiseScale);

                // Convert from [-1,1] to [0,1] range
                posNoiseX = (posNoiseX + 1.0f) * 0.5f;
                posNoiseZ = (posNoiseZ + 1.0f) * 0.5f;

                // Scale the noise to the chunk size and add some randomness
                int offsetX = (int)(posNoiseX * chunkSize * 0.8f + random.NextDouble() * chunkSize * 0.2f);
                int offsetZ = (int)(posNoiseZ * chunkSize * 0.8f + random.NextDouble() * chunkSize * 0.2f);

                // Ensure the offset is within the chunk
                offsetX = Math.Clamp(offsetX, 0, chunkSize - 1);
                offsetZ = Math.Clamp(offsetZ, 0, chunkSize - 1);

                // Calculate world position
                Vector2I position = new Vector2I(worldPos.X + offsetX, worldPos.Y + offsetZ);

                // Get the specific biome at this position (might differ from chunk center)
                BiomeType positionBiome = WorldGenerator.GetBiomeType(position.X, position.Y);
                float positionClusterProbability = GetClusterProbabilityForBiome(positionBiome);

                // Adjust probability based on noise to create more natural patterns
                float adjustedClusterProbability = positionClusterProbability * (0.8f + noiseValue * 0.4f);

                if (random.NextDouble() < adjustedClusterProbability)
                {
                    // Create a new cluster with more variation
                    VoxelType decorationType = GetRandomDecorationForBiome(positionBiome, random);

                    // Vary the radius based on decoration type and add some randomness
                    float baseRadius = CLUSTER_RADIUS_MIN + (float)random.NextDouble() * (CLUSTER_RADIUS_MAX - CLUSTER_RADIUS_MIN);

                    // Adjust radius based on decoration type
                    float typeMultiplier = 1.0f;
                    switch (decorationType)
                    {
                        case VoxelType.TallGrass:
                            typeMultiplier = 1.2f; // Larger grass clusters
                            break;
                        case VoxelType.Flower:
                            typeMultiplier = 0.8f; // Smaller flower clusters
                            break;
                        case VoxelType.Mushroom:
                            typeMultiplier = 0.7f; // Smaller mushroom clusters
                            break;
                        case VoxelType.Rock:
                            typeMultiplier = 0.9f; // Medium rock clusters
                            break;
                    }

                    float radius = baseRadius * typeMultiplier;

                    // Vary the item count based on radius and add some randomness - reduced multiplier for fewer items
                    int baseItemCount = (int)(radius * 0.8f); // Reduced from 1.5f
                    int itemCount = baseItemCount + random.Next(-1, 2); // Add -1 to +1 randomness (reduced from -2 to +2)

                    // Ensure item count is within bounds
                    itemCount = Math.Clamp(itemCount, MIN_ITEMS_PER_CLUSTER, MAX_ITEMS_PER_CLUSTER);

                    // THREAD SAFETY: Use lock when modifying the list
                    lock (_clusterLock)
                    {
                        // Get the current list or create a new one if it doesn't exist
                        List<ClusterInfo> clusterList = _clusterCenters.GetOrAdd(chunkPos, new List<ClusterInfo>());

                        // Add the new cluster
                        clusterList.Add(new ClusterInfo(position, radius, decorationType, itemCount));
                    }
                }
            }
        }

        // Check if a position is within a cluster and should have a decoration
        public static bool ShouldPlaceDecoration(Vector2I worldPos, out DecorationPlacement placement, Random random)
        {
            // Initialize with default values
            placement = new DecorationPlacement(VoxelType.Air, Vector2.Zero, 0, 1.0f);

            // Check all nearby chunks for clusters
            int chunkSize = WorldGenerator.CHUNK_SIZE;
            Vector2I chunkPos = new Vector2I(
                Mathf.FloorToInt((float)worldPos.X / chunkSize),
                Mathf.FloorToInt((float)worldPos.Y / chunkSize)
            );

            // Use a hash of the world position to create deterministic randomness for this position
            int positionHash = worldPos.X * 73856093 ^ worldPos.Y * 19349663;
            Random posRandom = new Random(positionHash);

            // Add a small random offset to the position to break the grid pattern
            // This creates a "jittered grid" effect that looks more natural
            float jitterX = (float)(posRandom.NextDouble() * 2.0 - 1.0); // -1.0 to 1.0
            float jitterY = (float)(posRandom.NextDouble() * 2.0 - 1.0); // -1.0 to 1.0

            // Create a jittered position for cluster checks
            Vector2 jitteredPos = new Vector2(
                worldPos.X + jitterX,
                worldPos.Y + jitterY
            );

            // Check the current chunk and all 8 surrounding chunks
            for (int xOffset = -1; xOffset <= 1; xOffset++)
            {
                for (int zOffset = -1; zOffset <= 1; zOffset++)
                {
                    Vector2I neighborChunkPos = new Vector2I(chunkPos.X + xOffset, chunkPos.Y + zOffset);

                    // THREAD SAFETY: Use thread-safe operations for ConcurrentDictionary
                    // Skip if this chunk has no clusters
                    if (!_clusterCenters.TryGetValue(neighborChunkPos, out List<ClusterInfo> clusterList) || clusterList == null)
                        continue;

                    // Make a thread-safe copy of the cluster list to avoid modification during enumeration
                    List<ClusterInfo> clusterListCopy;
                    lock (_clusterLock)
                    {
                        clusterListCopy = new List<ClusterInfo>(clusterList);
                    }

                    // Check each cluster in this chunk
                    foreach (var cluster in clusterListCopy)
                    {
                        // Calculate distance to cluster center using the jittered position
                        float distanceSquared =
                            (jitteredPos.X - cluster.Position.X) * (jitteredPos.X - cluster.Position.X) +
                            (jitteredPos.Y - cluster.Position.Y) * (jitteredPos.Y - cluster.Position.Y);

                        float radiusSquared = cluster.Radius * cluster.Radius;

                        // If within radius and cluster still has items to place
                        if (distanceSquared <= radiusSquared && cluster.ItemsPlaced < cluster.ItemCount)
                        {
                            // Calculate distance from center as a percentage of radius
                            float distanceRatio = Mathf.Sqrt(distanceSquared) / cluster.Radius;

                            // Create a more natural distribution pattern using multiple noise layers

                            // 1. Base distance factor - higher probability near center, drops off toward edges
                            // Use a quadratic falloff for more natural-looking clusters
                            float distanceFactor = 1.0f - (distanceRatio * distanceRatio);

                            // 2. Add noise to create irregular cluster shapes
                            int shapeHash = worldPos.X * 31 ^ worldPos.Y * 17 ^ (int)(cluster.Position.X * 13) ^ (int)(cluster.Position.Y * 7);
                            Random shapeRandom = new Random(shapeHash);

                            // Create irregular cluster shapes using noise
                            float angle = Mathf.Atan2(worldPos.Y - cluster.Position.Y, worldPos.X - cluster.Position.X);
                            float angleNoise = _noise.GetNoise2D(Mathf.Cos(angle) * 5.0f, Mathf.Sin(angle) * 5.0f);
                            angleNoise = (angleNoise + 1.0f) * 0.5f; // Convert to [0,1]

                            // Apply the angle noise to create irregular cluster shapes
                            // This makes clusters less circular and more natural
                            float shapeVariation = 0.3f; // How much the shape can vary (0.0 = circle, 1.0 = very irregular)
                            float adjustedDistance = distanceRatio * (1.0f - shapeVariation * angleNoise);

                            // Recalculate distance factor with the adjusted distance
                            distanceFactor = 1.0f - (adjustedDistance * adjustedDistance);

                            // 3. Add small-scale noise for micro-variation within clusters
                            float microNoiseX = worldPos.X * 0.2f + cluster.Position.X * 0.05f;
                            float microNoiseY = worldPos.Y * 0.2f + cluster.Position.Y * 0.05f;
                            float microNoise = _noise.GetNoise2D(microNoiseX, microNoiseY);
                            microNoise = (microNoise + 1.0f) * 0.5f; // Convert to [0,1]

                            // 4. Add medium-scale noise for sub-clusters within the main cluster
                            float mediumNoiseX = worldPos.X * 0.05f + cluster.Position.X * 0.02f;
                            float mediumNoiseY = worldPos.Y * 0.05f + cluster.Position.Y * 0.02f;
                            float mediumNoise = _noise.GetNoise2D(mediumNoiseX, mediumNoiseY);
                            mediumNoise = (mediumNoise + 1.0f) * 0.5f; // Convert to [0,1]

                            // Apply a threshold to medium noise to create distinct sub-clusters
                            mediumNoise = mediumNoise > 0.5f ? mediumNoise * 2.0f - 1.0f : 0.0f;

                            // OPTIMIZATION: Drastically reduced decoration density
                            // Base probability is extremely low (0.05) to create very sparse placement
                            // Distance factor creates higher density near center
                            // Medium noise creates sub-clusters
                            // Micro noise adds small-scale variation
                            float probability = 0.05f * distanceFactor + 0.1f * mediumNoise + 0.02f * microNoise;

                            // Apply a threshold to create more distinct edges
                            if (distanceRatio > 0.8f)
                            {
                                // Sharp falloff at edges
                                probability *= (1.0f - distanceRatio) * 5.0f;
                            }

                            // Clamp probability to valid range - reduced upper bound for fewer decorations
                            probability = Mathf.Clamp(probability, 0.01f, 0.15f);

                            // Random chance to place decoration based on combined factors
                            if (random.NextDouble() < probability)
                            {
                                // Get the decoration type for this cluster
                                VoxelType decorationType = cluster.Type;

                                // Add some variation to the decoration type
                                if (random.NextDouble() < 0.15f) // 15% chance to vary the decoration
                                {
                                    // Get a variation of the decoration type
                                    decorationType = GetVariationOfDecorationType(decorationType, random);
                                }

                                // Generate random offset from center of voxel (-0.4 to 0.4)
                                float offsetX = (float)(random.NextDouble() * 0.8 - 0.4);
                                float offsetZ = (float)(random.NextDouble() * 0.8 - 0.4);

                                // Generate random rotation (0 to 360 degrees)
                                float rotation = (float)(random.NextDouble() * 360.0);

                                // Generate random scale variation (1.0 to 1.5) - increased for larger decorations
                                float scale = 1.0f + (float)(random.NextDouble() * 0.5);

                                // Create the decoration placement
                                placement = new DecorationPlacement(
                                    decorationType,
                                    new Vector2(offsetX, offsetZ),
                                    rotation,
                                    scale
                                );

                                // Update the cluster's item count (this is approximate since we can't modify the struct directly)
                                // In a real implementation, you'd need to track this differently

                                return true;
                            }
                        }
                    }
                }
            }

            // If we get here, the position is not in any cluster or all clusters are full
            return false;
        }

        // Get a variation of a decoration type to add diversity within clusters
        private static VoxelType GetVariationOfDecorationType(VoxelType baseType, Random random)
        {
            switch (baseType)
            {
                case VoxelType.TallGrass:
                    // Occasionally mix flowers with grass
                    return random.NextDouble() < 0.3f ? VoxelType.Flower : VoxelType.TallGrass;

                case VoxelType.Flower:
                    // Occasionally mix grass with flowers
                    return random.NextDouble() < 0.3f ? VoxelType.TallGrass : VoxelType.Flower;

                case VoxelType.Mushroom:
                    // Occasionally mix sticks with mushrooms
                    return random.NextDouble() < 0.3f ? VoxelType.Stick : VoxelType.Mushroom;

                case VoxelType.Rock:
                    // Rocks sometimes vary with sticks
                    return random.NextDouble() < 0.2f ? VoxelType.Stick : VoxelType.Rock;

                case VoxelType.Stick:
                    // Sticks sometimes vary with mushrooms in forests
                    return random.NextDouble() < 0.2f ? VoxelType.Mushroom : VoxelType.Stick;

                case VoxelType.Seashell:
                    // Seashells sometimes vary with rocks
                    return random.NextDouble() < 0.3f ? VoxelType.Rock : VoxelType.Seashell;

                default:
                    return baseType;
            }
        }

        // Get cluster probability based on biome for strategic decoration placement
        private static float GetClusterProbabilityForBiome(BiomeType biomeType)
        {
            // For Forest biome, we'll use the ForestRegionGenerator to determine sub-region
            if (biomeType == BiomeType.Forest)
            {
                // We can't directly get the world position here, so we'll use a base value
                // The actual strategic placement is handled by ForestRegionGenerator.GetTreeDensity
                return 0.3f; // Higher probability for forest decorations to create distinct clusters
            }

            // For other biomes, use standard probabilities
            switch (biomeType)
            {
                case BiomeType.Plains:
                    return 0.25f; // Higher probability for grass clusters in open areas
                case BiomeType.Desert:
                    return 0.1f;  // Low probability for rock clusters in desert
                case BiomeType.Tundra:
                    return 0.08f; // Very low probability for rock clusters in tundra
                case BiomeType.Mountains:
                    return 0.05f; // Extremely low probability for any clusters in mountains
                default:
                    return 0.15f; // Default probability
            }
        }

        // Get a random decoration type appropriate for the biome
        private static VoxelType GetRandomDecorationForBiome(BiomeType biomeType, Random random)
        {
            switch (biomeType)
            {
                case BiomeType.Plains:
                    // Plains have tall grass and flowers
                    return random.NextDouble() < 0.8f ? VoxelType.TallGrass : VoxelType.Flower;

                case BiomeType.Forest:
                    // Forest has mushrooms and sticks
                    return random.NextDouble() < 0.7f ? VoxelType.Stick : VoxelType.Mushroom;

                case BiomeType.Desert:
                    // Desert has rocks and occasional seashells
                    return random.NextDouble() < 0.9f ? VoxelType.Rock : VoxelType.Seashell;

                case BiomeType.Tundra:
                    // Tundra has mostly rocks
                    return VoxelType.Rock;

                case BiomeType.Mountains:
                    // Mountains have rocks
                    return VoxelType.Rock;

                default:
                    // Default to tall grass
                    return VoxelType.TallGrass;
            }
        }
    }
}
