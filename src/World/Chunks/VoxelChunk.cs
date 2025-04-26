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
        VoxelType type = GetVoxel(x, y, z);
        return type != VoxelType.Air && type != VoxelType.Water;
    }

    // Get world position of this chunk
    public Vector3 GetWorldPosition()
    {
        return new Vector3(Position.X * Size * Scale, 0, Position.Y * Size * Scale);
    }
}
