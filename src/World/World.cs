using Godot;
using System;

public partial class World : Node3D
{
    [Export] public PackedScene PlayerScene { get; set; }
    [Export] public int ViewDistance { get; set; } = 5;
    [Export] public int Seed { get; set; } = 0;

    private WorldGenerator _worldGenerator;
    private ChunkManager _chunkManager;
    private Player _player;
    private Timer _chunkUpdateTimer;

    public override void _Ready()
    {
        _worldGenerator = GetNode<WorldGenerator>("WorldGenerator");
        _chunkManager = GetNode<ChunkManager>("WorldGenerator/ChunkManager");

        // Set seed
        if (Seed == 0)
        {
            // Random seed if not specified
            Random random = new Random();
            Seed = random.Next();
        }
        _worldGenerator.Seed = Seed;

        // Connect chunk requested signal
        _chunkManager.ChunkRequested += OnChunkRequested;

        // Create player
        SpawnPlayer();

        // Create timer for chunk updates
        _chunkUpdateTimer = new Timer();
        _chunkUpdateTimer.WaitTime = 0.5f; // Update chunks every half second
        _chunkUpdateTimer.Timeout += OnChunkUpdateTimerTimeout;
        AddChild(_chunkUpdateTimer);
        _chunkUpdateTimer.Start();
    }

    private void SpawnPlayer()
    {
        _player = PlayerScene.Instantiate<Player>();
        AddChild(_player);

        // Position player above the terrain at spawn point
        Vector3 spawnPosition = new Vector3(0, 100, 0); // Start high and let gravity pull down
        _player.Position = spawnPosition;

        GD.Print("Player spawned at position: " + spawnPosition);
    }

    private void OnChunkRequested(Vector2I chunkPosition)
    {
        // Generate the requested chunk
        _worldGenerator.GenerateChunk(chunkPosition);

        // Debug output to confirm chunk generation
        GD.Print($"Generated chunk at position: {chunkPosition}");
    }

    private void OnChunkUpdateTimerTimeout()
    {
        if (_player != null && _chunkManager != null)
        {
            // Update chunks around player
            _chunkManager.UpdateChunksAroundPlayer(_player.Position, ViewDistance);
        }
    }
}
