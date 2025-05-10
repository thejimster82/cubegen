using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using CubeGen.World.Common;

namespace CubeGen.World.Generation.POI
{
    /// <summary>
    /// Manages the generation and tracking of Points of Interest in the world
    /// </summary>
    public class POIManager
    {
        // Singleton instance
        private static POIManager _instance;
        public static POIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new POIManager();
                }
                return _instance;
            }
        }
        
        // World seed for consistent generation
        private int _worldSeed;
        
        // Minimum distance between POIs (in world units)
        private int _minDistanceBetweenPOIs = 100;
        
        // Dictionary to store all generated POIs by their position
        private ConcurrentDictionary<Vector2I, PointOfInterest> _pointsOfInterest = new ConcurrentDictionary<Vector2I, PointOfInterest>();
        
        // Dictionary to track POIs by region (for faster lookup)
        // Key is region coordinates, value is list of POIs in that region
        private ConcurrentDictionary<Vector2I, List<PointOfInterest>> _poiRegions = new ConcurrentDictionary<Vector2I, List<PointOfInterest>>();
        
        // Region size for POI organization (larger than chunk size)
        private const int REGION_SIZE = 256;
        
        // Noise generator for POI placement
        private FastNoiseLite _poiPlacementNoise;
        
        // Private constructor for singleton
        private POIManager()
        {
            // Initialize noise generator
            _poiPlacementNoise = new FastNoiseLite();
            _poiPlacementNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
            _poiPlacementNoise.CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean;
            _poiPlacementNoise.CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.CellValue;
            _poiPlacementNoise.Frequency = 0.001f; // Low frequency for sparse POI placement
        }
        
        /// <summary>
        /// Initialize the POI manager with the world seed
        /// </summary>
        public void Initialize(int worldSeed)
        {
            _worldSeed = worldSeed;
            _poiPlacementNoise.Seed = worldSeed;
            
            GD.Print($"POIManager initialized with seed: {worldSeed}");
        }
        
        /// <summary>
        /// Get the region coordinates for a world position
        /// </summary>
        private Vector2I GetRegionForPosition(Vector2I position)
        {
            return new Vector2I(
                Mathf.FloorToInt((float)position.X / REGION_SIZE),
                Mathf.FloorToInt((float)position.Y / REGION_SIZE)
            );
        }
        
        /// <summary>
        /// Generate POIs for a specific region if they don't already exist
        /// </summary>
        public void GeneratePOIsForRegion(Vector2I regionCoords)
        {
            // Check if we've already generated POIs for this region
            if (_poiRegions.ContainsKey(regionCoords))
            {
                return;
            }
            
            // Create a new list for this region
            List<PointOfInterest> regionPOIs = new List<PointOfInterest>();
            
            // Calculate world bounds for this region
            int regionStartX = regionCoords.X * REGION_SIZE;
            int regionStartZ = regionCoords.Y * REGION_SIZE;
            
            // Create a random generator for this region
            Random random = new Random(_worldSeed + regionCoords.X * 73856093 + regionCoords.Y * 19349663);
            
            // Determine how many POIs to generate in this region
            // Base chance on noise to create a more natural distribution
            float regionNoise = _poiPlacementNoise.GetNoise2D(regionCoords.X, regionCoords.Y);
            regionNoise = (regionNoise + 1f) * 0.5f; // Convert from [-1,1] to [0,1]
        
            // Determine number of POIs (1-3 based on noise value)
            int poiCount = 1;
            if (regionNoise > 0.8f) poiCount = 2;
            if (regionNoise > 0.9f) poiCount = 3;
            
            // Generate each POI
            for (int i = 0; i < poiCount; i++)
            {
                // Generate a position within the region
                int posX = regionStartX + random.Next(REGION_SIZE);
                int posZ = regionStartZ + random.Next(REGION_SIZE);
                Vector2I poiPosition = new Vector2I(posX, posZ);
                
                // Get biome type for this position
                BiomeType biomeType = WorldGenerator.GetBiomeType(posX, posZ);
                
                // Determine POI type based on biome
                POIType poiType = DeterminePOITypeForBiome(biomeType, random);
                
                // Determine POI size (weighted toward medium)
                POISize poiSize = DeterminePOISize(random);
                
                // Create the POI
                PointOfInterest poi = new PointOfInterest(poiPosition, poiType, poiSize, biomeType, _worldSeed);
                
                // Add to collections
                if (_pointsOfInterest.TryAdd(poiPosition, poi))
                {
                    regionPOIs.Add(poi);
                }
            }
            
            
            // Add the list to the regions dictionary
            _poiRegions[regionCoords] = regionPOIs;
        }
        
        /// <summary>
        /// Determine appropriate POI type based on biome
        /// </summary>
        private POIType DeterminePOITypeForBiome(BiomeType biomeType, Random random)
        {
            // List of possible POI types for each biome
            List<POIType> possibleTypes = new List<POIType>();
            
            switch (biomeType)
            {
                case BiomeType.ForestLands:
                    possibleTypes.Add(POIType.Village);
                    possibleTypes.Add(POIType.Camp);
                    possibleTypes.Add(POIType.SpecialTree);
                    possibleTypes.Add(POIType.Ruin);
                    possibleTypes.Add(POIType.Tower);
                    break;
                    
                case BiomeType.Desert:
                    possibleTypes.Add(POIType.Ruin);
                    possibleTypes.Add(POIType.Obelisk);
                    possibleTypes.Add(POIType.OreDeposit);
                    possibleTypes.Add(POIType.Camp);
                    break;
                    
                case BiomeType.Tundra:
                    possibleTypes.Add(POIType.Cave);
                    possibleTypes.Add(POIType.RockFormation);
                    possibleTypes.Add(POIType.Tower);
                    possibleTypes.Add(POIType.MagicSpring);
                    break;
                    
                case BiomeType.Islands:
                    possibleTypes.Add(POIType.Pond);
                    possibleTypes.Add(POIType.Waterfall);
                    possibleTypes.Add(POIType.Camp);
                    possibleTypes.Add(POIType.SpecialTree);
                    break;
                    
                default:
                    possibleTypes.Add(POIType.RockFormation);
                    possibleTypes.Add(POIType.SpecialTree);
                    break;
            }
            
            // Select a random POI type from the possibilities
            int index = random.Next(possibleTypes.Count);
            return possibleTypes[index];
        }
        
        /// <summary>
        /// Determine POI size with a weighted distribution
        /// </summary>
        private POISize DeterminePOISize(Random random)
        {
            float value = (float)random.NextDouble();
            
            if (value < 0.1f) return POISize.Tiny;      // 10% chance
            if (value < 0.3f) return POISize.Small;     // 20% chance
            if (value < 0.7f) return POISize.Medium;    // 40% chance
            if (value < 0.9f) return POISize.Large;     // 20% chance
            return POISize.Huge;                        // 10% chance
        }
        
        /// <summary>
        /// Get all POIs within a certain radius of a position
        /// </summary>
        public List<PointOfInterest> GetPOIsInRadius(Vector2I position, int radius)
        {
            List<PointOfInterest> result = new List<PointOfInterest>();
            
            // Calculate the regions that could contain POIs within this radius
            int regionRadius = Mathf.CeilToInt((float)radius / REGION_SIZE) + 1;
            Vector2I centerRegion = GetRegionForPosition(position);
            
            // Check each region in the area
            for (int rx = -regionRadius; rx <= regionRadius; rx++)
            {
                for (int rz = -regionRadius; rz <= regionRadius; rz++)
                {
                    Vector2I regionToCheck = new Vector2I(centerRegion.X + rx, centerRegion.Y + rz);
                    
                    // Generate POIs for this region if needed
                    GeneratePOIsForRegion(regionToCheck);
                    
                    // Get POIs from this region
                    if (_poiRegions.TryGetValue(regionToCheck, out List<PointOfInterest> regionPOIs))
                    {
                        // Check each POI's distance
                        foreach (PointOfInterest poi in regionPOIs)
                        {
                            int dx = poi.Position.X - position.X;
                            int dz = poi.Position.Y - position.Y;
                            float distanceSquared = dx * dx + dz * dz;
                            
                            if (distanceSquared <= radius * radius)
                            {
                                result.Add(poi);
                            }
                        }
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Get the nearest POI to a position
        /// </summary>
        public PointOfInterest GetNearestPOI(Vector2I position, int maxSearchRadius = 500)
        {
            PointOfInterest nearest = null;
            float nearestDistanceSquared = float.MaxValue;
            
            // Start with a small radius and expand until we find something
            for (int radius = 100; radius <= maxSearchRadius; radius += 100)
            {
                List<PointOfInterest> pois = GetPOIsInRadius(position, radius);
                
                foreach (PointOfInterest poi in pois)
                {
                    int dx = poi.Position.X - position.X;
                    int dz = poi.Position.Y - position.Y;
                    float distanceSquared = dx * dx + dz * dz;
                    
                    if (distanceSquared < nearestDistanceSquared)
                    {
                        nearest = poi;
                        nearestDistanceSquared = distanceSquared;
                    }
                }
                
                // If we found at least one POI, return the nearest
                if (nearest != null)
                {
                    return nearest;
                }
            }
            
            return null; // No POI found within max search radius
        }
    }
}
