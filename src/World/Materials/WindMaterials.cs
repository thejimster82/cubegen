using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Materials
{
    /// <summary>
    /// Manages materials for wind-affected voxel types
    /// </summary>
    public static class WindMaterials
    {
        // Dictionary to store wind-enabled shader materials for each biome and voxel type
        private static Dictionary<BiomeType, Dictionary<VoxelType, ShaderMaterial>> _windMaterials;

        // Flag to track initialization
        private static bool _isInitialized = false;

        // Wind shader resource
        private static Shader _windShader;

        /// <summary>
        /// Initialize wind materials for all biomes and applicable voxel types
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
                return;

            GD.Print("Initializing WindMaterials...");

            // Load the wind shader
            string shaderPath = "res://src/World/Materials/wind_shader.gdshader";
            GD.Print($"Attempting to load wind shader from: {shaderPath}");

            _windShader = GD.Load<Shader>(shaderPath);

            if (_windShader == null)
            {
                GD.PrintErr("Failed to load wind shader!");

                // Try to check if the file exists
                using var file = FileAccess.Open(shaderPath, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    GD.PrintErr($"Shader file does not exist or cannot be accessed at path: {shaderPath}");
                    GD.PrintErr($"FileAccess error: {FileAccess.GetOpenError()}");
                }
                else
                {
                    GD.Print("Shader file exists but could not be loaded as a Shader resource");
                }

                return;
            }

            GD.Print("Wind shader loaded successfully!");

            // Initialize materials dictionary
            _windMaterials = new Dictionary<BiomeType, Dictionary<VoxelType, ShaderMaterial>>();

            // Create materials for each biome
            foreach (BiomeType biomeType in Enum.GetValues(typeof(BiomeType)))
            {
                _windMaterials[biomeType] = new Dictionary<VoxelType, ShaderMaterial>();

                // Create wind materials for each applicable voxel type
                CreateWindMaterialsForBiome(biomeType);
            }

            _isInitialized = true;
            GD.Print("WindMaterials initialized successfully");
        }

        /// <summary>
        /// Create wind-enabled materials for a specific biome
        /// </summary>
        private static void CreateWindMaterialsForBiome(BiomeType biomeType)
        {
            // Create wind materials for grass
            CreateWindMaterialForVoxelType(biomeType, VoxelType.TallGrass);

            // Create wind materials for flowers
            CreateWindMaterialForVoxelType(biomeType, VoxelType.Flower);

            // Add other wind-affected voxel types as needed
        }

        /// <summary>
        /// Create a wind-enabled material for a specific voxel type in a biome
        /// </summary>
        private static void CreateWindMaterialForVoxelType(BiomeType biomeType, VoxelType voxelType)
        {
            // Get the standard material for this biome and voxel type
            Material baseMaterial = BiomeMaterials.GetMaterial(biomeType, voxelType);

            if (baseMaterial == null)
            {
                GD.PrintErr($"Failed to get base material for {biomeType} {voxelType}");
                return;
            }

            // Create a new shader material using the wind shader
            ShaderMaterial windMaterial = new ShaderMaterial();
            windMaterial.Shader = _windShader;

            // Copy properties from the base material
            if (baseMaterial is StandardMaterial3D standardMaterial)
            {
                // Set shader parameters based on standard material properties
                windMaterial.SetShaderParameter("albedo", standardMaterial.AlbedoColor);
                windMaterial.SetShaderParameter("roughness", standardMaterial.Roughness);
                windMaterial.SetShaderParameter("metallic", standardMaterial.Metallic);
                windMaterial.SetShaderParameter("specular", 0.5f); // Default value
                windMaterial.SetShaderParameter("use_vertex_color", true);

                // Initialize wind parameters
                windMaterial.SetShaderParameter("wind_strength", 0.5f);
                windMaterial.SetShaderParameter("wind_direction", 0.0f);
                windMaterial.SetShaderParameter("wind_time", 0.0f);
                windMaterial.SetShaderParameter("wind_gustiness", 0.3f);
            }

            // Store the wind material
            _windMaterials[biomeType][voxelType] = windMaterial;
        }

        /// <summary>
        /// Get a wind-enabled material for a specific biome and voxel type
        /// </summary>
        public static Material GetWindMaterial(BiomeType biomeType, VoxelType voxelType)
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            // Check if we have a wind material for this biome and voxel type
            if (_windMaterials.ContainsKey(biomeType) && _windMaterials[biomeType].ContainsKey(voxelType))
            {
                return _windMaterials[biomeType][voxelType];
            }

            // Fall back to standard material if no wind material is available
            return BiomeMaterials.GetMaterial(biomeType, voxelType);
        }

        /// <summary>
        /// Check if a voxel type should use wind materials
        /// </summary>
        public static bool ShouldUseWindMaterial(VoxelType voxelType)
        {
            // Currently only grass and flowers use wind materials
            return voxelType == VoxelType.TallGrass || voxelType == VoxelType.Flower;
        }
    }
}
