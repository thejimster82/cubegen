using Godot;
using System;
using CubeGen.World.Common;
using CubeGen.World.Materials;

namespace CubeGen.World.Environment
{
    /// <summary>
    /// Manages global wind parameters for the game world
    /// </summary>
    public partial class WindSystem : Node
    {
        // Wind direction (in radians, 0 = east, PI/2 = north, PI = west, 3PI/2 = south)
        [Export] public float WindDirection { get; set; } = 0.0f;

        // Wind strength (0.0 = no wind, 1.0 = strong wind)
        [Export] public float WindStrength { get; set; } = 0.5f;

        // Wind gusting factor (0.0 = steady wind, 1.0 = very gusty)
        [Export] public float WindGustiness { get; set; } = 0.3f;

        // Wind speed (how fast the wind animation cycles)
        [Export] public float WindSpeed { get; set; } = 1.0f;

        // Noise for wind variation
        private FastNoiseLite _windNoise;

        // Time accumulator for wind animation
        private float _windTime = 0.0f;

        // Shader parameter names
        private const string WIND_DIRECTION_PARAM = "wind_direction";
        private const string WIND_STRENGTH_PARAM = "wind_strength";
        private const string WIND_TIME_PARAM = "wind_time";
        private const string WIND_GUSTINESS_PARAM = "wind_gustiness";

        // List of voxel types affected by wind
        private static readonly VoxelType[] WindAffectedTypes = new VoxelType[]
        {
            VoxelType.TallGrass,
            VoxelType.Flower,
            // Add other types that should be affected by wind
        };

        public override void _Ready()
        {
            // Initialize noise for wind variation
            _windNoise = new FastNoiseLite();
            _windNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _windNoise.Seed = new Random().Next();
            _windNoise.Frequency = 0.005f;

            // Start with random wind direction
            WindDirection = (float)(new Random().NextDouble() * Mathf.Pi * 2);

            GD.Print("WindSystem initialized");
        }

        public override void _Process(double delta)
        {
            // Update wind time
            _windTime += (float)delta * WindSpeed;

            // Slowly change wind direction over time
            WindDirection += (float)delta * 0.05f * _windNoise.GetNoise1D(_windTime * 0.1f);

            // Keep wind direction in [0, 2π] range
            WindDirection = Mathf.PosMod(WindDirection, Mathf.Pi * 2);

            // Vary wind strength slightly over time
            float baseStrength = WindStrength;
            WindStrength = Mathf.Clamp(
                baseStrength + (_windNoise.GetNoise1D(_windTime * 0.2f + 100) * 0.2f),
                0.1f,
                0.2f
            );

            // Update all wind-affected materials
            UpdateWindMaterials();
        }

        /// <summary>
        /// Updates all wind-affected materials with current wind parameters
        /// </summary>
        private void UpdateWindMaterials()
        {
            foreach (BiomeType biomeType in Enum.GetValues(typeof(BiomeType)))
            {
                foreach (VoxelType voxelType in WindAffectedTypes)
                {
                    // Get the wind-enabled material for this biome and voxel type
                    Material material = WindMaterials.GetWindMaterial(biomeType, voxelType);

                    if (material is ShaderMaterial shaderMaterial)
                    {
                        // Update shader parameters
                        shaderMaterial.SetShaderParameter(WIND_DIRECTION_PARAM, WindDirection);
                        shaderMaterial.SetShaderParameter(WIND_STRENGTH_PARAM, WindStrength);
                        shaderMaterial.SetShaderParameter(WIND_TIME_PARAM, _windTime);
                        shaderMaterial.SetShaderParameter(WIND_GUSTINESS_PARAM, WindGustiness);

                        // Debug output (only occasionally to avoid spam)
                        if (Mathf.FloorToInt(_windTime) % 5 == 0 && (_windTime - Mathf.Floor(_windTime)) < 0.01f)
                        {
                            GD.Print($"Updated wind parameters for {biomeType} {voxelType}: " +
                                $"Time={_windTime:F2}, Strength={WindStrength:F2}, Direction={WindDirection:F2}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a voxel type is affected by wind
        /// </summary>
        public static bool IsAffectedByWind(VoxelType voxelType)
        {
            return Array.IndexOf(WindAffectedTypes, voxelType) >= 0;
        }
    }
}
