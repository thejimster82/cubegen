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
        private readonly float _poiDensity = 0.3f; // Increased to generate significantly more POIs

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

        // We don't need the cell center cache anymore since we're using a deterministic approach

        /// <summary>
        /// Find the nearest cell center for a given position
        /// </summary>
        private Vector2I GetCellCenter(int worldX, int worldZ)
        {
            // Since FastNoiseLite doesn't have a direct method to get cell centers,
            // we'll use a grid-based approach with the cell size determined by frequency

            // Calculate the cell size based on noise frequency
            float cellSize = 1.0f / _poiPlacementNoise.Frequency;

            // Calculate the cell coordinates
            int cellX = Mathf.FloorToInt(worldX / cellSize);
            int cellZ = Mathf.FloorToInt(worldZ / cellSize);

            // Calculate the cell center
            Vector2I cellCenter = new Vector2I(
                Mathf.FloorToInt(cellX * cellSize + cellSize / 2),
                Mathf.FloorToInt(cellZ * cellSize + cellSize / 2)
            );

            return cellCenter;
        }

        /// <summary>
        /// Check if a POI exists at a specific world position
        /// </summary>
        public bool DoesPOIExistAt(int worldX, int worldZ)
        {
            // Get the cell center for this position
            Vector2I cellCenter = GetCellCenter(worldX, worldZ);

            // Get the distance from this position to the cell center
            int dx = worldX - cellCenter.X;
            int dz = worldZ - cellCenter.Y;
            float distanceSquared = dx * dx + dz * dz;

            // Check if this position is close enough to the cell center
            float maxDistanceSquared = 4 * 4; // Only consider positions within 4 units of cell center

            if (distanceSquared > maxDistanceSquared)
            {
                return false;
            }

            // Get noise value at the cell center
            float noiseValue = _poiPlacementNoise.GetNoise2D(cellCenter.X, cellCenter.Y);

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
            // Get the cell center for this position
            Vector2I cellCenter = GetCellCenter(worldX, worldZ);

            // Check if a POI exists at the cell center
            float noiseValue = _poiPlacementNoise.GetNoise2D(cellCenter.X, cellCenter.Y);
            noiseValue = (noiseValue + 1f) * 0.5f;

            if (noiseValue <= (1f - _poiDensity))
            {
                return null;
            }

            // Get the distance from this position to the cell center
            int dx = worldX - cellCenter.X;
            int dz = worldZ - cellCenter.Y;
            float distanceSquared = dx * dx + dz * dz;

            // Only create POIs at or very near cell centers to avoid duplicates
            float maxDistanceSquared = 4 * 4; // Only consider positions within 4 units of cell center

            if (distanceSquared > maxDistanceSquared)
            {
                return null;
            }

            // Cache the cell center to avoid duplicates
            Vector2I poiPosition = cellCenter;

            // Get biome type for this position
            BiomeType biomeType = WorldGenerator.GetBiomeType(poiPosition.X, poiPosition.Y);

            // Determine POI type based on biome and noise
            float typeNoise = _poiTypeNoise.GetNoise2D(poiPosition.X, poiPosition.Y);
            typeNoise = (typeNoise + 1f) * 0.5f; // Convert to [0,1]
            POIType poiType = DeterminePOITypeForBiome(biomeType, typeNoise);

            // Determine POI size based on noise
            float sizeNoise = _poiSizeNoise.GetNoise2D(poiPosition.X, poiPosition.Y);
            sizeNoise = (sizeNoise + 1f) * 0.5f; // Convert to [0,1]
            POISize poiSize = DeterminePOISize(sizeNoise);

            // Create the POI
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

            // Increase search radius to ensure we capture all POIs that could affect the chunk
            // Use the full maxInfluenceRadius to ensure we don't miss any POIs at the edges
            int searchRadius = maxInfluenceRadius;

            // Calculate search area
            int minX = chunkWorldX - searchRadius;
            int maxX = chunkWorldX + chunkSize + searchRadius;
            int minZ = chunkWorldZ - searchRadius;
            int maxZ = chunkWorldZ + chunkSize + searchRadius;

            // Use a smaller grid step for more thorough coverage
            int gridStep = 2; // Reduced from 4 to 2 for much better coverage

            // Get all cell centers in the search area first
            HashSet<Vector2I> cellCenters = new HashSet<Vector2I>();

            for (int x = minX; x <= maxX; x += gridStep)
            {
                for (int z = minZ; z <= maxZ; z += gridStep)
                {
                    Vector2I cellCenter = GetCellCenter(x, z);
                    cellCenters.Add(cellCenter);
                }
            }

            // Now check each cell center for POIs
            foreach (Vector2I cellCenter in cellCenters)
            {
                PointOfInterest poi = GetPOIAt(cellCenter.X, cellCenter.Y);

                if (poi != null)
                {
                    // More accurate check if this POI affects the chunk
                    bool affectsChunk = DoesPOIAffectChunk(poi, chunkPos, chunkSize);

                    if (affectsChunk)
                    {
                        // Check if we already have this POI in our results
                        bool alreadyAdded = false;
                        foreach (var existingPoi in result)
                        {
                            if (existingPoi.Position == poi.Position)
                            {
                                alreadyAdded = true;
                                break;
                            }
                        }

                        if (!alreadyAdded)
                        {
                            result.Add(poi);
                            GD.Print($"POI at {poi.Position} affects chunk {chunkPos}");
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Determines if a POI affects a specific chunk
        /// </summary>
        private bool DoesPOIAffectChunk(PointOfInterest poi, Vector2I chunkPos, int chunkSize)
        {
            // Calculate world bounds for this chunk
            int chunkWorldX = chunkPos.X * chunkSize;
            int chunkWorldZ = chunkPos.Y * chunkSize;

            // Test if the POI is inside the chunk
            if (poi.Position.X >= chunkWorldX && poi.Position.X < chunkWorldX + chunkSize &&
                poi.Position.Y >= chunkWorldZ && poi.Position.Y < chunkWorldZ + chunkSize)
            {
                return true;
            }

            // Test against chunk center
            int centerX = chunkWorldX + chunkSize / 2;
            int centerZ = chunkWorldZ + chunkSize / 2;
            int dxCenter = poi.Position.X - centerX;
            int dzCenter = poi.Position.Y - centerZ;
            float distanceToCenter = Mathf.Sqrt(dxCenter * dxCenter + dzCenter * dzCenter);

            if (distanceToCenter <= poi.InfluenceRadius + chunkSize / 2)
            {
                return true;
            }

            // Test against all corners and edges
            // Test each corner of the chunk
            int[][] corners =
            {
                [chunkWorldX, chunkWorldZ],                    // Bottom-left
                [chunkWorldX + chunkSize, chunkWorldZ],        // Bottom-right
                [chunkWorldX, chunkWorldZ + chunkSize],        // Top-left
                [chunkWorldX + chunkSize, chunkWorldZ + chunkSize]  // Top-right
            };

            foreach (var corner in corners)
            {
                int dx = poi.Position.X - corner[0];
                int dz = poi.Position.Y - corner[1];
                float distanceToCorner = Mathf.Sqrt(dx * dx + dz * dz);

                if (distanceToCorner <= poi.InfluenceRadius)
                {
                    return true;
                }
            }

            // Test against edges
            // Bottom edge
            if (poi.Position.Y < chunkWorldZ &&
                poi.Position.X >= chunkWorldX && poi.Position.X < chunkWorldX + chunkSize &&
                chunkWorldZ - poi.Position.Y <= poi.InfluenceRadius)
            {
                return true;
            }

            // Top edge
            if (poi.Position.Y >= chunkWorldZ + chunkSize &&
                poi.Position.X >= chunkWorldX && poi.Position.X < chunkWorldX + chunkSize &&
                poi.Position.Y - (chunkWorldZ + chunkSize) <= poi.InfluenceRadius)
            {
                return true;
            }

            // Left edge
            if (poi.Position.X < chunkWorldX &&
                poi.Position.Y >= chunkWorldZ && poi.Position.Y < chunkWorldZ + chunkSize &&
                chunkWorldX - poi.Position.X <= poi.InfluenceRadius)
            {
                return true;
            }

            // Right edge
            if (poi.Position.X >= chunkWorldX + chunkSize &&
                poi.Position.Y >= chunkWorldZ && poi.Position.Y < chunkWorldZ + chunkSize &&
                poi.Position.X - (chunkWorldX + chunkSize) <= poi.InfluenceRadius)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determine appropriate POI type based on biome and noise
        /// </summary>
        private static POIType DeterminePOITypeForBiome(BiomeType biomeType, float noiseValue)
        {
            // List of possible POI types for each biome
            List<POIType> possibleTypes = new();

            switch (biomeType)
            {
                case BiomeType.ForestLands:
                    possibleTypes.Add(POIType.Tower);
                    break;

                case BiomeType.Desert:
                    possibleTypes.Add(POIType.Ruin);
                    break;

                case BiomeType.Tundra:
                    possibleTypes.Add(POIType.RockFormation);
                    break;

                case BiomeType.Islands:
                    possibleTypes.Add(POIType.Waterfall);
                    break;

                default:
                    possibleTypes.Add(POIType.RockFormation);
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
        private static POISize DeterminePOISize(float noiseValue)
        {
            if (noiseValue < 0.1f) return POISize.Tiny;      // 10% chance
            if (noiseValue < 0.3f) return POISize.Small;     // 20% chance
            if (noiseValue < 0.7f) return POISize.Medium;    // 40% chance
            if (noiseValue < 0.9f) return POISize.Large;     // 20% chance
            return POISize.Huge;                             // 10% chance
        }
    }
}
