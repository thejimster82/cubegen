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
        [Export] public Color EyeColor { get; set; } = new Color(0.0f, 0.0f, 0.0f); // Black eyes like in the image
        [Export] public Color HairColor { get; set; } = new Color(1.0f, 0.5f, 0.0f); // Orange hair like in the image
        [Export] public Color FaceColor { get; set; } = new Color(1.0f, 1.0f, 0.8f); // Cream/white face color
        [Export] public bool HasHair { get; set; } = true;
        [Export] public HairStyle HairStyle { get; set; } = HairStyle.PixelStyle; // New style for the pixel character

        public override void _Ready()
        {
            // Set default properties for head
            PartName = "Head";
            Size = new Vector3(0.45f, 0.45f, 0.45f); // Reduced head size by 25%
            BaseColor = FaceColor; // Use the face color for the base

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

            // Add pixel-style square eyes (black like in the image)
            int eyeY = sizeY / 2;
            int eyeZ = sizeZ - 1; // Front of head

            // Eye dimensions - make them square and small like in the pixel image
            int eyeSize = Mathf.Max(1, sizeX / 10);

            // Left eye (square black eye)
            int leftEyeX = sizeX / 3;
            for (int x = leftEyeX; x < leftEyeX + eyeSize; x++)
            {
                for (int y = eyeY; y < eyeY + eyeSize; y++)
                {
                    SetVoxel(x, y, eyeZ, VoxelType.Air);
                }
            }

            // Right eye (square black eye)
            int rightEyeX = (2 * sizeX) / 3;
            for (int x = rightEyeX; x < rightEyeX + eyeSize; x++)
            {
                for (int y = eyeY; y < eyeY + eyeSize; y++)
                {
                    SetVoxel(x, y, eyeZ, VoxelType.Air);
                }
            }

            // The pixel character doesn't appear to have a visible mouth, so we'll skip adding one

            // Add hair if enabled
            if (HasHair && HairStyle != HairStyle.Bald)
            {
                AddHair();
            }
        }

        /// <summary>
        /// Shape the head to be more blocky like in the pixel image
        /// </summary>
        private void RoundHeadCorners(int sizeX, int sizeY, int sizeZ)
        {
            // For the pixel character, we want a more blocky head with minimal rounding
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

                // Add a new pixel style hair like in the image
                case HairStyle.PixelStyle:
                    // Create a blocky orange hair style with white face pattern like in the image

                    // Create the main orange hair block covering most of the head
                    for (int x = 0; x < sizeX; x++)
                    {
                        for (int y = 0; y < sizeY; y++)
                        {
                            for (int z = 0; z < sizeZ; z++)
                            {
                                // Skip the face area (front of the head)
                                if (z == sizeZ - 1)
                                {
                                    continue;
                                }

                                // Set all non-face voxels to hair
                                if (GetVoxel(x, y, z) != VoxelType.Air)
                                {
                                    SetVoxel(x, y, z, VoxelType.Leaves);
                                }
                            }
                        }
                    }

                    // Create the white face pattern on the front
                    // The face is a T-shaped white area as seen in the image
                    int faceZ = sizeZ - 1;

                    // Define the T-shape pattern for the face
                    // Vertical part of T
                    int centerX = sizeX / 2;
                    int tWidth = Mathf.Max(2, sizeX / 4);
                    int tStart = centerX - tWidth / 2;

                    // Draw the vertical part of the T
                    for (int x = tStart; x < tStart + tWidth; x++)
                    {
                        for (int y = 0; y < sizeY - 1; y++)
                        {
                            // Make sure this voxel is not air (part of the head)
                            if (GetVoxel(x, y, faceZ) != VoxelType.Air)
                            {
                                // This is part of the face - keep it as the base color (white/cream)
                                // We'll use Stone type to distinguish it from hair
                                SetVoxel(x, y, faceZ, VoxelType.Stone);
                            }
                        }
                    }

                    // Horizontal part of T (across the eyes)
                    int eyeLevel = sizeY / 2;
                    int tHeight = Mathf.Max(2, sizeY / 4);

                    for (int x = 0; x < sizeX; x++)
                    {
                        for (int y = eyeLevel - tHeight/2; y < eyeLevel + tHeight/2; y++)
                        {
                            // Make sure this voxel is not air (part of the head)
                            if (GetVoxel(x, y, faceZ) != VoxelType.Air)
                            {
                                // This is part of the face - keep it as the base color (white/cream)
                                SetVoxel(x, y, faceZ, VoxelType.Stone);
                            }
                        }
                    }

                    // Add a small tuft of hair on top
                    for (int x = sizeX/4; x < 3*sizeX/4; x++)
                    {
                        SetVoxel(x, hairY + 1, sizeZ/2, VoxelType.Leaves);
                    }

                    break;
            }
        }

        /// <summary>
        /// Override to add custom colors for eyes, hair, and face pattern
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
            else if (voxelType == VoxelType.Stone && z == sizeZ - 1)
            {
                // This is the face pattern - use face color
                color = FaceColor;
            }
            else if (z == sizeZ - 1)
            {
                // This is the front of the head but not part of the face pattern
                // Use hair color for non-face parts on the front
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
