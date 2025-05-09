using Godot;
using System;

namespace CubeGen.World.Generation.POI
{
    /// <summary>
    /// Enum defining different categories of Points of Interest
    /// </summary>
    public enum POICategory
    {
        Settlement,    // Towns, villages, camps, etc.
        Dungeon,       // Underground structures, caves, ruins
        NaturalFeature, // Lakes, ponds, special trees, rock formations
        Landmark,      // Unique structures or features that serve as landmarks
        Resource       // Special resource locations like mines, quarries, etc.
    }

    /// <summary>
    /// Enum defining specific types of Points of Interest within each category
    /// </summary>
    public enum POIType
    {
        // Settlement types
        Village,        // Small collection of buildings
        Town,           // Larger settlement with more structures
        Camp,           // Temporary or small settlement
        
        // Dungeon types
        Cave,           // Natural underground formation
        Ruin,           // Abandoned structure
        Mine,           // Excavated underground area
        
        // Natural Feature types
        Lake,           // Large body of water
        Pond,           // Small body of water
        SpecialTree,    // Unique or large tree
        RockFormation,  // Interesting rock structure
        Waterfall,      // Flowing water feature
        
        // Landmark types
        Tower,          // Tall structure visible from a distance
        Statue,         // Decorative structure
        Obelisk,        // Tall, thin monument
        
        // Resource types
        Quarry,         // Stone resource
        OreDeposit,     // Metal resource
        MagicSpring     // Special water resource
    }

    /// <summary>
    /// Enum defining the size/scale of a Point of Interest
    /// </summary>
    public enum POISize
    {
        Tiny,     // Very small, affecting just a few blocks
        Small,    // Small area of influence
        Medium,   // Medium area of influence
        Large,    // Large area of influence
        Huge      // Very large, affecting a significant area
    }
}
