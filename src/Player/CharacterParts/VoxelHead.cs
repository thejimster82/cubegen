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
            Size = new Vector3(0.6f, 0.6f, 0.6f); // Slightly larger head for stylized look
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

            // Create a more stylized head shape by rounding corners
            RoundHeadCorners(sizeX, sizeY, sizeZ);

            // Add stylized rectangular eyes (blue like in the reference)
            int eyeY = sizeY / 2;
            int eyeZ = sizeZ - 1; // Front of head

            // Eye dimensions
            int eyeWidth = Mathf.Max(1, sizeX / 8);
            int eyeHeight = Mathf.Max(1, sizeY / 6);

            // Left eye (rectangular blue eye)
            int leftEyeX = sizeX / 3;
            for (int x = leftEyeX; x < leftEyeX + eyeWidth; x++)
            {
                for (int y = eyeY; y < eyeY + eyeHeight; y++)
                {
                    SetVoxel(x, y, eyeZ, VoxelType.Air);
                }
            }

            // Right eye (rectangular blue eye)
            int rightEyeX = (2 * sizeX) / 3;
            for (int x = rightEyeX; x < rightEyeX + eyeWidth; x++)
            {
                for (int y = eyeY; y < eyeY + eyeHeight; y++)
                {
                    SetVoxel(x, y, eyeZ, VoxelType.Air);
                }
            }

            // Add a simple mouth
            int mouthY = sizeY / 3;
            int mouthWidth = sizeX / 4;
            int mouthX = sizeX / 2 - mouthWidth / 2;

            for (int x = mouthX; x < mouthX + mouthWidth; x++)
            {
                SetVoxel(x, mouthY, eyeZ, VoxelType.Air);
            }

            // Add hair if enabled
            if (HasHair && HairStyle != HairStyle.Bald)
            {
                AddHair();
            }
        }

        /// <summary>
        /// Round the corners of the head for a more stylized look
        /// </summary>
        private void RoundHeadCorners(int sizeX, int sizeY, int sizeZ)
        {
            // Calculate corner rounding threshold
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
                    // Create a stylized short hair similar to the reference image
                    // This creates a blocky hairstyle with some variation

                    // Base hair layer on top
                    for (int x = 0; x < sizeX; x++)
                    {
                        for (int z = 0; z < sizeZ; z++)
                        {
                            // Skip corners for a rounded look
                            int distX = Mathf.Min(x, sizeX - 1 - x);
                            int distZ = Mathf.Min(z, sizeZ - 1 - z);

                            if (distX + distZ > 1) // Skip extreme corners
                            {
                                // Set hair voxel
                                SetVoxel(x, hairY, z, VoxelType.Leaves);
                            }
                        }
                    }

                    // Add some height to the front hair (bangs)
                    int bangWidth = sizeX / 2;
                    int bangStart = sizeX / 4;
                    for (int x = bangStart; x < bangStart + bangWidth; x++)
                    {
                        for (int z = sizeZ / 2; z < sizeZ; z++)
                        {
                            SetVoxel(x, hairY + 1, z, VoxelType.Leaves);
                        }
                    }

                    // Add side hair tufts
                    for (int y = hairY - 1; y >= hairY - 3; y--)
                    {
                        // Left side hair
                        SetVoxel(1, y, sizeZ / 2, VoxelType.Leaves);
                        SetVoxel(1, y, sizeZ / 2 + 1, VoxelType.Leaves);

                        // Right side hair
                        SetVoxel(sizeX - 2, y, sizeZ / 2, VoxelType.Leaves);
                        SetVoxel(sizeX - 2, y, sizeZ / 2 + 1, VoxelType.Leaves);
                    }
                    break;

                case HairStyle.Long:
                    // Create a stylized long hair that extends down

                    // Top hair layer
                    for (int x = 0; x < sizeX; x++)
                    {
                        for (int z = 0; z < sizeZ; z++)
                        {
                            // Skip corners for a rounded look
                            int distX = Mathf.Min(x, sizeX - 1 - x);
                            int distZ = Mathf.Min(z, sizeZ - 1 - z);

                            if (distX + distZ > 1) // Skip extreme corners
                            {
                                // Set hair voxel
                                SetVoxel(x, hairY, z, VoxelType.Leaves);
                            }
                        }
                    }

                    // Add some height to the front hair (bangs)
                    bangWidth = sizeX / 2;
                    bangStart = sizeX / 4;
                    for (int x = bangStart; x < bangStart + bangWidth; x++)
                    {
                        for (int z = sizeZ / 2; z < sizeZ; z++)
                        {
                            SetVoxel(x, hairY + 1, z, VoxelType.Leaves);
                        }
                    }

                    // Add long hair down the back and sides
                    for (int y = hairY - 1; y >= hairY - 5; y--)
                    {
                        // Back hair (full width)
                        for (int x = 1; x < sizeX - 1; x++)
                        {
                            SetVoxel(x, y, 1, VoxelType.Leaves);
                        }

                        // Side hair
                        for (int z = 1; z < sizeZ / 2; z++)
                        {
                            // Left side
                            SetVoxel(1, y, z, VoxelType.Leaves);

                            // Right side
                            SetVoxel(sizeX - 2, y, z, VoxelType.Leaves);
                        }
                    }
                    break;

                case HairStyle.Mohawk:
                    // Create a stylized mohawk with more volume

                    // Base mohawk strip
                    int mohawkWidth = Mathf.Max(1, sizeX / 4);
                    int mohawkStart = (sizeX - mohawkWidth) / 2;

                    // Add the mohawk strip with height
                    for (int x = mohawkStart; x < mohawkStart + mohawkWidth; x++)
                    {
                        for (int z = 1; z < sizeZ - 1; z++)
                        {
                            // Base layer
                            SetVoxel(x, hairY, z, VoxelType.Leaves);

                            // Add height
                            SetVoxel(x, hairY + 1, z, VoxelType.Leaves);

                            // Add more height in the middle
                            if (x == mohawkStart + mohawkWidth / 2 && z > sizeZ / 4 && z < 3 * sizeZ / 4)
                            {
                                SetVoxel(x, hairY + 2, z, VoxelType.Leaves);
                            }
                        }
                    }
                    break;

                // Add a new blonde hair style like in the reference image
                case HairStyle.Blonde:
                    // Create a stylized blonde hairstyle similar to the reference

                    // Top hair layer
                    for (int x = 0; x < sizeX; x++)
                    {
                        for (int z = 0; z < sizeZ; z++)
                        {
                            // Skip corners for a rounded look
                            int distX = Mathf.Min(x, sizeX - 1 - x);
                            int distZ = Mathf.Min(z, sizeZ - 1 - z);

                            if (distX + distZ > 1) // Skip extreme corners
                            {
                                // Set hair voxel
                                SetVoxel(x, hairY, z, VoxelType.Leaves);
                            }
                        }
                    }

                    // Add volume on top
                    for (int x = 1; x < sizeX - 1; x++)
                    {
                        for (int z = 1; z < sizeZ - 1; z++)
                        {
                            SetVoxel(x, hairY + 1, z, VoxelType.Leaves);
                        }
                    }

                    // Add side hair tufts (like in the reference)
                    for (int y = hairY; y >= hairY - 2; y--)
                    {
                        // Left side hair tuft
                        SetVoxel(0, y, sizeZ / 2, VoxelType.Leaves);
                        SetVoxel(0, y, sizeZ / 2 + 1, VoxelType.Leaves);
                        SetVoxel(0, y, sizeZ / 2 - 1, VoxelType.Leaves);

                        // Right side hair tuft
                        SetVoxel(sizeX - 1, y, sizeZ / 2, VoxelType.Leaves);
                        SetVoxel(sizeX - 1, y, sizeZ / 2 + 1, VoxelType.Leaves);
                        SetVoxel(sizeX - 1, y, sizeZ / 2 - 1, VoxelType.Leaves);
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
