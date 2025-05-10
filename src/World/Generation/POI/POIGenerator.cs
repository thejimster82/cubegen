using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation.POI
{
    /// <summary>
    /// Generates Points of Interest using deterministic noise functions
    /// This approach directly calculates POI locations without storing them in memory
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

        // World seed for consistent generation
        private int _worldSeed;

        // Noise generators for different aspects of POI generation
        private FastNoiseLite _poiPlacementNoise;
        private FastNoiseLite _poiTypeNoise;
        private FastNoiseLite _poiSizeNoise;

        // POI density factor (higher = more POIs)
        private float _poiDensity = 0.1f; // Increased from 0.1f to generate significantly more POIs

        // Minimum distance between POIs (in world units)
        private int _minDistanceBetweenPOIs = 32; // Further reduced to allow even more POIs

        // Private constructor for singleton
        private POIGenerator()
        {
            // Initialize noise generators
            InitializeNoiseGenerators();
        }

        /// <summary>
        /// Initialize the noise generators with default settings
        /// </summary>
        private void InitializeNoiseGenerators()
        {
            // Placement noise (determines if a POI exists at a location)
            _poiPlacementNoise = new FastNoiseLite();
            _poiPlacementNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
            _poiPlacementNoise.CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean;
            _poiPlacementNoise.CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.CellValue;
            _poiPlacementNoise.Frequency = 0.01f; // Increased from 0.005f for more varied POI distribution

            // Type noise (determines POI type)
            _poiTypeNoise = new FastNoiseLite();
            _poiTypeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _poiTypeNoise.Frequency = 0.01f;

            // Size noise (determines POI size)
            _poiSizeNoise = new FastNoiseLite();
            _poiSizeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _poiSizeNoise.Frequency = 0.02f;
        }

        /// <summary>
        /// Initialize the POI generator with the world seed
        /// </summary>
        public void Initialize(int worldSeed)
        {
            _worldSeed = worldSeed;

            // Set seeds for noise generators
            _poiPlacementNoise.Seed = worldSeed;
            _poiTypeNoise.Seed = worldSeed + 12345; // Offset to make it different from placement
            _poiSizeNoise.Seed = worldSeed + 67890; // Different offset for size

            GD.Print($"POIGenerator initialized with seed: {worldSeed}");
        }

        /// <summary>
        /// Check if a POI exists at a specific world position
        /// </summary>
        public bool DoesPOIExistAt(int worldX, int worldZ)
        {
            // Get noise value at this position
            float noiseValue = _poiPlacementNoise.GetNoise2D(worldX, worldZ);

            // Convert from [-1,1] to [0,1]
            noiseValue = (noiseValue + 1f) * 0.5f;

            // POI exists if noise value exceeds threshold
            return noiseValue > (1f - _poiDensity);
        }

        /// <summary>
        /// Get a deterministic POI at a specific world position if one exists
        /// </summary>
        public PointOfInterest GetPOIAt(int worldX, int worldZ)
        {
            // Check if a POI exists at this position
            if (!DoesPOIExistAt(worldX, worldZ))
            {
                return null;
            }

            // Get biome type for this position
            BiomeType biomeType = WorldGenerator.GetBiomeType(worldX, worldZ);

            // Create a deterministic random generator for this position
            Random random = new Random(_worldSeed + worldX * 73856093 + worldZ * 19349663);

            // Determine POI type based on biome and noise
            float typeNoise = _poiTypeNoise.GetNoise2D(worldX, worldZ);
            typeNoise = (typeNoise + 1f) * 0.5f; // Convert to [0,1]
            POIType poiType = DeterminePOITypeForBiome(biomeType, random, typeNoise);

            // Determine POI size based on noise
            float sizeNoise = _poiSizeNoise.GetNoise2D(worldX, worldZ);
            sizeNoise = (sizeNoise + 1f) * 0.5f; // Convert to [0,1]
            POISize poiSize = DeterminePOISize(sizeNoise);

            // Create the POI
            Vector2I poiPosition = new Vector2I(worldX, worldZ);
            PointOfInterest poi = new PointOfInterest(poiPosition, poiType, poiSize, biomeType, _worldSeed);

            return poi;
        }

        /// <summary>
        /// Find all POIs that could affect a specific chunk
        /// </summary>
        public List<PointOfInterest> GetPOIsAffectingChunk(Vector2I chunkPos, int chunkSize, int maxInfluenceRadius)
        {
            List<PointOfInterest> result = new List<PointOfInterest>();

            // Calculate world bounds for this chunk with padding for POI influence
            int chunkWorldX = chunkPos.X * chunkSize;
            int chunkWorldZ = chunkPos.Y * chunkSize;
            int searchRadius = chunkSize / 2 + maxInfluenceRadius;

            // Calculate search area
            int minX = chunkWorldX - searchRadius;
            int maxX = chunkWorldX + chunkSize + searchRadius;
            int minZ = chunkWorldZ - searchRadius;
            int maxZ = chunkWorldZ + chunkSize + searchRadius;

            // Use a grid-based approach to sample potential POI locations
            int gridStep = 8; // Use a fixed small step size for thorough coverage

            for (int x = minX; x <= maxX; x += gridStep)
            {
                for (int z = minZ; z <= maxZ; z += gridStep)
                {
                    // Check if there's a POI at this grid point
                    PointOfInterest poi = GetPOIAt(x, z);

                    if (poi != null)
                    {
                        // Check if this POI could affect the chunk
                        int dx = poi.Position.X - chunkWorldX;
                        int dz = poi.Position.Y - chunkWorldZ;
                        int distanceToChunkX = Math.Max(0, Math.Max(dx, -dx - chunkSize));
                        int distanceToChunkZ = Math.Max(0, Math.Max(dz, -dz - chunkSize));
                        float distanceToChunk = Mathf.Sqrt(distanceToChunkX * distanceToChunkX + distanceToChunkZ * distanceToChunkZ);

                        if (distanceToChunk <= poi.InfluenceRadius)
                        {
                            result.Add(poi);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Determine appropriate POI type based on biome and noise
        /// </summary>
        private POIType DeterminePOITypeForBiome(BiomeType biomeType, Random random, float noiseValue)
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

            // Use noise value to select a POI type
            int index = Mathf.FloorToInt(noiseValue * possibleTypes.Count);
            index = Mathf.Clamp(index, 0, possibleTypes.Count - 1);

            return possibleTypes[index];
        }

        /// <summary>
        /// Determine POI size with a weighted distribution based on noise
        /// </summary>
        private POISize DeterminePOISize(float noiseValue)
        {
            if (noiseValue < 0.1f) return POISize.Tiny;      // 10% chance
            if (noiseValue < 0.3f) return POISize.Small;     // 20% chance
            if (noiseValue < 0.7f) return POISize.Medium;    // 40% chance
            if (noiseValue < 0.9f) return POISize.Large;     // 20% chance
            return POISize.Huge;                             // 10% chance
        }
    }
}
