using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using CubeGen.World.Common;
using CubeGen.World.Generation;

namespace CubeGen.World.POI
{
    /// <summary>
    /// Singleton class responsible for generating and managing Points of Interest
    /// </summary>
    public class POIGenerator
    {
        // Singleton instance
        private static POIGenerator _instance;
        public static POIGenerator Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new POIGenerator();
                }
                return _instance;
            }
        }
        
        // POI storage - thread-safe collection
        private ConcurrentDictionary<string, PointOfInterest> _pointsOfInterest = new ConcurrentDictionary<string, PointOfInterest>();
        
        // Noise generators for POI placement
        private FastNoiseLite _poiPlacementNoise;
        private FastNoiseLite _poiTypeNoise;
        private FastNoiseLite _poiSizeNoise;
        
        // Configuration
        private int _seed = 0;
        private bool _isInitialized = false;
        private float _poiDensity = 0.5f; // Controls how many POIs are generated
        private int _minDistanceBetweenPOIs = 100; // Minimum distance between POIs in world units
        
        // POI type probabilities by biome
        private Dictionary<BiomeType, Dictionary<POIType, float>> _poiTypeProbabilities = new Dictionary<BiomeType, Dictionary<POIType, float>>();
        
        // Private constructor to enforce singleton
        private POIGenerator()
        {
            // Initialize noise generators
            _poiPlacementNoise = new FastNoiseLite();
            _poiTypeNoise = new FastNoiseLite();
            _poiSizeNoise = new FastNoiseLite();
        }
        
        /// <summary>
        /// Initialize the POI generator with the given seed
        /// </summary>
        public void Initialize(int seed)
        {
            _seed = seed;
            
            // Initialize noise generators
            _poiPlacementNoise.Seed = seed;
            _poiPlacementNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _poiPlacementNoise.Frequency = 0.001f; // Very low frequency for sparse POI placement
            
            _poiTypeNoise.Seed = seed + 1000; // Different seed for type variation
            _poiTypeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _poiTypeNoise.Frequency = 0.01f;
            
            _poiSizeNoise.Seed = seed + 2000; // Different seed for size variation
            _poiSizeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _poiSizeNoise.Frequency = 0.005f;
            
            // Initialize POI type probabilities
            InitializePOITypeProbabilities();
            
            _isInitialized = true;
            GD.Print($"POIGenerator initialized with seed: {seed}");
        }
        
        /// <summary>
        /// Initialize the probabilities of different POI types for each biome
        /// </summary>
        private void InitializePOITypeProbabilities()
        {
            // ForestLands biome POI probabilities
            var forestPOIs = new Dictionary<POIType, float>
            {
                { POIType.LargeRock, 0.2f },
                { POIType.Lake, 0.3f },
                { POIType.Town, 0.2f },
                { POIType.Farm, 0.3f },
                { POIType.Ruins, 0.1f },
                { POIType.TestSphere, 0.1f } // For testing
            };
            _poiTypeProbabilities[BiomeType.ForestLands] = forestPOIs;
            
            // Desert biome POI probabilities
            var desertPOIs = new Dictionary<POIType, float>
            {
                { POIType.LargeRock, 0.4f },
                { POIType.Volcano, 0.2f },
                { POIType.Ruins, 0.3f },
                { POIType.TestSphere, 0.1f } // For testing
            };
            _poiTypeProbabilities[BiomeType.Desert] = desertPOIs;
            
            // Tundra biome POI probabilities
            var tundraPOIs = new Dictionary<POIType, float>
            {
                { POIType.LargeRock, 0.4f },
                { POIType.Lake, 0.3f },
                { POIType.Ruins, 0.2f },
                { POIType.TestSphere, 0.1f } // For testing
            };
            _poiTypeProbabilities[BiomeType.Tundra] = tundraPOIs;
            
            // Islands biome POI probabilities
            var islandsPOIs = new Dictionary<POIType, float>
            {
                { POIType.LargeRock, 0.2f },
                { POIType.Lake, 0.4f },
                { POIType.Ruins, 0.3f },
                { POIType.TestSphere, 0.1f } // For testing
            };
            _poiTypeProbabilities[BiomeType.Islands] = islandsPOIs;
        }
        
        /// <summary>
        /// Determines if a POI should be generated at the given world position
        /// </summary>
        public bool ShouldGeneratePOI(int worldX, int worldZ)
        {
            if (!_isInitialized)
            {
                GD.PrintErr("POIGenerator not initialized!");
                return false;
            }
            
            // Use noise to determine if a POI should be generated
            float noiseValue = _poiPlacementNoise.GetNoise2D(worldX, worldZ);
            
            // Convert from [-1,1] to [0,1] range
            noiseValue = (noiseValue + 1.0f) * 0.5f;
            
            // Check if noise value exceeds threshold
            return noiseValue > (1.0f - _poiDensity);
        }
        
        /// <summary>
        /// Gets or generates a POI at the given world position
        /// </summary>
        public PointOfInterest GetOrGeneratePOI(int worldX, int worldZ)
        {
            // Check if a POI already exists at this position
            string poiKey = $"{worldX}_{worldZ}";
            if (_pointsOfInterest.TryGetValue(poiKey, out PointOfInterest existingPOI))
            {
                return existingPOI;
            }
            
            // Check if we should generate a POI here
            if (!ShouldGeneratePOI(worldX, worldZ))
            {
                return null;
            }
            
            // Check if this position is too close to an existing POI
            if (IsTooCloseToExistingPOI(worldX, worldZ))
            {
                return null;
            }
            
            // Get the biome at this position
            BiomeType biomeType = WorldGenerator.GetBiomeType(worldX, worldZ);
            
            // Generate a new POI
            PointOfInterest newPOI = GeneratePOI(worldX, worldZ, biomeType);
            
            // Add to collection if valid
            if (newPOI != null)
            {
                _pointsOfInterest[poiKey] = newPOI;
                GD.Print($"Generated new POI: {newPOI.Name} at {worldX}, {worldZ}");
            }
            
            return newPOI;
        }
        
        /// <summary>
        /// Checks if a position is too close to an existing POI
        /// </summary>
        private bool IsTooCloseToExistingPOI(int worldX, int worldZ)
        {
            // Check distance to all existing POIs
            foreach (var poi in _pointsOfInterest.Values)
            {
                int dx = worldX - poi.Center.X;
                int dz = worldZ - poi.Center.Y;
                int distanceSquared = dx * dx + dz * dz;
                
                // If too close, return true
                if (distanceSquared < _minDistanceBetweenPOIs * _minDistanceBetweenPOIs)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Generates a new POI at the given world position
        /// </summary>
        private PointOfInterest GeneratePOI(int worldX, int worldZ, BiomeType biomeType)
        {
            // Create a random generator with a seed based on position
            Random random = new Random(_seed + worldX * 1000 + worldZ);
            
            // Determine POI type based on biome
            POIType poiType = DeterminePOIType(worldX, worldZ, biomeType, random);
            
            // Determine POI size
            POISize poiSize = DeterminePOISize(worldX, worldZ, poiType, random);
            
            // Determine radius based on size
            int radius = DetermineRadius(poiSize, random);
            
            // Determine influence type
            POIInfluence influence = DetermineInfluence(poiType, random);
            
            // Create the POI
            PointOfInterest poi = new PointOfInterest(
                poiType,
                new Vector2I(worldX, worldZ),
                radius,
                poiSize,
                influence,
                _seed + worldX * 10000 + worldZ
            );
            
            // Set origin biome
            poi.OriginBiome = biomeType;
            
            return poi;
        }
        
        /// <summary>
        /// Determines the type of POI to generate based on biome and position
        /// </summary>
        private POIType DeterminePOIType(int worldX, int worldZ, BiomeType biomeType, Random random)
        {
            // Get probabilities for this biome
            if (!_poiTypeProbabilities.TryGetValue(biomeType, out var probabilities))
            {
                // Default to test sphere if no probabilities defined
                return POIType.TestSphere;
            }
            
            // Use weighted random selection
            float totalProbability = probabilities.Values.Sum();
            float randomValue = (float)random.NextDouble() * totalProbability;
            
            float cumulativeProbability = 0f;
            foreach (var kvp in probabilities)
            {
                cumulativeProbability += kvp.Value;
                if (randomValue <= cumulativeProbability)
                {
                    return kvp.Key;
                }
            }
            
            // Default to first type if something went wrong
            return probabilities.Keys.First();
        }
        
        /// <summary>
        /// Determines the size of the POI based on type and position
        /// </summary>
        private POISize DeterminePOISize(int worldX, int worldZ, POIType poiType, Random random)
        {
            // Use noise to influence size
            float noiseValue = _poiSizeNoise.GetNoise2D(worldX, worldZ);
            
            // Convert from [-1,1] to [0,1] range
            noiseValue = (noiseValue + 1.0f) * 0.5f;
            
            // Adjust based on POI type
            switch (poiType)
            {
                case POIType.Town:
                case POIType.Volcano:
                    // Towns and volcanoes tend to be larger
                    if (noiseValue < 0.3f) return POISize.Medium;
                    if (noiseValue < 0.7f) return POISize.Large;
                    return POISize.Massive;
                    
                case POIType.Farm:
                case POIType.Lake:
                    // Farms and lakes are medium to large
                    if (noiseValue < 0.4f) return POISize.Medium;
                    if (noiseValue < 0.9f) return POISize.Large;
                    return POISize.Massive;
                    
                case POIType.Ruins:
                case POIType.LargeRock:
                    // Ruins and rocks are small to medium
                    if (noiseValue < 0.6f) return POISize.Small;
                    return POISize.Medium;
                    
                case POIType.TestSphere:
                    // Test sphere is always medium
                    return POISize.Medium;
                    
                default:
                    // Default size distribution
                    if (noiseValue < 0.4f) return POISize.Small;
                    if (noiseValue < 0.8f) return POISize.Medium;
                    return POISize.Large;
            }
        }
        
        /// <summary>
        /// Determines the radius of the POI based on its size
        /// </summary>
        private int DetermineRadius(POISize size, Random random)
        {
            switch (size)
            {
                case POISize.Small:
                    return random.Next(15, 25);
                case POISize.Medium:
                    return random.Next(25, 40);
                case POISize.Large:
                    return random.Next(40, 60);
                case POISize.Massive:
                    return random.Next(60, 100);
                default:
                    return 30; // Default radius for test sphere
            }
        }
        
        /// <summary>
        /// Determines the influence type based on POI type
        /// </summary>
        private POIInfluence DetermineInfluence(POIType poiType, Random random)
        {
            switch (poiType)
            {
                case POIType.Volcano:
                    return POIInfluence.Terrain;
                case POIType.Lake:
                    return POIInfluence.Water;
                case POIType.Farm:
                    return POIInfluence.Vegetation;
                case POIType.Town:
                case POIType.Ruins:
                    return POIInfluence.Combined;
                default:
                    return POIInfluence.None;
            }
        }
        
        /// <summary>
        /// Gets all POIs that intersect with a chunk
        /// </summary>
        public List<PointOfInterest> GetPOIsForChunk(Vector2I chunkPos, int chunkSize)
        {
            List<PointOfInterest> result = new List<PointOfInterest>();
            
            foreach (var poi in _pointsOfInterest.Values)
            {
                if (poi.IntersectsChunk(chunkPos, chunkSize))
                {
                    result.Add(poi);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Gets all POIs
        /// </summary>
        public List<PointOfInterest> GetAllPOIs()
        {
            return _pointsOfInterest.Values.ToList();
        }
        
        /// <summary>
        /// Clears all POIs (for testing/debugging)
        /// </summary>
        public void ClearAllPOIs()
        {
            _pointsOfInterest.Clear();
            GD.Print("Cleared all POIs");
        }
    }
}
