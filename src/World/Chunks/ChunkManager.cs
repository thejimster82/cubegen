using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using CubeGen.World.Common;

public partial class ChunkManager : Node3D
{
	[Export] public PackedScene ChunkMeshScene { get; set; }

	// Store chunk meshes and chunk data separately
	// Using ConcurrentDictionary for thread safety
	private ConcurrentDictionary<Vector2I, ChunkMesh> _chunks = new ConcurrentDictionary<Vector2I, ChunkMesh>();
	private ConcurrentDictionary<Vector2I, VoxelChunk> _chunkData = new ConcurrentDictionary<Vector2I, VoxelChunk>();

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
	[Export] public float ChunkUpdateCooldown { get; set; } = 0.1f; // Reduced from 0.5f for faster updates
	private float _timeSinceLastUpdate = 0.0f;

	// Thread-safe collection for chunks that need to be requested
	private ConcurrentBag<(Vector2I, float)> _chunksToRequest = new ConcurrentBag<(Vector2I, float)>();

	// Background mesh generator
	private ChunkMeshGenerator _meshGenerator;

	// Maximum number of chunks to process per frame
	private const int MaxChunksPerFrame = 5; // Increased from 3 to process more chunks each frame

	public void Initialize(int chunkSize, int chunkHeight)
	{
		_chunkSize = chunkSize;
		_chunkHeight = chunkHeight;

		// Initialize the mesh generator with a reference to this ChunkManager
		_meshGenerator = new ChunkMeshGenerator(this);
	}

	// Method to get voxel type across chunk boundaries
	public VoxelType GetVoxelType(int worldX, int worldY, int worldZ)
	{
		// Use the WorldDataProvider as the source of truth
		return CubeGen.World.Generation.WorldDataProvider.Instance.GetVoxelTypeAt(worldX, worldY, worldZ);
	}

	// Method to get voxel data across chunk boundaries
	public bool IsVoxelSolid(int worldX, int worldY, int worldZ)
	{
		// Get the voxel type from the WorldDataProvider
		VoxelType voxelType = CubeGen.World.Generation.WorldDataProvider.Instance.GetVoxelTypeAt(worldX, worldY, worldZ);

		// Special case: don't consider decoration types as solid for visibility calculations
		if (VoxelProperties.IsDecoration(voxelType))
		{
			return false;
		}

		// Check if the voxel type is solid
		return voxelType != VoxelType.Air && voxelType != VoxelType.Water;
	}

	// Method to check if a voxel is occluding for ambient occlusion calculations
	public bool IsVoxelOccluding(int worldX, int worldY, int worldZ)
	{
		// Get the voxel type from the WorldDataProvider
		VoxelType voxelType = CubeGen.World.Generation.WorldDataProvider.Instance.GetVoxelTypeAt(worldX, worldY, worldZ);

		// Use the VoxelProperties.IsOccluding method to determine if this voxel occludes light
		return VoxelProperties.IsOccluding(voxelType);
	}

	public void AddChunk(VoxelChunk chunk)
	{
		// First, store the chunk data regardless of whether we have a mesh for it
		_chunkData.AddOrUpdate(chunk.Position, chunk, (key, oldValue) => chunk);

		// Check if we have a mesh for this chunk
		if (!_chunks.ContainsKey(chunk.Position))
		{
			// If chunk mesh doesn't exist, queue it for update
			_meshGenerator.QueueChunk(chunk);
		}
	}

	private void ProcessCompletedMeshes()
	{
		// Process any completed meshes from the mesh generator
		// Limit the number of meshes processed per frame
		int meshesProcessed = 0;

		while (meshesProcessed < MaxChunksPerFrame && _meshGenerator.HasCompletedMeshes())
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
					_chunks.TryAdd(chunk.Position, newMesh);

					// Check if this chunk has fauna that needs to be spawned
					if (CubeGen.World.Fauna.FaunaSpawner.Instance.ChunkHasFauna(chunk.Position))
					{
						// Get the BirdManager
						var birdManager = GetNode<CubeGen.World.Fauna.BirdManager>("/root/World/BirdManager");
						if (birdManager != null)
						{
							// Spawn fauna for this chunk
							CubeGen.World.Fauna.FaunaSpawner.Instance.SpawnFaunaForChunk(chunk.Position, birdManager);
							GD.Print($"Spawned fauna for chunk {chunk.Position} during mesh processing");
						}
					}
				}

