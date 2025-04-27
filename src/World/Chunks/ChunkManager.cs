using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

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

    // Thread-safe queue for chunks that need to be requested
    private List<Vector2I> _chunksToRequest = new List<Vector2I>();
    private System.Threading.Mutex _chunksMutex = new System.Threading.Mutex();

    // Background mesh generator
    private ChunkMeshGenerator _meshGenerator;

    // Queue for chunks waiting to be added to the scene
    private Queue<(VoxelChunk, ArrayMesh, List<Vector3>)> _meshesToAdd = new Queue<(VoxelChunk, ArrayMesh, List<Vector3>)>();

    public void Initialize(int chunkSize, int chunkHeight)
    {
        _chunkSize = chunkSize;
        _chunkHeight = chunkHeight;

        // Initialize the mesh generator
        _meshGenerator = new ChunkMeshGenerator();
    }

    public void AddChunk(VoxelChunk chunk)
    {
        if (_chunks.ContainsKey(chunk.Position))
        {
            // If chunk already exists, queue it for update
            _meshGenerator.QueueChunk(chunk);
        }
        else
        {
            // Queue the chunk for mesh generation in the background
            _meshGenerator.QueueChunk(chunk);
        }
    }

    private void ProcessCompletedMeshes()
    {
        // Process any completed meshes from the mesh generator
        while (_meshGenerator.HasCompletedMeshes())
        {
            var (chunk, mesh, collisionFaces) = _meshGenerator.GetNextCompletedMesh();

            if (chunk != null)
            {
                if (_chunks.ContainsKey(chunk.Position))
                {
                    // Update existing chunk mesh
                    ChunkMesh chunkMesh = _chunks[chunk.Position];

                    // Update the mesh
                    chunkMesh.UpdateMeshFromArrayMesh(mesh, collisionFaces);
                }
                else
                {
                    // Create new chunk mesh
                    ChunkMesh chunkMesh = ChunkMeshScene.Instantiate<ChunkMesh>();
                    AddChild(chunkMesh);

                    // Set position
                    chunkMesh.Position = chunk.GetWorldPosition();

                    // Set the mesh
                    chunkMesh.SetMeshFromArrayMesh(mesh, collisionFaces);

                    // Add to dictionary
                    _chunks.Add(chunk.Position, chunkMesh);
                }
            }
        }
    }

    // List of chunks to remove, processed on the main thread
    private List<Vector2I> _chunksToRemove = new List<Vector2I>();
    private System.Threading.Mutex _removeMutex = new System.Threading.Mutex();

    public void RemoveChunk(Vector2I position)
    {
        // Add to the removal queue instead of removing directly
        // This ensures chunk removal happens on the main thread
        _removeMutex.WaitOne();
        try
        {
            _chunksToRemove.Add(position);
        }
        finally
        {
            _removeMutex.ReleaseMutex();
        }
    }

    private void ProcessChunkRemovals()
    {
        // Get the list of chunks to remove in a thread-safe way
        List<Vector2I> chunksToRemove = new List<Vector2I>();

        _removeMutex.WaitOne();
        try
        {
            if (_chunksToRemove.Count > 0)
            {
                chunksToRemove.AddRange(_chunksToRemove);
                _chunksToRemove.Clear();
            }
        }
        finally
        {
            _removeMutex.ReleaseMutex();
        }

        // Now remove chunks on the main thread
        foreach (Vector2I position in chunksToRemove)
        {
            if (_chunks.ContainsKey(position))
            {
                _chunks[position].QueueFree();
                _chunks.Remove(position);
            }
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

        // Process any pending chunk requests, removals, and completed meshes on the main thread
        ProcessChunkRequests();
        ProcessChunkRemovals();
        ProcessCompletedMeshes();
    }

    private void ProcessChunkRequests()
    {
        // Get the list of chunks to request in a thread-safe way
        List<Vector2I> chunksToRequest = new List<Vector2I>();

        _chunksMutex.WaitOne();
        try
        {
            if (_chunksToRequest.Count > 0)
            {
                chunksToRequest.AddRange(_chunksToRequest);
                _chunksToRequest.Clear();
            }
        }
        finally
        {
            _chunksMutex.ReleaseMutex();
        }

        // Now emit signals for each chunk on the main thread
        foreach (Vector2I chunkPos in chunksToRequest)
        {
            EmitSignal(SignalName.ChunkRequested, chunkPos);
        }
    }

    public void UpdateChunksAroundPlayer(Vector3 playerPosition, int viewDistance)
    {
        GD.Print($"Started Updating Chunks");
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
                        // Add to the request queue instead of emitting signal directly
                        _chunksMutex.WaitOne();
                        try
                        {
                            _chunksToRequest.Add(chunkPos);
                        }
                        finally
                        {
                            _chunksMutex.ReleaseMutex();
                        }
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

        // Queue all chunks for removal - they'll be processed on the main thread
        foreach (Vector2I chunkPos in chunksToRemove)
        {
            RemoveChunk(chunkPos);
        }
        GD.Print($"Finished Updating Chunks");
    }

    public override void _ExitTree()
    {
        // Stop the mesh generator thread when the node is removed from the scene
        if (_meshGenerator != null)
        {
            _meshGenerator.Stop();
        }

        base._ExitTree();
    }

    [Signal]
    public delegate void ChunkRequestedEventHandler(Vector2I chunkPosition);
}
