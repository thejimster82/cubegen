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
            Size = new Vector3(0.5f, 0.6f, 0.3f); // Keep the same overall size
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
        /// Create a more stylized body shape by rounding corners and adding details
        /// </summary>
        private void CreateStylizedBodyShape(int sizeX, int sizeY, int sizeZ)
        {
            // Round the corners for a more stylized look
            int cornerThreshold = Mathf.Max(1, sizeX / 6);

            // Round the corners by setting corner voxels to air
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

                        // If this is a corner voxel, set to air
                        if (distX < cornerThreshold && distY < cornerThreshold && distZ < cornerThreshold)
                        {
                            // Calculate a simple distance metric
                            int distSum = distX + distY + distZ;
                            if (distSum < cornerThreshold)
                            {
                                SetVoxel(x, y, z, VoxelType.Air);
                            }
                        }
                    }
                }
            }

            // Create a slight taper at the waist (middle of the body)
            int waistY = sizeY / 2;
            int taperAmount = Mathf.Max(1, sizeX / 8);

            // Taper the sides at the waist
            for (int x = 0; x < taperAmount; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    // Left side taper
                    SetVoxel(x, waistY, z, VoxelType.Air);

                    // Right side taper
                    SetVoxel(sizeX - 1 - x, waistY, z, VoxelType.Air);
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
