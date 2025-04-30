using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
    /// <summary>
    /// Generates contiguous biome regions with irregular boundaries similar to states on a map.
    /// Uses a Voronoi diagram approach to create distinct regions.
    /// </summary>
    public class BiomeRegionGenerator
    {
        private int _seed;
        private int _regionCount;
        private float _worldScale;
        private List<Vector2> _regionCenters = new List<Vector2>();
        private Dictionary<Vector2, BiomeType> _regionBiomes = new Dictionary<Vector2, BiomeType>();
        private FastNoiseLite _borderNoise;
        private FastNoiseLite _biomeNoise;

        // Cache for region lookups to improve performance
        private Dictionary<Vector2I, Vector2> _regionCache = new Dictionary<Vector2I, Vector2>();
        private int _cacheGridSize = 64; // Size of grid cells for caching

        /// <summary>
        /// Creates a new BiomeRegionGenerator with the specified parameters.
        /// </summary>
        /// <param name="seed">Random seed for generation</param>
        /// <param name="regionCount">Number of regions to generate</param>
        /// <param name="worldScale">Scale factor for the world (larger values = smaller regions)</param>
        public BiomeRegionGenerator(int seed, int regionCount = 50, float worldScale = 0.002f)
        {
            _seed = seed;
            _regionCount = regionCount;
            _worldScale = worldScale;

            Initialize();
        }

        /// <summary>
        /// Initializes the region generator, creating region centers and assigning biomes.
        /// </summary>
        private void Initialize()
        {
            Random random = new Random(_seed);

            // Initialize noise for region border variation
            _borderNoise = new FastNoiseLite();
            _borderNoise.Seed = _seed + 500;
            _borderNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _borderNoise.Frequency = 0.02f;

            // Initialize noise for biome assignment
            _biomeNoise = new FastNoiseLite();
            _biomeNoise.Seed = _seed + 1000;
            _biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _biomeNoise.Frequency = 0.005f;

            // Generate region centers
            // Use a large area to ensure regions are well-distributed
            int worldSize = (int)(1.0f / _worldScale);
            for (int i = 0; i < _regionCount; i++)
            {
                float x = (float)random.NextDouble() * worldSize - worldSize / 2;
                float z = (float)random.NextDouble() * worldSize - worldSize / 2;
                Vector2 center = new Vector2(x, z);
                _regionCenters.Add(center);
            }

            // Assign biomes to regions
            AssignBiomesToRegions();

            // Pre-populate cache for common areas
            PopulateRegionCache(worldSize / 4);
        }

        /// <summary>
        /// Assigns biome types to each region, ensuring adjacent regions have different biomes when possible.
        /// </summary>
        private void AssignBiomesToRegions()
        {
            // Get all available biome types
            BiomeType[] biomeTypes = (BiomeType[])Enum.GetValues(typeof(BiomeType));

            // First, assign biomes based on position in the world using noise
            foreach (Vector2 center in _regionCenters)
            {
                // Use noise to determine biome type
                float biomeValue = _biomeNoise.GetNoise2D(center.X, center.Y);

                // Map noise value to biome type
                BiomeType biomeType = GetBiomeTypeFromNoise(biomeValue);

                // Store the biome type for this region
                _regionBiomes[center] = biomeType;
            }

            // Second pass: try to ensure no adjacent regions have the same biome
            // This is a simple greedy algorithm and may not be perfect
            bool changed;
            int maxIterations = 10;
            int iteration = 0;

            do
            {
                changed = false;
                iteration++;

                foreach (Vector2 center in _regionCenters)
                {
                    // Find adjacent regions
                    List<Vector2> adjacentRegions = FindAdjacentRegions(center);

                    // Check if any adjacent regions have the same biome
                    bool hasSameBiome = adjacentRegions.Any(adj => _regionBiomes[adj] == _regionBiomes[center]);

                    if (hasSameBiome)
                    {
                        // Get biomes of adjacent regions
                        HashSet<BiomeType> adjacentBiomes = new HashSet<BiomeType>();
                        foreach (Vector2 adj in adjacentRegions)
                        {
                            if (_regionBiomes.ContainsKey(adj))
                            {
                                adjacentBiomes.Add(_regionBiomes[adj]);
                            }
                        }

                        // Find a biome that's not used by adjacent regions
                        foreach (BiomeType biome in biomeTypes)
                        {
                            if (!adjacentBiomes.Contains(biome))
                            {
                                _regionBiomes[center] = biome;
                                changed = true;
                                break;
                            }
                        }

                        // If all biomes are used, just pick a different one
                        if (!changed && biomeTypes.Length > 0)
                        {
                            BiomeType currentBiome = _regionBiomes[center];
                            int currentIndex = Array.IndexOf(biomeTypes, currentBiome);
                            int newIndex = (currentIndex + 1) % biomeTypes.Length;
                            _regionBiomes[center] = biomeTypes[newIndex];
                            changed = true;
                        }
                    }
                }
            } while (changed && iteration < maxIterations);
        }

        /// <summary>
        /// Finds regions that are adjacent to the specified region.
        /// </summary>
        private List<Vector2> FindAdjacentRegions(Vector2 center)
        {
            List<Vector2> adjacentRegions = new List<Vector2>();

            // Sample points around the region to find adjacent regions
            float sampleRadius = 500.0f; // Adjust based on region density
            int sampleCount = 16; // Number of sample points around the region

            for (int i = 0; i < sampleCount; i++)
            {
                float angle = i * (2 * Mathf.Pi / sampleCount);
                float x = center.X + Mathf.Cos(angle) * sampleRadius;
                float y = center.Y + Mathf.Sin(angle) * sampleRadius;

                Vector2 samplePoint = new Vector2(x, y);
                Vector2 nearestCenter = FindNearestRegionCenter(samplePoint);

                if (nearestCenter != center && !adjacentRegions.Contains(nearestCenter))
                {
                    adjacentRegions.Add(nearestCenter);
                }
            }

            return adjacentRegions;
        }

        /// <summary>
        /// Gets the biome type for a specific world position.
        /// </summary>
        public BiomeType GetBiomeType(int worldX, int worldZ)
        {
            // Convert to Vector2 for lookup
            Vector2 worldPos = new Vector2(worldX, worldZ);

            // Check cache first
            Vector2I cacheKey = new Vector2I(
                (int)(worldX * _worldScale / _cacheGridSize),
                (int)(worldZ * _worldScale / _cacheGridSize)
            );

            if (_regionCache.TryGetValue(cacheKey, out Vector2 cachedCenter))
            {
                return _regionBiomes[cachedCenter];
            }

            // Find the nearest region center with border variation
            Vector2 nearestCenter = FindNearestRegionCenter(worldPos);

            // Cache the result
            _regionCache[cacheKey] = nearestCenter;

            // Return the biome type for this region
            return _regionBiomes[nearestCenter];
        }

        /// <summary>
        /// Finds the nearest region center to the specified world position,
        /// with border variation to create irregular boundaries.
        /// </summary>
        private Vector2 FindNearestRegionCenter(Vector2 worldPos)
        {
            // Scale the world position
            Vector2 scaledPos = worldPos * _worldScale;

            // Find the nearest region center
            Vector2 nearestCenter = Vector2.Zero;
            float minDistance = float.MaxValue;

            foreach (Vector2 center in _regionCenters)
            {
                // Calculate base distance
                float dx = center.X - scaledPos.X;
                float dy = center.Y - scaledPos.Y;
                float baseDistance = dx * dx + dy * dy;

                // Add border variation using noise
                // Use both position and center for noise to create more varied borders
                float borderNoise1 = _borderNoise.GetNoise2D(scaledPos.X, scaledPos.Y);
                float borderNoise2 = _borderNoise.GetNoise2D(center.X * 0.1f, center.Y * 0.1f);
                float borderVariation = (borderNoise1 * 0.7f + borderNoise2 * 0.3f) * 0.3f;

                // Apply variation to distance calculation
                float adjustedDistance = baseDistance * (1.0f + borderVariation);

                if (adjustedDistance < minDistance)
                {
                    minDistance = adjustedDistance;
                    nearestCenter = center;
                }
            }

            return nearestCenter;
        }

        /// <summary>
        /// Pre-populates the region cache for a given area around the origin.
        /// </summary>
        private void PopulateRegionCache(int radius)
        {
            int step = _cacheGridSize;
            for (int x = -radius; x <= radius; x += step)
            {
                for (int z = -radius; z <= radius; z += step)
                {
                    Vector2 worldPos = new Vector2(x, z);
                    Vector2 nearestCenter = FindNearestRegionCenter(worldPos);

                    Vector2I cacheKey = new Vector2I(
                        (int)(x * _worldScale / _cacheGridSize),
                        (int)(z * _worldScale / _cacheGridSize)
                    );

                    _regionCache[cacheKey] = nearestCenter;
                }
            }
        }

        /// <summary>
        /// Maps a noise value to a biome type.
        /// </summary>
        private static BiomeType GetBiomeTypeFromNoise(float biomeValue)
        {
            // Simple biome distribution based on noise value
            if (biomeValue < -0.5f)
                return BiomeType.Desert;
            else if (biomeValue < -0.2f)
                return BiomeType.Plains;
            else if (biomeValue < 0.2f)
                return BiomeType.Forest;
            else if (biomeValue < 0.5f)
                return BiomeType.Mountains;
            else
                return BiomeType.Tundra;
        }
    }
}
