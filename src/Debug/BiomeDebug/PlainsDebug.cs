using Godot;
using System;
using CubeGen.World.Common;
using CubeGen.World.Generation;

namespace CubeGen.Debug.BiomeDebug
{
    public partial class PlainsDebug : Node3D
    {
        [Export] public int ViewDistance { get; set; } = 15;
        [Export] public int Seed { get; set; } = 12345;

        private WorldGenerator _worldGenerator;
        private ChunkManager _chunkManager;

        public override void _Ready()
        {
            // Create a new SingleBiomeRegionGenerator for Plains biome
            SingleBiomeRegionGenerator biomeGenerator = new SingleBiomeRegionGenerator(BiomeType.Plains);

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

            GD.Print("Plains biome debug initialized with region-based hills feature");
            GD.Print("Hills are generated using domain-warped ridged noise for more natural terrain");
            GD.Print("Hills only appear in specific regions of the Plains biome for more varied landscapes");
        }

        private void OnChunkRequested(Vector2I chunkPosition)
        {
            // Generate the requested chunk
            _worldGenerator.GenerateChunk(chunkPosition);
            GD.Print($"Generated chunk at position: {chunkPosition}");
        }
    }
}
