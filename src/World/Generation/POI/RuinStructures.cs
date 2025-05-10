using System;
using Godot;
using CubeGen.World.Common;

namespace CubeGen.World.Generation.POI
{
    public static class RuinStructures
    {
        // Default chunk height (same as in WorldGenerator)
        private const int ChunkHeight = 128;
        // Generate a ruin structure for a POI
        public static void GenerateRuinStructure(VoxelChunk chunk, POI.PointOfInterest poi, int x, int z, int surfaceHeight, bool[,] featureMap, Random random)
        {
            // Only proceed if the POI is within the chunk
            if (x < 0 || x >= chunk.Size || z < 0 || z >= chunk.Size)
                return;

            // Mark the area as occupied in the feature map
            int radius = 8; // Ruin radius
            // Mark the feature position in the feature map
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    int nx = x + dx;
                    int nz = z + dz;

                    if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size)
                    {
                        featureMap[nx, nz] = true;
                    }
                }
            }

            // Determine ruin type (0 = broken tower, 1 = collapsed building, 2 = ancient temple)
            int ruinType = random.Next(3);

            switch (ruinType)
            {
                case 0: // Broken tower
                    GenerateBrokenTower(chunk, x, z, surfaceHeight, random);
                    break;
                case 1: // Collapsed building
                    GenerateCollapsedBuilding(chunk, x, z, surfaceHeight, random);
                    break;
                case 2: // Ancient temple
                    GenerateAncientTemple(chunk, x, z, surfaceHeight, random);
                    break;
            }
        }

        // Helper method to generate a broken tower
        private static void GenerateBrokenTower(VoxelChunk chunk, int x, int z, int surfaceHeight, Random random)
        {
            // Tower parameters
            int towerRadius = 3;
            int towerHeight = random.Next(5, 10); // Shorter than a regular tower
            int breakHeight = random.Next(3, towerHeight - 1); // Height at which the tower is broken

            // Build tower base
            for (int y = 0; y < breakHeight; y++)
            {
                for (int dx = -towerRadius; dx <= towerRadius; dx++)
                {
                    for (int dz = -towerRadius; dz <= towerRadius; dz++)
                    {
                        // Create a circular tower
                        float distance = (float)Math.Sqrt(dx * dx + dz * dz);

                        // Wall thickness
                        float wallThickness = 1.0f;

                        if (distance <= towerRadius && (distance > towerRadius - wallThickness || y == 0))
                        {
                            int nx = x + dx;
                            int nz = z + dz;
                            int ny = surfaceHeight + y + 1;

                            // Check chunk boundaries
                            if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size && ny < chunk.Height)
                            {
                                // Add some randomness to create a broken appearance
                                if (y > breakHeight - 2 && random.NextDouble() < 0.3)
                                {
                                    // Skip some blocks to create holes
                                    continue;
                                }

                                // Stone walls
                                chunk.SetVoxel(nx, ny, nz, VoxelType.Stone);
                            }
                        }
                    }
                }
            }

            // Add rubble around the base
            for (int i = 0; i < 20; i++)
            {
                int rubbleX = x + random.Next(-towerRadius - 2, towerRadius + 3);
                int rubbleZ = z + random.Next(-towerRadius - 2, towerRadius + 3);

                // Check chunk boundaries
                if (rubbleX >= 0 && rubbleX < chunk.Size && rubbleZ >= 0 && rubbleZ < chunk.Size)
                {
                    // Find surface height at rubble position
                    int rubbleSurfaceHeight = -1;
                    for (int y = ChunkHeight - 1; y >= 0; y--)
                    {
                        if (chunk.GetVoxel(rubbleX, y, rubbleZ) != VoxelType.Air &&
                            chunk.GetVoxel(rubbleX, y, rubbleZ) != VoxelType.Water)
                        {
                            rubbleSurfaceHeight = y;
                            break;
                        }
                    }

                    if (rubbleSurfaceHeight >= 0)
                    {
                        // Place stone rubble
                        chunk.SetVoxel(rubbleX, rubbleSurfaceHeight + 1, rubbleZ, VoxelType.Stone);

                        // Sometimes stack rubble
                        if (random.NextDouble() < 0.3 && rubbleSurfaceHeight + 2 < chunk.Height)
                        {
                            chunk.SetVoxel(rubbleX, rubbleSurfaceHeight + 2, rubbleZ, VoxelType.Stone);
                        }
                    }
                }
            }
        }

        // Helper method to generate a collapsed building
        private static void GenerateCollapsedBuilding(VoxelChunk chunk, int x, int z, int surfaceHeight, Random random)
        {
            // Building parameters
            int width = random.Next(5, 8);
            int depth = random.Next(5, 8);

            // Calculate corners
            int startX = x - width / 2;
            int startZ = z - depth / 2;
            int endX = startX + width - 1;
            int endZ = startZ + depth - 1;

            // Build partial walls (collapsed)
            for (int dx = 0; dx <= width; dx++)
            {
                for (int dz = 0; dz <= depth; dz++)
                {
                    // Only build walls (outer perimeter)
                    bool isWall = dx == 0 || dx == width || dz == 0 || dz == depth;

                    if (isWall)
                    {
                        int nx = startX + dx;
                        int nz = startZ + dz;

                        // Check chunk boundaries
                        if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size)
                        {
                            // Find surface height
                            int wallSurfaceHeight = -1;
                            for (int y = ChunkHeight - 1; y >= 0; y--)
                            {
                                if (chunk.GetVoxel(nx, y, nz) != VoxelType.Air &&
                                    chunk.GetVoxel(nx, y, nz) != VoxelType.Water)
                                {
                                    wallSurfaceHeight = y;
                                    break;
                                }
                            }

                            if (wallSurfaceHeight >= 0)
                            {
                                // Determine wall height (random, to create collapsed appearance)
                                int wallHeight = random.Next(0, 4); // 0 means fully collapsed

                                for (int y = 0; y < wallHeight; y++)
                                {
                                    if (wallSurfaceHeight + 1 + y < chunk.Height)
                                    {
                                        // Add some randomness to create a ruined appearance
                                        if (y > 0 && random.NextDouble() < 0.4)
                                        {
                                            // Skip some blocks to create holes
                                            continue;
                                        }

                                        // Stone walls
                                        chunk.SetVoxel(nx, wallSurfaceHeight + 1 + y, nz, VoxelType.Stone);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Add rubble inside and around the building
            for (int i = 0; i < 30; i++)
            {
                int rubbleX = startX + random.Next(-2, width + 3);
                int rubbleZ = startZ + random.Next(-2, depth + 3);

                // Check chunk boundaries
                if (rubbleX >= 0 && rubbleX < chunk.Size && rubbleZ >= 0 && rubbleZ < chunk.Size)
                {
                    // Find surface height at rubble position
                    int rubbleSurfaceHeight = -1;
                    for (int y = ChunkHeight - 1; y >= 0; y--)
                    {
                        if (chunk.GetVoxel(rubbleX, y, rubbleZ) != VoxelType.Air &&
                            chunk.GetVoxel(rubbleX, y, rubbleZ) != VoxelType.Water)
                        {
                            rubbleSurfaceHeight = y;
                            break;
                        }
                    }

                    if (rubbleSurfaceHeight >= 0)
                    {
                        // Place stone or wood rubble
                        VoxelType rubbleType = random.NextDouble() < 0.7 ? VoxelType.Stone : VoxelType.Wood;
                        chunk.SetVoxel(rubbleX, rubbleSurfaceHeight + 1, rubbleZ, rubbleType);

                        // Sometimes stack rubble
                        if (random.NextDouble() < 0.2 && rubbleSurfaceHeight + 2 < chunk.Height)
                        {
                            chunk.SetVoxel(rubbleX, rubbleSurfaceHeight + 2, rubbleZ, rubbleType);
                        }
                    }
                }
            }
        }

        // Helper method to generate an ancient temple
        private static void GenerateAncientTemple(VoxelChunk chunk, int x, int z, int surfaceHeight, Random random)
        {
            // Temple parameters
            int baseSize = 9; // Temple base size (odd number for center alignment)
            int height = 6; // Temple height

            // Build temple base platform
            for (int y = 0; y < 2; y++)
            {
                int currentSize = baseSize - y; // Base gets smaller as it goes up
                int offset = currentSize / 2;

                for (int dx = -offset; dx <= offset; dx++)
                {
                    for (int dz = -offset; dz <= offset; dz++)
                    {
                        int nx = x + dx;
                        int nz = z + dz;
                        int ny = surfaceHeight + y + 1;

                        // Check chunk boundaries
                        if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size && ny < chunk.Height)
                        {
                            // Stone base
                            chunk.SetVoxel(nx, ny, nz, VoxelType.Stone);
                        }
                    }
                }
            }

            // Build temple walls
            int wallSize = baseSize - 2;
            int wallOffset = wallSize / 2;

            for (int y = 0; y < height - 2; y++)
            {
                for (int dx = -wallOffset; dx <= wallOffset; dx++)
                {
                    for (int dz = -wallOffset; dz <= wallOffset; dz++)
                    {
                        // Only build walls (outer perimeter)
                        bool isWall = dx == -wallOffset || dx == wallOffset || dz == -wallOffset || dz == wallOffset;

                        if (isWall)
                        {
                            int nx = x + dx;
                            int nz = z + dz;
                            int ny = surfaceHeight + y + 3; // Start above the base

                            // Check chunk boundaries
                            if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size && ny < chunk.Height)
                            {
                                // Add some randomness to create a ruined appearance
                                if (random.NextDouble() < 0.3)
                                {
                                    // Skip some blocks to create holes
                                    continue;
                                }

                                // Stone walls
                                chunk.SetVoxel(nx, ny, nz, VoxelType.Stone);
                            }
                        }
                    }
                }
            }

            // Add entrance (always on one side)
            int entranceWidth = 3;
            int entranceOffset = entranceWidth / 2;

            for (int dx = -entranceOffset; dx <= entranceOffset; dx++)
            {
                for (int y = 0; y < height - 2; y++)
                {
                    int nx = x + dx;
                    int nz = z + wallOffset; // North side entrance
                    int ny = surfaceHeight + y + 3;

                    // Check chunk boundaries
                    if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size && ny < chunk.Height)
                    {
                        // Create entrance opening (air blocks)
                        chunk.SetVoxel(nx, ny, nz, VoxelType.Air);
                    }
                }
            }

            // Add some columns inside
            int columnPositions = 2;
            for (int i = 0; i < columnPositions; i++)
            {
                for (int j = 0; j < columnPositions; j++)
                {
                    int columnX = x - wallOffset + 2 + i * (wallSize - 3);
                    int columnZ = z - wallOffset + 2 + j * (wallSize - 3);

                    // Skip center position
                    if (i == columnPositions / 2 && j == columnPositions / 2)
                        continue;

                    // Check chunk boundaries
                    if (columnX >= 0 && columnX < chunk.Size && columnZ >= 0 && columnZ < chunk.Size)
                    {
                        // Build column
                        int columnHeight = random.Next(height - 3, height); // Some columns are broken
                        for (int y = 0; y < columnHeight; y++)
                        {
                            int ny = surfaceHeight + y + 3;

                            if (ny < chunk.Height)
                            {
                                // Stone column
                                chunk.SetVoxel(columnX, ny, columnZ, VoxelType.Stone);
                            }
                        }
                    }
                }
            }

            // Add some rubble around the temple
            for (int i = 0; i < 20; i++)
            {
                int rubbleX = x + random.Next(-baseSize, baseSize + 1);
                int rubbleZ = z + random.Next(-baseSize, baseSize + 1);

                // Check chunk boundaries
                if (rubbleX >= 0 && rubbleX < chunk.Size && rubbleZ >= 0 && rubbleZ < chunk.Size)
                {
                    // Find surface height at rubble position
                    int rubbleSurfaceHeight = -1;
                    for (int y = ChunkHeight - 1; y >= 0; y--)
                    {
                        if (chunk.GetVoxel(rubbleX, y, rubbleZ) != VoxelType.Air &&
                            chunk.GetVoxel(rubbleX, y, rubbleZ) != VoxelType.Water)
                        {
                            rubbleSurfaceHeight = y;
                            break;
                        }
                    }

                    if (rubbleSurfaceHeight >= 0)
                    {
                        // Place stone rubble
                        chunk.SetVoxel(rubbleX, rubbleSurfaceHeight + 1, rubbleZ, VoxelType.Stone);

                        // Sometimes stack rubble
                        if (random.NextDouble() < 0.3 && rubbleSurfaceHeight + 2 < chunk.Height)
                        {
                            chunk.SetVoxel(rubbleX, rubbleSurfaceHeight + 2, rubbleZ, VoxelType.Stone);
                        }
                    }
                }
            }
        }
    }
}
