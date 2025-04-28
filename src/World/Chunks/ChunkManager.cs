using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

public partial class ChunkManager : Node3D
{
    [Export] public PackedScene ChunkMeshScene { get; set; }

    // Store chunk meshes and chunk data separately
    private Dictionary<Vector2I, ChunkMesh> _chunks = new Dictionary<Vector2I, ChunkMesh>();
    private Dictionary<Vector2I, VoxelChunk> _chunkData = new Dictionary<Vector2I, VoxelChunk>();

    // Property to get the current number of active chunks
    public int ActiveChunkCount => _chunks.Count;
    public int ActiveChunkDataCount => _chunkData.Count;
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
    private List<(Vector2I, float)> _chunksToRequest = new List<(Vector2I, float)>();
    private System.Threading.Mutex _chunksMutex = new System.Threading.Mutex();

    // Background mesh generator
    private ChunkMeshGenerator _meshGenerator;

    // Maximum number of chunks to process per frame
    private const int MaxChunksPerFrame = 3;

    public void Initialize(int chunkSize, int chunkHeight)
    {
        _chunkSize = chunkSize;
        _chunkHeight = chunkHeight;

        // Initialize the mesh generator with a reference to this ChunkManager
        _meshGenerator = new ChunkMeshGenerator(this);
    }

    // Method to get voxel data across chunk boundaries
    public bool IsVoxelSolid(int worldX, int worldY, int worldZ)
    {
        // Convert world coordinates to chunk coordinates
        Vector2I chunkPos = new Vector2I(
            Mathf.FloorToInt((float)worldX / _chunkSize),
            Mathf.FloorToInt((float)worldZ / _chunkSize)
        );

        // Calculate local coordinates within the chunk
        int localX = worldX - (chunkPos.X * _chunkSize);
        int localY = worldY;
        int localZ = worldZ - (chunkPos.Y * _chunkSize);

        // First check if we have the chunk data directly
        if (_chunkData.TryGetValue(chunkPos, out VoxelChunk chunk))
        {
            return chunk.IsVoxelSolid(localX, localY, localZ);
        }

        // If no chunk data, check if we have a mesh with chunk data
        if (_chunks.TryGetValue(chunkPos, out ChunkMesh chunkMesh))
        {
            // Get the VoxelChunk from the ChunkMesh
            chunk = chunkMesh.GetChunk();
            if (chunk != null)
            {
                return chunk.IsVoxelSolid(localX, localY, localZ);
            }
        }

        // For Y out of bounds
        if (worldY < 0)
        {
            // Below the world is solid (ground)
            return true;
        }
        else if (worldY >= _chunkHeight)
        {
            // Above the world is air
            return false;
        }

        // If chunk doesn't exist or is out of bounds, assume air
        return false;
    }

    public void AddChunk(VoxelChunk chunk)
    {
        // First, store the chunk data regardless of whether we have a mesh for it
        _chunkData[chunk.Position] = chunk;

        if (_chunks.ContainsKey(chunk.Position))
        {
            // If chunk mesh already exists, queue it for update
            _meshGenerator.QueueChunk(chunk);
        }
        else
        {
            // Queue the chunk for mesh generation in the background
            _meshGenerator.QueueChunk(chunk);

            // Also queue any existing neighboring chunks for remeshing
            // This ensures proper AO calculation at chunk boundaries
            QueueNeighborsForRemeshing(chunk.Position);
        }
    }

