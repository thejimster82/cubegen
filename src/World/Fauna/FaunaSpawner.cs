using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using CubeGen.World.Common;
using CubeGen.World.Generation;

namespace CubeGen.World.Fauna
{
    /// <summary>
    /// Manages fauna spawning on a per-chunk basis
    /// </summary>
    public class FaunaSpawner
    {
        // Singleton instance
        private static FaunaSpawner _instance;
        public static FaunaSpawner Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new FaunaSpawner();
                }
                return _instance;
            }
        }

        // World seed for consistent fauna generation
        private int _seed;

        // Noise for fauna distribution
        private FastNoiseLite _faunaDistributionNoise;

        // Dictionary to track which chunks have fauna spawned
        private ConcurrentDictionary<Vector2I, bool> _chunksWithFauna = new ConcurrentDictionary<Vector2I, bool>();

        // Dictionary to track fauna spawn positions by chunk
        private ConcurrentDictionary<Vector2I, List<FaunaSpawnInfo>> _faunaSpawnPositions =
            new ConcurrentDictionary<Vector2I, List<FaunaSpawnInfo>>();

        // Reference to the BirdManager
        private BirdManager _birdManager;

        // Constants for fauna spawning
        private const float BIRD_SPAWN_PROBABILITY = 0.3f; // Base probability for a chunk to have birds
        private const int MAX_BIRDS_PER_CHUNK = 2; // Maximum number of birds that can spawn in a single chunk

        /// <summary>
        /// Initialize the fauna spawner with the world seed
        /// </summary>
        public void Initialize(int seed, BirdManager birdManager)
        {
            _seed = seed;
            _birdManager = birdManager;

            // Initialize noise for fauna distribution
            _faunaDistributionNoise = new FastNoiseLite();
            _faunaDistributionNoise.Seed = _seed;
            _faunaDistributionNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _faunaDistributionNoise.Frequency = 0.01f; // Low frequency for large-scale patterns

            GD.Print($"FaunaSpawner initialized with seed {_seed}");
        }

        /// <summary>
        /// Determine if fauna should spawn in a chunk and generate spawn positions
        /// </summary>
        public void ProcessChunkForFauna(global::VoxelChunk chunk)
        {
            // Skip if this chunk already has fauna
            if (_chunksWithFauna.ContainsKey(chunk.Position) && _chunksWithFauna[chunk.Position])
            {
                return;
            }

            // Create a deterministic random generator for this chunk
            Random chunkRandom = new Random(_seed + chunk.Position.X * 10000 + chunk.Position.Y);

            // Get biome type for this chunk (use center of chunk for biome determination)
            int centerX = chunk.Position.X * chunk.Size + chunk.Size / 2;
            int centerZ = chunk.Position.Y * chunk.Size + chunk.Size / 2;
            BiomeType biomeType = WorldGenerator.GetBiomeType(centerX, centerZ);

            // Get noise value for this chunk to create natural distribution
            float noiseValue = _faunaDistributionNoise.GetNoise2D(centerX * 0.05f, centerZ * 0.05f);

            // Convert noise from [-1,1] to [0,1] range
            noiseValue = (noiseValue + 1.0f) * 0.5f;

            // Calculate spawn probability based on biome and noise
            float biomeMultiplier = GetBiomeSpawnMultiplier(biomeType);
            float spawnProbability = BIRD_SPAWN_PROBABILITY * biomeMultiplier * noiseValue;

            // Determine if birds should spawn in this chunk
            if (chunkRandom.NextDouble() < spawnProbability)
            {
                // Determine how many birds to spawn (1 to MAX_BIRDS_PER_CHUNK)
                int birdCount = chunkRandom.Next(1, MAX_BIRDS_PER_CHUNK + 1);

                // Generate spawn positions for birds
                List<FaunaSpawnInfo> spawnPositions = GenerateBirdSpawnPositions(
                    chunk, biomeType, birdCount, chunkRandom);

                // Store spawn positions
                _faunaSpawnPositions[chunk.Position] = spawnPositions;

                // Mark chunk as having fauna
                _chunksWithFauna[chunk.Position] = true;

                GD.Print($"Chunk {chunk.Position} will spawn {birdCount} birds");
            }
            else
            {
                // Mark chunk as processed but with no fauna
                _chunksWithFauna[chunk.Position] = false;
            }
        }

        /// <summary>
        /// Generate spawn positions for birds in a chunk
        /// </summary>
        private List<FaunaSpawnInfo> GenerateBirdSpawnPositions(
            global::VoxelChunk chunk, BiomeType biomeType, int count, Random random)
        {
            List<FaunaSpawnInfo> positions = new List<FaunaSpawnInfo>();

            // Calculate world position of chunk
            Vector3 chunkWorldPos = chunk.GetWorldPosition();

            // Try to generate the requested number of positions
            for (int i = 0; i < count; i++)
            {
                // Generate a random position within the chunk
                float x = chunkWorldPos.X + random.Next(0, chunk.Size) * chunk.Scale;
                float z = chunkWorldPos.Z + random.Next(0, chunk.Size) * chunk.Scale;

                // Find the highest point at this position
                int worldX = (int)(x / chunk.Scale);
                int worldZ = (int)(z / chunk.Scale);

                // Get terrain height at this position
                float terrainHeight = FindHighestPoint(worldX, worldZ);

                // Calculate bird height above terrain
                float birdHeight = terrainHeight + 40.0f; // Base height above terrain

                // Add variation based on noise
                float heightVariation = 20.0f;
                float heightNoise = _faunaDistributionNoise.GetNoise2D(x * 0.1f, z * 0.1f);
                birdHeight += heightNoise * heightVariation;

                // Adjust height based on biome (birds fly higher in forests)
                if (biomeType == BiomeType.ForestLands)
                {
                    birdHeight += 20.0f;
                }

                // Ensure minimum height
                birdHeight = Mathf.Max(birdHeight, 60.0f);

                // Create spawn position
                Vector3 position = new Vector3(x, birdHeight, z);

                // Determine bird type based on biome
                BirdType birdType = GetBirdTypeForBiome(biomeType, random);

                // Create spawn info
                FaunaSpawnInfo spawnInfo = new FaunaSpawnInfo(
                    position,
                    FaunaType.Bird,
                    birdType,
                    biomeType);

                positions.Add(spawnInfo);
            }

            return positions;
        }

        /// <summary>
        /// Find the highest point at a world position
        /// </summary>
        private float FindHighestPoint(int worldX, int worldZ)
        {
            // Start from a reasonable height and search downward
            int startY = 100;

            for (int y = startY; y >= 0; y--)
            {
                // Get the voxel type at this position
                VoxelType voxelType = WorldGenerator.GetVoxelTypeAtPosition(worldX, y, worldZ);

                // If we find a non-air voxel, this is the highest point
                if (voxelType != VoxelType.Air && voxelType != VoxelType.Water)
                {
                    return y;
                }
            }

            // If we couldn't find terrain, return a default height
            return 30.0f;
        }

        /// <summary>
        /// Get a multiplier for fauna spawn probability based on biome
        /// </summary>
        private float GetBiomeSpawnMultiplier(BiomeType biomeType)
        {
            switch (biomeType)
            {
                case BiomeType.ForestLands:
                    return 1.5f; // More birds in forests
                case BiomeType.Islands:
                    return 1.2f; // Slightly more birds on islands
                case BiomeType.Desert:
                    return 0.5f; // Fewer birds in deserts
                case BiomeType.Tundra:
                    return 0.3f; // Even fewer birds in tundra
                default:
                    return 1.0f; // Default multiplier
            }
        }

        /// <summary>
        /// Get a bird type appropriate for a biome
        /// </summary>
        private BirdType GetBirdTypeForBiome(BiomeType biomeType, Random random)
        {
            // Base probabilities favor smaller birds
            float smallBirdProb = 0.7f;
            float mediumBirdProb = 0.2f;

            // Adjust probabilities based on biome
            switch (biomeType)
            {
                case BiomeType.ForestLands:
                    // More medium birds in forests
                    smallBirdProb = 0.6f;
                    mediumBirdProb = 0.3f;
                    break;
                case BiomeType.Islands:
                    // More large birds on islands
                    smallBirdProb = 0.5f;
                    mediumBirdProb = 0.3f;
                    break;
            }

            // Determine bird type based on probabilities
            float roll = (float)random.NextDouble();

            if (roll < smallBirdProb)
                return BirdType.Small;
            else if (roll < smallBirdProb + mediumBirdProb)
                return BirdType.Medium;
            else
                return BirdType.Large;
        }

        /// <summary>
        /// Spawn fauna for a chunk that has come into view
        /// </summary>
        public void SpawnFaunaForChunk(Vector2I chunkPosition, BirdManager birdManager)
        {
            // Check if this chunk has fauna spawn positions
            if (_faunaSpawnPositions.TryGetValue(chunkPosition, out List<FaunaSpawnInfo> spawnInfos))
            {
                foreach (FaunaSpawnInfo spawnInfo in spawnInfos)
                {
                    if (spawnInfo.Type == FaunaType.Bird)
                    {
                        // Spawn a bird at this position
                        birdManager.SpawnBirdAtPosition(
                            spawnInfo.Position,
                            spawnInfo.BirdType,
                            spawnInfo.BiomeType);
                    }
                }

                // Remove spawn positions after spawning to avoid duplicates
                _faunaSpawnPositions.TryRemove(chunkPosition, out _);

                GD.Print($"Spawned fauna for chunk {chunkPosition}");
            }
        }

        /// <summary>
        /// Check if a chunk has fauna that needs to be spawned
        /// </summary>
        public bool ChunkHasFauna(Vector2I chunkPosition)
        {
            return _faunaSpawnPositions.ContainsKey(chunkPosition);
        }

        /// <summary>
        /// Clear all fauna data (used when resetting the world)
        /// </summary>
        public void Clear()
        {
            _chunksWithFauna.Clear();
            _faunaSpawnPositions.Clear();
        }
    }

    /// <summary>
    /// Information about a fauna spawn position
    /// </summary>
    public class FaunaSpawnInfo
    {
        public Vector3 Position { get; private set; }
        public FaunaType Type { get; private set; }
        public BirdType BirdType { get; private set; }
        public BiomeType BiomeType { get; private set; }

        public FaunaSpawnInfo(Vector3 position, FaunaType type, BirdType birdType, BiomeType biomeType)
        {
            Position = position;
            Type = type;
            BirdType = birdType;
            BiomeType = biomeType;
        }
    }

    /// <summary>
    /// Types of fauna that can spawn
    /// </summary>
    public enum FaunaType
    {
        Bird,
        // Add more fauna types here as needed
    }
}
