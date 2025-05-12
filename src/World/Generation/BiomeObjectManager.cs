using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
    /// <summary>
    /// Manages biome objects that span across chunk boundaries
    /// </summary>
    public static class BiomeObjectManager
    {
        // Dictionary to store out-of-bounds voxels for each chunk
        // Key: Chunk position
        // Value: Dictionary of local positions to voxel types
        private static ConcurrentDictionary<Vector2I, ConcurrentDictionary<(int, int, int), VoxelType>> _outOfBoundsVoxels = 
            new ConcurrentDictionary<Vector2I, ConcurrentDictionary<(int, int, int), VoxelType>>();

        // Dictionary to store decoration placements for out-of-bounds voxels
        // Key: Chunk position
        // Value: Dictionary of local positions to decoration placements
        private static ConcurrentDictionary<Vector2I, ConcurrentDictionary<(int, int, int), DecorationClusters.DecorationPlacement>> _outOfBoundsDecorations =
            new ConcurrentDictionary<Vector2I, ConcurrentDictionary<(int, int, int), DecorationClusters.DecorationPlacement>>();

        // Dictionary to track which chunks have been processed
        // This helps avoid duplicate processing and ensures we clean up properly
        private static ConcurrentDictionary<Vector2I, bool> _processedChunks = 
            new ConcurrentDictionary<Vector2I, bool>();

        /// <summary>
        /// Records an out-of-bounds voxel for a future chunk
        /// </summary>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldY">World Y coordinate</param>
        /// <param name="worldZ">World Z coordinate</param>
        /// <param name="voxelType">Type of voxel to place</param>
        /// <param name="currentChunkPos">Position of the current chunk (for tracking)</param>
        /// <param name="chunkSize">Size of chunks in voxels</param>
        public static void RecordOutOfBoundsVoxel(int worldX, int worldY, int worldZ, VoxelType voxelType, 
            Vector2I currentChunkPos, int chunkSize)
        {
            // Calculate which chunk this voxel belongs to
            Vector2I targetChunkPos = new Vector2I(
                Mathf.FloorToInt((float)worldX / chunkSize),
                Mathf.FloorToInt((float)worldZ / chunkSize)
            );

            // Skip if this voxel is in the current chunk (not out of bounds)
            if (targetChunkPos == currentChunkPos)
                return;

            // Calculate local coordinates within the target chunk
            int localX = worldX - (targetChunkPos.X * chunkSize);
            int localY = worldY;
            int localZ = worldZ - (targetChunkPos.Y * chunkSize);

            // Get or create the dictionary for this chunk
            var chunkDict = _outOfBoundsVoxels.GetOrAdd(targetChunkPos, 
                _ => new ConcurrentDictionary<(int, int, int), VoxelType>());

            // Record the voxel
            chunkDict[(localX, localY, localZ)] = voxelType;
        }

        /// <summary>
        /// Records an out-of-bounds decoration for a future chunk
        /// </summary>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldY">World Y coordinate</param>
        /// <param name="worldZ">World Z coordinate</param>
        /// <param name="placement">Decoration placement information</param>
        /// <param name="currentChunkPos">Position of the current chunk (for tracking)</param>
        /// <param name="chunkSize">Size of chunks in voxels</param>
        public static void RecordOutOfBoundsDecoration(int worldX, int worldY, int worldZ, 
            DecorationClusters.DecorationPlacement placement, Vector2I currentChunkPos, int chunkSize)
        {
            // Calculate which chunk this decoration belongs to
            Vector2I targetChunkPos = new Vector2I(
                Mathf.FloorToInt((float)worldX / chunkSize),
                Mathf.FloorToInt((float)worldZ / chunkSize)
            );

            // Skip if this decoration is in the current chunk (not out of bounds)
            if (targetChunkPos == currentChunkPos)
                return;

            // Calculate local coordinates within the target chunk
            int localX = worldX - (targetChunkPos.X * chunkSize);
            int localY = worldY;
            int localZ = worldZ - (targetChunkPos.Y * chunkSize);

            // Get or create the dictionary for this chunk
            var chunkDict = _outOfBoundsDecorations.GetOrAdd(targetChunkPos, 
                _ => new ConcurrentDictionary<(int, int, int), DecorationClusters.DecorationPlacement>());

            // Record the decoration
            chunkDict[(localX, localY, localZ)] = placement;
        }

        /// <summary>
        /// Applies any out-of-bounds voxels to a newly generated chunk
        /// </summary>
        /// <param name="chunk">The chunk to apply voxels to</param>
        /// <returns>True if any voxels were applied, false otherwise</returns>
        public static bool ApplyOutOfBoundsVoxels(VoxelChunk chunk)
        {
            bool voxelsApplied = false;

            // Check if we have any out-of-bounds voxels for this chunk
            if (_outOfBoundsVoxels.TryGetValue(chunk.Position, out var voxelDict))
            {
                // Apply each voxel
                foreach (var kvp in voxelDict)
                {
                    var (x, y, z) = kvp.Key;
                    VoxelType voxelType = kvp.Value;

                    // Set the voxel in the chunk
                    chunk.SetVoxel(x, y, z, voxelType);
                    voxelsApplied = true;
                }
            }

            // Check if we have any out-of-bounds decorations for this chunk
            if (_outOfBoundsDecorations.TryGetValue(chunk.Position, out var decorDict))
            {
                // Apply each decoration
                foreach (var kvp in decorDict)
                {
                    var (x, y, z) = kvp.Key;
                    DecorationClusters.DecorationPlacement placement = kvp.Value;

                    // Set the decoration in the chunk
                    chunk.SetDecorationPlacement(x, y, z, placement);
                    voxelsApplied = true;
                }
            }

            // Mark this chunk as processed
            _processedChunks[chunk.Position] = true;

            return voxelsApplied;
        }

        /// <summary>
        /// Cleans up out-of-bounds voxels for a chunk that has been processed
        /// </summary>
        /// <param name="chunkPos">Position of the chunk to clean up</param>
        public static void CleanupOutOfBoundsVoxels(Vector2I chunkPos)
        {
            // Remove the out-of-bounds voxels for this chunk
            _outOfBoundsVoxels.TryRemove(chunkPos, out _);
            _outOfBoundsDecorations.TryRemove(chunkPos, out _);
            _processedChunks.TryRemove(chunkPos, out _);
        }

        /// <summary>
        /// Checks if a chunk has any out-of-bounds voxels
        /// </summary>
        /// <param name="chunkPos">Position of the chunk to check</param>
        /// <returns>True if the chunk has out-of-bounds voxels, false otherwise</returns>
        public static bool HasOutOfBoundsVoxels(Vector2I chunkPos)
        {
            return _outOfBoundsVoxels.ContainsKey(chunkPos) || _outOfBoundsDecorations.ContainsKey(chunkPos);
        }

        /// <summary>
        /// Resets all out-of-bounds voxel data (useful when starting a new world)
        /// </summary>
        public static void Reset()
        {
            _outOfBoundsVoxels.Clear();
            _outOfBoundsDecorations.Clear();
            _processedChunks.Clear();
        }
    }
}
