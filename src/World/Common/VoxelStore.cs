using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CubeGen.World.Common;
using CubeGen.World.Generation;

namespace CubeGen.World.Common
{
    /// <summary>
    /// Centralized store for all voxel data in the world.
    /// Acts as a single source of truth for voxel data, separating generation from rendering.
    /// </summary>
    public class VoxelStore
    {
        // Singleton instance
        private static VoxelStore _instance;
        public static VoxelStore Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new VoxelStore();
                }
                return _instance;
            }
        }

        // Thread-safe dictionary to store voxel data
        // Key is a string in the format "x,y,z", value is the voxel type
        private ConcurrentDictionary<string, VoxelType> _voxelData = new ConcurrentDictionary<string, VoxelType>();

        // Dictionary to store decoration placement information
        // Key is a string in the format "x,y,z", value is the placement data
        private ConcurrentDictionary<string, DecorationClusters.DecorationPlacement> _decorationPlacements = 
            new ConcurrentDictionary<string, DecorationClusters.DecorationPlacement>();

        // Dictionary to store biome blend weights
        // Key is a string in the format "x,z", value is a dictionary mapping BiomeType to blend weight (0.0-1.0)
        private ConcurrentDictionary<string, Dictionary<BiomeType, float>> _biomeBlendWeights = 
            new ConcurrentDictionary<string, Dictionary<BiomeType, float>>();

        // Track which chunks have been generated
        private HashSet<string> _generatedChunks = new HashSet<string>();

        // Reference to the world data provider for generating voxels
        private WorldDataProvider _worldDataProvider;

        // Private constructor for singleton
        private VoxelStore()
        {
        }

        /// <summary>
        /// Initialize the voxel store
        /// </summary>
        public void Initialize(WorldDataProvider worldDataProvider)
        {
            _worldDataProvider = worldDataProvider;
            GD.Print("VoxelStore initialized");
        }

        /// <summary>
        /// Get the voxel type at a world position
        /// </summary>
        public VoxelType GetVoxelType(int worldX, int worldY, int worldZ)
        {
            string key = GetVoxelKey(worldX, worldY, worldZ);

            // Check if the voxel exists in the store
            if (_voxelData.TryGetValue(key, out VoxelType voxelType))
            {
                return voxelType;
            }

            // If not, generate it using the world data provider
            voxelType = _worldDataProvider.GenerateVoxelTypeAt(worldX, worldY, worldZ);
            
            // Store the generated voxel
            _voxelData[key] = voxelType;
            
            return voxelType;
        }

        /// <summary>
        /// Set the voxel type at a world position
        /// </summary>
        public void SetVoxelType(int worldX, int worldY, int worldZ, VoxelType voxelType)
        {
            string key = GetVoxelKey(worldX, worldY, worldZ);
            _voxelData[key] = voxelType;
        }

        /// <summary>
        /// Set decoration placement information for a voxel
        /// </summary>
        public void SetDecorationPlacement(int worldX, int worldY, int worldZ, DecorationClusters.DecorationPlacement placement)
        {
            string key = GetVoxelKey(worldX, worldY, worldZ);
            _decorationPlacements[key] = placement;
        }

        /// <summary>
        /// Get decoration placement information for a voxel
        /// </summary>
        public bool TryGetDecorationPlacement(int worldX, int worldY, int worldZ, out DecorationClusters.DecorationPlacement placement)
        {
            string key = GetVoxelKey(worldX, worldY, worldZ);
            return _decorationPlacements.TryGetValue(key, out placement);
        }

        /// <summary>
        /// Set biome blend weights for a position
        /// </summary>
        public void SetBiomeBlendWeights(int worldX, int worldZ, Dictionary<BiomeType, float> weights)
        {
            string key = GetBiomeBlendKey(worldX, worldZ);
            _biomeBlendWeights[key] = weights;
        }

        /// <summary>
        /// Get biome blend weights for a position
        /// </summary>
        public Dictionary<BiomeType, float> GetBiomeBlendWeights(int worldX, int worldZ)
        {
            string key = GetBiomeBlendKey(worldX, worldZ);
            
            if (_biomeBlendWeights.TryGetValue(key, out var weights))
            {
                return weights;
            }
            
            // If not found, return a dictionary with just the main biome
            BiomeType biomeType = BiomeRegionGenerator.Instance.GetBiomeType(worldX, worldZ);
            return new Dictionary<BiomeType, float> { { biomeType, 1.0f } };
        }

        /// <summary>
        /// Mark a chunk as generated
        /// </summary>
        public void MarkChunkAsGenerated(Vector2I chunkPos)
        {
            string key = GetChunkKey(chunkPos);
            lock (_generatedChunks)
            {
                _generatedChunks.Add(key);
            }
        }

        /// <summary>
        /// Check if a chunk has been generated
        /// </summary>
        public bool IsChunkGenerated(Vector2I chunkPos)
        {
            string key = GetChunkKey(chunkPos);
            lock (_generatedChunks)
            {
                return _generatedChunks.Contains(key);
            }
        }

        /// <summary>
        /// Clear all voxel data
        /// </summary>
        public void Clear()
        {
            _voxelData.Clear();
            _decorationPlacements.Clear();
            _biomeBlendWeights.Clear();
            lock (_generatedChunks)
            {
                _generatedChunks.Clear();
            }
        }

        // Helper methods for creating dictionary keys
        private string GetVoxelKey(int x, int y, int z)
        {
            return $"{x},{y},{z}";
        }

        private string GetBiomeBlendKey(int x, int z)
        {
            return $"{x},{z}";
        }

        private string GetChunkKey(Vector2I chunkPos)
        {
            return $"{chunkPos.X},{chunkPos.Y}";
        }
    }
}
