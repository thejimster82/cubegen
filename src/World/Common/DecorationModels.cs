using Godot;
using System;
using System.Collections.Generic;

namespace CubeGen.World.Common
{
    public static class DecorationModels
    {
        // Structure to hold voxel position and color information
        public struct DecorationVoxel
        {
            public Vector3 Position;
            public Color Color;
            public float Scale;

            public DecorationVoxel(Vector3 position, Color color, float scale = 1.0f)
            {
                Position = position;
                Color = color;
                Scale = scale;
            }
        }

        // Get a model for a specific decoration type
        public static List<DecorationVoxel> GetDecorationModel(VoxelType decorationType, Color baseColor)
        {
            switch (decorationType)
            {
                case VoxelType.TallGrass:
                    return GenerateGrassModel(baseColor);
                case VoxelType.Flower:
                    return GenerateFlowerModel(baseColor);
                case VoxelType.Mushroom:
                    return GenerateMushroomModel(baseColor);
                case VoxelType.Rock:
                    return GenerateRockModel(baseColor);
                case VoxelType.Stick:
                    return GenerateStickModel(baseColor);
                case VoxelType.Seashell:
                    return GenerateSeashellModel(baseColor);
                default:
                    // Default to a simple cross shape
                    return GenerateSimpleCrossModel(baseColor);
            }
        }