    // Queue neighboring chunks for remeshing to fix AO at boundaries
    private void QueueNeighborsForRemeshing(Vector2I chunkPos)
    {
        // Check all 8 neighboring positions
        Vector2I[] neighbors = new Vector2I[]
        {
            new Vector2I(chunkPos.X - 1, chunkPos.Y - 1), // Bottom-left
            new Vector2I(chunkPos.X - 1, chunkPos.Y),     // Left
            new Vector2I(chunkPos.X - 1, chunkPos.Y + 1), // Top-left
            new Vector2I(chunkPos.X, chunkPos.Y - 1),     // Bottom
            new Vector2I(chunkPos.X, chunkPos.Y + 1),     // Top
            new Vector2I(chunkPos.X + 1, chunkPos.Y - 1), // Bottom-right
            new Vector2I(chunkPos.X + 1, chunkPos.Y),     // Right
            new Vector2I(chunkPos.X + 1, chunkPos.Y + 1)  // Top-right
        };

        // Queue each existing neighbor for remeshing
        foreach (Vector2I neighborPos in neighbors)
        {
            // First check if we have the chunk data
            if (_chunkData.TryGetValue(neighborPos, out VoxelChunk neighborChunk))
            {
                // Queue it for remeshing
                _meshGenerator.QueueChunk(neighborChunk);
            }
            // If no chunk data but we have a mesh, try to get the chunk from there
            else if (_chunks.TryGetValue(neighborPos, out ChunkMesh neighborMesh))
            {
                // Get the chunk from the mesh
                neighborChunk = neighborMesh.GetChunk();
                if (neighborChunk != null)
                {
                    // Queue it for remeshing
                    _meshGenerator.QueueChunk(neighborChunk);
                }
            }
        }
    }

