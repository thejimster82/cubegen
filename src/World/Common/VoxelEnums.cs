using Godot;
using System;

namespace CubeGen.World.Common
{
    public enum BiomeType
    {
        Plains,
        Forest,
        Desert,
        Mountains,
        Tundra
    }

    public enum VoxelType
    {
        Air,
        Grass,
        Dirt,
        Stone,
        Sand,
        Wood,
        Leaves,
        Water,
        Snow,
        Bedrock,
        Cloud,
        SmallGrass,   // 1/2 size grass
        TinyGrass,    // 1/4 size grass
        MicroGrass    // 1/8 size grass
    }

    // Enum to represent different voxel scales
    public enum VoxelScale
    {
        Full = 1,     // Regular size (1x)
        Half = 2,     // Half size (1/2x)
        Quarter = 4,  // Quarter size (1/4x)
        Eighth = 8    // Eighth size (1/8x)
    }
}
