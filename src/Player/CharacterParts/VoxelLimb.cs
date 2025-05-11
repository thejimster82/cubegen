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
        [Export] public Color ClothingColor { get; set; } = new Color(1.0f, 0.5f, 0.0f); // Orange for arms
        [Export] public Color PantsColor { get; set; } = new Color(0.4f, 0.3f, 0.6f); // Purple for legs
        [Export] public Color ShoeColor { get; set; } = new Color(0.0f, 0.0f, 0.0f); // Black shoes
        [Export] public Color SkinColor { get; set; } = new Color(1.0f, 0.8f, 0.6f); // Skin color for hands
        [Export] public bool HasClothing { get; set; } = true;

        public override void _Ready()
        {
            // Set default properties based on limb type
            if (Type == LimbType.Arm)
            {
                PartName = IsLeft ? "LeftArm" : "RightArm";
                Size = new Vector3(0.15f, 0.3f, 0.15f); // Thinner, shorter arms for pixel character
                BaseColor = ClothingColor; // Arms use the clothing color (orange)
            }
            else
            {
                PartName = IsLeft ? "LeftLeg" : "RightLeg";
                Size = new Vector3(0.15f, 0.3f, 0.15f); // Thinner, shorter legs for pixel character
                BaseColor = PantsColor; // Legs use the pants color (purple)
            }

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
        /// Create a blocky limb shape for the pixel character
        /// </summary>
        private void CreateStylizedLimbShape(int sizeX, int sizeY, int sizeZ)
        {
            // For the pixel character, we want completely blocky limbs
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
                        if (distX == 0 && distY == 0 && distZ == 0)
                        {
                            // Only remove the very corner voxels
                            SetVoxel(x, y, z, VoxelType.Air);
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
            // Position is implicit (always at the bottom)
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
        /// Add a black blocky shoe to the leg like in the pixel character
        /// </summary>
        private void AddStylizedFoot(int sizeX, int sizeY, int sizeZ)
        {
            // Only proceed if this is a leg
            if (Type != LimbType.Leg)
                return;

            // Foot position at the bottom of the leg
            // Position is implicit (always at the bottom)
            int footHeight = sizeY / 5; // Slightly taller black shoes

            // Make the foot slightly longer than the leg
            int footWidth = sizeX;
            int footDepth = sizeZ + 1; // Extend forward slightly

            // Calculate offset to center the foot
            int offsetZ = 0; // No need to extend too much for pixel character

            // Create a blocky black shoe
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

                        // Set foot voxel using a different type to mark it as a shoe
                        // We'll use Bedrock as a marker for shoes
                        SetVoxel(x, y, posZ, VoxelType.Bedrock);
                    }
                }
            }
        }

        /// <summary>
        /// Override to add custom colors for clothing, skin, and shoes
        /// </summary>
        protected override void AddFace(int x, int y, int z, int face, System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<Vector3> normals, System.Collections.Generic.List<Color> colors,
            System.Collections.Generic.List<int> indices, System.Collections.Generic.List<Vector3> collisionFaces)
        {
            // Get voxel type
            VoxelType voxelType = GetVoxel(x, y, z);

            // Determine color based on voxel type and limb type
            Color color;

            if (voxelType == VoxelType.Bedrock)
            {
                // This is a shoe - use black shoe color
                color = ShoeColor;
            }
            else if (voxelType == VoxelType.Leaves)
            {
                // This is clothing - use appropriate color based on limb type
                if (Type == LimbType.Arm)
                {
                    color = ClothingColor; // Orange for arms
                }
                else
                {
                    color = PantsColor; // Purple for legs
                }
            }
            else if (voxelType == VoxelType.Stone)
            {
                // This is skin (hands)
                color = SkinColor;
            }
            else
            {
                // Default color based on limb type
                if (Type == LimbType.Arm)
                {
                    color = ClothingColor; // Orange for arms
                }
                else
                {
                    color = PantsColor; // Purple for legs
                }
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
