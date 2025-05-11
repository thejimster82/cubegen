using Godot;
using System;
using CubeGen.World.Common;

namespace CubeGen.Player.CharacterParts
{
    /// <summary>
    /// Voxel-based character head
    /// </summary>
    public partial class VoxelHead : VoxelBodyPart
    {
        [Export] public Color EyeColor { get; set; } = new Color(0.2f, 0.2f, 0.8f);
        [Export] public Color HairColor { get; set; } = new Color(0.6f, 0.4f, 0.2f);
        [Export] public bool HasHair { get; set; } = true;
        [Export] public HairStyle HairStyle { get; set; } = HairStyle.Short;

        public override void _Ready()
        {
            // Set default properties for head
            PartName = "Head";
            Size = new Vector3(0.5f, 0.5f, 0.5f);
            BaseColor = new Color(0.9f, 0.75f, 0.65f); // Skin tone

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

            // Add eyes
            int eyeY = sizeY / 2;
            int eyeZ = sizeZ - 1; // Front of head

            // Left eye
            int leftEyeX = sizeX / 3;
            SetVoxel(leftEyeX, eyeY, eyeZ, VoxelType.Air);

            // Right eye
            int rightEyeX = (2 * sizeX) / 3;
            SetVoxel(rightEyeX, eyeY, eyeZ, VoxelType.Air);

            // Add hair if enabled
            if (HasHair && HairStyle != HairStyle.Bald)
            {
                AddHair();
            }
        }

        /// <summary>
        /// Add hair to the head based on the selected style
        /// </summary>
        private void AddHair()
        {
            // Get dimensions
            int sizeX = _voxels.GetLength(0);
            int sizeY = _voxels.GetLength(1);
            int sizeZ = _voxels.GetLength(2);

            // Hair Y position (top of head)
            int hairY = sizeY - 1;

            switch (HairStyle)
            {
                case HairStyle.Short:
                    // Add a layer of hair on top
                    for (int x = 0; x < sizeX; x++)
                    {
                        for (int z = 0; z < sizeZ; z++)
                        {
                            // Set hair voxel
                            SetVoxel(x, hairY, z, VoxelType.Leaves); // Using leaves as hair type
                        }
                    }
                    break;

                case HairStyle.Long:
                    // Add a layer of hair on top and sides
                    for (int x = 0; x < sizeX; x++)
                    {
                        for (int z = 0; z < sizeZ; z++)
                        {
                            // Set hair voxel on top
                            SetVoxel(x, hairY, z, VoxelType.Leaves);
                        }
                    }

                    // Add hair on sides (back and sides, not front)
                    for (int y = hairY - 1; y >= hairY - 2; y--)
                    {
                        for (int x = 0; x < sizeX; x++)
                        {
                            // Back hair
                            SetVoxel(x, y, 0, VoxelType.Leaves);

                            // Side hair (left)
                            SetVoxel(0, y, sizeZ / 2, VoxelType.Leaves);

                            // Side hair (right)
                            SetVoxel(sizeX - 1, y, sizeZ / 2, VoxelType.Leaves);
                        }
                    }
                    break;

                case HairStyle.Mohawk:
                    // Add a strip of hair down the middle
                    int midX = sizeX / 2;
                    for (int z = 0; z < sizeZ; z++)
                    {
                        // Set hair voxel in middle
                        SetVoxel(midX, hairY, z, VoxelType.Leaves);

                        // Add height to mohawk
                        SetVoxel(midX, hairY + 1, z, VoxelType.Leaves);
                    }
                    break;
            }
        }

        /// <summary>
        /// Override to add custom colors for eyes and hair
        /// </summary>
        protected override void AddFace(int x, int y, int z, int face, System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<Vector3> normals, System.Collections.Generic.List<Color> colors,
            System.Collections.Generic.List<int> indices, System.Collections.Generic.List<Vector3> collisionFaces)
        {
            // Get voxel type
            VoxelType voxelType = GetVoxel(x, y, z);

            // Determine color based on voxel type and position
            Color color = BaseColor;

            // Check if this is an eye voxel
            int sizeZ = _voxels.GetLength(2);
            if (z == sizeZ - 1 && (voxelType == VoxelType.Air))
            {
                // This is an eye - use eye color
                color = EyeColor;

                // For eyes, we want to render a colored voxel instead of air
                voxelType = VoxelType.Stone;

                // Set voxel to non-air so it renders
                _voxels[x, y, z] = voxelType;
            }
            else if (voxelType == VoxelType.Leaves)
            {
                // This is hair - use hair color
                color = HairColor;
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
