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
            
            // Central tall blade
            voxels.Add(new DecorationVoxel(new Vector3(0, 0, 0), color1, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.2f, 0), color1, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.4f, 0), color1, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.6f, 0), color1, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.8f, 0), color1, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0.05f, 1.0f, 0), color1, 0.15f));
            
            // Blade leaning right
            voxels.Add(new DecorationVoxel(new Vector3(0.15f, 0, 0.1f), color2, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0.2f, 0.2f, 0.1f), color2, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0.25f, 0.4f, 0.1f), color2, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0.3f, 0.6f, 0.1f), color2, 0.15f));
            
            // Blade leaning left
            voxels.Add(new DecorationVoxel(new Vector3(-0.15f, 0, -0.1f), color3, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.2f, 0.2f, -0.1f), color3, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.25f, 0.4f, -0.1f), color3, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.3f, 0.6f, -0.1f), color3, 0.15f));
            
            // Blade leaning forward
            voxels.Add(new DecorationVoxel(new Vector3(0.05f, 0, 0.2f), color2, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0.05f, 0.2f, 0.25f), color2, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0.05f, 0.4f, 0.3f), color2, 0.15f));
            
            // Blade leaning backward
            voxels.Add(new DecorationVoxel(new Vector3(-0.05f, 0, -0.2f), color3, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.05f, 0.2f, -0.25f), color3, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.05f, 0.4f, -0.3f), color3, 0.15f));
            
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
            
            // Create stem
            voxels.Add(new DecorationVoxel(new Vector3(0, 0, 0), stemColor, 0.15f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.15f, 0), stemColor, 0.15f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.3f, 0), stemColor, 0.15f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.45f, 0), stemColor, 0.15f));
            
            // Create flower center
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.6f, 0), centerColor, 0.2f));
            
            // Create petals
            voxels.Add(new DecorationVoxel(new Vector3(0.2f, 0.6f, 0), petalColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.2f, 0.6f, 0), petalColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.6f, 0.2f), petalColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.6f, -0.2f), petalColor, 0.2f));
            
            // Add diagonal petals for a fuller flower
            voxels.Add(new DecorationVoxel(new Vector3(0.15f, 0.6f, 0.15f), petalColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.15f, 0.6f, 0.15f), petalColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0.15f, 0.6f, -0.15f), petalColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.15f, 0.6f, -0.15f), petalColor, 0.2f));
            
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
            
            // Create stem
            voxels.Add(new DecorationVoxel(new Vector3(0, 0, 0), stemColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.2f, 0), stemColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.4f, 0), stemColor, 0.2f));
            
            // Create cap
            // Center of cap
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.6f, 0), capColor, 0.3f));
            
            // Edges of cap
            voxels.Add(new DecorationVoxel(new Vector3(0.25f, 0.5f, 0), capColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.25f, 0.5f, 0), capColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.5f, 0.25f), capColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.5f, -0.25f), capColor, 0.2f));
            
            // Diagonal edges
            voxels.Add(new DecorationVoxel(new Vector3(0.2f, 0.5f, 0.2f), capColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.2f, 0.5f, 0.2f), capColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0.2f, 0.5f, -0.2f), capColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.2f, 0.5f, -0.2f), capColor, 0.2f));
            
            // Underside of cap (with spots)
            voxels.Add(new DecorationVoxel(new Vector3(0.15f, 0.45f, 0.15f), undersideColor, 0.1f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.15f, 0.45f, 0.15f), undersideColor, 0.1f));
            voxels.Add(new DecorationVoxel(new Vector3(0.15f, 0.45f, -0.15f), undersideColor, 0.1f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.15f, 0.45f, -0.15f), undersideColor, 0.1f));
            
            return voxels;
        }
        
        // Generate a rock model (irregular shape)
        private static List<DecorationVoxel> GenerateRockModel(Color baseColor)
        {
            List<DecorationVoxel> voxels = new List<DecorationVoxel>();
            
            // Create variations of the base color for a more natural look
            Color darkColor = new Color(baseColor.R * 0.8f, baseColor.G * 0.8f, baseColor.B * 0.8f);
            Color lightColor = new Color(baseColor.R * 1.1f, baseColor.G * 1.1f, baseColor.B * 1.1f);
            
            // Base layer
            voxels.Add(new DecorationVoxel(new Vector3(0, 0, 0), baseColor, 0.4f));
            
            // Middle layer (slightly smaller)
            voxels.Add(new DecorationVoxel(new Vector3(0.05f, 0.2f, 0.05f), darkColor, 0.3f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.1f, 0.2f, -0.05f), lightColor, 0.25f));
            
            // Top layer (small)
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.35f, 0), baseColor, 0.2f));
            
            // Add some smaller rocks around the base
            voxels.Add(new DecorationVoxel(new Vector3(0.3f, 0, 0.1f), darkColor, 0.15f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.25f, 0, -0.2f), lightColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0.1f, 0, -0.3f), baseColor, 0.18f));
            
            return voxels;
        }
        
        // Generate a stick model (long thin shape)
        private static List<DecorationVoxel> GenerateStickModel(Color baseColor)
        {
            List<DecorationVoxel> voxels = new List<DecorationVoxel>();
            
            // Create variations of the base color
            Color darkColor = new Color(baseColor.R * 0.8f, baseColor.G * 0.8f, baseColor.B * 0.8f);
            Color lightColor = new Color(baseColor.R * 1.1f, baseColor.G * 1.1f, baseColor.B * 1.1f);
            
            // Create a stick lying on the ground at an angle
            // Main stick body
            voxels.Add(new DecorationVoxel(new Vector3(0, 0, 0), baseColor, 0.15f));
            voxels.Add(new DecorationVoxel(new Vector3(0.15f, 0.05f, 0.05f), baseColor, 0.15f));
            voxels.Add(new DecorationVoxel(new Vector3(0.3f, 0.1f, 0.1f), baseColor, 0.15f));
            voxels.Add(new DecorationVoxel(new Vector3(0.45f, 0.15f, 0.15f), baseColor, 0.15f));
            
            // Add a small branch
            voxels.Add(new DecorationVoxel(new Vector3(0.25f, 0.15f, 0.25f), darkColor, 0.12f));
            voxels.Add(new DecorationVoxel(new Vector3(0.35f, 0.2f, 0.35f), darkColor, 0.1f));
            
            // Add some texture/detail
            voxels.Add(new DecorationVoxel(new Vector3(0.1f, 0.1f, 0), lightColor, 0.08f));
            voxels.Add(new DecorationVoxel(new Vector3(0.4f, 0.2f, 0.1f), darkColor, 0.08f));
            
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
            
            // Base of shell
            voxels.Add(new DecorationVoxel(new Vector3(0, 0, 0), baseColor, 0.3f));
            
            // Spiral shape
            voxels.Add(new DecorationVoxel(new Vector3(0.15f, 0.1f, 0.1f), lightColor, 0.25f));
            voxels.Add(new DecorationVoxel(new Vector3(0.25f, 0.2f, 0.15f), accentColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0.3f, 0.25f, 0.2f), darkColor, 0.15f));
            voxels.Add(new DecorationVoxel(new Vector3(0.35f, 0.3f, 0.25f), lightColor, 0.1f));
            
            // Shell opening
            voxels.Add(new DecorationVoxel(new Vector3(-0.1f, 0.1f, -0.1f), darkColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.15f, 0.15f, -0.15f), accentColor, 0.15f));
            
            // Shell ridges
            voxels.Add(new DecorationVoxel(new Vector3(0.1f, 0.05f, -0.1f), lightColor, 0.1f));
            voxels.Add(new DecorationVoxel(new Vector3(0.2f, 0.1f, -0.05f), accentColor, 0.1f));
            voxels.Add(new DecorationVoxel(new Vector3(0.25f, 0.15f, 0), lightColor, 0.1f));
            
            return voxels;
        }
        
        // Generate a simple cross model (fallback)
        private static List<DecorationVoxel> GenerateSimpleCrossModel(Color baseColor)
        {
            List<DecorationVoxel> voxels = new List<DecorationVoxel>();
            
            // Create a simple cross shape
            voxels.Add(new DecorationVoxel(new Vector3(0, 0, 0), baseColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.2f, 0), baseColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.4f, 0), baseColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0.2f, 0.2f, 0), baseColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(-0.2f, 0.2f, 0), baseColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.2f, 0.2f), baseColor, 0.2f));
            voxels.Add(new DecorationVoxel(new Vector3(0, 0.2f, -0.2f), baseColor, 0.2f));
            
            return voxels;
        }
    }
}
