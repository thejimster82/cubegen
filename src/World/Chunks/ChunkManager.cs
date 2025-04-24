using Godot;
using System;
using System.Collections.Generic;

public partial class ChunkManager : Node3D
{
    [Export] public PackedScene ChunkMeshScene { get; set; }
    
    private Dictionary<Vector2I, ChunkMesh> _chunks = new Dictionary<Vector2I, ChunkMesh>();
    private int _chunkSize;
    private int _chunkHeight;
    
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
    
    public void UpdateChunksAroundPlayer(Vector3 playerPosition, int viewDistance)
    {
        // Convert player position to chunk coordinates
        Vector2I playerChunk = new Vector2I(
            Mathf.FloorToInt(playerPosition.X / _chunkSize),
            Mathf.FloorToInt(playerPosition.Z / _chunkSize)
        );
        
        // Get list of chunks that should be active
        HashSet<Vector2I> activeChunks = new HashSet<Vector2I>();
        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                Vector2I chunkPos = new Vector2I(playerChunk.X + x, playerChunk.Y + z);
                activeChunks.Add(chunkPos);
                
                // Request chunk generation if it doesn't exist
                if (!HasChunk(chunkPos))
                {
                    // This would call back to the WorldGenerator
                    // For now, we'll emit a signal
                    EmitSignal(SignalName.ChunkRequested, chunkPos);
                }
            }
        }
        
        // Find chunks to unload (chunks that are too far from player)
        List<Vector2I> chunksToRemove = new List<Vector2I>();
        foreach (Vector2I chunkPos in _chunks.Keys)
        {
            if (!activeChunks.Contains(chunkPos))
            {
                chunksToRemove.Add(chunkPos);
            }
        }
        
        // Remove chunks that are too far
        foreach (Vector2I chunkPos in chunksToRemove)
        {
            RemoveChunk(chunkPos);
        }
    }
    
    [Signal]
    public delegate void ChunkRequestedEventHandler(Vector2I chunkPosition);
}
