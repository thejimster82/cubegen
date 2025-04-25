using Godot;
using System;
using System.Collections.Generic;

public partial class ChunkManager : Node3D
{
    [Export] public PackedScene ChunkMeshScene { get; set; }

    private Dictionary<Vector2I, ChunkMesh> _chunks = new Dictionary<Vector2I, ChunkMesh>();

    // Property to get the current number of active chunks
    public int ActiveChunkCount => _chunks.Count;
    private int _chunkSize;
    private int _chunkHeight;

    // Keep track of the player's last chunk position to avoid unnecessary updates
    private Vector2I _lastPlayerChunk = new Vector2I(int.MaxValue, int.MaxValue);

    // Unloading distance should be greater than view distance to prevent frequent loading/unloading
    [Export] public int UnloadDistance { get; set; } = 20;

    // Minimum time between chunk updates to prevent too frequent updates
    [Export] public float ChunkUpdateCooldown { get; set; } = 0.5f;
    private float _timeSinceLastUpdate = 0.0f;

    public void Initialize(int chunkSize, int chunkHeight)
    {
        _chunkSize = chunkSize;
        _chunkHeight = chunkHeight;
    }

    public void AddChunk(VoxelChunk chunk)
    {
        if (_chunks.ContainsKey(chunk.Position))
        {
            // If chunk already exists, update it
            _chunks[chunk.Position].UpdateMesh(chunk);
        }
        else
        {
            // Create new chunk mesh
            ChunkMesh chunkMesh = ChunkMeshScene.Instantiate<ChunkMesh>();
            AddChild(chunkMesh);

            // Set position
            chunkMesh.Position = chunk.GetWorldPosition();

            // Generate mesh
            chunkMesh.GenerateMesh(chunk);

            // Add to dictionary
            _chunks.Add(chunk.Position, chunkMesh);
        }
    }

    public void RemoveChunk(Vector2I position)
    {
        if (_chunks.ContainsKey(position))
        {
            _chunks[position].QueueFree();
            _chunks.Remove(position);
        }
    }

    public bool HasChunk(Vector2I position)
    {
        return _chunks.ContainsKey(position);
    }

    public override void _Process(double delta)
    {
        // Update cooldown timer
        _timeSinceLastUpdate += (float)delta;
    }

    public void UpdateChunksAroundPlayer(Vector3 playerPosition, int viewDistance)
    {
        // Convert player position to chunk coordinates
        Vector2I playerChunk = new Vector2I(
            Mathf.FloorToInt(playerPosition.X / _chunkSize),
            Mathf.FloorToInt(playerPosition.Z / _chunkSize)
        );

        // Only update chunks if player has moved to a different chunk or enough time has passed
        if (playerChunk == _lastPlayerChunk && _timeSinceLastUpdate < ChunkUpdateCooldown)
        {
            return;
        }

        _lastPlayerChunk = playerChunk;
        _timeSinceLastUpdate = 0.0f;

        // Get list of chunks that should be active (within view distance)
        HashSet<Vector2I> activeChunks = new HashSet<Vector2I>();

        // Use a circular pattern for better visual appearance
        int viewDistanceSquared = viewDistance * viewDistance;

        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                // Use distance squared for a more circular pattern
                if (x * x + z * z <= viewDistanceSquared)
                {
                    Vector2I chunkPos = new Vector2I(playerChunk.X + x, playerChunk.Y + z);
                    activeChunks.Add(chunkPos);

                    // Request chunk generation if it doesn't exist
                    if (!HasChunk(chunkPos))
                    {
                        // This would call back to the WorldGenerator
                        EmitSignal(SignalName.ChunkRequested, chunkPos);
                    }
                }
            }
        }

        // Find chunks to unload (chunks that are too far from player)
        // Use a larger unload distance to prevent frequent loading/unloading
        List<Vector2I> chunksToRemove = new List<Vector2I>();
        int unloadDistanceSquared = UnloadDistance * UnloadDistance;

        foreach (Vector2I chunkPos in _chunks.Keys)
        {
            int dx = chunkPos.X - playerChunk.X;
            int dz = chunkPos.Y - playerChunk.Y;
            int distanceSquared = dx * dx + dz * dz;

            // Only unload chunks that are beyond the unload distance
            if (distanceSquared > unloadDistanceSquared)
            {
                chunksToRemove.Add(chunkPos);
            }
        }

        // Limit the number of chunks to remove per frame to prevent stuttering
        int maxChunksToRemovePerFrame = 5;
        int chunksRemoved = 0;

        foreach (Vector2I chunkPos in chunksToRemove)
        {
            if (chunksRemoved >= maxChunksToRemovePerFrame)
                break;

            RemoveChunk(chunkPos);
            chunksRemoved++;
        }
    }

    [Signal]
    public delegate void ChunkRequestedEventHandler(Vector2I chunkPosition);
}
