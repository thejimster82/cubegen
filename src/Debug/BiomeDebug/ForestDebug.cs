using Godot;
using System;
using CubeGen.World.Common;
using CubeGen.World.Generation;

namespace CubeGen.Debug.BiomeDebug
{
    public partial class ForestDebug : Node3D
    {
        [Export] public int ViewDistance { get; set; } = 15;
        [Export] public int Seed { get; set; } = 12345;

        private WorldGenerator _worldGenerator;
        private ChunkManager _chunkManager;

        public override void _Ready()
        {
            // Create a new SingleBiomeRegionGenerator for Forest biome
            SingleBiomeRegionGenerator biomeGenerator = new SingleBiomeRegionGenerator(BiomeType.Forest);

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

            GD.Print("Enhanced Forest biome debug initialized");
            GD.Print("Forest now contains three sub-regions: Forest Plains, Regular Forest, and Forest Mountains");
            GD.Print("Each sub-region has different terrain and flora characteristics");
            GD.Print("Trees are now strategically placed with natural clearings and dense forest patches");
            GD.Print("Paths and open areas are automatically generated through the forest");
            GD.Print("Mountain terrain is now smoother and more natural-looking");
            GD.Print("Region transitions are smoother with larger, more cohesive areas");
        }

        private void OnChunkRequested(Vector2I chunkPosition)
        {
            // Generate the requested chunk
            _worldGenerator.GenerateChunk(chunkPosition);
            GD.Print($"Generated chunk at position: {chunkPosition}");
        }
    }
}
