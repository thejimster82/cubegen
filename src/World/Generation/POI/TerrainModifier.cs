using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation.POI
{
    /// <summary>
    /// Handles terrain modification for Points of Interest
    /// </summary>
    public class TerrainModifier
    {
        /// <summary>
        /// Calculate how a POI affects terrain height at a specific position
        /// </summary>
        /// <param name="poi">The Point of Interest</param>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldZ">World Z coordinate</param>
        /// <param name="baseHeight">Original terrain height</param>
        /// <returns>Modified terrain height</returns>
        public static int ModifyTerrainHeight(PointOfInterest poi, int worldX, int worldZ, int baseHeight, int chunkHeight)
        {
            // Calculate distance from POI center
            int dx = worldX - poi.Position.X;
            int dz = worldZ - poi.Position.Y;
            float distanceSquared = dx * dx + dz * dz;
            float radius = poi.InfluenceRadius;
            
            // If outside influence radius, return original height
            if (distanceSquared > radius * radius)
            {
                return baseHeight;
            }
            
            // Calculate distance factor (1 at center, 0 at edge)
            float distance = Mathf.Sqrt(distanceSquared);
            float distanceFactor = 1.0f - (distance / radius);
            
            // Apply falloff curve for smoother transition
            distanceFactor = Mathf.Pow(distanceFactor, 2);
            
            // Apply POI's terrain influence factor
            float influenceFactor = distanceFactor * poi.TerrainInfluence;
            
            // Modify height based on POI type
            int modifiedHeight = baseHeight;
            
            switch (poi.Type)
            {
                case POIType.Village:
                case POIType.Town:
                    // Flatten terrain for settlements
                    modifiedHeight = FlattenTerrain(baseHeight, poi, influenceFactor, chunkHeight);
                    break;
                    
                case POIType.Lake:
                case POIType.Pond:
                    // Create depression for water bodies
                    modifiedHeight = CreateDepression(baseHeight, poi, influenceFactor, chunkHeight);
                    break;
                    
                case POIType.RockFormation:
                case POIType.Tower:
                case POIType.Obelisk:
                    // Create elevation for tall structures
                    modifiedHeight = CreateElevation(baseHeight, poi, influenceFactor, chunkHeight);
                    break;
                    
                case POIType.Cave:
                    // Create entrance depression for caves
                    modifiedHeight = CreateCaveEntrance(baseHeight, poi, influenceFactor, worldX, worldZ, chunkHeight);
                    break;
                    
                default:
                    // Default slight modification based on influence
                    Random random = new Random(poi.Seed + worldX * 73856093 + worldZ * 19349663);
                    float randomFactor = (float)random.NextDouble() * 0.4f - 0.2f; // -0.2 to 0.2
                    modifiedHeight = baseHeight + Mathf.FloorToInt(influenceFactor * 5 * randomFactor);
                    break;
            }
            
            return modifiedHeight;
        }
        
        /// <summary>
        /// Flatten terrain for settlements
        /// </summary>
        private static int FlattenTerrain(int baseHeight, PointOfInterest poi, float influenceFactor, int chunkHeight)
        {
            // Determine target height (slightly above water level)
            int waterLevelHeight = Mathf.FloorToInt(0.18f * chunkHeight); // Using same water level as WorldGenerator
            int targetHeight = waterLevelHeight + 3; // 3 blocks above water level
            
            // Blend between original and target height based on influence
            return Mathf.FloorToInt(Mathf.Lerp(baseHeight, targetHeight, influenceFactor));
        }
        
        /// <summary>
        /// Create a depression for water bodies
        /// </summary>
        private static int CreateDepression(int baseHeight, PointOfInterest poi, float influenceFactor, int chunkHeight)
        {
            // Get water depth from POI custom data
            float depth = 0.2f; // Default depth
            if (poi.CustomData.ContainsKey("Depth") && poi.CustomData["Depth"] is float customDepth)
            {
                depth = customDepth;
            }
            
            // Calculate water level
            int waterLevelHeight = Mathf.FloorToInt(0.18f * chunkHeight);
            
            // Target height is below water level based on depth
            int depthInBlocks = Mathf.FloorToInt(depth * chunkHeight);
            int targetHeight = waterLevelHeight - depthInBlocks;
            
            // Blend between original and target height based on influence
            return Mathf.FloorToInt(Mathf.Lerp(baseHeight, targetHeight, influenceFactor));
        }
        
        /// <summary>
        /// Create an elevation for tall structures
        /// </summary>
        private static int CreateElevation(int baseHeight, PointOfInterest poi, float influenceFactor, int chunkHeight)
        {
            // Determine how high to elevate based on POI type
            int elevationHeight;
            
            switch (poi.Type)
            {
                case POIType.RockFormation:
                    elevationHeight = 8;
                    break;
                case POIType.Tower:
                    elevationHeight = 5;
                    break;
                case POIType.Obelisk:
                    elevationHeight = 3;
                    break;
                default:
                    elevationHeight = 5;
                    break;
            }
            
            // Apply elevation with influence factor
            int heightIncrease = Mathf.FloorToInt(elevationHeight * influenceFactor);
            return baseHeight + heightIncrease;
        }
        
        /// <summary>
        /// Create a cave entrance
        /// </summary>
        private static int CreateCaveEntrance(int baseHeight, PointOfInterest poi, float influenceFactor, int worldX, int worldZ, int chunkHeight)
        {
            // Create a random generator for this position
            Random random = new Random(poi.Seed + worldX * 73856093 + worldZ * 19349663);
            
            // Calculate distance from center
            int dx = worldX - poi.Position.X;
            int dz = worldZ - poi.Position.Y;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            
            // Create a depression that's deeper at the center
            int maxDepth = 15; // Maximum depth of the cave entrance
            
            // Calculate depth based on distance from center
            float depthFactor = 1.0f - (distance / (poi.InfluenceRadius * 0.5f));
            depthFactor = Mathf.Clamp(depthFactor, 0.0f, 1.0f);
            
            // Add some noise to the depth for a more natural look
            float noise = (float)random.NextDouble() * 0.3f - 0.15f; // -0.15 to 0.15
            depthFactor += noise;
            depthFactor = Mathf.Clamp(depthFactor, 0.0f, 1.0f);
            
            // Calculate final depth
            int depth = Mathf.FloorToInt(maxDepth * depthFactor * influenceFactor);
            
            return baseHeight - depth;
        }
        
        /// <summary>
        /// Determine voxel type modifications for a POI
        /// </summary>
        public static VoxelType ModifyVoxelType(PointOfInterest poi, int worldX, int worldZ, int worldY, VoxelType originalType, int terrainHeight)
        {
            // Calculate distance from POI center
            int dx = worldX - poi.Position.X;
            int dz = worldZ - poi.Position.Y;
            float distanceSquared = dx * dx + dz * dz;
            float radius = poi.InfluenceRadius;
            
            // If outside influence radius, return original type
            if (distanceSquared > radius * radius)
            {
                return originalType;
            }
            
            // Calculate distance factor (1 at center, 0 at edge)
            float distance = Mathf.Sqrt(distanceSquared);
            float distanceFactor = 1.0f - (distance / radius);
            
            // Apply falloff curve for smoother transition
            distanceFactor = Mathf.Pow(distanceFactor, 2);
            
            // Create a random generator for this position
            Random random = new Random(poi.Seed + worldX * 73856093 + worldZ * 19349663 + worldY * 31);
            
            // Modify voxel type based on POI type
            switch (poi.Type)
            {
                case POIType.Lake:
                case POIType.Pond:
                    // If this is at the bottom of the lake/pond, use sand
                    if (worldY == terrainHeight && distanceFactor > 0.3f)
                    {
                        return VoxelType.Sand;
                    }
                    break;
                    
                case POIType.Village:
                case POIType.Town:
                    // If this is at the surface in a settlement, use a different block type
                    if (worldY == terrainHeight && distanceFactor > 0.7f)
                    {
                        // Central area of settlement might have different ground
                        float chance = random.Next(100) / 100.0f;
                        if (chance < 0.7f)
                        {
                            return VoxelType.Stone; // Stone paths/foundations
                        }
                    }
                    break;
            }
            
            // Default: return original type
            return originalType;
        }
    }
}
