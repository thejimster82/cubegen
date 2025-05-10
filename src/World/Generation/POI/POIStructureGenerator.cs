using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation.POI
{
    /// <summary>
    /// Handles the generation of POI structures in chunks
    /// </summary>
    public class POIStructureGenerator
    {
        // Singleton instance
        private static POIStructureGenerator _instance;
        public static POIStructureGenerator Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new POIStructureGenerator();
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// Generates POI structures for a chunk
        /// </summary>
        /// <param name="chunk">The chunk to generate structures for</param>
        /// <param name="chunkPos">The chunk position</param>
        public void GenerateStructuresForChunk(VoxelChunk chunk, Vector2I chunkPos)
        {
            // Get all POIs that might affect this chunk
            List<POIInstance> poiInstances = POIRegistry.Instance.GetPOIsForChunk(chunkPos);
            
            if (poiInstances.Count == 0)
            {
                // No POIs affect this chunk
                return;
            }
            
            // Calculate chunk bounds in world coordinates
            int chunkSize = WorldGenerator.CHUNK_SIZE;
            int minX = chunkPos.X * chunkSize;
            int minZ = chunkPos.Y * chunkSize;
            
            // Process each POI
            foreach (POIInstance poi in poiInstances)
            {
                // Generate the structure based on POI type
                switch (poi.Definition.Id)
                {
                    case "village":
                        GenerateVillage(chunk, poi, minX, minZ, chunkSize);
                        break;
                        
                    case "ruins":
                        GenerateRuins(chunk, poi, minX, minZ, chunkSize);
                        break;
                        
                    case "oasis":
                        GenerateOasis(chunk, poi, minX, minZ, chunkSize);
                        break;
                        
                    default:
                        // Unknown POI type, generate a generic structure
                        GenerateGenericStructure(chunk, poi, minX, minZ, chunkSize);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Generates a village structure
        /// </summary>
        private void GenerateVillage(VoxelChunk chunk, POIInstance poi, int minX, int minZ, int chunkSize)
        {
            // Create a random generator for this POI
            Random random = new Random(poi.Seed);
            
            // Calculate village bounds
            int halfWidth = poi.Definition.Dimensions.X / 2;
            int halfDepth = poi.Definition.Dimensions.Z / 2;
            
            // Calculate village area in world coordinates
            int villageMinX = poi.Position.X - halfWidth;
            int villageMaxX = poi.Position.X + halfWidth;
            int villageMinZ = poi.Position.Z - halfDepth;
            int villageMaxZ = poi.Position.Z + halfDepth;
            
            // Calculate intersection with chunk
            int intersectMinX = Math.Max(villageMinX, minX);
            int intersectMaxX = Math.Min(villageMaxX, minX + chunkSize);
            int intersectMinZ = Math.Max(villageMinZ, minZ);
            int intersectMaxZ = Math.Min(villageMaxZ, minZ + chunkSize);
            
            // Check if there's an intersection
            if (intersectMinX >= intersectMaxX || intersectMinZ >= intersectMaxZ)
            {
                return; // No intersection
            }
            
            // Generate village buildings
            int numBuildings = random.Next(3, 6);
            
            for (int i = 0; i < numBuildings; i++)
            {
                // Determine building position within village
                int buildingX = poi.Position.X + random.Next(-halfWidth + 5, halfWidth - 5);
                int buildingZ = poi.Position.Z + random.Next(-halfDepth + 5, halfDepth - 5);
                
                // Check if building is in this chunk
                if (buildingX >= minX && buildingX < minX + chunkSize &&
                    buildingZ >= minZ && buildingZ < minZ + chunkSize)
                {
                    // Generate a building
                    GenerateBuilding(chunk, buildingX - minX, poi.TerrainHeight, buildingZ - minZ, random);
                }
            }
            
            // Generate paths between buildings
            GenerateVillagePaths(chunk, poi, minX, minZ, chunkSize);
        }
        
        /// <summary>
        /// Generates a building for a village
        /// </summary>
        private void GenerateBuilding(VoxelChunk chunk, int x, int baseY, int z, Random random)
        {
            // Building parameters
            int width = random.Next(5, 8);
            int depth = random.Next(5, 8);
            int height = random.Next(4, 7);
            
            // Calculate building bounds
            int halfWidth = width / 2;
            int halfDepth = depth / 2;
            
            // Generate foundation
            for (int dx = -halfWidth; dx <= halfWidth; dx++)
            {
                for (int dz = -halfDepth; dz <= halfDepth; dz++)
                {
                    int nx = x + dx;
                    int nz = z + dz;
                    
                    // Check chunk boundaries
                    if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size)
                    {
                        // Place foundation blocks
                        for (int y = -1; y <= 0; y++)
                        {
                            int ny = baseY + y;
                            if (ny >= 0 && ny < chunk.Height)
                            {
                                chunk.SetVoxel(nx, ny, nz, VoxelType.Stone);
                            }
                        }
                    }
                }
            }
            
            // Generate walls
            for (int dx = -halfWidth; dx <= halfWidth; dx++)
            {
                for (int dz = -halfDepth; dz <= halfDepth; dz++)
                {
                    // Only place walls on the perimeter
                    if (dx == -halfWidth || dx == halfWidth || dz == -halfDepth || dz == halfDepth)
                    {
                        int nx = x + dx;
                        int nz = z + dz;
                        
                        // Check chunk boundaries
                        if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size)
                        {
                            // Place wall blocks
                            for (int y = 1; y <= height; y++)
                            {
                                int ny = baseY + y;
                                if (ny >= 0 && ny < chunk.Height)
                                {
                                    // Door in the middle of one wall
                                    if (y <= 2 && ((dx == 0 && dz == -halfDepth) || (random.Next(0, 20) == 0)))
                                    {
                                        // Door opening
                                        chunk.SetVoxel(nx, ny, nz, VoxelType.Air);
                                    }
                                    else
                                    {
                                        // Wall
                                        chunk.SetVoxel(nx, ny, nz, VoxelType.Wood);
                                    }
                                }
                            }
                        }
                    }
                    // Interior floor
                    else
                    {
                        int nx = x + dx;
                        int nz = z + dz;
                        
                        // Check chunk boundaries
                        if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size)
                        {
                            // Place floor
                            int ny = baseY + 1;
                            if (ny >= 0 && ny < chunk.Height)
                            {
                                chunk.SetVoxel(nx, ny, nz, VoxelType.Wood);
                            }
                        }
                    }
                }
            }
            
            // Generate roof
            for (int dx = -halfWidth - 1; dx <= halfWidth + 1; dx++)
            {
                for (int dz = -halfDepth - 1; dz <= halfDepth + 1; dz++)
                {
                    int nx = x + dx;
                    int nz = z + dz;
                    
                    // Check chunk boundaries
                    if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size)
                    {
                        // Place roof blocks
                        int ny = baseY + height + 1;
                        if (ny >= 0 && ny < chunk.Height)
                        {
                            chunk.SetVoxel(nx, ny, nz, VoxelType.Wood);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Generates paths between buildings in a village
        /// </summary>
        private void GenerateVillagePaths(VoxelChunk chunk, POIInstance poi, int minX, int minZ, int chunkSize)
        {
            // TODO: Implement path generation
        }
        
        /// <summary>
        /// Generates ruins structure
        /// </summary>
        private void GenerateRuins(VoxelChunk chunk, POIInstance poi, int minX, int minZ, int chunkSize)
        {
            // TODO: Implement ruins generation
        }
        
        /// <summary>
        /// Generates oasis structure
        /// </summary>
        private void GenerateOasis(VoxelChunk chunk, POIInstance poi, int minX, int minZ, int chunkSize)
        {
            // TODO: Implement oasis generation
        }
        
        /// <summary>
        /// Generates a generic structure for unknown POI types
        /// </summary>
        private void GenerateGenericStructure(VoxelChunk chunk, POIInstance poi, int minX, int minZ, int chunkSize)
        {
            // Create a random generator for this POI
            Random random = new Random(poi.Seed);
            
            // Calculate structure bounds
            int halfWidth = poi.Definition.Dimensions.X / 2;
            int halfDepth = poi.Definition.Dimensions.Z / 2;
            
            // Calculate structure area in world coordinates
            int structureMinX = poi.Position.X - halfWidth;
            int structureMaxX = poi.Position.X + halfWidth;
            int structureMinZ = poi.Position.Z - halfDepth;
            int structureMaxZ = poi.Position.Z + halfDepth;
            
            // Calculate intersection with chunk
            int intersectMinX = Math.Max(structureMinX, minX);
            int intersectMaxX = Math.Min(structureMaxX, minX + chunkSize);
            int intersectMinZ = Math.Max(structureMinZ, minZ);
            int intersectMaxZ = Math.Min(structureMaxZ, minZ + chunkSize);
            
            // Check if there's an intersection
            if (intersectMinX >= intersectMaxX || intersectMinZ >= intersectMaxZ)
            {
                return; // No intersection
            }
            
            // Generate a simple marker structure
            for (int x = intersectMinX; x < intersectMaxX; x++)
            {
                for (int z = intersectMinZ; z < intersectMaxZ; z++)
                {
                    // Convert to chunk coordinates
                    int localX = x - minX;
                    int localZ = z - minZ;
                    
                    // Place a marker block at the surface
                    int ny = poi.TerrainHeight + 1;
                    if (ny >= 0 && ny < chunk.Height)
                    {
                        chunk.SetVoxel(localX, ny, localZ, VoxelType.Stone);
                    }
                }
            }
        }
    }
}
