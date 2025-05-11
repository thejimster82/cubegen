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
        [Export] public Color ShirtColor { get; set; } = new Color(1.0f, 0.5f, 0.0f); // Orange shirt like in the image
        [Export] public Color PantsColor { get; set; } = new Color(0.4f, 0.3f, 0.6f); // Purple pants like in the image
        [Export] public bool HasShirt { get; set; } = true;
        [Export] public bool HasBelt { get; set; } = false; // No visible belt in the pixel character
        [Export] public Color BeltColor { get; set; } = new Color(0.6f, 0.4f, 0.2f);

        public override void _Ready()
        {
            // Set default properties for body
            PartName = "Body";
            Size = new Vector3(0.5f, 0.4f, 0.3f); // Shorter body for pixel character proportions
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

            // Create a more stylized body shape
            CreateStylizedBodyShape(sizeX, sizeY, sizeZ);

            // Add shirt and pants sections
            int beltY = sizeY / 2;

            // Add belt if enabled
            if (HasBelt)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        // Check if this voxel is solid (not air)
                        if (GetVoxel(x, beltY, z) != VoxelType.Air)
                        {
                            // Mark belt voxels with a different type
                            // We'll use Wood as a marker for belt
                            SetVoxel(x, beltY, z, VoxelType.Wood);
                        }
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
                        // Check if this voxel is solid (not air)
                        if (GetVoxel(x, y, z) != VoxelType.Air)
                        {
                            SetVoxel(x, y, z, VoxelType.Dirt);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Create a blocky body shape for the pixel character
        /// </summary>
        private void CreateStylizedBodyShape(int sizeX, int sizeY, int sizeZ)
        {
            // For the pixel character, we want a more blocky body with minimal rounding
            // Only round the extreme corners to maintain the blocky look
            int cornerThreshold = Mathf.Max(1, sizeX / 10);

            // Round only the extreme corners by setting corner voxels to air
            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        // Calculate distance from corners
                        int distX = Mathf.Min(x, sizeX - 1 - x);
                        int distY = Mathf.Min(y, sizeY - 1 - y);
                        int distZ = Mathf.Min(z, sizeZ - 1 - z);

                        // If this is an extreme corner voxel, set to air
                        if (distX < cornerThreshold && distY < cornerThreshold && distZ < cornerThreshold)
                        {
                            // Calculate a simple distance metric
                            int distSum = distX + distY + distZ;
                            if (distSum < cornerThreshold / 2) // Much smaller threshold for more blockiness
                            {
                                SetVoxel(x, y, z, VoxelType.Air);
                            }
                        }
                    }
                }
            }

            // No taper at the waist for the pixel character - we want a straight blocky body
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
