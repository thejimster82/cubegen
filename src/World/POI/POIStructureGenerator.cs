using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;
using CubeGen.World.Generation;

namespace CubeGen.World.POI
{
    /// <summary>
    /// Handles the generation of structures for Points of Interest
    /// </summary>
    public static class POIStructureGenerator
    {
        /// <summary>
        /// Generates the structure for a POI in a chunk
        /// </summary>
        public static void GeneratePOIStructure(PointOfInterest poi, VoxelChunk chunk, int chunkSize, int chunkHeight)
        {
            // Create a random generator with the POI's seed
            Random random = new Random(poi.Seed);
            
            // Generate structure based on POI type
            switch (poi.Type)
            {
                case POIType.TestSphere:
                    GenerateTestSphere(poi, chunk, chunkSize, chunkHeight, random);
                    break;
                    
                case POIType.LargeRock:
                    GenerateLargeRock(poi, chunk, chunkSize, chunkHeight, random);
                    break;
                    
                case POIType.Lake:
                    GenerateLake(poi, chunk, chunkSize, chunkHeight, random);
                    break;
                    
                case POIType.Volcano:
                    GenerateVolcano(poi, chunk, chunkSize, chunkHeight, random);
                    break;
                    
                case POIType.Town:
                    GenerateTown(poi, chunk, chunkSize, chunkHeight, random);
                    break;
                    
                case POIType.Farm:
                    GenerateFarm(poi, chunk, chunkSize, chunkHeight, random);
                    break;
                    
                case POIType.Ruins:
                    GenerateRuins(poi, chunk, chunkSize, chunkHeight, random);
                    break;
            }
        }
        
