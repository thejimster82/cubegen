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

        // Decoration voxels (no colliders)
        TallGrass,    // 1/4 size tall grass
        Flower,       // 1/4 size flower
        Mushroom,     // 1/4 size mushroom
        Rock,         // 1/4 size rock
        Stick,        // 1/4 size stick
        Seashell      // 1/4 size seashell
    }

    // Enum to represent different voxel scales
    public enum VoxelScale
    {
        Full = 1,     // Regular size (1x)
        Half = 2,     // Half size (1/2x)
        Quarter = 4,  // Quarter size (1/4x)
        Eighth = 8    // Eighth size (1/8x)
    }

    // Enum to classify voxel types
    public enum VoxelCategory
    {
        Terrain,      // Regular terrain blocks (with colliders)
        Fluid,        // Fluid blocks like water (special physics)
        Decoration,   // Decorative elements (no colliders)
        Structure      // Structure elements like wood, leaves (with colliders)
    }
}
