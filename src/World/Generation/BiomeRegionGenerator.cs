using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
    /// <summary>
    /// Generates biome regions using Voronoi/Cellular noise with domain warping to create contiguous regions
    /// similar to how US states are joined together, with irregular boundaries but similarly-sized regions.
    /// </summary>
    public class BiomeRegionGenerator
    {
        private FastNoiseLite _voronoiNoise;
        private FastNoiseLite _biomeTypeNoise;
        private FastNoiseLite _domainWarpNoise; // Noise for domain warping
        private int _seed;
        private float _regionScale = 0.0005f; // Controls the size of regions (smaller value = larger regions)
        private float _warpStrength = 50.0f; // Controls how much the domain is warped
        private Dictionary<int, BiomeType> _cellToBiomeMap = new Dictionary<int, BiomeType>();
        private static BiomeRegionGenerator _instance;

        // Singleton instance
        public static BiomeRegionGenerator Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new BiomeRegionGenerator();
                }
                return _instance;
            }
        }

        // Initialize with a specific seed
        public void Initialize(int seed)
        {
            _seed = seed;

            // Initialize Voronoi noise for region boundaries
            _voronoiNoise = new FastNoiseLite();
            _voronoiNoise.Seed = seed;
            _voronoiNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
            _voronoiNoise.Frequency = _regionScale;
            _voronoiNoise.CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean;
            _voronoiNoise.CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.CellValue;
            _voronoiNoise.CellularJitter = 0.01f; // Reduced jitter for more uniform cell sizes

            // Initialize domain warping noise
            _domainWarpNoise = new FastNoiseLite();
            _domainWarpNoise.Seed = seed + 500;
            _domainWarpNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
            _domainWarpNoise.Frequency = 0.0025f; // Lower frequency for smoother warping
            _domainWarpNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _domainWarpNoise.FractalOctaves = 3;
            _domainWarpNoise.FractalLacunarity = 2.0f;
            _domainWarpNoise.FractalGain = 0.5f;

            // Initialize noise for determining biome type for each cell
            _biomeTypeNoise = new FastNoiseLite();
            _biomeTypeNoise.Seed = seed + 1000;
            _biomeTypeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _biomeTypeNoise.Frequency = 0.01f;

            // Clear any existing mappings
            _cellToBiomeMap.Clear();
        }

        // Dictionary to track neighboring cell relationships
        private Dictionary<int, List<int>> _cellNeighbors = new Dictionary<int, List<int>>();

        // Get biome type for a world position using domain warping
        public BiomeType GetBiomeType(int worldX, int worldZ)
        {
            // Apply domain warping to the coordinates
            (float warpedX, float warpedZ) = WarpPosition(worldX, worldZ);

            // Get the cell value from Voronoi noise using the warped coordinates
            float cellValue = _voronoiNoise.GetNoise2D(warpedX, warpedZ);

            // Convert to a stable integer cell ID
            // The cell value from FastNoiseLite is in range [-1,1], so we scale and convert to int
            int cellId = (int)((cellValue + 1.0f) * 1000.0f);

            // If we haven't assigned a biome to this cell yet, do so now
            if (!_cellToBiomeMap.ContainsKey(cellId))
            {
                AssignBiomeToCell(cellId);
            }

            return _cellToBiomeMap[cellId];
        }

        // Apply domain warping to a position
        private (float, float) WarpPosition(float x, float z)
        {
            // Sample the domain warp noise at the input position
            float warpX = _domainWarpNoise.GetNoise2D(x + 1000, z);
            float warpZ = _domainWarpNoise.GetNoise2D(x, z + 1000);

            // Apply the warp with controlled strength
            return (
                x + warpX * _warpStrength,
                z + warpZ * _warpStrength
            );
        }

        // Assign a biome to a cell, ensuring it's different from adjacent cells if possible
        private void AssignBiomeToCell(int cellId)
        {
            // Use the cell ID to seed a new random generator
            Random random = new Random(_seed + cellId);

            // Get all biome types
            Array biomeTypesArray = Enum.GetValues(typeof(BiomeType));
            List<BiomeType> availableBiomes = new List<BiomeType>();

            // Convert to list for easier manipulation
            foreach (BiomeType biomeType in biomeTypesArray)
            {
                availableBiomes.Add(biomeType);
            }

            // Find neighboring cells by sampling points around this cell
            List<int> neighbors = FindNeighboringCells(cellId);

            // Store the neighbors for future reference
            _cellNeighbors[cellId] = neighbors;

            // Remove biome types that are already used by neighbors
            foreach (int neighborId in neighbors)
            {
                if (_cellToBiomeMap.ContainsKey(neighborId))
                {
                    BiomeType neighborBiome = _cellToBiomeMap[neighborId];
                    availableBiomes.Remove(neighborBiome);
                }
            }

            // If we've removed all biomes, add them back (can happen with limited biome types)
            if (availableBiomes.Count == 0)
            {
                foreach (BiomeType biomeType in biomeTypesArray)
                {
                    availableBiomes.Add(biomeType);
                }
            }

            // Select a random biome from the available ones
            BiomeType selectedBiome = availableBiomes[random.Next(availableBiomes.Count)];
            _cellToBiomeMap[cellId] = selectedBiome;
        }

        // Find neighboring cells by sampling points around the given cell
        private List<int> FindNeighboringCells(int cellId)
        {
            // Convert cell ID back to approximate world coordinates
            // This is an approximation since we don't store the exact coordinates
            float cellValue = (cellId / 1000.0f) - 1.0f;

            // Use the cell value to seed a random generator for this cell
            Random random = new Random(_seed + cellId);

            // Generate a random position within this cell
            // The scale factor should match the one used in the noise frequency
            float sampleX = random.Next(-10000, 10000);
            float sampleZ = random.Next(-10000, 10000);

            // Sample points in a circle around this position to find neighbors
            List<int> neighbors = new List<int>();
            int sampleCount = 16; // Number of samples to take
            float radius = 1.0f / _regionScale; // Radius based on region scale

            for (int i = 0; i < sampleCount; i++)
            {
                // Calculate position on the circle
                float angle = i * (2.0f * Mathf.Pi / sampleCount);
                float x = sampleX + radius * Mathf.Cos(angle);
                float z = sampleZ + radius * Mathf.Sin(angle);

                // Get the cell value at this position
                float neighborCellValue = _voronoiNoise.GetNoise2D(x, z);
                int neighborCellId = (int)((neighborCellValue + 1.0f) * 1000.0f);

                // If it's a different cell, add it to neighbors
                if (neighborCellId != cellId && !neighbors.Contains(neighborCellId))
                {
                    neighbors.Add(neighborCellId);
                }
            }

            return neighbors;
        }

        // Adjust region scale (smaller value = larger regions)
        public void SetRegionScale(float scale)
        {
            _regionScale = scale;
            if (_voronoiNoise != null)
            {
                _voronoiNoise.Frequency = _regionScale;
            }
        }

        // Get the raw cell value for a position (useful for visualizing region boundaries)
        public float GetCellValue(int worldX, int worldZ)
        {
            // Apply domain warping to the coordinates
            (float warpedX, float warpedZ) = WarpPosition(worldX, worldZ);

            return _voronoiNoise.GetNoise2D(warpedX, warpedZ);
        }

        // Check if a position is near a region boundary
        public bool IsNearBoundary(int worldX, int worldZ, float threshold = 0.05f)
        {
            // Apply domain warping to the coordinates
            (float warpedX, float warpedZ) = WarpPosition(worldX, worldZ);

            // Sample points in a small radius around the position
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;

                    // Get cell value at this position
                    float cellValue = _voronoiNoise.GetNoise2D(warpedX, warpedZ);

                    // Get cell value at the nearby position
                    // Apply domain warping to the nearby position as well
                    (float nearbyWarpedX, float nearbyWarpedZ) = WarpPosition(worldX + dx, worldZ + dz);
                    float nearbyCellValue = _voronoiNoise.GetNoise2D(nearbyWarpedX, nearbyWarpedZ);

                    // If the cell values are different, we're near a boundary
                    if (Math.Abs(cellValue - nearbyCellValue) > threshold)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Get distance to the nearest region boundary
        public float GetDistanceToBoundary(int worldX, int worldZ)
        {
            // This is a simplified approach - for a more accurate distance,
            // you would need to implement a more sophisticated algorithm

            // Apply domain warping to the coordinates
            (float warpedX, float warpedZ) = WarpPosition(worldX, worldZ);

            // Get the cell value at this position
            float cellValue = _voronoiNoise.GetNoise2D(warpedX, warpedZ);

            // Sample in a circle to find the nearest boundary
            float minDistance = float.MaxValue;
            int sampleCount = 16;

            for (int radius = 1; radius <= 20; radius++)
            {
                bool foundBoundary = false;

                for (int i = 0; i < sampleCount; i++)
                {
                    float angle = i * (2.0f * Mathf.Pi / sampleCount);
                    int dx = (int)(radius * Mathf.Cos(angle));
                    int dz = (int)(radius * Mathf.Sin(angle));

                    // Apply domain warping to the sample position
                    (float sampleWarpedX, float sampleWarpedZ) = WarpPosition(worldX + dx, worldZ + dz);
                    float sampleCellValue = _voronoiNoise.GetNoise2D(sampleWarpedX, sampleWarpedZ);

                    // If we've crossed a boundary
                    if (Math.Abs(cellValue - sampleCellValue) > 0.01f)
                    {
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);
                        minDistance = Mathf.Min(minDistance, distance);
                        foundBoundary = true;
                    }
                }

                // If we found a boundary at this radius, we can stop searching
                if (foundBoundary)
                {
                    break;
                }
            }

            return minDistance;
        }

        // Set the warp strength (higher values = more irregular boundaries)
        public void SetWarpStrength(float strength)
        {
            _warpStrength = strength;
        }

        // Get the current warp strength
        public float GetWarpStrength()
        {
            return _warpStrength;
        }
    }
}