    private void ProcessCompletedMeshes()
    {
        // Process any completed meshes from the mesh generator
        // Limit the number of meshes processed per frame
        int meshesProcessed = 0;

        while (_meshGenerator.HasCompletedMeshes() && meshesProcessed < MaxChunksPerFrame)
        {
            var (chunk, mesh, collisionFaces) = _meshGenerator.GetNextCompletedMesh();

            if (chunk != null)
            {
                if (_chunks.TryGetValue(chunk.Position, out ChunkMesh existingMesh))
                {
                    // Update existing chunk mesh
                    // Update the mesh and store the chunk reference
                    existingMesh.UpdateMeshFromArrayMesh(mesh, collisionFaces, chunk);
                }
                else
                {
                    // Create new chunk mesh
                    ChunkMesh newMesh = ChunkMeshScene.Instantiate<ChunkMesh>();
                    AddChild(newMesh);

                    // Set position
                    newMesh.Position = chunk.GetWorldPosition();

                    // Set the mesh and store the chunk reference
                    newMesh.SetMeshFromArrayMesh(mesh, collisionFaces, chunk);

                    // Add to dictionary
                    _chunks.Add(chunk.Position, newMesh);
                }

                meshesProcessed++;
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
            // Remove chunk mesh if it exists
            if (_chunks.TryGetValue(position, out ChunkMesh chunkToRemove))
            {
                chunkToRemove.QueueFree();
                _chunks.Remove(position);
            }

            // Also remove chunk data
            _chunkData.Remove(position);
        }
    }

    // Check if we have a chunk mesh at the given position
    public bool HasChunk(Vector2I position)
    {
        return _chunks.ContainsKey(position);
    }

    // Check if we have chunk data at the given position
    public bool HasChunkData(Vector2I position)
    {
        return _chunkData.ContainsKey(position);
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
        List<(Vector2I, float)> chunksToRequest = new();

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

        // Sort chunks by distance (priority)
        chunksToRequest.Sort((a, b) => a.Item2.CompareTo(b.Item2));

        // Limit the number of chunks processed per frame
        int chunksProcessed = 0;

        // Now emit signals for each chunk on the main thread
        foreach (var (chunkPos, _) in chunksToRequest)
        {
            EmitSignal(SignalName.ChunkRequested, chunkPos);

            // Limit the number of chunks processed per frame
            chunksProcessed++;
            if (chunksProcessed >= MaxChunksPerFrame)
            {
                // Re-queue the remaining chunks for next frame
                _chunksMutex.WaitOne();
                try
                {
                    for (int i = chunksProcessed; i < chunksToRequest.Count; i++)
                    {
                        _chunksToRequest.Add(chunksToRequest[i]);
                    }
                }
                finally
                {
                    _chunksMutex.ReleaseMutex();
                }
                break;
            }
        }
    }

    public void UpdateChunksAroundPlayer(Vector3 playerPosition, int viewDistance, Vector3 playerMovementDirection = default)
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

        // Store chunks to request with their priorities
        List<(Vector2I, float)> chunksToRequest = new();

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
                    if (!HasChunkData(chunkPos))
                    {
                        // Calculate base distance from player for prioritization
                        float distanceSquared = x * x + z * z;

                        // Apply direction-based prioritization if we have a movement direction
                        if (playerMovementDirection != Vector3.Zero)
                        {
                            // Convert chunk offset to world direction
                            Vector3 chunkDirection = new Vector3(x, 0, z);
                            if (chunkDirection != Vector3.Zero)
                            {
                                chunkDirection = chunkDirection.Normalized();

                                // Calculate dot product to determine if chunk is in front of player
                                // Dot product ranges from -1 (behind) to 1 (in front)
                                float directionFactor = playerMovementDirection.Dot(chunkDirection);

                                // Adjust priority based on direction
                                // Chunks in front get lower priority value (loaded first)
                                // Chunks behind get higher priority value (loaded later)
                                if (directionFactor > 0) // In front of player
                                {
                                    // Reduce distance (higher priority) for chunks in front
                                    distanceSquared *= (1.0f - directionFactor * 0.5f);
                                }
                                else // Behind player
                                {
                                    // Increase distance (lower priority) for chunks behind
                                    distanceSquared *= (1.0f + Math.Abs(directionFactor) * 2.0f);
                                }
                            }
                        }
                        else
                        {
                            // If no movement direction (e.g., at game start), use pure distance from center
                            // This ensures chunks are generated in concentric circles
                            // No adjustment needed, distanceSquared is already set correctly
                            // GD.Print($"Using concentric circle pattern for chunk at {chunkPos}, distance: {Math.Sqrt(distanceSquared):F2}");
                        }

                        // Add to the local request list
                        chunksToRequest.Add((chunkPos, distanceSquared));
                    }
                }
            }
        }

        // Sort chunks by priority (distance adjusted by direction)
        chunksToRequest.Sort((a, b) => a.Item2.CompareTo(b.Item2));

        // Add chunks to the request queue
        _chunksMutex.WaitOne();
        try
        {
            foreach (var chunk in chunksToRequest)
            {
                _chunksToRequest.Add(chunk);
            }
        }
        finally
        {
            _chunksMutex.ReleaseMutex();
        }

        // Find chunks to unload (chunks that are too far from player)
        // Use a larger unload distance to prevent frequent loading/unloading
        List<Vector2I> chunksToRemove = new List<Vector2I>();
        int unloadDistanceSquared = UnloadDistance * UnloadDistance;

        // Check both chunk data and chunk meshes for unloading
        HashSet<Vector2I> allChunkPositions = new HashSet<Vector2I>();

        // Add all chunk positions from both dictionaries
        foreach (Vector2I pos in _chunks.Keys)
        {
            allChunkPositions.Add(pos);
        }

        foreach (Vector2I pos in _chunkData.Keys)
        {
            allChunkPositions.Add(pos);
        }

        // Check each position for unloading
        foreach (Vector2I chunkPos in allChunkPositions)
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

        // Also remove any chunks from the request queue that are no longer needed
        CleanupRequestQueue(activeChunks);

        GD.Print($"Finished Updating Chunks");
    }

    // Remove chunks from the request queue that are no longer needed
    private void CleanupRequestQueue(HashSet<Vector2I> activeChunks)
    {
        _chunksMutex.WaitOne();
        try
        {
            // Create a new list with only the chunks we still need
            List<(Vector2I, float)> updatedRequests = new();

            foreach (var (chunkPos, priority) in _chunksToRequest)
            {
                // Only keep chunks that are still active
                if (activeChunks.Contains(chunkPos))
                {
                    updatedRequests.Add((chunkPos, priority));
                }
            }

            // Replace the request queue with the filtered list
            _chunksToRequest.Clear();
            foreach (var chunk in updatedRequests)
            {
                _chunksToRequest.Add(chunk);
            }
        }
        finally
        {
            _chunksMutex.ReleaseMutex();
        }
    }

    public override void _ExitTree()
    {
        // Stop the mesh generator thread when the node is removed from the scene
        _meshGenerator?.Stop();

        base._ExitTree();
    }

    [Signal]
    public delegate void ChunkRequestedEventHandler(Vector2I chunkPosition);
}
