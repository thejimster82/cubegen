using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;
using CubeGen.World.Generation;

/// <summary>
/// Represents a chunk of voxels in the world
/// </summary>
public class VoxelChunk
{
    public Vector2I Position { get; private set; }
    public int Size { get; private set; }
    public int Height { get; private set; }
    public float Scale { get; private set; } = 1.0f;

    private VoxelType[][][] _voxels;

    // Dictionary to store out-of-bounds voxels
    // Key is a string in the format "x,y,z", value is the voxel type
    private Dictionary<string, VoxelType> _outOfBoundsVoxels = new();

    // Dictionary to store decoration placement information
    // Key is a string in the format "x,y,z", value is the placement data
    private Dictionary<string, DecorationClusters.DecorationPlacement> _decorationPlacements = new();

    // Dictionary to store biome blend weights for each voxel
    // Key is a string in the format "x,z", value is a dictionary mapping BiomeType to blend weight (0.0-1.0)
    private Dictionary<string, Dictionary<BiomeType, float>> _biomeBlendWeights = new();

    public VoxelChunk(int size, int height, Vector2I position, float scale = 1.0f)
    {
        Size = size;
        Height = height;
        Position = position;
        Scale = scale;

        // Initialize 3D array of voxels
        _voxels = new VoxelType[Size][][];
        for (int x = 0; x < Size; x++)
        {
            _voxels[x] = new VoxelType[Height][];
            for (int y = 0; y < Height; y++)
            {
                _voxels[x][y] = new VoxelType[Size];
                for (int z = 0; z < Size; z++)
                {
                    _voxels[x][y][z] = VoxelType.Air; // Initialize all voxels as air
                }
            }
        }
    }

    // Helper method to create a key for the out-of-bounds voxels dictionary
    private static string GetVoxelKey(int x, int y, int z)
    {
        return $"{x},{y},{z}";
    }

    public VoxelType GetVoxel(int x, int y, int z)
    {
        if (IsInBounds(x, y, z))
        {
            return _voxels[x][y][z];
        }

        // Check if we have an out-of-bounds voxel at this position
        string key = GetVoxelKey(x, y, z);
        if (_outOfBoundsVoxels.TryGetValue(key, out VoxelType voxelType))
        {
            return voxelType;
        }

        return VoxelType.Air; // Return air for out of bounds if not explicitly set
    }

    public void SetVoxel(int x, int y, int z, VoxelType type)
    {
        if (IsInBounds(x, y, z))
        {
            _voxels[x][y][z] = type;
        }
        else
        {
            // Store out-of-bounds voxel in the dictionary
            string key = GetVoxelKey(x, y, z);
            _outOfBoundsVoxels[key] = type;
        }
    }

    public bool IsInBounds(int x, int y, int z)
    {
        return x >= 0 && x < Size && y >= 0 && y < Height && z >= 0 && z < Size;
    }

    public bool IsVoxelSolid(int x, int y, int z)
    {
        // Get the voxel type (handles both in-bounds and out-of-bounds)
        VoxelType type = GetVoxel(x, y, z);

        // Don't consider decoration types as solid for visibility calculations
        if (VoxelProperties.IsDecoration(type))
        {
            return false;
        }

        // For out-of-bounds positions in the Y direction that aren't explicitly set
        if (!IsInBounds(x, y, z) && !_outOfBoundsVoxels.ContainsKey(GetVoxelKey(x, y, z)))
        {
            if (y < 0 || y >= Height)
            {
                // Below the chunk is solid (ground), above is air
                return y < 0;
            }
        }

        return type != VoxelType.Air && type != VoxelType.Water;
    }

    // Check if a voxel is water
    public bool IsVoxelWater(int x, int y, int z)
    {
        // Get the voxel type (handles both in-bounds and out-of-bounds)
        return GetVoxel(x, y, z) == VoxelType.Water;
    }

    // Check if a voxel is occluding for ambient occlusion calculations
    public bool IsVoxelOccluding(int x, int y, int z)
    {
        // Get the voxel type (handles both in-bounds and out-of-bounds)
        VoxelType type = GetVoxel(x, y, z);

        // For out-of-bounds positions in the Y direction that aren't explicitly set
        if (!IsInBounds(x, y, z) && !_outOfBoundsVoxels.ContainsKey(GetVoxelKey(x, y, z)))
        {
            if (y < 0 || y >= Height)
            {
                // Below the chunk is occluding (ground), above is not
                return y < 0;
            }
        }

        return VoxelProperties.IsOccluding(type);
    }

