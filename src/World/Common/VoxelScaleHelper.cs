using Godot;
using System;
using System.Collections.Generic;

namespace CubeGen.World.Common
{
    // This class is deprecated - use VoxelProperties instead
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
        }

        // Get the scale factor for a voxel type
        public static float GetScaleFactor(VoxelType voxelType)
        {
            // Forward to VoxelProperties
            return VoxelProperties.GetScaleFactor(voxelType);
        }

        // Get the scale enum for a voxel type
        public static VoxelScale GetScale(VoxelType voxelType)
        {
            // Forward to VoxelProperties
            return VoxelProperties.GetScale(voxelType);
        }

        // Calculate the offset for a smaller voxel to center it within a full voxel space
        public static Vector3 GetCenteringOffset(VoxelType voxelType)
        {
            // Forward to VoxelProperties
            return VoxelProperties.GetCenteringOffset(voxelType);
        }
    }
}
