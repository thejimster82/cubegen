using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using CubeGen.World.Common;

namespace CubeGen.World.Generation.POI
{
    /// <summary>
    /// Registry for POI definitions and instances
    /// </summary>
    public class POIRegistry
    {
        // Singleton instance
        private static POIRegistry _instance;
        public static POIRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new POIRegistry();
                }
                return _instance;
            }
        }

        // Dictionary of POI definitions by ID
        public Dictionary<string, POIDefinition> Definitions { get; private set; } = new Dictionary<string, POIDefinition>();
        
        // List of POI instances in the world
        public List<POIInstance> Instances { get; private set; } = new List<POIInstance>();
        
        // Spatial index for efficient POI queries
        private Dictionary<Vector2I, List<POIInstance>> _spatialIndex = new Dictionary<Vector2I, List<POIInstance>>();
        
        // Constructor - initialize with default POI definitions
        private POIRegistry()
        {
            RegisterDefaultPOIs();
        }
        
        // Register a new POI definition
        public void RegisterPOIDefinition(POIDefinition definition)
        {
            if (Definitions.ContainsKey(definition.Id))
            {
                GD.PrintErr($"POI definition with ID {definition.Id} already exists!");
                return;
            }
            
            Definitions[definition.Id] = definition;
            GD.Print($"Registered POI definition: {definition.Name} (ID: {definition.Id})");
        }
        
        // Get a POI definition by ID
        public POIDefinition GetPOIDefinition(string id)
        {
            if (Definitions.TryGetValue(id, out POIDefinition definition))
            {
                return definition;
            }
            
            GD.PrintErr($"POI definition with ID {id} not found!");
            return null;
        }
        
        // Add a POI instance to the registry
        public void AddPOIInstance(POIInstance instance)
        {
            Instances.Add(instance);
            
            // Add to spatial index
            Vector2I chunkPos = WorldToChunkPos(instance.Position);
            if (!_spatialIndex.ContainsKey(chunkPos))
            {
                _spatialIndex[chunkPos] = new List<POIInstance>();
            }
            _spatialIndex[chunkPos].Add(instance);
            
            GD.Print($"Added POI instance: {instance.Definition.Name} at {instance.Position}");
        }
        
        // Get all POI instances that affect a chunk
        public List<POIInstance> GetPOIsForChunk(Vector2I chunkPos)
        {
            List<POIInstance> result = new List<POIInstance>();
            
            // Check the target chunk and all adjacent chunks
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Vector2I checkPos = new Vector2I(chunkPos.X + x, chunkPos.Y + z);
                    if (_spatialIndex.TryGetValue(checkPos, out List<POIInstance> instances))
                    {
                        result.AddRange(instances);
                    }
                }
            }
            
            return result;
        }
        
        // Get all POI instances of a specific type
        public List<POIInstance> GetPOIsByType(string poiId)
        {
            return Instances.Where(poi => poi.Definition.Id == poiId).ToList();
        }
        
        // Clear all POI instances (e.g., when regenerating the world)
        public void ClearInstances()
        {
            Instances.Clear();
            _spatialIndex.Clear();
            GD.Print("Cleared all POI instances");
        }
        
        // Helper method to convert world position to chunk position
        private Vector2I WorldToChunkPos(Vector3I worldPos)
        {
            int chunkSize = WorldGenerator.CHUNK_SIZE;
            return new Vector2I(
                Mathf.FloorToInt((float)worldPos.X / chunkSize),
                Mathf.FloorToInt((float)worldPos.Z / chunkSize)
            );
        }
        
        // Register default POI definitions
        private void RegisterDefaultPOIs()
        {
            // Village POI
            RegisterPOIDefinition(new POIDefinition(
                "village",
                "Village",
                new Vector3I(48, 16, 48), // 48x48 voxel area, 16 voxels high
                new List<BiomeType> { BiomeType.ForestLands, BiomeType.Desert, BiomeType.Tundra },
                0.7f, // High spawn probability
                200, // Minimum 200 voxels between villages
                new TerrainModificationRules(
                    null, // No fixed height, adapt to terrain
                    16,   // 16 voxel blend distance
                    2,    // 2 voxel foundation depth
                    true  // Clear water in village area
                ),
                new FloraRules(
                    8, // 8 voxel clearance for natural flora
                    new Dictionary<VoxelType, float> // Village-specific flora
                    {
                        { VoxelType.TallGrass, 0.1f },
                        { VoxelType.Flower, 0.05f }
                    }
                )
            ));
            
            // Ruins POI
            RegisterPOIDefinition(new POIDefinition(
                "ruins",
                "Ancient Ruins",
                new Vector3I(32, 12, 32), // 32x32 voxel area, 12 voxels high
                new List<BiomeType> { BiomeType.ForestLands, BiomeType.Desert, BiomeType.Tundra },
                0.5f, // Medium spawn probability
                150, // Minimum 150 voxels between ruins
                new TerrainModificationRules(
                    null, // No fixed height, adapt to terrain
                    12,   // 12 voxel blend distance
                    3,    // 3 voxel foundation depth
                    false // Don't clear water (ruins can be partially submerged)
                ),
                new FloraRules(
                    4, // 4 voxel clearance for natural flora
                    new Dictionary<VoxelType, float> // Ruins-specific flora
                    {
                        { VoxelType.TallGrass, 0.2f },
                        { VoxelType.Rock, 0.1f }
                    }
                )
            ));
            
            // Oasis POI (Desert-specific)
            RegisterPOIDefinition(new POIDefinition(
                "oasis",
                "Oasis",
                new Vector3I(24, 8, 24), // 24x24 voxel area, 8 voxels high
                new List<BiomeType> { BiomeType.Desert },
                0.8f, // High spawn probability in desert
                180, // Minimum 180 voxels between oases
                new TerrainModificationRules(
                    null, // No fixed height, adapt to terrain
                    10,   // 10 voxel blend distance
                    1,    // 1 voxel foundation depth
                    false // Don't clear water (oasis has water)
                ),
                new FloraRules(
                    0, // No clearance for natural flora
                    new Dictionary<VoxelType, float> // Oasis-specific flora
                    {
                        { VoxelType.TallGrass, 0.3f },
                        { VoxelType.Flower, 0.1f }
                    }
                )
            ));
        }
    }
}
