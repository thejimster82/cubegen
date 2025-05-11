using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.POI
{
    /// <summary>
    /// Represents a Point of Interest in the world
    /// </summary>
    public class PointOfInterest
    {
        // Basic properties
        public POIType Type { get; private set; }
        public Vector2I Center { get; private set; }  // Center position in world coordinates
        public int Radius { get; private set; }       // Radius of influence in world units
        public POISize Size { get; private set; }
        public POIInfluence Influence { get; private set; }
        public int Seed { get; private set; }         // Seed for this specific POI
        
        // Additional properties
        public string Name { get; set; }              // Unique name for this POI
        public BiomeType OriginBiome { get; set; }    // The biome this POI originated in
        
        // Bounds of the POI in world coordinates
        public Rect2I Bounds { get; private set; }
        
        // Dictionary to store custom properties for this POI
        private Dictionary<string, object> _properties = new Dictionary<string, object>();
        
        /// <summary>
        /// Creates a new Point of Interest
        /// </summary>
        public PointOfInterest(POIType type, Vector2I center, int radius, POISize size, POIInfluence influence, int seed)
        {
            Type = type;
            Center = center;
            Radius = radius;
            Size = size;
            Influence = influence;
            Seed = seed;
            
            // Calculate bounds
            Bounds = new Rect2I(
                center.X - radius, 
                center.Y - radius, 
                radius * 2, 
                radius * 2
            );
            
            // Generate a default name based on type and position
            Name = $"{Type}_{center.X}_{center.Y}";
        }
        
        /// <summary>
        /// Checks if a world position is within this POI's bounds
        /// </summary>
        public bool ContainsPosition(int worldX, int worldZ)
        {
            // Calculate distance squared from center
            int dx = worldX - Center.X;
            int dz = worldZ - Center.Y;
            int distanceSquared = dx * dx + dz * dz;
            
            // Check if within radius
            return distanceSquared <= Radius * Radius;
        }
        
        /// <summary>
        /// Checks if a chunk is within or intersects this POI's bounds
        /// </summary>
        public bool IntersectsChunk(Vector2I chunkPos, int chunkSize)
        {
            // Calculate chunk bounds
            Rect2I chunkBounds = new Rect2I(
                chunkPos.X * chunkSize,
                chunkPos.Y * chunkSize,
                chunkSize,
                chunkSize
            );
            
            // Check if the chunk bounds intersect with the POI bounds
            return Bounds.Intersects(chunkBounds);
        }
        
        /// <summary>
        /// Gets the influence factor at a specific world position (0.0 to 1.0)
        /// 1.0 = full influence (at center), 0.0 = no influence (outside radius)
        /// </summary>
        public float GetInfluenceFactor(int worldX, int worldZ)
        {
            // Calculate distance from center
            int dx = worldX - Center.X;
            int dz = worldZ - Center.Y;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            
            // If outside radius, no influence
            if (distance > Radius)
                return 0.0f;
                
            // Calculate influence factor (1.0 at center, 0.0 at radius)
            // Using smooth falloff for more natural blending
            return 1.0f - Mathf.Pow(distance / Radius, 2);
        }
        
        /// <summary>
        /// Sets a custom property for this POI
        /// </summary>
        public void SetProperty(string key, object value)
        {
            _properties[key] = value;
        }
        
        /// <summary>
        /// Gets a custom property for this POI
        /// </summary>
        public T GetProperty<T>(string key, T defaultValue = default)
        {
            if (_properties.TryGetValue(key, out object value) && value is T typedValue)
            {
                return typedValue;
            }
            
            return defaultValue;
        }
    }
}
