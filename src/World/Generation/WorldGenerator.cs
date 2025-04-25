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
        // Initialize terrain noise with flatter settings
        _terrainNoise = new FastNoiseLite();
        _terrainNoise.Seed = Seed;
        _terrainNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _terrainNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _terrainNoise.Frequency = 0.005f; // Lower frequency for smoother, more gradual changes
        _terrainNoise.FractalOctaves = 2; // Fewer octaves for less detail and flatter terrain

        // Initialize biome noise (different settings for variety)
        _biomeNoise = new FastNoiseLite();
        _biomeNoise.Seed = Seed + 1000; // Different seed for biome variation
        _biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _biomeNoise.Frequency = 0.003f; // Even larger scale for biomes (more gradual transitions)

        // Initialize static noise for use by other classes
        InitializeStaticNoise(Seed);
    }

    private void GenerateInitialChunks()
    {
        // Generate chunks around origin
        int viewDistance = 12; // Number of chunks to generate in each direction

        GD.Print($"Generating initial chunks with view distance: {viewDistance}");

        // Use a circular pattern for better visual appearance
        int viewDistanceSquared = viewDistance * viewDistance;

        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                // Use distance squared for a more circular pattern
                if (x * x + z * z <= viewDistanceSquared)
                {
                    Vector2I chunkPos = new Vector2I(x, z);
                    GD.Print($"Generating initial chunk at: {chunkPos}");
                    GenerateChunk(chunkPos);
                }
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
            _staticBiomeNoise.Frequency = 0.003f; // Even larger scale for biomes (more gradual transitions)
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

        // Apply biome-specific height modifications - much flatter for all biomes
        // Add a consistent base height to ensure terrain is at a predictable level
        float baseHeight = 0.3f; // Consistent base height for all terrain

        switch (biomeType)
        {
            case BiomeType.Desert:
                heightNoise = heightNoise * 0.1f + baseHeight; // Very flat, low
                break;
            case BiomeType.Plains:
                heightNoise = heightNoise * 0.15f + baseHeight; // Very flat
                break;
            case BiomeType.Forest:
                heightNoise = heightNoise * 0.2f + baseHeight; // Slightly more varied but still flat
                break;
            case BiomeType.Mountains:
                heightNoise = heightNoise * 0.3f + baseHeight; // Less mountainous, more like hills
                break;
            case BiomeType.Tundra:
                heightNoise = heightNoise * 0.15f + baseHeight; // Very flat
                break;
        }

        // Convert to actual height value
        int height = Mathf.FloorToInt(heightNoise * ChunkHeight);

        // Debug output for the first chunk to help understand terrain height
        if (worldX == 0 && worldZ == 0)
        {
            GD.Print($"Terrain height at origin: {height}");
        }

        return height;
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
