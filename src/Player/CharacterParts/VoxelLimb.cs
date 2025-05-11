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
                Size = new Vector3(0.2f, 0.5f, 0.2f); // Thinner arms for stylized look
            }
            else
            {
                PartName = IsLeft ? "LeftLeg" : "RightLeg";
                Size = new Vector3(0.2f, 0.5f, 0.2f); // Thinner legs for stylized look
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

            // Create a more stylized limb shape
            CreateStylizedLimbShape(sizeX, sizeY, sizeZ);

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
                                // Only mark solid voxels as clothing
                                if (GetVoxel(x, y, z) != VoxelType.Air)
                                {
                                    // Mark sleeve voxels with a different type
                                    // We'll use Leaves as a marker for clothing
                                    SetVoxel(x, y, z, VoxelType.Leaves);
                                }
                            }
                        }
                    }

                    // Add a blocky hand at the end
                    AddStylizedHand(sizeX, sizeY, sizeZ);
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
                                // Only mark solid voxels as clothing
                                if (GetVoxel(x, y, z) != VoxelType.Air)
                                {
                                    // Mark pants voxels with a different type
                                    // We'll use Leaves as a marker for clothing
                                    SetVoxel(x, y, z, VoxelType.Leaves);
                                }
                            }
                        }
                    }

                    // Add a blocky foot/boot at the end
                    AddStylizedFoot(sizeX, sizeY, sizeZ);
                }
            }
            else
            {
                // If no clothing, still add stylized hands/feet
                if (Type == LimbType.Arm)
                {
                    AddStylizedHand(sizeX, sizeY, sizeZ);
                }
                else
                {
                    AddStylizedFoot(sizeX, sizeY, sizeZ);
                }
            }
        }

        /// <summary>
        /// Create a more stylized limb shape
        /// </summary>
        private void CreateStylizedLimbShape(int sizeX, int sizeY, int sizeZ)
        {
            // Make the limb slightly thinner in the middle for a more stylized look
            int midY = sizeY / 2;
            int thinAmount = Mathf.Max(1, sizeX / 4);

            // Make the middle section slightly thinner
            for (int y = midY - sizeY / 6; y < midY + sizeY / 6; y++)
            {
                if (y >= 0 && y < sizeY)
                {
                    // Thin the edges
                    for (int x = 0; x < thinAmount; x++)
                    {
                        for (int z = 0; z < sizeZ; z++)
                        {
                            // Only thin the edges
                            if (x == 0 || x == sizeX - 1 || z == 0 || z == sizeZ - 1)
                            {
                                SetVoxel(x, y, z, VoxelType.Air);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Add a stylized blocky hand to the arm
        /// </summary>
        private void AddStylizedHand(int sizeX, int sizeY, int sizeZ)
        {
            // Only proceed if this is an arm
            if (Type != LimbType.Arm)
                return;

            // Hand position at the bottom of the arm
            int handY = 0;
            int handHeight = sizeY / 6;

            // Make the hand slightly wider than the arm
            int handWidth = sizeX + 2;
            int handDepth = sizeZ;

            // Calculate offset to center the hand
            int offsetX = (sizeX - handWidth) / 2;

            // Create a blocky hand
            for (int y = 0; y < handHeight; y++)
            {
                for (int x = 0; x < handWidth; x++)
                {
                    for (int z = 0; z < handDepth; z++)
                    {
                        // Calculate actual position with offset
                        int posX = x + offsetX;

                        // Skip if out of bounds
                        if (posX < 0 || posX >= sizeX)
                            continue;

                        // Set hand voxel (using Stone type, will be colored with skin color)
                        SetVoxel(posX, y, z, VoxelType.Stone);
                    }
                }
            }
        }

        /// <summary>
        /// Add a stylized blocky foot/boot to the leg
        /// </summary>
        private void AddStylizedFoot(int sizeX, int sizeY, int sizeZ)
        {
            // Only proceed if this is a leg
            if (Type != LimbType.Leg)
                return;

            // Foot position at the bottom of the leg
            int footY = 0;
            int footHeight = sizeY / 6;

            // Make the foot slightly longer than the leg
            int footWidth = sizeX;
            int footDepth = sizeZ + 2;

            // Calculate offset to center the foot
            int offsetZ = -1; // Extend forward

            // Create a blocky foot
            for (int y = 0; y < footHeight; y++)
            {
                for (int x = 0; x < footWidth; x++)
                {
                    for (int z = 0; z < footDepth; z++)
                    {
                        // Calculate actual position with offset
                        int posZ = z + offsetZ;

                        // Skip if out of bounds
                        if (posZ < 0 || posZ >= sizeZ)
                            continue;

                        // Set foot voxel (using Stone type, will be colored appropriately)
                        SetVoxel(x, y, posZ, VoxelType.Stone);
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
