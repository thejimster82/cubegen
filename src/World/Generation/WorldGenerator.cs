using Godot;
using System;

public partial class WorldGenerator : Node3D
{
    [Export] public int Seed { get; set; } = 0;
    [Export] public Vector2I WorldSize { get; set; } = new Vector2I(16, 16); // Size in chunks
    [Export] public int ChunkSize { get; set; } = 16; // Size of each chunk in voxels
    [Export] public int ChunkHeight { get; set; } = 128; // Maximum height of the world
    [Export] public float VoxelScale { get; set; } = 1.0f; // Scale of each voxel

    private FastNoiseLite _terrainNoise;
    private FastNoiseLite _biomeNoise;
    private ChunkManager _chunkManager;

    public override void _Ready()
    {
        InitializeNoise();
        _chunkManager = GetNode<ChunkManager>("ChunkManager");

        if (_chunkManager != null)
        {
            GD.Print("ChunkManager found, initializing...");
            _chunkManager.Initialize(ChunkSize, ChunkHeight);
            GenerateInitialChunks();
        }
        else
        {
            GD.PrintErr("ChunkManager not found!");
        }
    }

    private void InitializeNoise()
    {
        // Initialize terrain noise
        _terrainNoise = new FastNoiseLite();
        _terrainNoise.Seed = Seed;
        _terrainNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _terrainNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _terrainNoise.Frequency = 0.01f;
        _terrainNoise.FractalOctaves = 4;

        // Initialize biome noise (different settings for variety)
        _biomeNoise = new FastNoiseLite();
        _biomeNoise.Seed = Seed + 1000; // Different seed for biome variation
        _biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _biomeNoise.Frequency = 0.005f; // Larger scale for biomes

        // Initialize static noise for use by other classes
        InitializeStaticNoise(Seed);
    }