    // Get world position of this chunk
    public Vector3 GetWorldPosition()
    {
        return new Vector3(Position.X * Size * Scale, 0, Position.Y * Size * Scale);
    }

    // Helper method to create a key for the decoration dictionary
    private static string GetDecorationKey(int x, int y, int z)
    {
        return $"{x},{y},{z}";
    }

    // Set decoration placement information for a voxel
    public void SetDecorationPlacement(int x, int y, int z, DecorationClusters.DecorationPlacement placement)
    {
        // Allow setting decoration placement for any voxel, including out-of-bounds
        string key = GetDecorationKey(x, y, z);
        _decorationPlacements[key] = placement;
    }

    // Get decoration placement information for a voxel
    public bool TryGetDecorationPlacement(int x, int y, int z, out DecorationClusters.DecorationPlacement placement)
    {
        // Allow getting decoration placement for any voxel, including out-of-bounds
        string key = GetDecorationKey(x, y, z);
        return _decorationPlacements.TryGetValue(key, out placement);
    }

    // Check if a voxel has decoration placement information
    public bool HasDecorationPlacement(int x, int y, int z)
    {
        // Allow checking decoration placement for any voxel, including out-of-bounds
        string key = GetDecorationKey(x, y, z);
        return _decorationPlacements.ContainsKey(key);
    }

    // Helper method to create a key for the biome blend weights dictionary
    private static string GetBiomeBlendKey(int x, int z)
    {
        return $"{x},{z}";
    }

    /// <summary>
    /// Sets the blend weight for a specific biome at a voxel position
    /// </summary>
    /// <param name="x">X coordinate (can be outside chunk boundaries)</param>
    /// <param name="z">Z coordinate (can be outside chunk boundaries)</param>
    /// <param name="biomeType">The biome type</param>
    /// <param name="weight">The blend weight (0.0-1.0)</param>
    public void SetBiomeBlendWeight(int x, int z, BiomeType biomeType, float weight)
    {
        // Allow setting biome blend weights for any position, including out-of-bounds
        string key = GetBiomeBlendKey(x, z);

        // Initialize the dictionary for this position if it doesn't exist
        if (!_biomeBlendWeights.TryGetValue(key, out var biomeWeights))
        {
            biomeWeights = new();
            _biomeBlendWeights[key] = biomeWeights;
        }

        // Set the blend weight
        biomeWeights[biomeType] = weight;
    }

    /// <summary>
    /// Gets the blend weight for a specific biome at a voxel position
    /// </summary>
    /// <param name="x">X coordinate (can be outside chunk boundaries)</param>
    /// <param name="z">Z coordinate (can be outside chunk boundaries)</param>
    /// <param name="biomeType">The biome type</param>
    /// <returns>The blend weight (0.0-1.0), or 0.0 if not set</returns>
    public float GetBiomeBlendWeight(int x, int z, BiomeType biomeType)
    {
        // Allow getting biome blend weights for any position, including out-of-bounds
        string key = GetBiomeBlendKey(x, z);

        if (_biomeBlendWeights.TryGetValue(key, out var weights) &&
            weights.TryGetValue(biomeType, out var weight))
        {
            return weight;
        }

        return 0.0f; // Default to no influence if not set
    }

    /// <summary>
    /// Gets all biome blend weights for a voxel position
    /// </summary>
    /// <param name="x">X coordinate (can be outside chunk boundaries)</param>
    /// <param name="z">Z coordinate (can be outside chunk boundaries)</param>
    /// <returns>Dictionary mapping BiomeType to blend weight, or null if none set</returns>
    public Dictionary<BiomeType, float> GetAllBiomeBlendWeights(int x, int z)
    {
        // Allow getting all biome blend weights for any position, including out-of-bounds
        string key = GetBiomeBlendKey(x, z);

        if (_biomeBlendWeights.TryGetValue(key, out var weights))
        {
            return new Dictionary<BiomeType, float>(weights); // Return a copy to prevent modification
        }

        return null;
    }

    /// <summary>
    /// Checks if a voxel position has any biome blend weights set
    /// </summary>
    /// <param name="x">X coordinate (can be outside chunk boundaries)</param>
    /// <param name="z">Z coordinate (can be outside chunk boundaries)</param>
    /// <returns>True if any blend weights are set, false otherwise</returns>
    public bool HasBiomeBlendWeights(int x, int z)
    {
        // Allow checking biome blend weights for any position, including out-of-bounds
        string key = GetBiomeBlendKey(x, z);

        if (_biomeBlendWeights.TryGetValue(key, out var weights))
        {
            return weights.Count > 0;
        }

        return false;
    }
}
