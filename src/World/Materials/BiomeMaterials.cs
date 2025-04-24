using Godot;
using System;
using System.Collections.Generic;

public static class BiomeMaterials
{
    private static Dictionary<BiomeType, Dictionary<VoxelType, Material>> _materials;
    
    public static void Initialize()
    {
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
                material.AlbedoColor = new Color(0.6f, 0.4f, 0.2f); // Brown
                break;
            case VoxelType.Leaves:
                material.AlbedoColor = new Color(0.3f, 0.7f, 0.2f); // Green
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
                material.AlbedoColor = new Color(0.5f, 0.3f, 0.1f); // Dark brown
                break;
            case VoxelType.Leaves:
                material.AlbedoColor = new Color(0.1f, 0.5f, 0.1f); // Dark green
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
            default:
                material.AlbedoColor = new Color(1.0f, 1.0f, 1.0f); // White
                break;
        }
    }
    
    public static Material GetMaterial(BiomeType biomeType, VoxelType voxelType)
    {
        if (_materials == null)
        {
            Initialize();
        }
        
        if (_materials.ContainsKey(biomeType) && _materials[biomeType].ContainsKey(voxelType))
        {
            return _materials[biomeType][voxelType];
        }
        
        // Fallback to a default material
        StandardMaterial3D defaultMaterial = new StandardMaterial3D();
        defaultMaterial.AlbedoColor = new Color(1.0f, 1.0f, 1.0f);
        return defaultMaterial;
    }
}