    private void GenerateInitialChunks()
    {
        // Generate chunks around origin
        int viewDistance = 3; // Number of chunks to generate in each direction

        GD.Print($"Generating initial chunks with view distance: {viewDistance}");

        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                Vector2I chunkPos = new Vector2I(x, z);
                GD.Print($"Generating initial chunk at: {chunkPos}");
                GenerateChunk(chunkPos);
            }
        }

        GD.Print("Initial chunk generation complete");
    }

    public void GenerateChunk(Vector2I chunkPos)
    {
        if (_chunkManager == null) return;

        // Create chunk data
        VoxelChunk chunk = new VoxelChunk(ChunkSize, ChunkHeight, chunkPos, VoxelScale);

        // Generate terrain for the chunk
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                // Get world coordinates
                int worldX = chunkPos.X * ChunkSize + x;
                int worldZ = chunkPos.Y * ChunkSize + z;

                // Get biome type based on noise
                BiomeType biomeType = GetBiomeTypeForChunk(worldX, worldZ);

                // Generate terrain height based on noise
                int terrainHeight = GenerateTerrainHeight(worldX, worldZ, biomeType);

                // Fill voxels from bottom to terrain height
                for (int y = 0; y < terrainHeight && y < ChunkHeight; y++)
                {
                    VoxelType voxelType = DetermineVoxelType(y, terrainHeight, biomeType);
                    chunk.SetVoxel(x, y, z, voxelType);
                }
            }
        }

        // Add objects like trees based on biome
        AddBiomeObjects(chunk, chunkPos, ChunkSize);

        // Send chunk to chunk manager for mesh generation
        _chunkManager.AddChunk(chunk);
    }

    // Static biome noise for use by other classes
    private static FastNoiseLite _staticBiomeNoise;

    // Initialize static noise
    private static void InitializeStaticNoise(int seed)
    {
        if (_staticBiomeNoise == null)
        {
            _staticBiomeNoise = new FastNoiseLite();
            _staticBiomeNoise.Seed = seed + 1000; // Different seed for biome variation
            _staticBiomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _staticBiomeNoise.Frequency = 0.005f; // Larger scale for biomes
        }
    }

    // Get biome type for a world position - instance method
    private BiomeType GetBiomeTypeForChunk(int worldX, int worldZ)
    {
        float biomeValue = _biomeNoise.GetNoise2D(worldX, worldZ);
        return GetBiomeTypeFromNoise(biomeValue);
    }

    // Get biome type for a world position - static method for use by other classes
    public static BiomeType GetBiomeType(int worldX, int worldZ)
    {
        if (_staticBiomeNoise == null)
        {
            // Use a default seed if not initialized
            InitializeStaticNoise(0);
        }

        float biomeValue = _staticBiomeNoise.GetNoise2D(worldX, worldZ);
        return GetBiomeTypeFromNoise(biomeValue);
    }

    // Helper method to convert noise value to biome type
    private static BiomeType GetBiomeTypeFromNoise(float biomeValue)
    {
        // Simple biome distribution based on noise value
        if (biomeValue < -0.5f)
            return BiomeType.Desert;
        else if (biomeValue < -0.2f)
            return BiomeType.Plains;
        else if (biomeValue < 0.2f)
            return BiomeType.Forest;
        else if (biomeValue < 0.5f)
            return BiomeType.Mountains;
        else
            return BiomeType.Tundra;
    }

    private int GenerateTerrainHeight(int worldX, int worldZ, BiomeType biomeType)
    {
        // Base terrain height from noise
        float heightNoise = _terrainNoise.GetNoise2D(worldX, worldZ);

        // Convert noise from [-1, 1] to [0, 1]
        heightNoise = (heightNoise + 1f) * 0.5f;

        // Apply biome-specific height modifications
        switch (biomeType)
        {
            case BiomeType.Desert:
                heightNoise = heightNoise * 0.3f + 0.2f; // Flatter, lower
                break;
            case BiomeType.Plains:
                heightNoise = heightNoise * 0.4f + 0.3f; // Relatively flat
                break;
            case BiomeType.Forest:
                heightNoise = heightNoise * 0.5f + 0.35f; // Moderate hills
                break;
            case BiomeType.Mountains:
                heightNoise = heightNoise * 0.8f + 0.4f; // Tall, varied
                break;
            case BiomeType.Tundra:
                heightNoise = heightNoise * 0.45f + 0.4f; // Moderate height, some hills
                break;
        }

        // Convert to actual height value
        return Mathf.FloorToInt(heightNoise * ChunkHeight);
    }

    private VoxelType DetermineVoxelType(int y, int terrainHeight, BiomeType biomeType)
    {
        // Bedrock at bottom
        if (y == 0)
            return VoxelType.Bedrock;

        // Surface layer and layers just below
        if (y == terrainHeight - 1)
        {
            // Top layer depends on biome
            switch (biomeType)
            {
                case BiomeType.Desert:
                    return VoxelType.Sand;
                case BiomeType.Tundra:
                    return VoxelType.Snow;
                default:
                    return VoxelType.Grass;
            }
        }
        else if (y >= terrainHeight - 4)
        {
            // Layers just below surface
            switch (biomeType)
            {
                case BiomeType.Desert:
                    return VoxelType.Sand;
                case BiomeType.Tundra:
                    return VoxelType.Dirt;
                default:
                    return VoxelType.Dirt;
            }
        }

        // Stone for deeper layers
        if (y < terrainHeight * 0.6f)
            return VoxelType.Stone;

        return VoxelType.Dirt;
    }

    private void AddBiomeObjects(VoxelChunk chunk, Vector2I chunkPos, int chunkSize)
    {
        // This would be expanded to add trees, structures, etc.
        // For now, just a placeholder
        Random random = new Random(Seed + chunkPos.X * 10000 + chunkPos.Y);

        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                int worldX = chunkPos.X * chunkSize + x;
                int worldZ = chunkPos.Y * chunkSize + z;

                BiomeType biomeType = GetBiomeType(worldX, worldZ);

                // Only add objects on certain biomes and with low probability
                if (biomeType == BiomeType.Forest && random.NextDouble() < 0.02)
                {
                    // Find surface height
                    int surfaceHeight = -1;
                    for (int y = ChunkHeight - 1; y >= 0; y--)
                    {
                        if (chunk.GetVoxel(x, y, z) != VoxelType.Air)
                        {
                            surfaceHeight = y;
                            break;
                        }
                    }

                    if (surfaceHeight >= 0 && chunk.GetVoxel(x, surfaceHeight, z) == VoxelType.Grass)
                    {
                        // Add a simple tree (just a trunk for now)
                        int treeHeight = random.Next(4, 8);
                        for (int y = 1; y <= treeHeight; y++)
                        {
                            if (surfaceHeight + y < ChunkHeight)
                            {
                                chunk.SetVoxel(x, surfaceHeight + y, z, VoxelType.Wood);
                            }
                        }
                    }
                }
            }
        }
    }
}

public enum BiomeType
{
    Plains,
    Forest,
    Desert,
    Mountains,
    Tundra
}

public enum VoxelType
{
    Air,
    Grass,
    Dirt,
    Stone,
    Sand,
    Wood,
    Leaves,
    Water,
    Snow,
    Bedrock
}
