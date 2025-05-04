using Godot;
using System;
using CubeGen.World.Common;
using CubeGen.World.Generation;

namespace CubeGen.Debug.BiomeDebug
{
    public partial class MountainsDebug : Node3D
    {
        [Export] public int ViewDistance { get; set; } = 15;
        [Export] public int Seed { get; set; } = 12345;

        private WorldGenerator _worldGenerator;
        private ChunkManager _chunkManager;

        public override void _Ready()
        {
            // Create a new SingleBiomeRegionGenerator for Mountains biome
            SingleBiomeRegionGenerator biomeGenerator = new SingleBiomeRegionGenerator(BiomeType.Mountains);

            // Initialize with a seed
            biomeGenerator.Initialize(Seed);

            // Set as the singleton instance
            SingleBiomeRegionGenerator.SetInstance(biomeGenerator);

            // Initialize the world generator
            _worldGenerator = GetNode<WorldGenerator>("WorldGenerator");
            _worldGenerator.Initialize(Seed, ViewDistance);

            // Get the chunk manager
            _chunkManager = GetNode<ChunkManager>("WorldGenerator/ChunkManager");

            // Connect chunk requested signal
            _chunkManager.ChunkRequested += OnChunkRequested;

            GD.Print("Mountains biome debug initialized");
        }

        private void OnChunkRequested(Vector2I chunkPosition)
        {
            // Generate the requested chunk
            _worldGenerator.GenerateChunk(chunkPosition);
            GD.Print($"Generated chunk at position: {chunkPosition}");
        }
    }
}
