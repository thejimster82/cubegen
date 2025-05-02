using Godot;
using System;
using System.Collections.Generic;

namespace CubeGen.World.Common
{
    public static class VoxelScaleHelper
    {
        // Dictionary to map voxel types to their scales
        private static Dictionary<VoxelType, VoxelScale> _voxelScales = new Dictionary<VoxelType, VoxelScale>();

        // Initialize the scale mappings
        static VoxelScaleHelper()
        {
            // Default all voxel types to full scale
            foreach (VoxelType type in Enum.GetValues(typeof(VoxelType)))
            {
                _voxelScales[type] = VoxelScale.Full;
            }

            // Set specific scales for our small voxel types
            _voxelScales[VoxelType.SmallGrass] = VoxelScale.Half;     // 1/2 size
            _voxelScales[VoxelType.TinyGrass] = VoxelScale.Quarter;   // 1/4 size
            _voxelScales[VoxelType.MicroGrass] = VoxelScale.Eighth;   // 1/8 size
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

            // Special case for grass types - position them at the bottom of the block
            if (voxelType == VoxelType.SmallGrass || voxelType == VoxelType.TinyGrass || voxelType == VoxelType.MicroGrass)
            {
                return new Vector3(offset, 0, offset); // Only offset X and Z, keep Y at 0 to rest on ground
            }

            // For other voxel types, center them in all dimensions
            return new Vector3(offset, offset, offset);
        }
    }
}
