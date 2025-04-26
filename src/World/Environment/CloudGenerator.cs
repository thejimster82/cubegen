using Godot;
using System;
using System.Collections.Generic;

public partial class CloudGenerator : Node3D
{
    [Export] public int Seed { get; set; } = 0;
    [Export] public int CloudHeight { get; set; } = 80; // Height at which clouds appear
    [Export] public int CloudLayerThickness { get; set; } = 10; // Thickness of the cloud layer
    [Export] public int CloudCount { get; set; } = 15; // Number of clouds to generate
    [Export] public float CloudScale { get; set; } = 0.5f; // Scale of cloud voxels (0.5 = double resolution)
    [Export] public float CloudSpread { get; set; } = 300.0f; // How far clouds spread from center
    [Export] public float CloudSpeed { get; set; } = 0.5f; // Speed of cloud movement

    private List<VoxelChunk> _cloudChunks = new List<VoxelChunk>();
    private List<ChunkMesh> _cloudMeshes = new List<ChunkMesh>();
    private FastNoiseLite _cloudNoise;
    private FastNoiseLite _cloudShapeNoise;
    private Random _random;
    private PackedScene _chunkMeshScene;

    public override void _Ready()
    {
        // Initialize noise generators
        InitializeNoise();

        // Get the chunk mesh scene
        _chunkMeshScene = GD.Load<PackedScene>("res://src/World/ChunkMesh.tscn");

        // Generate clouds
        GenerateClouds();
    }

    public override void _Process(double delta)
    {
        // Move clouds slowly
        MoveClouds((float)delta);
    }

    private void InitializeNoise()
    {
        _random = new Random(Seed);

        // Cloud distribution noise
        _cloudNoise = new FastNoiseLite();
        _cloudNoise.Seed = Seed;
        _cloudNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _cloudNoise.Frequency = 0.02f; // Doubled frequency for higher resolution (was 0.01f)

        // Cloud shape noise (more detailed)
        _cloudShapeNoise = new FastNoiseLite();
        _cloudShapeNoise.Seed = Seed + 1000;
        _cloudShapeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _cloudShapeNoise.Frequency = 0.2f; // Doubled frequency for higher resolution (was 0.1f)
        _cloudShapeNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _cloudShapeNoise.FractalOctaves = 4;
    }

    private void GenerateClouds()
    {
        // Clear any existing clouds
        ClearClouds();

        // Generate new clouds
        for (int i = 0; i < CloudCount; i++)
        {
            // Random position within spread range
            float x = (_random.Next(0, 1000) / 1000.0f * 2.0f - 1.0f) * CloudSpread;
            float z = (_random.Next(0, 1000) / 1000.0f * 2.0f - 1.0f) * CloudSpread;

            // Random cloud size - doubled for higher resolution
            int cloudSizeX = _random.Next(16, 40); // Doubled size for higher resolution (was 8-20)
            int cloudSizeZ = _random.Next(16, 40); // Doubled size for higher resolution (was 8-20)
            int cloudSizeY = _random.Next(6, 12);  // Doubled size for higher resolution (was 3-6)

            // Create a chunk for this cloud
            Vector2I chunkPos = new Vector2I((int)(x / 16), (int)(z / 16));
            VoxelChunk cloudChunk = new VoxelChunk(Math.Max(cloudSizeX, cloudSizeZ), cloudSizeY, chunkPos, CloudScale);

            // Generate cloud shape
            GenerateCloudShape(cloudChunk, cloudSizeX, cloudSizeY, cloudSizeZ, x, z);

            // Create mesh for the cloud
            ChunkMesh cloudMesh = _chunkMeshScene.Instantiate<ChunkMesh>();
            AddChild(cloudMesh);

            // Position the cloud mesh
            cloudMesh.Position = new Vector3(x, CloudHeight, z);

            // Generate mesh
            cloudMesh.GenerateMesh(cloudChunk);

            // Store references
            _cloudChunks.Add(cloudChunk);
            _cloudMeshes.Add(cloudMesh);
        }
    }

    private void GenerateCloudShape(VoxelChunk chunk, int sizeX, int sizeY, int sizeZ, float centerX, float centerZ)
    {
        // Center of the cloud chunk
        int centerChunkX = sizeX / 2;
        int centerChunkZ = sizeZ / 2;

        for (int x = 0; x < sizeX && x < chunk.Size; x++)
        {
            for (int z = 0; z < sizeZ && z < chunk.Size; z++)
            {
                // World position for noise sampling
                float worldX = centerX + (x - centerChunkX) * CloudScale;
                float worldZ = centerZ + (z - centerChunkZ) * CloudScale;

                // Get base cloud shape from noise
                float cloudValue = _cloudShapeNoise.GetNoise2D(worldX, worldZ);

                // Convert to 0-1 range
                cloudValue = (cloudValue + 1.0f) * 0.5f;

                // Determine cloud height at this position
                int cloudHeight = (int)(cloudValue * sizeY);

                // Add voxels from bottom to determined height
                for (int y = 0; y < cloudHeight && y < sizeY && y < chunk.Height; y++)
                {
                    // Make edges more fluffy/sparse
                    float distanceFromCenter = Mathf.Sqrt(
                        Mathf.Pow(x - centerChunkX, 2) +
                        Mathf.Pow(z - centerChunkZ, 2)
                    ) / (sizeX * 0.5f);

                    // More likely to have voxels near center, less at edges
                    float edgeFactor = 1.0f - distanceFromCenter;

                    // Add some noise to make it less uniform
                    float noiseValue = _cloudNoise.GetNoise3D(worldX, y * CloudScale, worldZ);

                    // Combine factors
                    float finalFactor = edgeFactor * (0.7f + 0.3f * noiseValue);

                    // Threshold for placing a voxel
                    if (finalFactor > 0.4f)
                    {
                        chunk.SetVoxel(x, y, z, VoxelType.Cloud);
                    }
                }
            }
        }
    }

    private void MoveClouds(float delta)
    {
        // Move each cloud mesh
        for (int i = 0; i < _cloudMeshes.Count; i++)
        {
            ChunkMesh cloudMesh = _cloudMeshes[i];

            // Move in X direction (east to west)
            Vector3 newPosition = cloudMesh.Position;
            newPosition.X -= CloudSpeed * delta;

            // Wrap around if cloud goes too far
            if (newPosition.X < -CloudSpread)
            {
                newPosition.X = CloudSpread;
            }

            cloudMesh.Position = newPosition;
        }
    }

    private void ClearClouds()
    {
        // Remove all cloud meshes
        foreach (ChunkMesh mesh in _cloudMeshes)
        {
            mesh.QueueFree();
        }

        _cloudMeshes.Clear();
        _cloudChunks.Clear();
    }

    // Call this to regenerate clouds with a new seed
    public void RegenerateClouds(int newSeed)
    {
        Seed = newSeed;
        InitializeNoise();
        GenerateClouds();
    }
}
