using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

public static class BiomeMaterials
{
    private static Dictionary<BiomeType, Dictionary<VoxelType, Material>> _materials;
    private static bool _isInitialized = false;
    private static readonly object _initLock = new object();

    public static void Initialize()
    {
        // Use a lock to ensure thread safety during initialization
        lock (_initLock)
        {
            // Only initialize once
            if (_isInitialized)
                return;

            GD.Print("Initializing BiomeMaterials...");
            _materials = new Dictionary<BiomeType, Dictionary<VoxelType, Material>>();

        // Initialize materials for each biome
        foreach (BiomeType biomeType in Enum.GetValues(typeof(BiomeType)))
        {
            _materials[biomeType] = new Dictionary<VoxelType, Material>();

            // Create materials for each voxel type in this biome
            foreach (VoxelType voxelType in Enum.GetValues(typeof(VoxelType)))
            {
                if (voxelType == VoxelType.Air)
                    continue;

                StandardMaterial3D material = new StandardMaterial3D();
                material.VertexColorUseAsAlbedo = true;

                // Set color based on biome and voxel type
                switch (biomeType)
                {
                    case BiomeType.Plains:
                        SetPlainsColors(material, voxelType);
                        break;
                    case BiomeType.Forest:
                        SetForestColors(material, voxelType);
                        break;
                    case BiomeType.Desert:
                        SetDesertColors(material, voxelType);
                        break;
                    case BiomeType.Mountains:
                        SetMountainColors(material, voxelType);
                        break;
                    case BiomeType.Tundra:
                        SetTundraColors(material, voxelType);
                        break;
                    default:
                        material.AlbedoColor = new Color(1.0f, 1.0f, 1.0f);
                        break;
                }

                _materials[biomeType][voxelType] = material;
            }
        }

        _isInitialized = true;
        GD.Print("BiomeMaterials initialization complete.");
        }
    }

    private static void SetPlainsColors(StandardMaterial3D material, VoxelType voxelType)
    {
        switch (voxelType)
        {
            case VoxelType.Grass:
                material.AlbedoColor = new Color(0.4f, 0.83f, 0.3f); // Bright green
                break;
            case VoxelType.Dirt:
                material.AlbedoColor = new Color(0.6f, 0.4f, 0.2f); // Brown
                break;
            case VoxelType.Stone:
                material.AlbedoColor = new Color(0.6f, 0.6f, 0.6f); // Light gray
                break;
            case VoxelType.Sand:
                material.AlbedoColor = new Color(0.9f, 0.8f, 0.5f); // Tan
                break;
            case VoxelType.Wood:
                material.AlbedoColor = new Color(0.65f, 0.45f, 0.25f); // Lighter brown for Plains biome
                break;
            case VoxelType.Leaves:
                material.AlbedoColor = new Color(0.35f, 0.75f, 0.25f); // Brighter green for Plains biome
                break;

            // Decoration types
            case VoxelType.TallGrass:
                material.AlbedoColor = new Color(0.5f, 0.87f, 0.4f); // Bright green for tall grass
                break;
            case VoxelType.Flower:
                material.AlbedoColor = new Color(0.95f, 0.6f, 0.7f); // Pink flower
                break;
            case VoxelType.Mushroom:
                material.AlbedoColor = new Color(0.9f, 0.4f, 0.4f); // Red mushroom
                break;
            case VoxelType.Rock:
                material.AlbedoColor = new Color(0.7f, 0.7f, 0.7f); // Light gray rock
                break;
            case VoxelType.Stick:
                material.AlbedoColor = new Color(0.6f, 0.4f, 0.2f); // Brown stick
                break;
            case VoxelType.Seashell:
                material.AlbedoColor = new Color(0.95f, 0.9f, 0.8f); // Off-white seashell
                break;
            case VoxelType.Water:
                material.AlbedoColor = new Color(0.2f, 0.4f, 0.8f); // Blue
                material.Roughness = 0.1f;
                break;
            case VoxelType.Snow:
                material.AlbedoColor = new Color(0.9f, 0.9f, 0.95f); // White
                break;
            case VoxelType.Bedrock:
                material.AlbedoColor = new Color(0.2f, 0.2f, 0.2f); // Dark gray
                break;
            case VoxelType.Cloud:
                material.AlbedoColor = new Color(1.0f, 1.0f, 1.0f); // Pure white
                material.Roughness = 0.9f; // Soft, diffuse appearance
                material.Metallic = 0.0f; // Non-metallic
                break;
            default:
                material.AlbedoColor = new Color(1.0f, 1.0f, 1.0f); // White
                break;
        }
    }

