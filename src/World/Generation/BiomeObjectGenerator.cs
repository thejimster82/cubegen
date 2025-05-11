using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
    /// <summary>
    /// Generates biome objects for the world
    /// </summary>
    public static class BiomeObjectGenerator
    {
        // Probability of generating a biome object in a chunk
        private const float BIOME_OBJECT_PROBABILITY = 0.05f; // 5% chance per chunk

        // Noise generator for natural patterns
        private static FastNoiseLite _noise;

        // Initialize noise
        static BiomeObjectGenerator()
        {
            _noise = new FastNoiseLite();
            _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _noise.Frequency = 0.01f;
        }

        /// <summary>
        /// Attempts to generate biome objects for a chunk
        /// </summary>
        /// <param name="chunkPos">Position of the chunk</param>
        /// <param name="chunkSize">Size of the chunk</param>
        /// <param name="seed">Seed for random number generation</param>
        /// <returns>List of generated biome objects</returns>
        public static List<BiomeObject> GenerateBiomeObjectsForChunk(Vector2I chunkPos, int chunkSize, int seed)
        {
            List<BiomeObject> generatedObjects = new List<BiomeObject>();

            // Create a deterministic random number generator for this chunk
            Random random = new Random(seed + chunkPos.X * 10000 + chunkPos.Y);

            // Set the noise seed
            _noise.Seed = seed;

            // Calculate world position of chunk corner
            Vector2I worldPos = new Vector2I(chunkPos.X * chunkSize, chunkPos.Y * chunkSize);

            // Get biome type for this chunk (use center of chunk for biome determination)
            int centerX = worldPos.X + chunkSize / 2;
            int centerZ = worldPos.Y + chunkSize / 2;
            BiomeType biomeType = WorldGenerator.GetBiomeType(centerX, centerZ);

            // Use noise to create more natural distribution
            float noiseValue = _noise.GetNoise2D(chunkPos.X * 0.1f, chunkPos.Y * 0.1f);

            // Convert from [-1,1] to [0,1] range
            noiseValue = (noiseValue + 1.0f) * 0.5f;

            // Adjust probability based on noise and biome type
            float adjustedProbability = BIOME_OBJECT_PROBABILITY * GetBiomeProbabilityMultiplier(biomeType);
            adjustedProbability *= (0.8f + noiseValue * 0.4f); // Vary by ±20% based on noise

            // Check if we should generate a large biome object in this chunk
            if (random.NextDouble() < adjustedProbability)
            {
                // Choose a random position within the chunk for the anchor point
                int offsetX = random.Next(chunkSize / 4, chunkSize * 3 / 4); // Keep away from edges
                int offsetZ = random.Next(chunkSize / 4, chunkSize * 3 / 4);

                // Calculate world position for the anchor point
                int anchorX = worldPos.X + offsetX;
                int anchorZ = worldPos.Y + offsetZ;

                // Determine the height at this position
                int terrainHeight = DetermineTerrainHeight(anchorX, anchorZ, biomeType, seed);

                // Create the anchor point
                Vector3I anchorPoint = new Vector3I(anchorX, terrainHeight, anchorZ);

                // Choose a large biome object type appropriate for this biome
                BiomeObjectType objectType = ChooseLargeBiomeObjectType(biomeType, random);

                // Generate the biome object
                BiomeObject biomeObject = GenerateBiomeObject(objectType, anchorPoint, biomeType, random);

                if (biomeObject != null)
                {
                    generatedObjects.Add(biomeObject);
                }
            }

            // Generate regular biome objects (trees, cacti, etc.)
            // Use a grid-based approach to ensure even distribution
            int gridSize = 8; // Grid cells are 8x8 voxels
            int gridCellsPerChunk = chunkSize / gridSize;

            for (int gridX = 0; gridX < gridCellsPerChunk; gridX++)
            {
                for (int gridZ = 0; gridZ < gridCellsPerChunk; gridZ++)
                {
                    // Add some randomness to the position within the grid cell
                    int offsetX = random.Next(gridSize);
                    int offsetZ = random.Next(gridSize);

                    // Calculate the world position
                    int worldX = worldPos.X + (gridX * gridSize) + offsetX;
                    int worldZ = worldPos.Y + (gridZ * gridSize) + offsetZ;

                    // Determine the biome type at this position
                    BiomeType localBiomeType = WorldGenerator.GetBiomeType(worldX, worldZ);

                    // Determine the probability of generating an object based on biome type
                    float probability = GetRegularObjectProbability(localBiomeType);

                    if (random.NextDouble() < probability)
                    {
                        // Determine the terrain height at this position
                        int terrainHeight = DetermineTerrainHeight(worldX, worldZ, localBiomeType, seed);

                        // Create the anchor point
                        Vector3I anchorPoint = new Vector3I(worldX, terrainHeight, worldZ);

                        // Choose a regular biome object type appropriate for the biome
                        BiomeObjectType objectType = ChooseRegularBiomeObjectType(localBiomeType, random);

                        // Generate the biome object
                        BiomeObject biomeObject = GenerateBiomeObject(objectType, anchorPoint, localBiomeType, random);

                        if (biomeObject != null)
                        {
                            generatedObjects.Add(biomeObject);
                        }
                    }
                }
            }

            return generatedObjects;
        }

        /// <summary>
        /// Gets the probability of generating a regular biome object based on biome type
        /// </summary>
        /// <param name="biomeType">Biome type</param>
        /// <returns>Probability (0-1)</returns>
        private static float GetRegularObjectProbability(BiomeType biomeType)
        {
            switch (biomeType)
            {
                case BiomeType.ForestLands:
                    return 0.3f; // 30% chance per grid cell
                case BiomeType.Desert:
                    return 0.15f; // 15% chance per grid cell
                case BiomeType.Tundra:
                    return 0.2f; // 20% chance per grid cell
                case BiomeType.Islands:
                    return 0.25f; // 25% chance per grid cell
                default:
                    return 0.1f; // 10% chance per grid cell
            }
        }

        /// <summary>
        /// Generates a biome object of the specified type
        /// </summary>
        /// <param name="objectType">Type of biome object to generate</param>
        /// <param name="anchorPoint">Anchor point for the object</param>
        /// <param name="biomeType">Biome type</param>
        /// <param name="random">Random number generator</param>
        /// <returns>Generated biome object</returns>
        public static BiomeObject GenerateBiomeObject(BiomeObjectType objectType, Vector3I anchorPoint, BiomeType biomeType, Random random)
        {
            BiomeObject biomeObject = new BiomeObject(objectType, anchorPoint, biomeType);

            switch (objectType)
            {
                // Large biome objects
                case BiomeObjectType.LargeTree:
                    GenerateLargeTree(biomeObject, random);
                    break;
                case BiomeObjectType.RockFormation:
                    GenerateRockFormation(biomeObject, random);
                    break;
                case BiomeObjectType.Ruins:
                    GenerateRuins(biomeObject, random);
                    break;
                case BiomeObjectType.Temple:
                    GenerateTemple(biomeObject, random);
                    break;
                case BiomeObjectType.Volcano:
                    GenerateVolcano(biomeObject, random);
                    break;
                case BiomeObjectType.Lake:
                    GenerateLake(biomeObject, random);
                    break;
                case BiomeObjectType.Mountain:
                    GenerateMountain(biomeObject, random);
                    break;

                // Regular biome objects
                case BiomeObjectType.Tree:
                    GenerateTree(biomeObject, random);
                    break;
                case BiomeObjectType.Cactus:
                    GenerateCactus(biomeObject, random);
                    break;
                case BiomeObjectType.SnowTree:
                    GenerateSnowTree(biomeObject, random);
                    break;
                case BiomeObjectType.PalmTree:
                    GeneratePalmTree(biomeObject, random);
                    break;
                case BiomeObjectType.Bush:
                    GenerateBush(biomeObject, random);
                    break;
                case BiomeObjectType.Boulder:
                    GenerateBoulder(biomeObject, random);
                    break;
                case BiomeObjectType.RockSpire:
                    GenerateRockSpire(biomeObject, random);
                    break;
                case BiomeObjectType.IceFormation:
                    GenerateIceFormation(biomeObject, random);
                    break;

                default:
                    // Unknown object type
                    GD.PrintErr($"Unknown biome object type: {objectType}");
                    return null;
            }

            return biomeObject;
        }

        /// <summary>
        /// Determines the terrain height at a world position
        /// </summary>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldZ">World Z coordinate</param>
        /// <param name="biomeType">Biome type</param>
        /// <param name="seed">Seed for random number generation</param>
        /// <returns>Terrain height</returns>
        private static int DetermineTerrainHeight(int worldX, int worldZ, BiomeType biomeType, int seed)
        {
            // Create a temporary noise instance for biome-specific noise settings
            FastNoiseLite biomeNoise = new FastNoiseLite();
            biomeNoise.Seed = seed;

            // Apply a consistent base height for all biomes
            float baseHeight = 0.2f;
            float noiseContribution = 0.4f;

            // Define water level height in voxels
            int chunkHeight = 128; // Default chunk height
            float waterLevel = 0.18f; // Default water level as a fraction of chunk height
            int waterLevelHeight = Mathf.FloorToInt(waterLevel * chunkHeight);

            // Set biome-specific noise characteristics
            switch (biomeType)
            {
                case BiomeType.Desert:
                    biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
                    biomeNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
                    biomeNoise.Frequency = 0.01f;
                    biomeNoise.FractalOctaves = 2;
                    biomeNoise.FractalLacunarity = 2.0f;
                    biomeNoise.FractalGain = 0.6f;
                    break;

                case BiomeType.Tundra:
                    biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
                    biomeNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
                    biomeNoise.Frequency = 0.012f;
                    biomeNoise.FractalOctaves = 2;
                    biomeNoise.FractalLacunarity = 1.8f;
                    biomeNoise.FractalGain = 0.5f;
                    break;

                case BiomeType.Islands:
                    biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
                    biomeNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
                    biomeNoise.Frequency = 0.018f;
                    biomeNoise.FractalOctaves = 2;
                    biomeNoise.FractalLacunarity = 1.8f;
                    biomeNoise.FractalGain = 0.5f;
                    break;

                case BiomeType.ForestLands:
                default:
                    biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
                    biomeNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
                    biomeNoise.Frequency = 0.012f;
                    biomeNoise.FractalOctaves = 3;
                    biomeNoise.FractalLacunarity = 2.0f;
                    biomeNoise.FractalGain = 0.6f;
                    break;
            }

            // Get noise value with biome-specific settings
            float heightNoise = biomeNoise.GetNoise2D(worldX, worldZ);

            // Convert noise from [-1, 1] to [0, 1]
            heightNoise = (heightNoise + 1f) * 0.5f;

            // Special handling for Islands biome
            if (biomeType == BiomeType.Islands)
            {
                // Create islands by thresholding the noise
                float islandThreshold = 0.55f;

                if (heightNoise >= islandThreshold)
                {
                    // Above threshold - scale the remaining range to create island terrain
                    float scaledNoise = ((heightNoise - islandThreshold) / (1.0f - islandThreshold));
                    scaledNoise = scaledNoise * scaledNoise * 2.0f;
                    scaledNoise = Mathf.Min(scaledNoise, 1.0f);
                    heightNoise = baseHeight + scaledNoise * noiseContribution;
                }
                else
                {
                    // Below threshold - generate normal underwater terrain
                    heightNoise = heightNoise * 0.3f;
                }
            }
            else
            {
                // Standard terrain generation for other biomes
                heightNoise = baseHeight + (heightNoise * noiseContribution);
            }

            // Convert to actual height value
            int height = Mathf.FloorToInt(heightNoise * chunkHeight);

            return height;
        }

        /// <summary>
        /// Generates a large tree biome object
        /// </summary>
        private static void GenerateLargeTree(BiomeObject biomeObject, Random random)
        {
            // Tree parameters
            int trunkHeight = random.Next(20, 35); // Taller than regular trees
            int trunkThickness = random.Next(2, 4); // Thicker trunk
            int leafRadius = random.Next(10, 15); // Larger leaf radius
            int leafHeight = random.Next(15, 20); // Taller leaf canopy

            // Generate trunk
            for (int y = 0; y < trunkHeight; y++)
            {
                // Make the trunk thicker at the base, gradually tapering
                int currentThickness = Mathf.Max(1, trunkThickness - (y * trunkThickness / trunkHeight));

                for (int dx = -currentThickness; dx <= currentThickness; dx++)
                {
                    for (int dz = -currentThickness; dz <= currentThickness; dz++)
                    {
                        // Create a rounded trunk shape
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);
                        if (distance <= currentThickness + 0.5f)
                        {
                            Vector3I pos = new Vector3I(dx, y, dz);
                            biomeObject.SetVoxel(pos, VoxelType.Wood);
                        }
                    }
                }
            }

            // Generate branches
            int numBranches = random.Next(4, 8);
            for (int i = 0; i < numBranches; i++)
            {
                // Branch parameters
                int branchStartHeight = random.Next(trunkHeight / 3, trunkHeight * 2 / 3);
                int branchLength = random.Next(5, 10);
                float branchAngle = (float)i / numBranches * 2 * Mathf.Pi;

                // Calculate branch direction
                Vector3 branchDir = new Vector3(
                    Mathf.Cos(branchAngle),
                    0.3f, // Slight upward angle
                    Mathf.Sin(branchAngle)
                ).Normalized();

                // Generate branch voxels
                for (int j = 0; j < branchLength; j++)
                {
                    Vector3I pos = new Vector3I(
                        Mathf.RoundToInt(branchDir.X * j),
                        branchStartHeight + Mathf.RoundToInt(branchDir.Y * j),
                        Mathf.RoundToInt(branchDir.Z * j)
                    );

                    biomeObject.SetVoxel(pos, VoxelType.Wood);

                    // Add some thickness to the branch
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                if (dx == 0 && dy == 0 && dz == 0) continue;

                                // Only add thickness near the trunk
                                if (j < branchLength / 2 && random.NextDouble() < 0.5f)
                                {
                                    Vector3I thicknessPos = new Vector3I(
                                        pos.X + dx,
                                        pos.Y + dy,
                                        pos.Z + dz
                                    );

                                    biomeObject.SetVoxel(thicknessPos, VoxelType.Wood);
                                }
                            }
                        }
                    }

                    // Add small leaf clusters along the branch
                    if (j > branchLength / 2 && random.NextDouble() < 0.7f)
                    {
                        int smallLeafRadius = random.Next(2, 4);
                        for (int dx = -smallLeafRadius; dx <= smallLeafRadius; dx++)
                        {
                            for (int dy = -smallLeafRadius; dy <= smallLeafRadius; dy++)
                            {
                                for (int dz = -smallLeafRadius; dz <= smallLeafRadius; dz++)
                                {
                                    float distance = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                                    if (distance <= smallLeafRadius)
                                    {
                                        Vector3I leafPos = new Vector3I(
                                            pos.X + dx,
                                            pos.Y + dy,
                                            pos.Z + dz
                                        );

                                        // Don't overwrite trunk
                                        if (biomeObject.GetVoxel(leafPos) != VoxelType.Wood)
                                        {
                                            biomeObject.SetVoxel(leafPos, VoxelType.Leaves);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Generate leaf canopy
            int leafStartHeight = trunkHeight - leafHeight / 2;

            for (int y = 0; y < leafHeight; y++)
            {
                // Calculate the radius at this height (ellipsoid shape)
                float heightFactor = 1.0f - Math.Abs((y - leafHeight / 2.0f) / (leafHeight / 2.0f));
                int currentRadius = (int)Math.Ceiling(leafRadius * heightFactor);

                for (int dx = -currentRadius; dx <= currentRadius; dx++)
                {
                    for (int dz = -currentRadius; dz <= currentRadius; dz++)
                    {
                        // Create a rounded shape
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);

                        // Add some noise to the edge
                        float edgeNoise = (float)random.NextDouble() * 0.8f;
                        float effectiveRadius = currentRadius + edgeNoise;

                        if (distance <= effectiveRadius)
                        {
                            Vector3I pos = new Vector3I(dx, leafStartHeight + y, dz);

                            // Don't overwrite trunk
                            if (biomeObject.GetVoxel(pos) != VoxelType.Wood)
                            {
                                biomeObject.SetVoxel(pos, VoxelType.Leaves);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generates a rock formation biome object
        /// </summary>
        private static void GenerateRockFormation(BiomeObject biomeObject, Random random)
        {
            // Rock formation parameters
            int baseRadius = random.Next(6, 12);
            int height = random.Next(10, 20);

            // Generate a rounded rock formation
            for (int y = 0; y < height; y++)
            {
                // Rocks get smaller as they go up
                float heightFactor = 1.0f - (y / (float)height);
                int currentRadius = (int)Math.Ceiling(baseRadius * heightFactor);

                for (int dx = -currentRadius; dx <= currentRadius; dx++)
                {
                    for (int dz = -currentRadius; dz <= currentRadius; dz++)
                    {
                        // Create a rounded shape
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);

                        // Add some noise to the edge
                        float edgeNoise = (float)random.NextDouble() * 0.5f;
                        float effectiveRadius = currentRadius - edgeNoise;

                        if (distance <= effectiveRadius)
                        {
                            Vector3I pos = new Vector3I(dx, y, dz);

                            // Add some randomness to make rocks less uniform
                            if (distance > effectiveRadius - 0.8f && random.NextDouble() < 0.4f)
                            {
                                // Skip some edge blocks randomly
                                continue;
                            }

                            biomeObject.SetVoxel(pos, VoxelType.Stone);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generates a regular tree biome object
        /// </summary>
        private static void GenerateTree(BiomeObject biomeObject, Random random)
        {
            // Tree parameters
            int trunkHeight = random.Next(12, 24);
            int trunkThickness = random.Next(1, 3);
            int leafRadius = random.Next(6, 9);
            int leafHeight = random.Next(10, 11);
            int trunkShiftX = random.Next(-6, 6);
            int trunkShiftZ = random.Next(-6, 6);

            // Generate trunk
            for (int y = 0; y < trunkHeight; y++)
            {
                // Make the trunk thicker at the base, gradually tapering
                int currentThickness = (y < 4) ? 2 : 1;

                for (int dx = -currentThickness; dx <= currentThickness; dx++)
                {
                    for (int dz = -currentThickness; dz <= currentThickness; dz++)
                    {
                        // Create a rounded trunk shape
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);
                        if (distance <= currentThickness + 0.5f)
                        {
                            // Calculate trunk position with shift
                            float shiftFactorX = (float)y / trunkHeight;
                            float shiftFactorZ = (float)y / trunkHeight;
                            int shiftedX = dx + (int)(trunkShiftX * shiftFactorX);
                            int shiftedZ = dz + (int)(trunkShiftZ * shiftFactorZ);

                            Vector3I pos = new Vector3I(shiftedX, y, shiftedZ);
                            biomeObject.SetVoxel(pos, VoxelType.Wood);
                        }
                    }
                }
            }

            // Generate leaf canopy
            int leafStartHeight = trunkHeight - leafHeight / 2;

            for (int y = 0; y < leafHeight; y++)
            {
                // Calculate the radius at this height (ellipsoid shape)
                float heightFactor = 1.0f - Math.Abs((y - leafHeight / 2.0f) / (leafHeight / 2.0f));
                int currentRadius = (int)Math.Ceiling(leafRadius * heightFactor);

                // Add some randomness to the leaf shape
                float randomFactor = 0.8f + (float)random.NextDouble() * 0.4f;
                currentRadius = (int)Math.Ceiling(currentRadius * randomFactor);

                // Calculate trunk shift at this height
                float shiftFactorX = (float)(leafStartHeight + y) / trunkHeight;
                float shiftFactorZ = (float)(leafStartHeight + y) / trunkHeight;
                int shiftedCenterX = (int)(trunkShiftX * shiftFactorX);
                int shiftedCenterZ = (int)(trunkShiftZ * shiftFactorZ);

                for (int dx = -currentRadius; dx <= currentRadius; dx++)
                {
                    for (int dz = -currentRadius; dz <= currentRadius; dz++)
                    {
                        // Create a rounded shape
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);

                        // Add some noise to the edge
                        float edgeNoise = (float)random.NextDouble() * 0.8f;
                        float effectiveRadius = currentRadius + edgeNoise;

                        if (distance <= effectiveRadius)
                        {
                            Vector3I pos = new Vector3I(dx + shiftedCenterX, leafStartHeight + y, dz + shiftedCenterZ);

                            // Don't overwrite trunk
                            if (biomeObject.GetVoxel(pos) != VoxelType.Wood)
                            {
                                biomeObject.SetVoxel(pos, VoxelType.Leaves);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generates a cactus biome object
        /// </summary>
        private static void GenerateCactus(BiomeObject biomeObject, Random random)
        {
            // Cactus parameters
            int mainHeight = random.Next(8, 16);
            bool hasArms = random.NextDouble() < 0.7f;

            // Generate main trunk
            for (int y = 0; y < mainHeight; y++)
            {
                biomeObject.SetVoxel(new Vector3I(0, y, 0), VoxelType.Cactus);
            }

            // Generate arms if needed
            if (hasArms)
            {
                // Number of arms (1-2)
                int numArms = random.Next(1, 3);

                for (int i = 0; i < numArms; i++)
                {
                    // Arm parameters
                    int armHeight = mainHeight / 2 + random.Next(-2, 3);
                    int armLength = random.Next(3, 6);
                    int armDirection = random.Next(0, 4); // 0=north, 1=east, 2=south, 3=west

                    // Direction vectors
                    int dx = 0, dz = 0;
                    switch (armDirection)
                    {
                        case 0: dz = -1; break; // North
                        case 1: dx = 1; break;  // East
                        case 2: dz = 1; break;  // South
                        case 3: dx = -1; break; // West
                    }

                    // Generate arm
                    for (int j = 1; j <= armLength; j++)
                    {
                        biomeObject.SetVoxel(new Vector3I(dx * j, armHeight, dz * j), VoxelType.Cactus);

                        // Add vertical part of the arm
                        for (int y = 1; y <= random.Next(2, 5); y++)
                        {
                            biomeObject.SetVoxel(new Vector3I(dx * j, armHeight + y, dz * j), VoxelType.Cactus);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generates a snow tree biome object
        /// </summary>
        private static void GenerateSnowTree(BiomeObject biomeObject, Random random)
        {
            // Snow tree parameters
            int trunkHeight = random.Next(10, 20);
            int leafLayers = random.Next(3, 6);

            // Generate trunk
            for (int y = 0; y < trunkHeight; y++)
            {
                biomeObject.SetVoxel(new Vector3I(0, y, 0), VoxelType.Wood);
            }

            // Generate conical leaf layers
            for (int layer = 0; layer < leafLayers; layer++)
            {
                int layerY = trunkHeight - (leafLayers - layer) * 3;
                if (layerY < 0) continue;

                int layerRadius = leafLayers - layer + 1;

                for (int y = 0; y < 3; y++)
                {
                    int currentRadius = layerRadius - (y / 2);

                    for (int dx = -currentRadius; dx <= currentRadius; dx++)
                    {
                        for (int dz = -currentRadius; dz <= currentRadius; dz++)
                        {
                            float distance = Mathf.Sqrt(dx * dx + dz * dz);
                            if (distance <= currentRadius + 0.5f)
                            {
                                Vector3I pos = new Vector3I(dx, layerY + y, dz);

                                // Don't overwrite trunk
                                if (biomeObject.GetVoxel(pos) != VoxelType.Wood)
                                {
                                    // Use snow-covered leaves
                                    biomeObject.SetVoxel(pos, VoxelType.SnowLeaves);
                                }
                            }
                        }
                    }
                }
            }

            // Add snow on top
            int topY = trunkHeight + 1;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    biomeObject.SetVoxel(new Vector3I(dx, topY, dz), VoxelType.Snow);
                }
            }
        }

        /// <summary>
        /// Generates a palm tree biome object
        /// </summary>
        private static void GeneratePalmTree(BiomeObject biomeObject, Random random)
        {
            // Palm tree parameters
            int trunkHeight = random.Next(12, 20);
            int numFronds = random.Next(5, 8);
            int frondLength = random.Next(6, 10);

            // Generate curved trunk
            float xOffset = 0;
            float zOffset = 0;
            float curveFactor = (float)random.NextDouble() * 0.3f + 0.1f;
            float curveDirection = (float)(random.NextDouble() * Math.PI * 2);

            for (int y = 0; y < trunkHeight; y++)
            {
                // Calculate curve
                float curveMagnitude = y * curveFactor;
                xOffset = curveMagnitude * Mathf.Cos(curveDirection);
                zOffset = curveMagnitude * Mathf.Sin(curveDirection);

                int trunkX = (int)xOffset;
                int trunkZ = (int)zOffset;

                biomeObject.SetVoxel(new Vector3I(trunkX, y, trunkZ), VoxelType.Wood);
            }

            // Generate palm fronds
            int topX = (int)xOffset;
            int topZ = (int)zOffset;
            int topY = trunkHeight - 1;

            for (int i = 0; i < numFronds; i++)
            {
                float angle = (float)i / numFronds * Mathf.Pi * 2;
                float dirX = Mathf.Cos(angle);
                float dirZ = Mathf.Sin(angle);

                for (int j = 0; j < frondLength; j++)
                {
                    int frondX = topX + (int)(dirX * j);
                    int frondZ = topZ + (int)(dirZ * j);
                    int frondY = topY + (j < 2 ? 0 : -1);

                    biomeObject.SetVoxel(new Vector3I(frondX, frondY, frondZ), VoxelType.Leaves);

                    // Add side leaves for wider fronds
                    if (j > 1 && j < frondLength - 1)
                    {
                        int sideX1 = frondX + (int)dirZ;
                        int sideZ1 = frondZ - (int)dirX;
                        int sideX2 = frondX - (int)dirZ;
                        int sideZ2 = frondZ + (int)dirX;

                        biomeObject.SetVoxel(new Vector3I(sideX1, frondY, sideZ1), VoxelType.Leaves);
                        biomeObject.SetVoxel(new Vector3I(sideX2, frondY, sideZ2), VoxelType.Leaves);
                    }
                }
            }

            // Add coconuts
            int numCoconuts = random.Next(0, 4);
            for (int i = 0; i < numCoconuts; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.Pi * 2;
                int coconutX = topX + (int)(Mathf.Cos(angle) * 1.5f);
                int coconutZ = topZ + (int)(Mathf.Sin(angle) * 1.5f);

                biomeObject.SetVoxel(new Vector3I(coconutX, topY - 1, coconutZ), VoxelType.Coconut);
            }
        }

        /// <summary>
        /// Generates a bush biome object
        /// </summary>
        private static void GenerateBush(BiomeObject biomeObject, Random random)
        {
            // Bush parameters
            int radius = random.Next(2, 5);
            int height = random.Next(2, 4);

            // Generate bush (sphere of leaves)
            for (int y = 0; y < height; y++)
            {
                // Calculate radius at this height
                float heightFactor = 1.0f - Math.Abs((y - height / 2.0f) / (height / 2.0f));
                int currentRadius = (int)Math.Ceiling(radius * heightFactor);

                for (int dx = -currentRadius; dx <= currentRadius; dx++)
                {
                    for (int dz = -currentRadius; dz <= currentRadius; dz++)
                    {
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);
                        if (distance <= currentRadius + 0.5f)
                        {
                            biomeObject.SetVoxel(new Vector3I(dx, y, dz), VoxelType.Leaves);
                        }
                    }
                }
            }

            // Add some wood blocks inside
            biomeObject.SetVoxel(new Vector3I(0, 0, 0), VoxelType.Wood);
            if (height > 2)
            {
                biomeObject.SetVoxel(new Vector3I(0, 1, 0), VoxelType.Wood);
            }
        }

        /// <summary>
        /// Generates a boulder biome object
        /// </summary>
        private static void GenerateBoulder(BiomeObject biomeObject, Random random)
        {
            // Boulder parameters
            int radius = random.Next(3, 7);
            int height = random.Next(4, 8);

            // Generate boulder (sphere of stone)
            for (int y = 0; y < height; y++)
            {
                // Calculate radius at this height
                float heightFactor = 1.0f - Math.Abs((y - height / 2.0f) / (height / 2.0f));
                int currentRadius = (int)Math.Ceiling(radius * heightFactor);

                for (int dx = -currentRadius; dx <= currentRadius; dx++)
                {
                    for (int dz = -currentRadius; dz <= currentRadius; dz++)
                    {
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);
                        if (distance <= currentRadius + 0.5f)
                        {
                            // Add some randomness to the edge
                            if (distance > currentRadius - 0.8f && random.NextDouble() < 0.3f)
                            {
                                continue;
                            }

                            biomeObject.SetVoxel(new Vector3I(dx, y, dz), VoxelType.Stone);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generates a rock spire biome object
        /// </summary>
        private static void GenerateRockSpire(BiomeObject biomeObject, Random random)
        {
            // Rock spire parameters
            int baseRadius = random.Next(3, 6);
            int height = random.Next(15, 25);

            // Generate rock spire (tall, thin rock formation)
            for (int y = 0; y < height; y++)
            {
                // Spires get much thinner as they go up
                float heightFactor = 1.0f - (y / (float)height) * 0.8f;
                int currentRadius = (int)Math.Ceiling(baseRadius * heightFactor);

                // Ensure we have at least a 1x1 column at the top
                currentRadius = Math.Max(1, currentRadius);

                for (int dx = -currentRadius; dx <= currentRadius; dx++)
                {
                    for (int dz = -currentRadius; dz <= currentRadius; dz++)
                    {
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);
                        if (distance <= currentRadius + 0.5f)
                        {
                            // Add some randomness to the edge
                            if (distance > currentRadius - 0.8f && random.NextDouble() < 0.4f)
                            {
                                continue;
                            }

                            biomeObject.SetVoxel(new Vector3I(dx, y, dz), VoxelType.Stone);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generates an ice formation biome object
        /// </summary>
        private static void GenerateIceFormation(BiomeObject biomeObject, Random random)
        {
            // Ice formation parameters
            int baseRadius = random.Next(3, 6);
            int height = random.Next(10, 20);

            // Generate ice formation
            for (int y = 0; y < height; y++)
            {
                // Ice formations get thinner as they go up
                float heightFactor = 1.0f - (y / (float)height) * 0.7f;
                int currentRadius = (int)Math.Ceiling(baseRadius * heightFactor);

                for (int dx = -currentRadius; dx <= currentRadius; dx++)
                {
                    for (int dz = -currentRadius; dz <= currentRadius; dz++)
                    {
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);
                        if (distance <= currentRadius + 0.5f)
                        {
                            // Add some randomness to the edge
                            if (distance > currentRadius - 0.8f && random.NextDouble() < 0.5f)
                            {
                                continue;
                            }

                            biomeObject.SetVoxel(new Vector3I(dx, y, dz), VoxelType.Ice);
                        }
                    }
                }
            }

            // Add snow on top
            int topY = height;
            int topRadius = (int)Math.Ceiling(baseRadius * 0.3f);

            for (int dx = -topRadius; dx <= topRadius; dx++)
            {
                for (int dz = -topRadius; dz <= topRadius; dz++)
                {
                    float distance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distance <= topRadius + 0.5f)
                    {
                        biomeObject.SetVoxel(new Vector3I(dx, topY, dz), VoxelType.Snow);
                    }
                }
            }
        }

        // Placeholder implementations for other biome object types
        private static void GenerateRuins(BiomeObject biomeObject, Random random) { /* Placeholder */ }
        private static void GenerateTemple(BiomeObject biomeObject, Random random) { /* Placeholder */ }
        private static void GenerateVolcano(BiomeObject biomeObject, Random random) { /* Placeholder */ }
        private static void GenerateLake(BiomeObject biomeObject, Random random) { /* Placeholder */ }
        private static void GenerateMountain(BiomeObject biomeObject, Random random) { /* Placeholder */ }

        /// <summary>
        /// Gets the probability multiplier for a biome type
        /// </summary>
        /// <param name="biomeType">Biome type</param>
        /// <returns>Probability multiplier</returns>
        private static float GetBiomeProbabilityMultiplier(BiomeType biomeType)
        {
            switch (biomeType)
            {
                case BiomeType.Desert:
                    return 0.8f; // Less objects in desert
                case BiomeType.Tundra:
                    return 1.2f; // More objects in tundra
                case BiomeType.Islands:
                    return 1.5f; // More objects in islands
                case BiomeType.ForestLands:
                    return 1.0f; // Normal amount in forest lands
                default:
                    return 1.0f;
            }
        }

        /// <summary>
        /// Chooses a large biome object type appropriate for a biome
        /// </summary>
        /// <param name="biomeType">Biome type</param>
        /// <param name="random">Random number generator</param>
        /// <returns>Chosen biome object type</returns>
        private static BiomeObjectType ChooseLargeBiomeObjectType(BiomeType biomeType, Random random)
        {
            switch (biomeType)
            {
                case BiomeType.Desert:
                    // Desert biome objects: rock formations, ruins, temples
                    float desertRand = (float)random.NextDouble();
                    if (desertRand < 0.5f)
                        return BiomeObjectType.RockFormation;
                    else if (desertRand < 0.8f)
                        return BiomeObjectType.Ruins;
                    else
                        return BiomeObjectType.Temple;

                case BiomeType.Tundra:
                    // Tundra biome objects: rock formations, mountains
                    return random.NextDouble() < 0.7f ? BiomeObjectType.RockFormation : BiomeObjectType.Mountain;

                case BiomeType.Islands:
                    // Islands biome objects: large trees, lakes, temples
                    float islandRand = (float)random.NextDouble();
                    if (islandRand < 0.6f)
                        return BiomeObjectType.LargeTree;
                    else if (islandRand < 0.9f)
                        return BiomeObjectType.Lake;
                    else
                        return BiomeObjectType.Temple;

                case BiomeType.ForestLands:
                default:
                    // Forest lands biome objects: large trees, rock formations, ruins
                    float forestRand = (float)random.NextDouble();
                    if (forestRand < 0.7f)
                        return BiomeObjectType.LargeTree;
                    else if (forestRand < 0.9f)
                        return BiomeObjectType.RockFormation;
                    else
                        return BiomeObjectType.Ruins;
            }
        }

        /// <summary>
        /// Chooses a regular biome object type appropriate for a biome
        /// </summary>
        /// <param name="biomeType">Biome type</param>
        /// <param name="random">Random number generator</param>
        /// <returns>Chosen biome object type</returns>
        private static BiomeObjectType ChooseRegularBiomeObjectType(BiomeType biomeType, Random random)
        {
            switch (biomeType)
            {
                case BiomeType.Desert:
                    // Desert regular objects: cacti, rock spires, boulders
                    float desertRand = (float)random.NextDouble();
                    if (desertRand < 0.6f)
                        return BiomeObjectType.Cactus;
                    else if (desertRand < 0.8f)
                        return BiomeObjectType.RockSpire;
                    else
                        return BiomeObjectType.Boulder;

                case BiomeType.Tundra:
                    // Tundra regular objects: snow trees, ice formations, boulders
                    float tundraRand = (float)random.NextDouble();
                    if (tundraRand < 0.6f)
                        return BiomeObjectType.SnowTree;
                    else if (tundraRand < 0.8f)
                        return BiomeObjectType.IceFormation;
                    else
                        return BiomeObjectType.Boulder;

                case BiomeType.Islands:
                    // Islands regular objects: palm trees, bushes
                    return random.NextDouble() < 0.7f ? BiomeObjectType.PalmTree : BiomeObjectType.Bush;

                case BiomeType.ForestLands:
                default:
                    // Forest lands regular objects: trees, bushes, boulders
                    float forestRand = (float)random.NextDouble();
                    if (forestRand < 0.7f)
                        return BiomeObjectType.Tree;
                    else if (forestRand < 0.9f)
                        return BiomeObjectType.Bush;
                    else
                        return BiomeObjectType.Boulder;
            }
        }
    }
}