        /// <summary>
        /// Generates a test sphere POI (30x30 voxels)
        /// </summary>
        private static void GenerateTestSphere(PointOfInterest poi, VoxelChunk chunk, int chunkSize, int chunkHeight, Random random)
        {
            // Get chunk world position
            Vector2I chunkWorldPos = new Vector2I(
                chunk.Position.X * chunkSize,
                chunk.Position.Y * chunkSize
            );
            
            // Sphere parameters
            int sphereRadius = 15; // 30x30 sphere
            Vector3 sphereCenter = new Vector3(
                poi.Center.X - chunkWorldPos.X,
                chunkHeight / 2, // Center vertically in the chunk
                poi.Center.Y - chunkWorldPos.Y
            );
            
            // Only proceed if the sphere intersects this chunk
            if (!SphereIntersectsChunk(sphereCenter, sphereRadius, chunkSize))
            {
                return;
            }
            
            // Generate the sphere
            for (int x = 0; x < chunkSize; x++)
            {
                for (int y = 0; y < chunkHeight; y++)
                {
                    for (int z = 0; z < chunkSize; z++)
                    {
                        // Calculate distance from center
                        float dx = x - sphereCenter.X;
                        float dy = y - sphereCenter.Y;
                        float dz = z - sphereCenter.Z;
                        float distanceSquared = dx * dx + dy * dy + dz * dz;
                        
                        // If inside sphere radius, set to stone
                        if (distanceSquared <= sphereRadius * sphereRadius)
                        {
                            chunk.SetVoxel(x, y, z, VoxelType.Stone);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Generates a large rock formation POI
        /// </summary>
        private static void GenerateLargeRock(PointOfInterest poi, VoxelChunk chunk, int chunkSize, int chunkHeight, Random random)
        {
            // Get chunk world position
            Vector2I chunkWorldPos = new Vector2I(
                chunk.Position.X * chunkSize,
                chunk.Position.Y * chunkSize
            );
            
            // Rock parameters
            int rockRadius = poi.Radius / 2; // Half the POI radius
            Vector3 rockCenter = new Vector3(
                poi.Center.X - chunkWorldPos.X,
                chunkHeight / 3, // Lower in the chunk
                poi.Center.Y - chunkWorldPos.Y
            );
            
            // Only proceed if the rock intersects this chunk
            if (!SphereIntersectsChunk(rockCenter, rockRadius, chunkSize))
            {
                return;
            }
            
            // Use noise to create a more natural rock shape
            FastNoiseLite rockNoise = new FastNoiseLite();
            rockNoise.Seed = poi.Seed;
            rockNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            rockNoise.Frequency = 0.1f;
            
            // Generate the rock
            for (int x = 0; x < chunkSize; x++)
            {
                for (int y = 0; y < chunkHeight; y++)
                {
                    for (int z = 0; z < chunkSize; z++)
                    {
                        // Calculate distance from center
                        float dx = x - rockCenter.X;
                        float dy = y - rockCenter.Y;
                        float dz = z - rockCenter.Z;
                        float distanceSquared = dx * dx + dy * dy + dz * dz;
                        float distance = Mathf.Sqrt(distanceSquared);
                        
                        // Get noise value for this position
                        float noiseValue = rockNoise.GetNoise3D(x, y, z) * 0.5f; // -0.5 to 0.5
                        
                        // Adjust radius based on noise
                        float adjustedRadius = rockRadius * (1.0f + noiseValue);
                        
                        // If inside adjusted radius, set to stone
                        if (distance <= adjustedRadius)
                        {
                            // Use different voxel types based on position
                            VoxelType rockType = VoxelType.Stone;
                            
                            // Add some dirt on top
                            if (y > rockCenter.Y && random.NextDouble() < 0.3)
                            {
                                rockType = VoxelType.Dirt;
                            }
                            
                            chunk.SetVoxel(x, y, z, rockType);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Generates a lake POI
        /// </summary>
        private static void GenerateLake(PointOfInterest poi, VoxelChunk chunk, int chunkSize, int chunkHeight, Random random)
        {
            // Get chunk world position
            Vector2I chunkWorldPos = new Vector2I(
                chunk.Position.X * chunkSize,
                chunk.Position.Y * chunkSize
            );
            
            // Lake parameters
            int lakeRadius = poi.Radius / 2; // Half the POI radius
            int lakeDepth = 10; // Depth of the lake
            int waterLevel = chunkHeight / 3; // Water level
            
            Vector3 lakeCenter = new Vector3(
                poi.Center.X - chunkWorldPos.X,
                waterLevel, // At water level
                poi.Center.Y - chunkWorldPos.Y
            );
            
            // Only proceed if the lake intersects this chunk
            if (!SphereIntersectsChunk(new Vector3(lakeCenter.X, 0, lakeCenter.Z), lakeRadius, chunkSize))
            {
                return;
            }
            
            // Use noise to create a more natural lake shape
            FastNoiseLite lakeNoise = new FastNoiseLite();
            lakeNoise.Seed = poi.Seed;
            lakeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            lakeNoise.Frequency = 0.05f;
            
            // Generate the lake
            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    // Calculate distance from center (2D)
                    float dx = x - lakeCenter.X;
                    float dz = z - lakeCenter.Z;
                    float distanceSquared = dx * dx + dz * dz;
                    float distance = Mathf.Sqrt(distanceSquared);
                    
                    // Get noise value for this position
                    float noiseValue = lakeNoise.GetNoise2D(x + chunkWorldPos.X, z + chunkWorldPos.Y) * 0.3f; // -0.3 to 0.3
                    
                    // Adjust radius based on noise
                    float adjustedRadius = lakeRadius * (1.0f + noiseValue);
                    
                    // If inside adjusted radius, create lake
                    if (distance <= adjustedRadius)
                    {
                        // Calculate depth based on distance from edge
                        float depthFactor = 1.0f - (distance / adjustedRadius);
                        int depth = Mathf.FloorToInt(lakeDepth * depthFactor);
                        
                        // Carve out the lake and fill with water
                        for (int y = waterLevel - depth; y <= waterLevel; y++)
                        {
                            if (y >= 0 && y < chunkHeight)
                            {
                                if (y < waterLevel)
                                {
                                    // Lake bottom (sand or dirt)
                                    if (y == waterLevel - depth || random.NextDouble() < 0.2)
                                    {
                                        chunk.SetVoxel(x, y, z, VoxelType.Sand);
                                    }
                                    else
                                    {
                                        chunk.SetVoxel(x, y, z, VoxelType.Dirt);
                                    }
                                }
                                else
                                {
                                    // Water
                                    chunk.SetVoxel(x, y, z, VoxelType.Water);
                                }
                            }
                        }
                        
                        // Add sand around the edge of the lake
                        if (distance > adjustedRadius * 0.8f)
                        {
                            // Add sand at water level and slightly above
                            for (int y = waterLevel + 1; y <= waterLevel + 2; y++)
                            {
                                if (y >= 0 && y < chunkHeight)
                                {
                                    if (chunk.GetVoxel(x, y, z) != VoxelType.Air)
                                    {
                                        chunk.SetVoxel(x, y, z, VoxelType.Sand);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Checks if a sphere intersects with a chunk
        /// </summary>
        private static bool SphereIntersectsChunk(Vector3 sphereCenter, float sphereRadius, int chunkSize)
        {
            // Calculate the closest point on the chunk to the sphere center
            float closestX = Mathf.Clamp(sphereCenter.X, 0, chunkSize);
            float closestY = Mathf.Clamp(sphereCenter.Y, 0, chunkSize);
            float closestZ = Mathf.Clamp(sphereCenter.Z, 0, chunkSize);
            
            // Calculate distance squared from closest point to sphere center
            float dx = closestX - sphereCenter.X;
            float dy = closestY - sphereCenter.Y;
            float dz = closestZ - sphereCenter.Z;
            float distanceSquared = dx * dx + dy * dy + dz * dz;
            
            // If the distance is less than the radius, the sphere intersects the chunk
            return distanceSquared <= sphereRadius * sphereRadius;
        }
        
        // Placeholder methods for other POI types
        // These would be implemented with actual generation logic
        
        private static void GenerateVolcano(PointOfInterest poi, VoxelChunk chunk, int chunkSize, int chunkHeight, Random random)
        {
            // TODO: Implement volcano generation
        }
        
        private static void GenerateTown(PointOfInterest poi, VoxelChunk chunk, int chunkSize, int chunkHeight, Random random)
        {
            // TODO: Implement town generation
        }
        
        private static void GenerateFarm(PointOfInterest poi, VoxelChunk chunk, int chunkSize, int chunkHeight, Random random)
        {
            // TODO: Implement farm generation
        }
        
        private static void GenerateRuins(PointOfInterest poi, VoxelChunk chunk, int chunkSize, int chunkHeight, Random random)
        {
            // TODO: Implement ruins generation
        }
    }
}