    private static void SetForestColors(StandardMaterial3D material, VoxelType voxelType)
    {
        switch (voxelType)
        {
            case VoxelType.Grass:
                material.AlbedoColor = new Color(0.2f, 0.6f, 0.2f); // Dark green
                break;
            case VoxelType.Dirt:
                material.AlbedoColor = new Color(0.5f, 0.3f, 0.1f); // Dark brown
                break;
            case VoxelType.Stone:
                material.AlbedoColor = new Color(0.5f, 0.5f, 0.5f); // Gray
                break;
            case VoxelType.Sand:
                material.AlbedoColor = new Color(0.8f, 0.7f, 0.4f); // Darker tan
                break;
            case VoxelType.Wood:
                material.AlbedoColor = new Color(0.6f, 0.35f, 0.15f); // Warmer brown for Forest biome
                break;
            case VoxelType.Leaves:
                material.AlbedoColor = new Color(0.25f, 0.65f, 0.2f); // Vibrant green for Forest biome
                break;
            case VoxelType.Water:
                material.AlbedoColor = new Color(0.1f, 0.3f, 0.6f); // Darker blue
                material.Roughness = 0.1f;
                break;
            case VoxelType.Snow:
                material.AlbedoColor = new Color(0.9f, 0.9f, 0.95f); // White
                break;
            case VoxelType.Bedrock:
                material.AlbedoColor = new Color(0.2f, 0.2f, 0.2f); // Dark gray
                break;
            case VoxelType.Cloud:
                material.AlbedoColor = new Color(1.0f, 1.0f, 1.0f); // Pure white
                material.Roughness = 0.9f; // Soft, diffuse appearance
                material.Metallic = 0.0f; // Non-metallic
                break;
            default:
                material.AlbedoColor = new Color(1.0f, 1.0f, 1.0f); // White
                break;
        }
    }

    private static void SetDesertColors(StandardMaterial3D material, VoxelType voxelType)
    {
        switch (voxelType)
        {
            case VoxelType.Grass:
                material.AlbedoColor = new Color(0.7f, 0.7f, 0.3f); // Yellow-green (dry grass)
                break;
            case VoxelType.Dirt:
                material.AlbedoColor = new Color(0.8f, 0.6f, 0.3f); // Light brown
                break;
            case VoxelType.Stone:
                material.AlbedoColor = new Color(0.8f, 0.7f, 0.5f); // Sandstone color
                break;
            case VoxelType.Sand:
                material.AlbedoColor = new Color(0.95f, 0.85f, 0.5f); // Bright sand
                break;
            case VoxelType.Wood:
                material.AlbedoColor = new Color(0.7f, 0.5f, 0.3f); // Light brown
                break;
            case VoxelType.Leaves:
                material.AlbedoColor = new Color(0.5f, 0.6f, 0.2f); // Olive green
                break;
            case VoxelType.Water:
                material.AlbedoColor = new Color(0.1f, 0.5f, 0.7f); // Oasis blue
                material.Roughness = 0.1f;
                break;
            case VoxelType.Snow:
                material.AlbedoColor = new Color(0.95f, 0.95f, 0.8f); // Off-white
                break;
            case VoxelType.Bedrock:
                material.AlbedoColor = new Color(0.4f, 0.3f, 0.2f); // Dark tan
                break;
            case VoxelType.Cloud:
                material.AlbedoColor = new Color(1.0f, 1.0f, 1.0f); // Pure white
                material.Roughness = 0.9f; // Soft, diffuse appearance
                material.Metallic = 0.0f; // Non-metallic
                break;
            default:
                material.AlbedoColor = new Color(1.0f, 1.0f, 1.0f); // White
                break;
        }
    }

