using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
    /// <summary>
    /// Generates biome regions using Voronoi/Cellular noise to create contiguous regions
    /// similar to how US states are joined together.
    /// </summary>
    public class BiomeRegionGenerator
    {
        private FastNoiseLite _voronoiNoise;
        private FastNoiseLite _biomeTypeNoise;
        private int _seed;
        private float _regionScale = 0.001f; // Controls the size of regions (smaller value = larger regions)
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

            // Use Manhattan distance function which tends to create more uniformly sized cells
            // compared to Euclidean distance
            _voronoiNoise.CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Manhattan;

            // Use CellValue return type to get stable cell IDs
            _voronoiNoise.CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.CellValue;

            // Set jitter to a very low value (0.1-0.2) to create much more uniform, grid-like cells
            // This will eliminate the tiny biomes by making all cells more consistent in size
            _voronoiNoise.CellularJitter = 0.15f; // Very low jitter for extremely uniform cells

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

        // Get biome type for a world position
        public BiomeType GetBiomeType(int worldX, int worldZ)
        {
            // Get the cell value from Voronoi noise
            float cellValue = _voronoiNoise.GetNoise2D(worldX, worldZ);

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
            // With Manhattan distance and low jitter, cells are arranged in a more grid-like pattern
            // We can use a more structured approach to find neighbors

            // Convert cell ID back to approximate world coordinates
            // This is an approximation since we don't store the exact coordinates
            float cellValue = (cellId / 1000.0f) - 1.0f;

            // Use the cell value to seed a random generator for this cell
            Random random = new Random(_seed + cellId);

            // Generate a position within this cell
            // With low jitter, cells are more predictably positioned
            float sampleX = random.Next(-10000, 10000);
            float sampleZ = random.Next(-10000, 10000);

            // Sample points in a grid pattern around this position to find neighbors
            List<int> neighbors = new List<int>();

            // With Manhattan distance, we need to check in a diamond pattern
            // Calculate cell size based on region scale (approximate)
            float cellSize = 1.0f / _regionScale;

            // Check in all 8 directions (N, NE, E, SE, S, SW, W, NW)
            // This works better with Manhattan distance and grid-like cells
            int[][] directions = new int[][]
            {
                new int[] { 0, 1 },   // North
                new int[] { 1, 1 },   // Northeast
                new int[] { 1, 0 },   // East
                new int[] { 1, -1 },  // Southeast
                new int[] { 0, -1 },  // South
                new int[] { -1, -1 }, // Southwest
                new int[] { -1, 0 },  // West
                new int[] { -1, 1 }   // Northwest
            };

            foreach (int[] dir in directions)
            {
                // Sample at a distance that's guaranteed to be in the neighboring cell
                float x = sampleX + dir[0] * cellSize;
                float z = sampleZ + dir[1] * cellSize;

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
            return _voronoiNoise.GetNoise2D(worldX, worldZ);
        }

        // Check if a position is near a region boundary
        public bool IsNearBoundary(int worldX, int worldZ, float threshold = 0.05f)
        {
            // Get cell value at this position
            float cellValue = _voronoiNoise.GetNoise2D(worldX, worldZ);

            // With Manhattan distance and low jitter, boundaries are more grid-like
            // We need a smaller sampling radius but more precise threshold
            int sampleRadius = 1; // Reduced radius since boundaries are sharper with Manhattan distance

            // Sample points in a small radius around the position
            for (int dx = -sampleRadius; dx <= sampleRadius; dx++)
            {
                for (int dz = -sampleRadius; dz <= sampleRadius; dz++)
                {
                    // Skip the center point
                    if (dx == 0 && dz == 0) continue;

                    // Get cell value at the nearby position
                    float nearbyCellValue = _voronoiNoise.GetNoise2D(worldX + dx, worldZ + dz);

                    // With Manhattan distance, the threshold needs to be smaller
                    // to accurately detect the sharper boundaries
                    float boundaryThreshold = threshold * 0.5f;

                    // If the cell values are different, we're near a boundary
                    if (Math.Abs(cellValue - nearbyCellValue) > boundaryThreshold)
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
            // With Manhattan distance and low jitter, boundaries are more grid-like
            // and we can use a more efficient approach

            // Get the cell value at this position
            float cellValue = _voronoiNoise.GetNoise2D(worldX, worldZ);

            // Calculate approximate cell size based on region scale
            float cellSize = 1.0f / _regionScale;

            // With Manhattan distance, we can search in a grid pattern
            // which is more efficient for finding boundaries
            float minDistance = float.MaxValue;

            // Maximum search radius (in cells)
            int maxRadius = 10;

            // Search in expanding squares
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                bool foundBoundary = false;

                // Check the perimeter of a square with this radius
                // Top and bottom edges
                for (int dx = -radius; dx <= radius; dx++)
                {
                    // Top edge
                    float topCellValue = _voronoiNoise.GetNoise2D(worldX + dx, worldZ + radius);
                    if (Math.Abs(cellValue - topCellValue) > 0.001f)
                    {
                        float distance = Mathf.Sqrt(dx * dx + radius * radius);
                        minDistance = Mathf.Min(minDistance, distance);
                        foundBoundary = true;
                    }

                    // Bottom edge
                    float bottomCellValue = _voronoiNoise.GetNoise2D(worldX + dx, worldZ - radius);
                    if (Math.Abs(cellValue - bottomCellValue) > 0.001f)
                    {
                        float distance = Mathf.Sqrt(dx * dx + radius * radius);
                        minDistance = Mathf.Min(minDistance, distance);
                        foundBoundary = true;
                    }
                }

                // Left and right edges (excluding corners which were already checked)
                for (int dz = -radius + 1; dz <= radius - 1; dz++)
                {
                    // Left edge
                    float leftCellValue = _voronoiNoise.GetNoise2D(worldX - radius, worldZ + dz);
                    if (Math.Abs(cellValue - leftCellValue) > 0.001f)
                    {
                        float distance = Mathf.Sqrt(radius * radius + dz * dz);
                        minDistance = Mathf.Min(minDistance, distance);
                        foundBoundary = true;
                    }

                    // Right edge
                    float rightCellValue = _voronoiNoise.GetNoise2D(worldX + radius, worldZ + dz);
                    if (Math.Abs(cellValue - rightCellValue) > 0.001f)
                    {
                        float distance = Mathf.Sqrt(radius * radius + dz * dz);
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
    }
}
