using Godot;
using System;

namespace CubeGen.World.POI
{
    /// <summary>
    /// Enum defining different types of Points of Interest
    /// </summary>
    public enum POIType
    {
        // Natural formations
        LargeRock,      // A large rock formation
        Lake,           // A natural lake
        Volcano,        // A volcano with lava flows
        
        // Structures
        Town,           // A small town with multiple buildings
        Farm,           // A farm with fields
        Ruins,          // Ancient ruins
        
        // Test POI
        TestSphere      // Simple 30x30 sphere for testing
    }
    
    /// <summary>
    /// Enum defining the size category of a POI
    /// </summary>
    public enum POISize
    {
        Small,      // Small POI (fits within a single chunk)
        Medium,     // Medium POI (spans 2-3 chunks)
        Large,      // Large POI (spans 4-8 chunks)
        Massive     // Massive POI (spans 9+ chunks)
    }
    
    /// <summary>
    /// Enum defining how a POI affects the surrounding terrain
    /// </summary>
    public enum POIInfluence
    {
        None,           // No influence on surrounding terrain
        Terrain,        // Modifies terrain height/shape
        Vegetation,     // Modifies vegetation (adds/removes trees, etc.)
        Water,          // Adds water features (streams, etc.)
        Combined        // Combination of multiple influences
    }
}
