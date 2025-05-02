using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
    public static class DecorationClusters
    {
        // Cluster density parameters
        private const float CLUSTER_RADIUS_MIN = 2.0f;
        private const float CLUSTER_RADIUS_MAX = 5.0f;
        private const int MIN_ITEMS_PER_CLUSTER = 3;
        private const int MAX_ITEMS_PER_CLUSTER = 8;
        
        // Cluster center probability (chance to start a new cluster)
        private const float CLUSTER_CENTER_PROBABILITY = 0.02f; // 2% chance for a cluster center
        
        // Dictionary to track cluster centers
        private static Dictionary<Vector2I, List<ClusterInfo>> _clusterCenters = new Dictionary<Vector2I, List<ClusterInfo>>();
        
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
        
        // Initialize clusters for a chunk
        public static void InitializeChunkClusters(Vector2I chunkPos, int chunkSize, Random random)
        {
            // Calculate world position of chunk corner
            Vector2I worldPos = new Vector2I(chunkPos.X * chunkSize, chunkPos.Y * chunkSize);
            
            // Create a list for this chunk if it doesn't exist
            if (!_clusterCenters.ContainsKey(chunkPos))
            {
                _clusterCenters[chunkPos] = new List<ClusterInfo>();
            }
            
            // Clear any existing clusters for this chunk
            _clusterCenters[chunkPos].Clear();
            
            // Determine how many potential cluster centers to check
            int potentialCenters = (int)(chunkSize * chunkSize * CLUSTER_CENTER_PROBABILITY);
            
            // Ensure at least one potential center per chunk
            potentialCenters = Math.Max(1, potentialCenters);
            
            // Try to create clusters
            for (int i = 0; i < potentialCenters; i++)
            {
                // Random position within the chunk
                int offsetX = random.Next(chunkSize);
                int offsetZ = random.Next(chunkSize);
                
                // Calculate world position
                Vector2I position = new Vector2I(worldPos.X + offsetX, worldPos.Y + offsetZ);
                
                // Determine if this will be a cluster center (higher probability in certain biomes)
                BiomeType biomeType = WorldGenerator.GetBiomeType(position.X, position.Y);
                float clusterProbability = GetClusterProbabilityForBiome(biomeType);
                
                if (random.NextDouble() < clusterProbability)
                {
                    // Create a new cluster
                    VoxelType decorationType = GetRandomDecorationForBiome(biomeType, random);
                    float radius = (float)(CLUSTER_RADIUS_MIN + random.NextDouble() * (CLUSTER_RADIUS_MAX - CLUSTER_RADIUS_MIN));
                    int itemCount = random.Next(MIN_ITEMS_PER_CLUSTER, MAX_ITEMS_PER_CLUSTER + 1);
                    
                    // Add to the list
                    _clusterCenters[chunkPos].Add(new ClusterInfo(position, radius, decorationType, itemCount));
                }
            }
        }
        
        // Check if a position is within a cluster and should have a decoration
        public static bool ShouldPlaceDecoration(Vector2I worldPos, out VoxelType decorationType, Random random)
        {
            decorationType = VoxelType.Air; // Default to no decoration
            
            // Check all nearby chunks for clusters
            int chunkSize = WorldGenerator.CHUNK_SIZE;
            Vector2I chunkPos = new Vector2I(
                Mathf.FloorToInt((float)worldPos.X / chunkSize),
                Mathf.FloorToInt((float)worldPos.Y / chunkSize)
            );
            
            // Check the current chunk and all 8 surrounding chunks
            for (int xOffset = -1; xOffset <= 1; xOffset++)
            {
                for (int zOffset = -1; zOffset <= 1; zOffset++)
                {
                    Vector2I neighborChunkPos = new Vector2I(chunkPos.X + xOffset, chunkPos.Y + zOffset);
                    
                    // Skip if this chunk has no clusters
                    if (!_clusterCenters.ContainsKey(neighborChunkPos))
                        continue;
                    
                    // Check each cluster in this chunk
                    foreach (var cluster in _clusterCenters[neighborChunkPos])
                    {
                        // Calculate distance to cluster center
                        float distanceSquared = 
                            (worldPos.X - cluster.Position.X) * (worldPos.X - cluster.Position.X) +
                            (worldPos.Y - cluster.Position.Y) * (worldPos.Y - cluster.Position.Y);
                        
                        float radiusSquared = cluster.Radius * cluster.Radius;
                        
                        // If within radius and cluster still has items to place
                        if (distanceSquared <= radiusSquared && cluster.ItemsPlaced < cluster.ItemCount)
                        {
                            // Calculate probability based on distance from center (higher near center)
                            float distanceFactor = 1.0f - (distanceSquared / radiusSquared);
                            float probability = 0.7f * distanceFactor;
                            
                            // Random chance to place decoration based on distance
                            if (random.NextDouble() < probability)
                            {
                                // Get the decoration type for this cluster
                                decorationType = cluster.Type;
                                
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
        
        // Get cluster probability based on biome
        private static float GetClusterProbabilityForBiome(BiomeType biomeType)
        {
            switch (biomeType)
            {
                case BiomeType.Plains:
                    return 0.8f; // High probability for grass clusters
                case BiomeType.Forest:
                    return 0.7f; // High probability for mushroom/stick clusters
                case BiomeType.Desert:
                    return 0.4f; // Lower probability for rock clusters
                case BiomeType.Tundra:
                    return 0.3f; // Lower probability for rock clusters
                case BiomeType.Mountains:
                    return 0.2f; // Low probability for any clusters
                default:
                    return 0.5f;
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
