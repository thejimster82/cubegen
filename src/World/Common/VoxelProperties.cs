using Godot;
using System;
using System.Collections.Generic;

namespace CubeGen.World.Common
{
    public static class VoxelProperties
    {
        // Dictionary to map voxel types to their categories
        private static Dictionary<VoxelType, VoxelCategory> _voxelCategories = new Dictionary<VoxelType, VoxelCategory>();

        // Dictionary to map voxel types to their scales
        private static Dictionary<VoxelType, VoxelScale> _voxelScales = new Dictionary<VoxelType, VoxelScale>();

        // Initialize the mappings
        static VoxelProperties()
        {
            // Initialize all voxel types to Terrain category and Full scale by default
            foreach (VoxelType type in Enum.GetValues(typeof(VoxelType)))
            {
                _voxelCategories[type] = VoxelCategory.Terrain;
                _voxelScales[type] = VoxelScale.Full;
            }

            // Set specific categories
            _voxelCategories[VoxelType.Air] = VoxelCategory.Terrain;
            _voxelCategories[VoxelType.Water] = VoxelCategory.Fluid;
            _voxelCategories[VoxelType.Wood] = VoxelCategory.Structure;
            _voxelCategories[VoxelType.Leaves] = VoxelCategory.Structure;
            _voxelCategories[VoxelType.Cactus] = VoxelCategory.Structure;
            _voxelCategories[VoxelType.IceBlock] = VoxelCategory.Structure;
            _voxelCategories[VoxelType.SnowLeaves] = VoxelCategory.Structure;

            // Set decoration categories
            _voxelCategories[VoxelType.TallGrass] = VoxelCategory.Decoration;
            _voxelCategories[VoxelType.Flower] = VoxelCategory.Decoration;
            _voxelCategories[VoxelType.Mushroom] = VoxelCategory.Decoration;
            _voxelCategories[VoxelType.Rock] = VoxelCategory.Decoration;
            _voxelCategories[VoxelType.Stick] = VoxelCategory.Decoration;
            _voxelCategories[VoxelType.Seashell] = VoxelCategory.Decoration;

            // Set specific scales - using Half scale instead of Quarter for larger decorations
            _voxelScales[VoxelType.TallGrass] = VoxelScale.Half;
            _voxelScales[VoxelType.Flower] = VoxelScale.Half;
            _voxelScales[VoxelType.Mushroom] = VoxelScale.Half;
            _voxelScales[VoxelType.Rock] = VoxelScale.Half;
            _voxelScales[VoxelType.Stick] = VoxelScale.Half;
            _voxelScales[VoxelType.Seashell] = VoxelScale.Half;

            // Debug output to verify all voxel types are registered
            DebugCheckVoxelTypes();
        }

        // Debug method to check if all voxel types are properly registered
        private static void DebugCheckVoxelTypes()
        {
            // Get all voxel types from the enum
            Array voxelTypes = Enum.GetValues(typeof(VoxelType));

            GD.Print($"VoxelProperties initialized with {voxelTypes.Length} voxel types:");

            // List all registered voxel types
            foreach (VoxelType type in voxelTypes)
            {
                VoxelCategory category = GetCategory(type);
                VoxelScale scale = GetScale(type);
                GD.Print($"  - {type}: Category={category}, Scale={scale}");
            }
        }

        // Get the category for a voxel type
        public static VoxelCategory GetCategory(VoxelType voxelType)
        {
            if (_voxelCategories.TryGetValue(voxelType, out VoxelCategory category))
            {
                return category;
            }

            // Default to Terrain if not found
            return VoxelCategory.Terrain;
        }

        // Check if a voxel type is a decoration
        public static bool IsDecoration(VoxelType voxelType)
        {
            return GetCategory(voxelType) == VoxelCategory.Decoration;
        }

        // Check if a voxel type should have a collider
        public static bool HasCollider(VoxelType voxelType)
        {
            VoxelCategory category = GetCategory(voxelType);
            // Exclude Fluid category (water) from having colliders
            return (category == VoxelCategory.Terrain || category == VoxelCategory.Structure) && category != VoxelCategory.Fluid;
        }

        // Check if a voxel type is water
        public static bool IsWater(VoxelType voxelType)
        {
            return voxelType == VoxelType.Water;
        }

        // Check if a voxel type is transparent
        public static bool IsTransparent(VoxelType voxelType)
        {
            // Air is always transparent
            if (voxelType == VoxelType.Air)
                return true;

            // Water is transparent
            if (voxelType == VoxelType.Water)
                return true;

            // Decoration types are considered transparent for mesh generation
            if (IsDecoration(voxelType))
                return true;

            // Leaves are semi-transparent
            if (voxelType == VoxelType.Leaves || voxelType == VoxelType.SnowLeaves)
                return true;

            // All other blocks are not transparent
            return false;
        }

        // Check if a voxel type is occluding for ambient occlusion calculations
        // This is different from IsSolid - only fully opaque blocks should occlude light
        public static bool IsOccluding(VoxelType voxelType)
        {
            // Air and transparent blocks like Water should not occlude light
            if (voxelType == VoxelType.Air || voxelType == VoxelType.Water)
            {
                return false;
            }

            // Decoration types should not occlude light
            if (IsDecoration(voxelType))
            {
                return false;
            }

            // All other blocks (Stone, Dirt, etc.) should occlude light
            return true;
        }

        // Get the scale factor for a voxel type
        public static float GetScaleFactor(VoxelType voxelType)
        {
            if (_voxelScales.TryGetValue(voxelType, out VoxelScale scale))
            {
                return 1.0f / (int)scale;
            }

            // Default to full scale if not found
            return 1.0f;
        }

        // Get the scale enum for a voxel type
        public static VoxelScale GetScale(VoxelType voxelType)
        {
            if (_voxelScales.TryGetValue(voxelType, out VoxelScale scale))
            {
                return scale;
            }

            // Default to full scale if not found
            return VoxelScale.Full;
        }

        // Calculate the offset for a smaller voxel to center it within a full voxel space
        public static Vector3 GetCenteringOffset(VoxelType voxelType)
        {
            float scaleFactor = GetScaleFactor(voxelType);

            // If it's full scale, no offset needed
            if (scaleFactor >= 1.0f)
            {
                return Vector3.Zero;
            }

            // Calculate offset to center the smaller voxel in the full voxel space
            float offset = (1.0f - scaleFactor) * 0.5f;

            // Special case for decoration types - position them at the bottom of the block
            if (IsDecoration(voxelType))
            {
                return new Vector3(offset, 0, offset); // Only offset X and Z, keep Y at 0 to rest on ground
            }

            // For other voxel types, center them in all dimensions
            return new Vector3(offset, offset, offset);
        }
    }
}
