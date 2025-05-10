using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation.POI
{
    /// <summary>
    /// Defines a Point of Interest (POI) type with its properties and generation rules
    /// </summary>
    public class POIDefinition
    {
        // Unique identifier for this POI type
        public string Id { get; private set; }
        
        // Display name for this POI type
        public string Name { get; private set; }
        
        // Dimensions of the POI (in voxels)
        public Vector3I Dimensions { get; private set; }
        
        // Biomes where this POI can spawn
        public List<BiomeType> ValidBiomes { get; private set; }
        
        // Probability of this POI spawning (relative to other POIs)
        public float SpawnProbability { get; private set; }
        
        // Minimum distance between POIs of this type (in voxels)
        public int MinDistanceBetween { get; private set; }
        
        // Terrain modification parameters
        public TerrainModificationRules TerrainRules { get; private set; }
        
        // Flora/detail rules
        public FloraRules FloraRules { get; private set; }
        
        // Constructor
        public POIDefinition(
            string id, 
            string name, 
            Vector3I dimensions, 
            List<BiomeType> validBiomes, 
            float spawnProbability, 
            int minDistanceBetween,
            TerrainModificationRules terrainRules,
            FloraRules floraRules)
        {
            Id = id;
            Name = name;
            Dimensions = dimensions;
            ValidBiomes = validBiomes;
            SpawnProbability = spawnProbability;
            MinDistanceBetween = minDistanceBetween;
            TerrainRules = terrainRules;
            FloraRules = floraRules;
        }
    }

    /// <summary>
    /// Rules for how a POI modifies the terrain
    /// </summary>
    public class TerrainModificationRules
    {
        // Target height for the POI (absolute Y value or null for no flattening)
        public int? TargetHeight { get; private set; }
        
        // Distance over which to blend from natural terrain to POI terrain
        public int BlendDistance { get; private set; }
        
        // Depth of the foundation below the surface
        public int FoundationDepth { get; private set; }
        
        // Whether to clear water within the POI area
        public bool ClearWater { get; private set; }
        
        // Constructor
        public TerrainModificationRules(
            int? targetHeight, 
            int blendDistance, 
            int foundationDepth, 
            bool clearWater)
        {
            TargetHeight = targetHeight;
            BlendDistance = blendDistance;
            FoundationDepth = foundationDepth;
            ClearWater = clearWater;
        }
    }

    /// <summary>
    /// Rules for flora and detail placement around a POI
    /// </summary>
    public class FloraRules
    {
        // Distance around the POI where natural flora should not spawn
        public int ClearanceRadius { get; private set; }
        
        // POI-specific flora/details to place
        public Dictionary<VoxelType, float> POISpecificFlora { get; private set; }
        
        // Constructor
        public FloraRules(
            int clearanceRadius, 
            Dictionary<VoxelType, float> poiSpecificFlora)
        {
            ClearanceRadius = clearanceRadius;
            POISpecificFlora = poiSpecificFlora;
        }
    }

    /// <summary>
    /// Represents an instance of a POI placed in the world
    /// </summary>
    public class POIInstance
    {
        // Reference to the POI definition
        public POIDefinition Definition { get; private set; }
        
        // Position of the POI in the world (bottom center)
        public Vector3I Position { get; private set; }
        
        // Rotation of the POI (in degrees, around Y axis)
        public int Rotation { get; private set; }
        
        // Seed for this specific POI instance
        public int Seed { get; private set; }
        
        // Terrain height at the POI center (before modification)
        public int TerrainHeight { get; set; }
        
        // Constructor
        public POIInstance(
            POIDefinition definition, 
            Vector3I position, 
            int rotation, 
            int seed)
        {
            Definition = definition;
            Position = position;
            Rotation = rotation;
            Seed = seed;
        }
        
        // Get the bounding box of this POI instance
        public Aabb GetBoundingBox()
        {
            // Calculate the size of the POI including the blend distance
            Vector3 size = new Vector3(
                Definition.Dimensions.X + Definition.TerrainRules.BlendDistance * 2,
                Definition.Dimensions.Y,
                Definition.Dimensions.Z + Definition.TerrainRules.BlendDistance * 2
            );
            
            // Calculate the position of the bottom-left corner
            Vector3 position = new Vector3(
                Position.X - size.X / 2,
                Position.Y,
                Position.Z - size.Z / 2
            );
            
            return new Aabb(position, size);
        }
    }
}