				meshesProcessed++;
			}
		}
	}

	// Thread-safe collection for chunks to remove, processed on the main thread
	private ConcurrentBag<Vector2I> _chunksToRemove = new ConcurrentBag<Vector2I>();

	private void ProcessChunkRemovals()
	{
		int chunksRemoved = 0;
		int maxChunksToRemove = 1; // Process more chunks per frame
		while (chunksRemoved < maxChunksToRemove && _chunksToRemove.TryTake(out Vector2I position))
		{
			// Remove chunk mesh if it exists
			if (_chunks.TryGetValue(position, out ChunkMesh chunkToRemove))
			{
				chunkToRemove.QueueFree();
				_chunks.TryRemove(position, out _);
			}

			// Also remove chunk data
			_chunkData.TryRemove(position, out _);

			chunksRemoved++;
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
		// Limit the number of chunks processed per frame
		int chunksProcessed = 0;

		// Sort chunks by distance before processing
		List<(Vector2I, float)> sortedChunks = new();

		// Collect all chunks to request
		while(_chunksToRequest.TryTake(out (Vector2I, float) chunkRequest))
		{
			sortedChunks.Add(chunkRequest);
		}

		// Sort by distance (closest first)
		sortedChunks.Sort((a, b) => a.Item2.CompareTo(b.Item2));

		// Process chunks in order of distance
		foreach (var chunkRequest in sortedChunks)
		{
			if (chunksProcessed >= MaxChunksPerFrame)
			{
				// Re-queue remaining chunks for next frame
				_chunksToRequest.Add(chunkRequest);
				continue;
			}

			EmitSignal(SignalName.ChunkRequested, chunkRequest.Item1);
			chunksProcessed++;
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
		ConcurrentBag<Vector2I> ChunksToRemove = new();
		ConcurrentBag<(Vector2I, float)> ChunksToRequest = new();

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
					if (!HasChunkData(chunkPos))
					{
						// Calculate base distance from player for prioritization
						float distanceSquared = x * x + z * z;

						// Add to the local request list
						ChunksToRequest.Add((chunkPos, distanceSquared));
					}
				}
			}
		}

		// Check ALL existing chunks for unloading, not just active ones
		// Create a combined list of all chunk positions from both dictionaries
		HashSet<Vector2I> allExistingChunks = new();

		// Create thread-safe copies of the keys
		var chunkKeys = _chunks.Keys.ToArray();
		var chunkDataKeys = _chunkData.Keys.ToArray();

		// Add all chunk positions from both dictionaries
		foreach (var key in chunkKeys)
		{
			allExistingChunks.Add(key);
		}

		foreach (var key in chunkDataKeys)
		{
			allExistingChunks.Add(key);
		}

		// Check each existing chunk to see if it's outside the view distance
		foreach (Vector2I chunkPos in allExistingChunks)
		{
			int dx = chunkPos.X - playerChunk.X;
			int dz = chunkPos.Y - playerChunk.Y;
			int distanceSquared = dx * dx + dz * dz;

			// If the chunk is outside the view distance, mark it for removal
			if (distanceSquared > viewDistanceSquared)
			{
				ChunksToRemove.Add(chunkPos);
			}
		}

		_chunksToRemove = ChunksToRemove;
		_chunksToRequest = ChunksToRequest;
		GD.Print($"Finished Updating Chunks - Active: {activeChunks.Count}, To Remove: {ChunksToRemove.Count}, To Request: {ChunksToRequest.Count}");
	}

	public override void _ExitTree()
	{
		// Stop the mesh generator thread when the node is removed from the scene
		_meshGenerator?.Stop();

		base._ExitTree();
	}

	// Clear all chunks from the scene
	public void ClearAllChunks()
	{
		// Create a copy of the keys to avoid modification during enumeration
		var chunkKeys = _chunks.Keys.ToArray();

		// Remove all chunk meshes
		foreach (Vector2I position in chunkKeys)
		{
			if (_chunks.TryGetValue(position, out ChunkMesh chunkToRemove))
			{
				chunkToRemove.QueueFree();
				_chunks.TryRemove(position, out _);
			}
		}

		// Clear chunk data
		_chunkData.Clear();

		// Clear any pending requests
		while (_chunksToRequest.TryTake(out _)) { }
		while (_chunksToRemove.TryTake(out _)) { }

		GD.Print("All chunks cleared");
	}

	[Signal]
	public delegate void ChunkRequestedEventHandler(Vector2I chunkPosition);
}