    private static void SetMountainColors(StandardMaterial3D material, VoxelType voxelType)
    {
        switch (voxelType)
        {
            case VoxelType.Grass:
                material.AlbedoColor = new Color(0.3f, 0.5f, 0.3f); // Mountain grass
                break;
            case VoxelType.Dirt:
                material.AlbedoColor = new Color(0.4f, 0.3f, 0.2f); // Dark soil
                break;
            case VoxelType.Stone:
                material.AlbedoColor = new Color(0.4f, 0.4f, 0.45f); // Dark stone
                break;
            case VoxelType.Sand:
                material.AlbedoColor = new Color(0.7f, 0.6f, 0.5f); // Rocky sand
                break;
            case VoxelType.Wood:
                material.AlbedoColor = new Color(0.4f, 0.3f, 0.2f); // Dark wood
                break;
            case VoxelType.Leaves:
                material.AlbedoColor = new Color(0.3f, 0.4f, 0.2f); // Pine green
                break;
            case VoxelType.Water:
                material.AlbedoColor = new Color(0.1f, 0.2f, 0.4f); // Mountain lake blue
                material.Roughness = 0.1f;
                break;
            case VoxelType.Snow:
                material.AlbedoColor = new Color(1.0f, 1.0f, 1.0f); // Bright white
                break;
            case VoxelType.Bedrock:
                material.AlbedoColor = new Color(0.15f, 0.15f, 0.15f); // Very dark gray
                break;
            case VoxelType.Cloud:
                material.AlbedoColor = new Color(1.0f, 1.0f, 1.0f); // Pure white
                material.Roughness = 0.9f; // Soft, diffuse appearance
                material.Metallic = 0.0f; // Non-metallic
                break;
            default:
                material.AlbedoColor = new Color(1.0f, 1.0f, 1.0f); // White
                break;
        }
    }

    private static void SetTundraColors(StandardMaterial3D material, VoxelType voxelType)
    {
        switch (voxelType)
        {
            case VoxelType.Grass:
                material.AlbedoColor = new Color(0.6f, 0.7f, 0.6f); // Pale grass
                break;
            case VoxelType.Dirt:
                material.AlbedoColor = new Color(0.5f, 0.4f, 0.3f); // Frozen soil
                break;
            case VoxelType.Stone:
                material.AlbedoColor = new Color(0.5f, 0.55f, 0.6f); // Bluish stone
                break;
            case VoxelType.Sand:
                material.AlbedoColor = new Color(0.8f, 0.8f, 0.7f); // Pale sand
                break;
            case VoxelType.Wood:
                material.AlbedoColor = new Color(0.5f, 0.4f, 0.3f); // Pale wood
                break;
            case VoxelType.Leaves:
                material.AlbedoColor = new Color(0.4f, 0.5f, 0.4f); // Pale green
                break;
            case VoxelType.Water:
                material.AlbedoColor = new Color(0.2f, 0.3f, 0.5f); // Icy blue
                material.Roughness = 0.05f;
                break;
            case VoxelType.Snow:
                material.AlbedoColor = new Color(0.95f, 0.97f, 1.0f); // Bright white with blue tint
                break;
            case VoxelType.Bedrock:
                material.AlbedoColor = new Color(0.3f, 0.3f, 0.35f); // Dark bluish gray
                break;
            case VoxelType.Cloud:
                material.AlbedoColor = new Color(1.0f, 1.0f, 1.0f); // Pure white
                material.Roughness = 0.9f; // Soft, diffuse appearance
                material.Metallic = 0.0f; // Non-metallic
                break;
            default:
                material.AlbedoColor = new Color(1.0f, 1.0f, 1.0f); // White
                break;
        }
    }

    public static Material GetMaterial(BiomeType biomeType, VoxelType voxelType)
    {
        if (_materials == null || !_isInitialized)
        {
            Initialize();
        }

        try
        {
            if (_materials.ContainsKey(biomeType) && _materials[biomeType].ContainsKey(voxelType))
            {
                return _materials[biomeType][voxelType];
            }
            else
            {
                // Log the missing material
                GD.PrintErr($"Missing material for biome {biomeType} and voxel type {voxelType}. Using fallback material.");

                // Try to get a material for this voxel type from any biome
                foreach (var biome in _materials.Keys)
                {
                    if (_materials[biome].ContainsKey(voxelType))
                    {
                        GD.Print($"Using material from biome {biome} for voxel type {voxelType}");
                        return _materials[biome][voxelType];
                    }
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error getting material: {ex.Message}");
        }

        // Fallback to a default material with a distinctive color to make it obvious
        StandardMaterial3D defaultMaterial = new StandardMaterial3D();
        defaultMaterial.AlbedoColor = new Color(1.0f, 0.0f, 1.0f); // Magenta for visibility
        defaultMaterial.VertexColorUseAsAlbedo = true;
        GD.PrintErr($"Using magenta fallback material for biome {biomeType} and voxel type {voxelType}");
        return defaultMaterial;
    }
}
