using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
    /// <summary>
    /// Manages biome objects in the world
    /// </summary>
    public class BiomeObjectManager
    {
        // Debug flag - set to true to enable detailed logging
        public static bool DebugMode = true;

        // Singleton instance
        private static BiomeObjectManager _instance;
        public static BiomeObjectManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new BiomeObjectManager();
                }
                return _instance;
            }
        }

        // Helper method for debug logging
        private void DebugLog(string message)
        {
            if (DebugMode)
            {
                GD.Print($"[BiomeObjectManager] {message}");
            }
        }

        // Dictionary of all biome objects
        // Key is the object's unique ID
        private Dictionary<Guid, BiomeObject> _biomeObjects = new Dictionary<Guid, BiomeObject>();

        // Dictionary to track anchor points
        // Key is the chunk position (Vector2I)
        // Value is a list of biome object IDs that have anchor points in that chunk
        private Dictionary<Vector2I, List<Guid>> _anchorPointsByChunk = new Dictionary<Vector2I, List<Guid>>();

        // Dictionary to track which chunks each biome object intersects with
        // Key is the chunk position (Vector2I)
        // Value is a list of biome object IDs that intersect with that chunk
        private Dictionary<Vector2I, List<Guid>> _objectsByChunk = new Dictionary<Vector2I, List<Guid>>();

        // Global registry of all biome object voxels by world coordinates
        // Key is the world position (Vector3I)
        // Value is the voxel type at that position
        private static Dictionary<Vector3I, VoxelType> _globalVoxelRegistry = new Dictionary<Vector3I, VoxelType>();

        // Dictionary to track which biome object each voxel belongs to
        // Key is the world position (Vector3I)
        // Value is the ID of the biome object that placed the voxel
        private static Dictionary<Vector3I, Guid> _voxelToObjectMap = new Dictionary<Vector3I, Guid>();

        // Set to track which chunks have already processed their anchor biome objects
        // This ensures each biome object is only generated once
        private static HashSet<Vector2I> _processedAnchorChunks = new HashSet<Vector2I>();

        // Dictionary to track which chunks each voxel belongs to
        // Key is the chunk position (Vector2I)
        // Value is a list of world positions of voxels in that chunk
        private static Dictionary<Vector2I, List<Vector3I>> _chunkVoxels = new Dictionary<Vector2I, List<Vector3I>>();

        // Random number generator
        private Random _random;

        // Seed for random number generation
        private int _seed;

        // Chunk size
        private int _chunkSize;

        // Whether the manager has been initialized
        private bool _isInitialized = false;

        // Private constructor to enforce singleton pattern
        private BiomeObjectManager()
        {
        }

        /// <summary>
        /// Initializes the biome object manager
        /// </summary>
        /// <param name="seed">Seed for random number generation</param>
        /// <param name="chunkSize">Size of chunks in the world</param>
        public void Initialize(int seed, int chunkSize)
        {
            _seed = seed;
            _random = new Random(seed);
            _chunkSize = chunkSize;
            _isInitialized = true;

            // Clear all collections to ensure a fresh start
            _biomeObjects.Clear();
            _anchorPointsByChunk.Clear();
            _objectsByChunk.Clear();
            _globalVoxelRegistry.Clear();
            _voxelToObjectMap.Clear();
            _processedAnchorChunks.Clear();
            _chunkVoxels.Clear();

            DebugLog($"Initialized with seed: {seed}, chunk size: {chunkSize}");
            DebugLog($"Registry stats - Objects: {_biomeObjects.Count}, Anchor chunks: {_anchorPointsByChunk.Count}, Voxels: {_globalVoxelRegistry.Count}");
        }

        /// <summary>
        /// Registers a biome object with the manager
        /// </summary>
        /// <param name="biomeObject">Biome object to register</param>
        public void RegisterBiomeObject(BiomeObject biomeObject)
        {
            if (!_isInitialized)
            {
                GD.PrintErr("BiomeObjectManager not initialized!");
                return;
            }

            // Add to the main dictionary
            _biomeObjects[biomeObject.Id] = biomeObject;

            // Calculate the chunk position of the anchor point
            Vector2I anchorChunkPos = WorldToChunkPosition(new Vector2I(biomeObject.AnchorPoint.X, biomeObject.AnchorPoint.Z));

            // Add to the anchor points dictionary
            if (!_anchorPointsByChunk.ContainsKey(anchorChunkPos))
            {
                _anchorPointsByChunk[anchorChunkPos] = new List<Guid>();
            }
            _anchorPointsByChunk[anchorChunkPos].Add(biomeObject.Id);

            DebugLog($"Registered biome object {biomeObject.Id} of type {biomeObject.Type} at anchor point {biomeObject.AnchorPoint}");
            DebugLog($"Anchor chunk: {anchorChunkPos}");

            // Update the objects by chunk dictionary
            UpdateObjectChunkIntersections(biomeObject);

            // Register all voxels of this biome object in the global registry
            int voxelCount = RegisterBiomeObjectVoxels(biomeObject);

            DebugLog($"Registered {voxelCount} voxels for biome object {biomeObject.Id}");
            DebugLog($"Registry stats - Objects: {_biomeObjects.Count}, Anchor chunks: {_anchorPointsByChunk.Count}, Voxels: {_globalVoxelRegistry.Count}");
        }

        /// <summary>
        /// Updates which chunks a biome object intersects with
        /// </summary>
        /// <param name="biomeObject">Biome object to update</param>
        private void UpdateObjectChunkIntersections(BiomeObject biomeObject)
        {
            // Calculate the min and max chunk positions that the object's bounding box intersects with
            Vector3 min = biomeObject.BoundingBox.Min;
            Vector3 max = biomeObject.BoundingBox.Max;

            // Add a small buffer to ensure we catch all intersecting chunks
            min -= new Vector3(1, 1, 1);
            max += new Vector3(1, 1, 1);

            Vector2I minChunkPos = WorldToChunkPosition(new Vector2I((int)min.X, (int)min.Z));
            Vector2I maxChunkPos = WorldToChunkPosition(new Vector2I((int)max.X, (int)max.Z));

            DebugLog($"Biome object {biomeObject.Id} bounding box: Min={min}, Max={max}");
            DebugLog($"Intersects with chunks from {minChunkPos} to {maxChunkPos}");

            // Track how many chunks this object intersects with
            int intersectingChunksCount = 0;

            // Add the object to each chunk it intersects with
            for (int x = minChunkPos.X; x <= maxChunkPos.X; x++)
            {
                for (int z = minChunkPos.Y; z <= maxChunkPos.Y; z++)
                {
                    Vector2I chunkPos = new Vector2I(x, z);

                    // Always consider the chunk containing the anchor point as intersecting
                    Vector2I anchorChunkPos = WorldToChunkPosition(new Vector2I(biomeObject.AnchorPoint.X, biomeObject.AnchorPoint.Z));
                    bool isAnchorChunk = (chunkPos == anchorChunkPos);

                    // Check if the object actually intersects with this chunk
                    bool intersectsChunk = biomeObject.IntersectsChunk(chunkPos, _chunkSize);

                    if (isAnchorChunk || intersectsChunk)
                    {
                        if (!_objectsByChunk.ContainsKey(chunkPos))
                        {
                            _objectsByChunk[chunkPos] = new List<Guid>();
                        }

                        if (!_objectsByChunk[chunkPos].Contains(biomeObject.Id))
                        {
                            _objectsByChunk[chunkPos].Add(biomeObject.Id);
                            intersectingChunksCount++;

                            DebugLog($"Added biome object {biomeObject.Id} to chunk {chunkPos} (AnchorChunk: {isAnchorChunk}, Intersects: {intersectsChunk})");
                        }
                    }
                }
            }

            DebugLog($"Biome object {biomeObject.Id} intersects with {intersectingChunksCount} chunks total");
        }

        /// <summary>
        /// Gets all biome objects that have anchor points in a chunk
        /// </summary>
        /// <param name="chunkPos">Position of the chunk</param>
        /// <returns>List of biome objects</returns>
        public List<BiomeObject> GetBiomeObjectsWithAnchorInChunk(Vector2I chunkPos)
        {
            if (!_isInitialized)
            {
                GD.PrintErr("BiomeObjectManager not initialized!");
                return new List<BiomeObject>();
            }

            if (_anchorPointsByChunk.TryGetValue(chunkPos, out List<Guid> objectIds))
            {
                return objectIds.Select(id => _biomeObjects[id]).ToList();
            }

            return new List<BiomeObject>();
        }

        /// <summary>
        /// Gets all biome objects that intersect with a chunk
        /// </summary>
        /// <param name="chunkPos">Position of the chunk</param>
        /// <returns>List of biome objects</returns>
        public List<BiomeObject> GetBiomeObjectsIntersectingChunk(Vector2I chunkPos)
        {
            if (!_isInitialized)
            {
                GD.PrintErr("BiomeObjectManager not initialized!");
                return new List<BiomeObject>();
            }

            if (_objectsByChunk.TryGetValue(chunkPos, out List<Guid> objectIds))
            {
                return objectIds.Select(id => _biomeObjects[id]).ToList();
            }

            return new List<BiomeObject>();
        }

        /// <summary>
        /// Converts a world position to a chunk position
        /// </summary>
        /// <param name="worldPos">World position</param>
        /// <returns>Chunk position</returns>
        private Vector2I WorldToChunkPosition(Vector2I worldPos)
        {
            return new Vector2I(
                Mathf.FloorToInt((float)worldPos.X / _chunkSize),
                Mathf.FloorToInt((float)worldPos.Y / _chunkSize)
            );
        }

        /// <summary>
        /// Clears all biome objects
        /// </summary>
        public void Clear()
        {
            _biomeObjects.Clear();
            _anchorPointsByChunk.Clear();
            _objectsByChunk.Clear();
            _globalVoxelRegistry.Clear();
            _voxelToObjectMap.Clear();
            _processedAnchorChunks.Clear();
            _chunkVoxels.Clear();

            GD.Print("Cleared all biome objects");
        }

        /// <summary>
        /// Marks a chunk as having processed its anchor biome objects
        /// </summary>
        /// <param name="chunkPos">Position of the chunk</param>
        public void MarkChunkProcessed(Vector2I chunkPos)
        {
            _processedAnchorChunks.Add(chunkPos);
            GD.Print($"Marked chunk {chunkPos} as processed");
        }

        /// <summary>
        /// Checks if a chunk has already processed its anchor biome objects
        /// </summary>
        /// <param name="chunkPos">Position of the chunk</param>
        /// <returns>True if the chunk has been processed, false otherwise</returns>
        public bool IsChunkProcessed(Vector2I chunkPos)
        {
            return _processedAnchorChunks.Contains(chunkPos);
        }

        /// <summary>
        /// Registers a voxel in the global registry
        /// </summary>
        /// <param name="worldPosition">World position of the voxel</param>
        /// <param name="voxelType">Type of voxel</param>
        /// <param name="objectId">ID of the biome object that placed the voxel</param>
        public void RegisterVoxel(Vector3I worldPosition, VoxelType voxelType, Guid objectId)
        {
            // Add to global registry
            _globalVoxelRegistry[worldPosition] = voxelType;
            _voxelToObjectMap[worldPosition] = objectId;

            // Determine which chunk this voxel belongs to
            Vector2I chunkPos = WorldToChunkPosition(new Vector2I(worldPosition.X, worldPosition.Z));

            // Add to chunk voxels dictionary
            if (!_chunkVoxels.ContainsKey(chunkPos))
            {
                _chunkVoxels[chunkPos] = new List<Vector3I>();
            }

            if (!_chunkVoxels[chunkPos].Contains(worldPosition))
            {
                _chunkVoxels[chunkPos].Add(worldPosition);
            }
        }

        /// <summary>
        /// Registers all voxels of a biome object in the global registry
        /// </summary>
        /// <param name="biomeObject">Biome object to register voxels for</param>
        /// <returns>Number of voxels registered</returns>
        public int RegisterBiomeObjectVoxels(BiomeObject biomeObject)
        {
            Dictionary<Vector3I, VoxelType> objectVoxels = biomeObject.GetAllVoxels();

            // Track voxels by chunk for debugging
            Dictionary<Vector2I, int> voxelsByChunk = new Dictionary<Vector2I, int>();

            foreach (var voxelEntry in objectVoxels)
            {
                Vector3I relativePos = voxelEntry.Key;
                Vector3I worldPos = biomeObject.RelativeToWorldPosition(relativePos);
                RegisterVoxel(worldPos, voxelEntry.Value, biomeObject.Id);

                // Track which chunk this voxel belongs to
                Vector2I chunkPos = WorldToChunkPosition(new Vector2I(worldPos.X, worldPos.Z));
                if (!voxelsByChunk.ContainsKey(chunkPos))
                {
                    voxelsByChunk[chunkPos] = 0;
                }
                voxelsByChunk[chunkPos]++;
            }

            // Log detailed information about voxel distribution
            DebugLog($"Voxel distribution for biome object {biomeObject.Id}:");
            foreach (var entry in voxelsByChunk)
            {
                DebugLog($"  Chunk {entry.Key}: {entry.Value} voxels");
            }

            return objectVoxels.Count;
        }

        /// <summary>
        /// Checks if a voxel exists in the global registry
        /// </summary>
        /// <param name="worldPosition">World position to check</param>
        /// <returns>True if a voxel exists at the position, false otherwise</returns>
        public bool HasVoxelAt(Vector3I worldPosition)
        {
            return _globalVoxelRegistry.ContainsKey(worldPosition);
        }

        /// <summary>
        /// Gets the voxel type at a world position
        /// </summary>
        /// <param name="worldPosition">World position to check</param>
        /// <param name="voxelType">Output voxel type</param>
        /// <returns>True if a voxel exists at the position, false otherwise</returns>
        public bool TryGetVoxelAt(Vector3I worldPosition, out VoxelType voxelType)
        {
            return _globalVoxelRegistry.TryGetValue(worldPosition, out voxelType);
        }

        /// <summary>
        /// Gets all biome object voxels that fall within a chunk
        /// </summary>
        /// <param name="chunkPos">Position of the chunk</param>
        /// <param name="chunkSize">Size of the chunk</param>
        /// <returns>Dictionary mapping world positions to voxel types</returns>
        public Dictionary<Vector3I, VoxelType> GetVoxelsInChunk(Vector2I chunkPos, int chunkSize)
        {
            Dictionary<Vector3I, VoxelType> result = new Dictionary<Vector3I, VoxelType>();

            // Calculate chunk bounds for debugging
            int chunkStartX = chunkPos.X * chunkSize;
            int chunkStartZ = chunkPos.Y * chunkSize;
            int chunkEndX = chunkStartX + chunkSize;
            int chunkEndZ = chunkStartZ + chunkSize;

            DebugLog($"Getting voxels for chunk {chunkPos} (bounds: ({chunkStartX},{chunkStartZ}) to ({chunkEndX},{chunkEndZ}))");

            // Check if we have a record of voxels in this chunk
            if (_chunkVoxels.TryGetValue(chunkPos, out List<Vector3I> voxelPositions))
            {
                DebugLog($"Found {voxelPositions.Count} voxels recorded for chunk {chunkPos}");

                // Get the voxel type for each position
                foreach (Vector3I worldPos in voxelPositions)
                {
                    if (_globalVoxelRegistry.TryGetValue(worldPos, out VoxelType voxelType))
                    {
                        result[worldPos] = voxelType;
                    }
                    else
                    {
                        DebugLog($"WARNING: Voxel at {worldPos} is in chunk record but not in global registry!");
                    }
                }
            }
            else
            {
                DebugLog($"No voxels recorded for chunk {chunkPos}");

                // Fallback: Scan the entire registry for voxels in this chunk
                // This is inefficient but helps with debugging
                int fallbackCount = 0;
                foreach (var entry in _globalVoxelRegistry)
                {
                    Vector3I worldPos = entry.Key;

                    if (worldPos.X >= chunkStartX && worldPos.X < chunkEndX &&
                        worldPos.Z >= chunkStartZ && worldPos.Z < chunkEndZ)
                    {
                        result[worldPos] = entry.Value;
                        fallbackCount++;

                        DebugLog($"Fallback found voxel at {worldPos} of type {entry.Value}");
                    }
                }

                if (fallbackCount > 0)
                {
                    DebugLog($"Fallback scan found {fallbackCount} voxels for chunk {chunkPos}");
                }
            }

            DebugLog($"Returning {result.Count} voxels for chunk {chunkPos}");
            return result;
        }

        /// <summary>
        /// Removes voxels from the registry after they have been applied to a chunk
        /// </summary>
        /// <param name="chunkPos">Position of the chunk</param>
        /// <returns>Number of voxels removed</returns>
        public int CleanupVoxelsInChunk(Vector2I chunkPos)
        {
            int removedCount = 0;

            // Check if we have a record of voxels in this chunk
            if (_chunkVoxels.TryGetValue(chunkPos, out List<Vector3I> voxelPositions))
            {
                DebugLog($"Cleaning up {voxelPositions.Count} voxels for chunk {chunkPos}");

                // Remove each voxel from the global registry
                foreach (Vector3I worldPos in voxelPositions)
                {
                    bool removedFromRegistry = _globalVoxelRegistry.Remove(worldPos);
                    bool removedFromMap = _voxelToObjectMap.Remove(worldPos);

                    if (removedFromRegistry && removedFromMap)
                    {
                        removedCount++;
                    }
                    else
                    {
                        DebugLog($"WARNING: Failed to remove voxel at {worldPos} from registry!");
                    }
                }

                // Clear the list of voxels for this chunk
                _chunkVoxels.Remove(chunkPos);

                DebugLog($"Removed {removedCount} voxels from registry for chunk {chunkPos}");
            }
            else
            {
                DebugLog($"No voxels recorded for chunk {chunkPos} to clean up");
            }

            return removedCount;
        }
    }
}
