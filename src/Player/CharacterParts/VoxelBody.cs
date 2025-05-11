using Godot;
using System;
using CubeGen.World.Common;

namespace CubeGen.Player.CharacterParts
{
    /// <summary>
    /// Voxel-based character body (torso)
    /// </summary>
    public partial class VoxelBody : VoxelBodyPart
    {
        [Export] public Color ShirtColor { get; set; } = new Color(0.2f, 0.4f, 0.8f);
        [Export] public Color PantsColor { get; set; } = new Color(0.3f, 0.3f, 0.7f);
        [Export] public bool HasShirt { get; set; } = true;
        [Export] public bool HasBelt { get; set; } = true;
        [Export] public Color BeltColor { get; set; } = new Color(0.6f, 0.4f, 0.2f);

        public override void _Ready()
        {
            // Set default properties for body
            PartName = "Body";
            Size = new Vector3(0.6f, 0.8f, 0.4f);
            BaseColor = ShirtColor; // Default to shirt color
            
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
            
            // Add shirt and pants sections
            int beltY = sizeY / 2;
            
            // Add belt if enabled
            if (HasBelt)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        // Mark belt voxels with a different type
                        // We'll use Wood as a marker for belt
                        SetVoxel(x, beltY, z, VoxelType.Wood);
                    }
                }
            }
            
            // Mark pants voxels with a different type
            // We'll use Dirt as a marker for pants
            for (int y = 0; y < beltY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        SetVoxel(x, y, z, VoxelType.Dirt);
                    }
                }
            }
        }

        /// <summary>
        /// Override to add custom colors for shirt, pants, and belt
        /// </summary>
        protected override void AddFace(int x, int y, int z, int face, System.Collections.Generic.List<Vector3> vertices, 
            System.Collections.Generic.List<Vector3> normals, System.Collections.Generic.List<Color> colors, 
            System.Collections.Generic.List<int> indices, System.Collections.Generic.List<Vector3> collisionFaces)
        {
            // Get voxel type
            VoxelType voxelType = GetVoxel(x, y, z);
            
            // Determine color based on voxel type
            Color color = ShirtColor; // Default to shirt color
            
            if (voxelType == VoxelType.Dirt)
            {
                // This is pants
                color = PantsColor;
            }
            else if (voxelType == VoxelType.Wood)
            {
                // This is belt
                color = BeltColor;
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
