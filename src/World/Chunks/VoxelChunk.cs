using Godot;
using System;
using CubeGen.World.Common;

public class VoxelChunk
{
    public Vector2I Position { get; private set; }
    public int Size { get; private set; }
    public int Height { get; private set; }
    public float Scale { get; private set; } = 1.0f;

    private VoxelType[][][] _voxels;

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
}
