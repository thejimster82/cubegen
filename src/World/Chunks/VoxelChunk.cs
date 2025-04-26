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
        // Check if this is at or near a chunk boundary
        bool isNearBoundary = (x <= 1 || x >= Size - 2 || z <= 1 || z >= Size - 2);

        // For positions near chunk boundaries, always return consistent results
        // to ensure identical mesh generation on both sides of the boundary
        if (isNearBoundary)
        {
            // If within bounds, check directly
            if (IsInBounds(x, y, z))
            {
                VoxelType type = _voxels[x][y][z];
                bool isSolid = (type != VoxelType.Air && type != VoxelType.Water);

                // For the exact boundary voxels, force them to match their neighbors
                // This ensures consistent mesh generation across chunk boundaries
                if (x == 0 || x == Size - 1 || z == 0 || z == Size - 1)
                {
                    // Get the type of the neighboring voxel inside the chunk
                    int nx = x == 0 ? 1 : (x == Size - 1 ? Size - 2 : x);
                    int nz = z == 0 ? 1 : (z == Size - 1 ? Size - 2 : z);

                    VoxelType neighborType = _voxels[nx][y][nz];
                    bool neighborSolid = (neighborType != VoxelType.Air && neighborType != VoxelType.Water);

                    // Use the neighbor's solidity to ensure consistency
                    return neighborSolid;
                }

                return isSolid;
            }

            // For out-of-bounds positions near chunk boundaries, always assume solid
            return true;
        }

        // For normal in-bounds voxels, check directly
        if (IsInBounds(x, y, z))
        {
            VoxelType type = _voxels[x][y][z];
            return type != VoxelType.Air && type != VoxelType.Water;
        }

        // For other out-of-bounds positions, assume solid at horizontal boundaries
        if (x < 0 || x >= Size || z < 0 || z >= Size)
        {
            return true;
        }

        // For y out of bounds, use normal air behavior
        return false;
    }

    // Get world position of this chunk
    public Vector3 GetWorldPosition()
    {
        return new Vector3(Position.X * Size * Scale, 0, Position.Y * Size * Scale);
    }
}
