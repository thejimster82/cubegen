using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation.POI
{
    /// <summary>
    /// Handles direct voxel modifications for Points of Interest
    /// This approach ensures consistent POI generation across chunk boundaries
    /// </summary>
    public class POIVoxelModifier
    {
        // Cache of POI voxel modifications to ensure consistency across chunks
        // Key is world position (x, z), value is a dictionary of y-coordinate to voxel type
        private static Dictionary<Vector2I, Dictionary<int, VoxelType>> _voxelModifications = new Dictionary<Vector2I, Dictionary<int, VoxelType>>();
        
        // Cache of POI terrain height modifications
        // Key is world position (x, z), value is the modified terrain height
        private static Dictionary<Vector2I, int> _terrainHeightModifications = new Dictionary<Vector2I, int>();
        
        /// <summary>
        /// Generate all voxel modifications for a POI
        /// This is called once when the POI is first encountered
        /// </summary>
        public static void GeneratePOIVoxels(PointOfInterest poi, int chunkHeight, float waterLevel)
        {
            // Skip if this POI has already been processed
            if (_voxelModifications.ContainsKey(poi.Position))
            {
                return;
            }
            
            GD.Print($"Generating voxel modifications for POI {poi.Type} at {poi.Position}");
            
            // Generate voxel modifications based on POI type
            switch (poi.Type)
            {
                case POIType.Tower:
                    GenerateTower(poi, chunkHeight, waterLevel);
                    break;
                case POIType.RockFormation:
                    GenerateRockFormation(poi, chunkHeight, waterLevel);
                    break;
                case POIType.Ruin:
                    GenerateRuin(poi, chunkHeight, waterLevel);
                    break;
                case POIType.Waterfall:
                    GenerateWaterfall(poi, chunkHeight, waterLevel);
                    break;
                default:
                    // Default simple structure
                    GenerateDefaultStructure(poi, chunkHeight, waterLevel);
                    break;
            }
        }
        
        /// <summary>
        /// Get the modified terrain height at a specific world position
        /// </summary>
        public static int GetModifiedTerrainHeight(int worldX, int worldZ, int originalHeight)
        {
            Vector2I pos = new Vector2I(worldX, worldZ);
            
            if (_terrainHeightModifications.TryGetValue(pos, out int modifiedHeight))
            {
                return modifiedHeight;
            }
            
            return originalHeight;
        }
        
        /// <summary>
        /// Get the modified voxel type at a specific world position
        /// </summary>
        public static VoxelType GetModifiedVoxelType(int worldX, int worldY, int worldZ, VoxelType originalType)
        {
            Vector2I pos = new Vector2I(worldX, worldZ);
            
            if (_voxelModifications.TryGetValue(pos, out Dictionary<int, VoxelType> yModifications))
            {
                if (yModifications.TryGetValue(worldY, out VoxelType modifiedType))
                {
                    return modifiedType;
                }
            }
            
            return originalType;
        }
        
        /// <summary>
        /// Set a voxel modification at a specific world position
        /// </summary>
        private static void SetVoxel(int worldX, int worldY, int worldZ, VoxelType voxelType)
        {
            Vector2I pos = new Vector2I(worldX, worldZ);
            
            if (!_voxelModifications.TryGetValue(pos, out Dictionary<int, VoxelType> yModifications))
            {
                yModifications = new Dictionary<int, VoxelType>();
                _voxelModifications[pos] = yModifications;
            }
            
            yModifications[worldY] = voxelType;
        }
        
        /// <summary>
        /// Set a terrain height modification at a specific world position
        /// </summary>
        private static void SetTerrainHeight(int worldX, int worldZ, int height)
        {
            Vector2I pos = new Vector2I(worldX, worldZ);
            _terrainHeightModifications[pos] = height;
        }
        
        /// <summary>
        /// Generate a tower structure
        /// </summary>
        private static void GenerateTower(PointOfInterest poi, int chunkHeight, float waterLevel)
        {
            // Create a random generator with the POI's seed
            Random random = new Random(poi.Seed);
            
            // Tower parameters
            int baseRadius = 6;
            int towerHeight = 30 + random.Next(20); // 30-50 blocks tall
            int baseHeight = Mathf.FloorToInt(waterLevel * chunkHeight) + 2; // Slightly above water level
            
            // Create a flat base for the tower
            int minX = poi.Position.X - baseRadius * 2;
            int maxX = poi.Position.X + baseRadius * 2;
            int minZ = poi.Position.Y - baseRadius * 2;
            int maxZ = poi.Position.Y + baseRadius * 2;
            
            // Flatten terrain around the tower
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    int dx = x - poi.Position.X;
                    int dz = z - poi.Position.Y;
                    float distanceSquared = dx * dx + dz * dz;
                    
                    if (distanceSquared <= baseRadius * baseRadius * 4)
                    {
                        // Set terrain height to base height
                        SetTerrainHeight(x, z, baseHeight);
                        
                        // Set the surface to stone
                        SetVoxel(x, baseHeight, z, VoxelType.Stone);
                    }
                }
            }
            
            // Build the tower
            for (int y = baseHeight + 1; y <= baseHeight + towerHeight; y++)
            {
                // Tower gets slightly narrower as it goes up
                float heightFactor = 1.0f - ((float)(y - baseHeight) / towerHeight * 0.3f);
                int currentRadius = Mathf.FloorToInt(baseRadius * heightFactor);
                
                // Add floors every 5 blocks
                bool isFloor = (y - baseHeight) % 5 == 0;
                
                for (int x = poi.Position.X - currentRadius; x <= poi.Position.X + currentRadius; x++)
                {
                    for (int z = poi.Position.Y - currentRadius; z <= poi.Position.Y + currentRadius; z++)
                    {
                        int dx = x - poi.Position.X;
                        int dz = z - poi.Position.Y;
                        float distanceSquared = dx * dx + dz * dz;
                        
                        // Create walls (hollow tower)
                        if (distanceSquared <= currentRadius * currentRadius && 
                            (distanceSquared >= (currentRadius - 1) * (currentRadius - 1) || isFloor))
                        {
                            // Use different materials for variety
                            VoxelType material = VoxelType.Stone;
                            
                            // Add some snow at the top
                            if (y > baseHeight + towerHeight * 0.8f)
                            {
                                material = VoxelType.Snow;
                            }
                            
                            SetVoxel(x, y, z, material);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Generate a rock formation
        /// </summary>
        private static void GenerateRockFormation(PointOfInterest poi, int chunkHeight, float waterLevel)
        {
            // Create a random generator with the POI's seed
            Random random = new Random(poi.Seed);
            
            // Rock formation parameters
            int baseRadius = 8;
            int height = 15 + random.Next(10); // 15-25 blocks tall
            int baseHeight = Mathf.FloorToInt(waterLevel * chunkHeight) + 1; // Slightly above water level
            
            // Create the rock formation
            for (int y = baseHeight; y <= baseHeight + height; y++)
            {
                // Formation gets narrower as it goes up
                float heightFactor = 1.0f - ((float)(y - baseHeight) / height * 0.7f);
                int currentRadius = Mathf.FloorToInt(baseRadius * heightFactor);
                
                for (int x = poi.Position.X - currentRadius; x <= poi.Position.X + currentRadius; x++)
                {
                    for (int z = poi.Position.Y - currentRadius; z <= poi.Position.Y + currentRadius; z++)
                    {
                        int dx = x - poi.Position.X;
                        int dz = z - poi.Position.Y;
                        float distanceSquared = dx * dx + dz * dz;
                        
                        // Create a somewhat irregular shape
                        float noise = (float)random.NextDouble() * 0.3f;
                        float radiusSquared = (currentRadius * (1.0f - noise)) * (currentRadius * (1.0f - noise));
                        
                        if (distanceSquared <= radiusSquared)
                        {
                            // Set terrain height
                            if (y == baseHeight)
                            {
                                SetTerrainHeight(x, z, baseHeight);
                            }
                            
                            // Use different materials for variety
                            VoxelType material = VoxelType.Stone;
                            
                            // Add some snow at the top
                            if (y > baseHeight + height * 0.7f)
                            {
                                material = VoxelType.Snow;
                            }
                            
                            SetVoxel(x, y, z, material);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Generate ruins
        /// </summary>
        private static void GenerateRuin(PointOfInterest poi, int chunkHeight, float waterLevel)
        {
            // Implementation for ruins
            // Similar to tower but with more randomness and gaps
            // Create a random generator with the POI's seed
            Random random = new Random(poi.Seed);
            
            // Ruin parameters
            int baseRadius = 7;
            int height = 10 + random.Next(8); // 10-18 blocks tall
            int baseHeight = Mathf.FloorToInt(waterLevel * chunkHeight) + 1; // Slightly above water level
            
            // Create a flat base for the ruin
            int minX = poi.Position.X - baseRadius * 2;
            int maxX = poi.Position.X + baseRadius * 2;
            int minZ = poi.Position.Y - baseRadius * 2;
            int maxZ = poi.Position.Y + baseRadius * 2;
            
            // Flatten terrain around the ruin
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    int dx = x - poi.Position.X;
                    int dz = z - poi.Position.Y;
                    float distanceSquared = dx * dx + dz * dz;
                    
                    if (distanceSquared <= baseRadius * baseRadius * 3)
                    {
                        // Set terrain height to base height
                        SetTerrainHeight(x, z, baseHeight);
                        
                        // Set the surface to sand or stone
                        SetVoxel(x, baseHeight, z, VoxelType.Sand);
                    }
                }
            }
            
            // Build the ruin
            for (int y = baseHeight + 1; y <= baseHeight + height; y++)
            {
                // Ruin gets narrower as it goes up
                float heightFactor = 1.0f - ((float)(y - baseHeight) / height * 0.4f);
                int currentRadius = Mathf.FloorToInt(baseRadius * heightFactor);
                
                for (int x = poi.Position.X - currentRadius; x <= poi.Position.X + currentRadius; x++)
                {
                    for (int z = poi.Position.Y - currentRadius; z <= poi.Position.Y + currentRadius; z++)
                    {
                        int dx = x - poi.Position.X;
                        int dz = z - poi.Position.Y;
                        float distanceSquared = dx * dx + dz * dz;
                        
                        // Create walls with gaps for a ruined look
                        if (distanceSquared <= currentRadius * currentRadius && 
                            distanceSquared >= (currentRadius - 1) * (currentRadius - 1))
                        {
                            // Add random gaps to make it look ruined
                            float ruinChance = (float)(y - baseHeight) / height; // More gaps higher up
                            if (random.NextDouble() > ruinChance * 0.7f)
                            {
                                SetVoxel(x, y, z, VoxelType.Sand);
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Generate a waterfall
        /// </summary>
        private static void GenerateWaterfall(PointOfInterest poi, int chunkHeight, float waterLevel)
        {
            // Implementation for waterfall
            // Create a random generator with the POI's seed
            Random random = new Random(poi.Seed);
            
            // Waterfall parameters
            int baseRadius = 5;
            int height = 20 + random.Next(10); // 20-30 blocks tall
            int baseHeight = Mathf.FloorToInt(waterLevel * chunkHeight) - 2; // Below water level for the pool
            
            // Create a pool at the bottom
            int poolRadius = baseRadius * 2;
            for (int x = poi.Position.X - poolRadius; x <= poi.Position.X + poolRadius; x++)
            {
                for (int z = poi.Position.Y - poolRadius; z <= poi.Position.Y + poolRadius; z++)
                {
                    int dx = x - poi.Position.X;
                    int dz = z - poi.Position.Y;
                    float distanceSquared = dx * dx + dz * dz;
                    
                    if (distanceSquared <= poolRadius * poolRadius)
                    {
                        // Set terrain height to base height
                        SetTerrainHeight(x, z, baseHeight);
                        
                        // Fill with water up to water level
                        for (int y = baseHeight + 1; y <= Mathf.FloorToInt(waterLevel * chunkHeight); y++)
                        {
                            SetVoxel(x, y, z, VoxelType.Water);
                        }
                    }
                }
            }
            
            // Create the waterfall
            for (int y = baseHeight + 1; y <= baseHeight + height; y++)
            {
                // Waterfall gets narrower as it goes up
                float heightFactor = 1.0f - ((float)(y - baseHeight) / height * 0.5f);
                int currentRadius = Mathf.FloorToInt(baseRadius * heightFactor);
                
                for (int x = poi.Position.X - currentRadius; x <= poi.Position.X + currentRadius; x++)
                {
                    for (int z = poi.Position.Y - currentRadius; z <= poi.Position.Y + currentRadius; z++)
                    {
                        int dx = x - poi.Position.X;
                        int dz = z - poi.Position.Y;
                        float distanceSquared = dx * dx + dz * dz;
                        
                        // Create the water stream
                        if (distanceSquared <= currentRadius * currentRadius)
                        {
                            // Set water voxels
                            SetVoxel(x, y, z, VoxelType.Water);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Generate a default simple structure
        /// </summary>
        private static void GenerateDefaultStructure(PointOfInterest poi, int chunkHeight, float waterLevel)
        {
            // Create a random generator with the POI's seed
            Random random = new Random(poi.Seed);
            
            // Structure parameters
            int baseRadius = 4;
            int height = 8 + random.Next(5); // 8-13 blocks tall
            int baseHeight = Mathf.FloorToInt(waterLevel * chunkHeight) + 1; // Slightly above water level
            
            // Create a simple structure
            for (int y = baseHeight; y <= baseHeight + height; y++)
            {
                // Structure gets narrower as it goes up
                float heightFactor = 1.0f - ((float)(y - baseHeight) / height * 0.5f);
                int currentRadius = Mathf.FloorToInt(baseRadius * heightFactor);
                
                for (int x = poi.Position.X - currentRadius; x <= poi.Position.X + currentRadius; x++)
                {
                    for (int z = poi.Position.Y - currentRadius; z <= poi.Position.Y + currentRadius; z++)
                    {
                        int dx = x - poi.Position.X;
                        int dz = z - poi.Position.Y;
                        float distanceSquared = dx * dx + dz * dz;
                        
                        if (distanceSquared <= currentRadius * currentRadius)
                        {
                            // Set terrain height
                            if (y == baseHeight)
                            {
                                SetTerrainHeight(x, z, baseHeight);
                            }
                            
                            // Use stone for the structure
                            SetVoxel(x, y, z, VoxelType.Stone);
                        }
                    }
                }
            }
        }
    }
}