        // Generate a tall grass model (multiple thin blades)
        private static List<DecorationVoxel> GenerateGrassModel(Color baseColor)
        {
            List<DecorationVoxel> voxels = new List<DecorationVoxel>();

            // Create multiple grass blades with slight variations
            // Base color with slight variations
            Color color1 = baseColor;
            Color color2 = new Color(baseColor.R * 0.9f, baseColor.G * 1.1f, baseColor.B * 0.9f);
            Color color3 = new Color(baseColor.R * 0.85f, baseColor.G * 1.05f, baseColor.B * 0.85f);

            // Increase the scale of all voxels by ~50% and make them even taller
            float voxelSize = 0.3f; // Increased from 0.2f

            // Central tall blade - make it much taller (3+ voxels high)
            voxels.Add(new DecorationVoxel(new Vector3(0, 0, 0), color1, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.3f, 0), color1, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.6f, 0), color1, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.9f, 0), color1, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.2f, 0), color1, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.5f, 0), color1, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.8f, 0), color1, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 2.1f, 0), color1, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.05f, 2.4f, 0), color1, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(0.1f, 2.7f, 0), color1, 0.2f));

            // Blade leaning right - also taller
            voxels.Add(new DecorationVoxel(new Vector3(0.2f, 0, 0.1f), color2, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.3f, 0.3f, 0.1f), color2, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.4f, 0.6f, 0.1f), color2, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.5f, 0.9f, 0.1f), color2, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.6f, 1.2f, 0.1f), color2, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.7f, 1.5f, 0.1f), color2, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(0.8f, 1.8f, 0.1f), color2, 0.2f));

            // Blade leaning left - also taller
            voxels.Add(new DecorationVoxel(new Vector3(-0.2f, 0, -0.1f), color3, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.3f, 0.3f, -0.1f), color3, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.4f, 0.6f, -0.1f), color3, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.5f, 0.9f, -0.1f), color3, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.6f, 1.2f, -0.1f), color3, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.7f, 1.5f, -0.1f), color3, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.8f, 1.8f, -0.1f), color3, 0.2f));

            // Blade leaning forward - also taller
            voxels.Add(new DecorationVoxel(new Vector3(0.05f, 0, 0.2f), color2, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.05f, 0.3f, 0.3f), color2, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.05f, 0.6f, 0.4f), color2, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.05f, 0.9f, 0.5f), color2, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.05f, 1.2f, 0.6f), color2, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(0.05f, 1.5f, 0.7f), color2, 0.2f));

            // Blade leaning backward - also taller
            voxels.Add(new DecorationVoxel(new Vector3(-0.05f, 0, -0.2f), color3, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.05f, 0.3f, -0.3f), color3, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.05f, 0.6f, -0.4f), color3, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.05f, 0.9f, -0.5f), color3, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.05f, 1.2f, -0.6f), color3, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.05f, 1.5f, -0.7f), color3, 0.2f));

            // Add some shorter blades for variety
            voxels.Add(new DecorationVoxel(new Vector3(0.35f, 0, 0.35f), color1, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(0.35f, 0.25f, 0.35f), color1, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(0.35f, 0.5f, 0.35f), color1, 0.2f));

            voxels.Add(new DecorationVoxel(new Vector3(-0.35f, 0, -0.35f), color2, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.35f, 0.25f, -0.35f), color2, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.35f, 0.5f, -0.35f), color2, 0.2f));

            return voxels;
        }

        // Generate a flower model (stem and petals)
        private static List<DecorationVoxel> GenerateFlowerModel(Color baseColor)
        {
            List<DecorationVoxel> voxels = new List<DecorationVoxel>();

            // Stem color (green)
            Color stemColor = new Color(0.3f, 0.7f, 0.3f);

            // Petal color (use the base color)
            Color petalColor = baseColor;

            // Center color (yellow)
            Color centerColor = new Color(1.0f, 0.9f, 0.2f);

            // Increase the scale of all voxels
            float stemSize = 0.25f; // Increased from 0.15f
            float petalSize = 0.3f; // Increased from 0.2f
            float leafSize = 0.25f; // Increased from 0.15f

            // Create a much taller stem (extending beyond 2 voxels height)
            voxels.Add(new DecorationVoxel(new Vector3(0, 0, 0), stemColor, stemSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.25f, 0), stemColor, stemSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.5f, 0), stemColor, stemSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.75f, 0), stemColor, stemSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.0f, 0), stemColor, stemSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.25f, 0), stemColor, stemSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.5f, 0), stemColor, stemSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.75f, 0), stemColor, stemSize));

            // Create flower center at the top of the stem
            voxels.Add(new DecorationVoxel(new Vector3(0, 2.0f, 0), centerColor, 0.35f));

            // Create petals around the center - larger and further out
            voxels.Add(new DecorationVoxel(new Vector3(0.35f, 2.0f, 0), petalColor, petalSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.35f, 2.0f, 0), petalColor, petalSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 2.0f, 0.35f), petalColor, petalSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 2.0f, -0.35f), petalColor, petalSize));

            // Add diagonal petals for a fuller flower
            voxels.Add(new DecorationVoxel(new Vector3(0.25f, 2.0f, 0.25f), petalColor, petalSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.25f, 2.0f, 0.25f), petalColor, petalSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.25f, 2.0f, -0.25f), petalColor, petalSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.25f, 2.0f, -0.25f), petalColor, petalSize));

            // Add a second layer of petals for more volume
            voxels.Add(new DecorationVoxel(new Vector3(0.3f, 1.9f, 0), petalColor, petalSize * 0.8f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.3f, 1.9f, 0), petalColor, petalSize * 0.8f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.9f, 0.3f), petalColor, petalSize * 0.8f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.9f, -0.3f), petalColor, petalSize * 0.8f));

            // Add some larger leaves on the stem
            Color leafColor = new Color(0.2f, 0.6f, 0.2f); // Darker green for leaves
            voxels.Add(new DecorationVoxel(new Vector3(0.25f, 0.6f, 0), leafColor, leafSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.35f, 0.7f, 0), leafColor, leafSize * 0.8f));

            voxels.Add(new DecorationVoxel(new Vector3(-0.25f, 1.1f, 0), leafColor, leafSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.35f, 1.2f, 0), leafColor, leafSize * 0.8f));

            return voxels;
        }

        // Generate a mushroom model (stem and cap)
        private static List<DecorationVoxel> GenerateMushroomModel(Color baseColor)
        {
            List<DecorationVoxel> voxels = new List<DecorationVoxel>();

            // Stem color (off-white)
            Color stemColor = new Color(0.9f, 0.85f, 0.8f);

            // Cap color (use the base color)
            Color capColor = baseColor;

            // Underside color (darker)
            Color undersideColor = new Color(baseColor.R * 0.7f, baseColor.G * 0.7f, baseColor.B * 0.7f);

            // Spots color (lighter)
            Color spotsColor = new Color(
                Math.Min(1.0f, baseColor.R * 1.3f),
                Math.Min(1.0f, baseColor.G * 1.3f),
                Math.Min(1.0f, baseColor.B * 1.3f)
            );

            // Increase the scale of all voxels
            float stemSize = 0.3f; // Increased from 0.2f
            float capCenterSize = 0.5f; // Increased from 0.35f
            float capEdgeSize = 0.3f; // Increased from 0.2f
            float spotSize = 0.12f; // Increased from 0.08f

            // Create taller stem
            voxels.Add(new DecorationVoxel(new Vector3(0, 0, 0), stemColor, stemSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.3f, 0), stemColor, stemSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.6f, 0), stemColor, stemSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.9f, 0), stemColor, stemSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.2f, 0), stemColor, stemSize));

            // Create cap - larger and higher
            // Center of cap
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.5f, 0), capColor, capCenterSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.65f, 0), capColor, capCenterSize * 0.9f));

            // Edges of cap - wider
            voxels.Add(new DecorationVoxel(new Vector3(0.45f, 1.4f, 0), capColor, capEdgeSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.45f, 1.4f, 0), capColor, capEdgeSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.4f, 0.45f), capColor, capEdgeSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.4f, -0.45f), capColor, capEdgeSize));

            // Diagonal edges
            voxels.Add(new DecorationVoxel(new Vector3(0.35f, 1.4f, 0.35f), capColor, capEdgeSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.35f, 1.4f, 0.35f), capColor, capEdgeSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.35f, 1.4f, -0.35f), capColor, capEdgeSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.35f, 1.4f, -0.35f), capColor, capEdgeSize));

            // Add more cap edges for a fuller mushroom
            voxels.Add(new DecorationVoxel(new Vector3(0.5f, 1.35f, 0.15f), capColor, capEdgeSize * 0.8f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.5f, 1.35f, 0.15f), capColor, capEdgeSize * 0.8f));
            voxels.Add(new DecorationVoxel(new Vector3(0.15f, 1.35f, 0.5f), capColor, capEdgeSize * 0.8f));
            voxels.Add(new DecorationVoxel(new Vector3(0.15f, 1.35f, -0.5f), capColor, capEdgeSize * 0.8f));

            // Underside of cap
            voxels.Add(new DecorationVoxel(new Vector3(0.3f, 1.3f, 0.3f), undersideColor, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.3f, 1.3f, 0.3f), undersideColor, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(0.3f, 1.3f, -0.3f), undersideColor, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.3f, 1.3f, -0.3f), undersideColor, 0.25f));

            // Add spots on top of the cap
            voxels.Add(new DecorationVoxel(new Vector3(0.15f, 1.7f, 0.15f), spotsColor, spotSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.2f, 1.7f, -0.1f), spotsColor, spotSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.1f, 1.7f, -0.2f), spotsColor, spotSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.15f, 1.7f, 0.2f), spotsColor, spotSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 1.75f, 0), spotsColor, spotSize));

            // Add a small mushroom next to the main one (for cluster effect)
            voxels.Add(new DecorationVoxel(new Vector3(0.6f, 0, 0.4f), stemColor, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(0.6f, 0.25f, 0.4f), stemColor, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(0.6f, 0.5f, 0.4f), capColor, 0.4f));

            // Add a tiny mushroom for more variety
            voxels.Add(new DecorationVoxel(new Vector3(-0.5f, 0, -0.3f), stemColor, 0.15f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.5f, 0.15f, -0.3f), capColor, 0.25f));

            return voxels;
        }

        // Generate a rock model (irregular shape)
        private static List<DecorationVoxel> GenerateRockModel(Color baseColor)
        {
            List<DecorationVoxel> voxels = new List<DecorationVoxel>();

            // Create variations of the base color for a more natural look
            Color darkColor = new Color(baseColor.R * 0.8f, baseColor.G * 0.8f, baseColor.B * 0.8f);
            Color lightColor = new Color(baseColor.R * 1.1f, baseColor.G * 1.1f, baseColor.B * 1.1f);
            Color mossColor = new Color(0.3f, 0.5f, 0.3f); // Add some moss to rocks

            // Increase the scale of all voxels
            float baseSize = 0.6f; // Increased from 0.4f
            float midSize = 0.45f; // Increased from 0.3f
            float topSize = 0.3f; // Increased from 0.2f
            float smallRockSize = 0.25f; // Increased from 0.15-0.2f

            // Base layer - larger
            voxels.Add(new DecorationVoxel(new Vector3(0, 0, 0), baseColor, baseSize));

            // Middle layer (slightly smaller)
            voxels.Add(new DecorationVoxel(new Vector3(0.1f, 0.3f, 0.1f), darkColor, midSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.15f, 0.3f, -0.1f), lightColor, midSize * 0.9f));

            // Add more middle layer voxels for a more complex shape
            voxels.Add(new DecorationVoxel(new Vector3(0.05f, 0.3f, -0.2f), baseColor, midSize * 0.8f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.2f, 0.3f, 0.15f), darkColor, midSize * 0.85f));

            // Top layer (small)
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.55f, 0), baseColor, topSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.1f, 0.5f, 0.1f), lightColor, topSize * 0.8f));

            // Add some moss patches
            voxels.Add(new DecorationVoxel(new Vector3(0.25f, 0.2f, 0.2f), mossColor, 0.15f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.2f, 0.15f, 0.25f), mossColor, 0.12f));

            // Add some smaller rocks around the base - larger and more of them
            voxels.Add(new DecorationVoxel(new Vector3(0.45f, 0, 0.15f), darkColor, smallRockSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.4f, 0, -0.3f), lightColor, smallRockSize * 1.1f));
            voxels.Add(new DecorationVoxel(new Vector3(0.15f, 0, -0.45f), baseColor, smallRockSize * 0.9f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.3f, 0, 0.4f), darkColor, smallRockSize * 0.8f));
            voxels.Add(new DecorationVoxel(new Vector3(0.35f, 0, 0.35f), lightColor, smallRockSize * 0.7f));

            return voxels;
        }

        // Generate a stick model (long thin shape)
        private static List<DecorationVoxel> GenerateStickModel(Color baseColor)
        {
            List<DecorationVoxel> voxels = new List<DecorationVoxel>();

            // Create variations of the base color
            Color darkColor = new Color(baseColor.R * 0.8f, baseColor.G * 0.8f, baseColor.B * 0.8f);
            Color lightColor = new Color(baseColor.R * 1.1f, baseColor.G * 1.1f, baseColor.B * 1.1f);
            Color barkColor = new Color(baseColor.R * 0.7f, baseColor.G * 0.6f, baseColor.B * 0.5f);

            // Increase the scale of all voxels
            float mainSize = 0.25f; // Increased from 0.15f
            float branchSize = 0.2f; // Increased from 0.12f
            float detailSize = 0.15f; // Increased from 0.08f

            // Create a longer stick lying on the ground at an angle
            // Main stick body - longer and thicker
            voxels.Add(new DecorationVoxel(new Vector3(0, 0, 0), baseColor, mainSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.25f, 0.05f, 0.05f), baseColor, mainSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.5f, 0.1f, 0.1f), baseColor, mainSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.75f, 0.15f, 0.15f), baseColor, mainSize));
            voxels.Add(new DecorationVoxel(new Vector3(1.0f, 0.2f, 0.2f), baseColor, mainSize * 0.9f));

            // Add bark texture along the stick
            voxels.Add(new DecorationVoxel(new Vector3(0.15f, 0.05f, -0.05f), barkColor, detailSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.4f, 0.1f, -0.05f), barkColor, detailSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.65f, 0.15f, -0.05f), barkColor, detailSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.9f, 0.2f, -0.05f), barkColor, detailSize));

            // Add a larger branch
            voxels.Add(new DecorationVoxel(new Vector3(0.4f, 0.15f, 0.25f), darkColor, branchSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.55f, 0.25f, 0.35f), darkColor, branchSize * 0.9f));
            voxels.Add(new DecorationVoxel(new Vector3(0.7f, 0.35f, 0.45f), darkColor, branchSize * 0.8f));

            // Add a smaller branch
            voxels.Add(new DecorationVoxel(new Vector3(0.6f, 0.15f, -0.2f), darkColor, branchSize * 0.7f));
            voxels.Add(new DecorationVoxel(new Vector3(0.7f, 0.2f, -0.3f), darkColor, branchSize * 0.6f));

            // Add some texture/detail - knots and bumps
            voxels.Add(new DecorationVoxel(new Vector3(0.2f, 0.1f, 0.1f), lightColor, detailSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.7f, 0.2f, 0.2f), darkColor, detailSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.3f, 0.05f, -0.1f), lightColor, detailSize * 0.8f));

            // Add some moss or lichen
            Color mossColor = new Color(0.3f, 0.5f, 0.3f);
            voxels.Add(new DecorationVoxel(new Vector3(0.5f, 0.15f, 0.15f), mossColor, detailSize * 0.7f));
            voxels.Add(new DecorationVoxel(new Vector3(0.8f, 0.2f, 0.1f), mossColor, detailSize * 0.6f));

            return voxels;
        }

        // Generate a seashell model (spiral shape)
        private static List<DecorationVoxel> GenerateSeashellModel(Color baseColor)
        {
            List<DecorationVoxel> voxels = new List<DecorationVoxel>();

            // Create variations of the base color
            Color darkColor = new Color(baseColor.R * 0.8f, baseColor.G * 0.8f, baseColor.B * 0.8f);
            Color lightColor = new Color(baseColor.R * 1.1f, baseColor.G * 1.1f, baseColor.B * 1.1f);
            Color accentColor = new Color(baseColor.R * 0.9f, baseColor.G * 0.9f, baseColor.B * 1.2f);
            Color pearlColor = new Color(0.95f, 0.95f, 0.9f); // Pearly interior

            // Increase the scale of all voxels
            float baseSize = 0.45f; // Increased from 0.3f
            float spiralSize = 0.35f; // Increased from 0.25f
            float ridgeSize = 0.15f; // Increased from 0.1f

            // Base of shell - larger
            voxels.Add(new DecorationVoxel(new Vector3(0, 0, 0), baseColor, baseSize));

            // Add sand under the shell
            Color sandColor = new Color(0.9f, 0.85f, 0.7f);
            voxels.Add(new DecorationVoxel(new Vector3(0.1f, -0.1f, 0.1f), sandColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.1f, -0.1f, -0.1f), sandColor, 0.2f));

            // Spiral shape - more detailed and larger
            voxels.Add(new DecorationVoxel(new Vector3(0.2f, 0.15f, 0.15f), lightColor, spiralSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.35f, 0.25f, 0.2f), accentColor, spiralSize * 0.9f));
            voxels.Add(new DecorationVoxel(new Vector3(0.45f, 0.35f, 0.25f), darkColor, spiralSize * 0.8f));
            voxels.Add(new DecorationVoxel(new Vector3(0.5f, 0.4f, 0.3f), lightColor, spiralSize * 0.7f));
            voxels.Add(new DecorationVoxel(new Vector3(0.55f, 0.45f, 0.35f), accentColor, spiralSize * 0.6f));

            // Shell opening - larger and more detailed
            voxels.Add(new DecorationVoxel(new Vector3(-0.15f, 0.15f, -0.15f), darkColor, 0.3f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.25f, 0.2f, -0.2f), accentColor, 0.25f));

            // Add pearly interior
            voxels.Add(new DecorationVoxel(new Vector3(-0.1f, 0.1f, -0.1f), pearlColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.2f, 0.15f, -0.15f), pearlColor, 0.15f));

            // Shell ridges - more of them and more detailed
            voxels.Add(new DecorationVoxel(new Vector3(0.15f, 0.1f, -0.15f), lightColor, ridgeSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.25f, 0.15f, -0.1f), accentColor, ridgeSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.35f, 0.2f, -0.05f), lightColor, ridgeSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.4f, 0.25f, 0), accentColor, ridgeSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.45f, 0.3f, 0.05f), lightColor, ridgeSize));

            // Add some texture details
            voxels.Add(new DecorationVoxel(new Vector3(0.3f, 0.2f, 0.25f), darkColor, 0.1f));
            voxels.Add(new DecorationVoxel(new Vector3(0.4f, 0.3f, 0.15f), darkColor, 0.1f));
            voxels.Add(new DecorationVoxel(new Vector3(0.1f, 0.1f, 0.2f), darkColor, 0.1f));

            // Add a small starfish or barnacle nearby
            Color starfishColor = new Color(0.9f, 0.6f, 0.5f);
            voxels.Add(new DecorationVoxel(new Vector3(-0.3f, 0, 0.3f), starfishColor, 0.15f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.4f, 0, 0.2f), starfishColor, 0.12f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.2f, 0, 0.4f), starfishColor, 0.12f));

            return voxels;
        }

        // Generate a simple cross model (fallback)
        private static List<DecorationVoxel> GenerateSimpleCrossModel(Color baseColor)
        {
            List<DecorationVoxel> voxels = new List<DecorationVoxel>();

            // Increase the scale of all voxels
            float voxelSize = 0.3f; // Increased from 0.2f

            // Create a simple cross shape - taller and larger
            voxels.Add(new DecorationVoxel(new Vector3(0, 0, 0), baseColor, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.3f, 0), baseColor, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.6f, 0), baseColor, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.9f, 0), baseColor, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0.3f, 0.3f, 0), baseColor, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(-0.3f, 0.3f, 0), baseColor, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.3f, 0.3f), baseColor, voxelSize));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.3f, -0.3f), baseColor, voxelSize));

            // Add some variation in color
            Color darkColor = new Color(baseColor.R * 0.8f, baseColor.G * 0.8f, baseColor.B * 0.8f);
            Color lightColor = new Color(baseColor.R * 1.1f, baseColor.G * 1.1f, baseColor.B * 1.1f);

            // Add some smaller voxels for detail
            voxels.Add(new DecorationVoxel(new Vector3(0.2f, 0.6f, 0.2f), darkColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.2f, 0.6f, -0.2f), lightColor, 0.2f));

            return voxels;
        }
    }
}
