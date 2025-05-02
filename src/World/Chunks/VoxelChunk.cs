using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;
using CubeGen.World.Generation;

public class VoxelChunk
{
    public Vector2I Position { get; private set; }
    public int Size { get; private set; }
    public int Height { get; private set; }
    public float Scale { get; private set; } = 1.0f;

    private VoxelType[][][] _voxels;

    // Dictionary to store decoration placement information
    // Key is a string in the format "x,y,z", value is the placement data
    private Dictionary<string, DecorationClusters.DecorationPlacement> _decorationPlacements = new Dictionary<string, DecorationClusters.DecorationPlacement>();

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

    public VoxelType GetVoxel(int x, int y, int z)
    {
        if (IsInBounds(x, y, z))
        {
            return _voxels[x][y][z];
        }
        return VoxelType.Air; // Return air for out of bounds
    }

    public void SetVoxel(int x, int y, int z, VoxelType type)
    {
        if (IsInBounds(x, y, z))
        {
            _voxels[x][y][z] = type;
        }
    }

    private bool IsInBounds(int x, int y, int z)
    {
        return x >= 0 && x < Size && y >= 0 && y < Height && z >= 0 && z < Size;
    }

    public bool IsVoxelSolid(int x, int y, int z)
    {
        // First, handle in-bounds voxels directly
        if (IsInBounds(x, y, z))
        {
            VoxelType type = _voxels[x][y][z];

            // Don't consider decoration types as solid for visibility calculations
            if (VoxelProperties.IsDecoration(type))
            {
                return false;
            }

            return type != VoxelType.Air && type != VoxelType.Water;
        }

        // For out-of-bounds positions in the Y direction
        if (y < 0 || y >= Height)
        {
            // Below the chunk is solid (ground), above is air
            return y < 0;
        }

        // For out-of-bounds positions in X and Z, we need to check if this would be
        // a neighboring chunk. For now, we'll assume air at chunk boundaries.
        // In a full implementation, you would query the world for the neighboring chunk.
        return false;
    }

    // Get world position of this chunk
    public Vector3 GetWorldPosition()
    {
        return new Vector3(Position.X * Size * Scale, 0, Position.Y * Size * Scale);
    }

    // Helper method to create a key for the decoration dictionary
    private string GetDecorationKey(int x, int y, int z)
    {
        return $"{x},{y},{z}";
    }

    // Set decoration placement information for a voxel
    public void SetDecorationPlacement(int x, int y, int z, DecorationClusters.DecorationPlacement placement)
    {
        if (IsInBounds(x, y, z))
        {
            string key = GetDecorationKey(x, y, z);
            _decorationPlacements[key] = placement;
        }
    }

    // Get decoration placement information for a voxel
    public bool TryGetDecorationPlacement(int x, int y, int z, out DecorationClusters.DecorationPlacement placement)
    {
        placement = new DecorationClusters.DecorationPlacement(VoxelType.Air, Vector2.Zero, 0, 1.0f);

        if (IsInBounds(x, y, z))
        {
            string key = GetDecorationKey(x, y, z);
            return _decorationPlacements.TryGetValue(key, out placement);
        }

        return false;
    }

    // Check if a voxel has decoration placement information
    public bool HasDecorationPlacement(int x, int y, int z)
    {
        if (IsInBounds(x, y, z))
        {
            string key = GetDecorationKey(x, y, z);
            return _decorationPlacements.ContainsKey(key);
        }

        return false;
    }
}
