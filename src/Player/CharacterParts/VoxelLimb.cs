using Godot;
using System;
using CubeGen.World.Common;

namespace CubeGen.Player.CharacterParts
{
    /// <summary>
    /// Voxel-based character limb (arm or leg)
    /// </summary>
    public partial class VoxelLimb : VoxelBodyPart
    {
        public enum LimbType
        {
            Arm,
            Leg
        }

        [Export] public LimbType Type { get; set; } = LimbType.Arm;
        [Export] public bool IsLeft { get; set; } = true;
        [Export] public Color ClothingColor { get; set; } = new Color(0.3f, 0.3f, 0.7f);
        [Export] public Color SkinColor { get; set; } = new Color(0.9f, 0.75f, 0.65f);
        [Export] public bool HasClothing { get; set; } = true;

        public override void _Ready()
        {
            // Set default properties based on limb type
            if (Type == LimbType.Arm)
            {
                PartName = IsLeft ? "LeftArm" : "RightArm";
                Size = new Vector3(0.25f, 0.6f, 0.25f);
            }
            else
            {
                PartName = IsLeft ? "LeftLeg" : "RightLeg";
                Size = new Vector3(0.25f, 0.6f, 0.25f);
            }
            
            // Set base color to skin color
            BaseColor = SkinColor;
            
            // Call base ready method
            base._Ready();
        }

        protected override void InitializeVoxelData()
        {
            // Initialize base voxel data
            base.InitializeVoxelData();
            
            // Get dimensions
            int sizeX = _voxels.GetLength(0);
            int sizeY = _voxels.GetLength(1);
            int sizeZ = _voxels.GetLength(2);
            
            if (HasClothing)
            {
                if (Type == LimbType.Arm)
                {
                    // For arms, add clothing on upper part (sleeve)
                    int sleeveHeight = sizeY / 3;
                    
                    for (int y = sizeY - sleeveHeight; y < sizeY; y++)
                    {
                        for (int x = 0; x < sizeX; x++)
                        {
                            for (int z = 0; z < sizeZ; z++)
                            {
                                // Mark sleeve voxels with a different type
                                // We'll use Leaves as a marker for clothing
                                SetVoxel(x, y, z, VoxelType.Leaves);
                            }
                        }
                    }
                }
                else // Leg
                {
                    // For legs, add clothing on all parts except maybe feet
                    int footHeight = sizeY / 4;
                    
                    for (int y = footHeight; y < sizeY; y++)
                    {
                        for (int x = 0; x < sizeX; x++)
                        {
                            for (int z = 0; z < sizeZ; z++)
                            {
                                // Mark pants voxels with a different type
                                // We'll use Leaves as a marker for clothing
                                SetVoxel(x, y, z, VoxelType.Leaves);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Override to add custom colors for clothing and skin
        /// </summary>
        protected override void AddFace(int x, int y, int z, int face, System.Collections.Generic.List<Vector3> vertices, 
            System.Collections.Generic.List<Vector3> normals, System.Collections.Generic.List<Color> colors, 
            System.Collections.Generic.List<int> indices, System.Collections.Generic.List<Vector3> collisionFaces)
        {
            // Get voxel type
            VoxelType voxelType = GetVoxel(x, y, z);
            
            // Determine color based on voxel type
            Color color = SkinColor; // Default to skin color
            
            if (voxelType == VoxelType.Leaves)
            {
                // This is clothing
                color = ClothingColor;
            }
            
            // Store original color
            Color originalColor = BaseColor;
            
            // Set temporary color for this face
            BaseColor = color;
            
            // Call base method to add the face
            base.AddFace(x, y, z, face, vertices, normals, colors, indices, collisionFaces);
            
            // Restore original color
            BaseColor = originalColor;
        }
    }
}
