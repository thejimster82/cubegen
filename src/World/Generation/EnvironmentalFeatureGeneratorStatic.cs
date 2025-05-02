using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
    /// <summary>
    /// Static version of the EnvironmentalFeatureGenerator for use by other classes
    /// </summary>
    public static class EnvironmentalFeatureGeneratorStatic
    {
        private static EnvironmentalFeatureGenerator _instance;
        
        public static void Initialize(int seed)
        {
            _instance = new EnvironmentalFeatureGenerator(seed);
        }
        
        public static int ModifyTerrainHeight(int worldX, int worldZ, int baseHeight, BiomeType biomeType, int maxHeight)
        {
            if (_instance == null)
            {
                // If not initialized, return the base height
                return baseHeight;
            }
            
            return _instance.ModifyTerrainHeight(worldX, worldZ, baseHeight, biomeType, maxHeight);
        }
        
        public static bool ShouldPlaceWater(int worldX, int worldZ, int y, int terrainHeight, BiomeType biomeType)
        {
            if (_instance == null)
            {
                // If not initialized, return false
                return false;
            }
            
            return _instance.ShouldPlaceWater(worldX, worldZ, y, terrainHeight, biomeType);
        }
    }
}
