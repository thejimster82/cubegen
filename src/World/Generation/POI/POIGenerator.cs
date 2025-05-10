using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using CubeGen.World.Common;

namespace CubeGen.World.Generation.POI
{
    /// <summary>
    /// Handles the generation and placement of POIs in the world
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

        // World seed
        private int _worldSeed;

        // Noise generators for POI placement
        private FastNoiseLite _poiVoronoiNoise;
        private FastNoiseLite _poiWarpNoise;

        // POI placement parameters
        private float _poiRegionScale = 0.0003f; // Reduced from 0.0005f for better performance
        private float _warpStrength = 50.0f; // Reduced from 100.0f for better performance

        // Dictionary to track POI cell IDs and their types
        private Dictionary<int, string> _cellToPOIMap = new Dictionary<int, string>();

        // Dictionary to cache POI influence maps for chunks
        private Dictionary<Vector2I, Dictionary<Vector2I, POIInfluence>> _chunkPOIInfluenceCache =
            new Dictionary<Vector2I, Dictionary<Vector2I, POIInfluence>>();

        // Flag to track if the generator has been initialized
        public bool IsInitialized { get; private set; } = false;

        /// <summary>
        /// Represents how a POI influences a specific column in a chunk
        /// </summary>
        public class POIInfluence
        {
            // The POI instance that is influencing this column
            public POIInstance POI { get; set; }

            // Target height for this column (null if no height modification)
            public int? TargetHeight { get; set; }

            // Blend factor (0.0 = natural terrain, 1.0 = fully POI terrain)
            public float BlendFactor { get; set; }

            // Whether this column is within the POI structure footprint
            public bool IsWithinStructure { get; set; }

            // Whether this column is within the POI flora clearance zone
            public bool IsWithinClearanceZone { get; set; }

            // Constructor
            public POIInfluence(POIInstance poi, int? targetHeight, float blendFactor, bool isWithinStructure, bool isWithinClearanceZone)
            {
                POI = poi;
                TargetHeight = targetHeight;
                BlendFactor = blendFactor;
                IsWithinStructure = isWithinStructure;
                IsWithinClearanceZone = isWithinClearanceZone;
            }
        }

        // Initialize the generator with the world seed
        public void Initialize(int worldSeed)
        {
            _worldSeed = worldSeed;

            // Initialize Voronoi noise for POI placement
            _poiVoronoiNoise = new FastNoiseLite();
            _poiVoronoiNoise.Seed = worldSeed + 12345; // Different seed for POI placement
            _poiVoronoiNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
            _poiVoronoiNoise.Frequency = _poiRegionScale;
            _poiVoronoiNoise.CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean;
            _poiVoronoiNoise.CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.CellValue;
            _poiVoronoiNoise.CellularJitter = 0.5f; // Higher jitter for more varied POI placement

            // Initialize domain warping noise
            _poiWarpNoise = new FastNoiseLite();
            _poiWarpNoise.Seed = worldSeed + 67890; // Different seed for POI warping
            _poiWarpNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
            _poiWarpNoise.Frequency = 0.001f; // Lower frequency for smoother warping
            _poiWarpNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            _poiWarpNoise.FractalOctaves = 3;
            _poiWarpNoise.FractalLacunarity = 2.0f;
            _poiWarpNoise.FractalGain = 0.5f;

            IsInitialized = true;

            GD.Print($"POIGenerator initialized with seed: {_worldSeed}");
        }

        // Generate POIs for a region around a chunk
        public void GeneratePOIsForRegion(Vector2I chunkPos, int radius)
        {
            if (!IsInitialized)
            {
                GD.PrintErr("POIGenerator not initialized!");
                return;
            }

            // Calculate world bounds for the region
            int chunkSize = WorldGenerator.CHUNK_SIZE;

            // OPTIMIZATION: Only process the chunks that are actually needed
            // This is much more efficient than processing a large square area
            HashSet<Vector2I> chunksToProcess = new HashSet<Vector2I>();

            // Add the center chunk
            chunksToProcess.Add(chunkPos);

            // Add chunks within radius (only immediate neighbors)
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && z == 0) continue; // Skip center chunk (already added)
                    chunksToProcess.Add(new Vector2I(chunkPos.X + x, chunkPos.Y + z));
                }
            }

            // Process each chunk
            foreach (Vector2I chunk in chunksToProcess)
            {
                // Calculate chunk bounds
                int minX = chunk.X * chunkSize;
                int minZ = chunk.Y * chunkSize;
                int maxX = (chunk.X + 1) * chunkSize;
                int maxZ = (chunk.Y + 1) * chunkSize;

                // OPTIMIZATION: Use a larger sample step to reduce the number of samples
                int sampleStep = chunkSize; // Sample at chunk resolution instead of half chunk

                // Sample points in the chunk to find POI cells
                for (int x = minX; x < maxX; x += sampleStep)
                {
                    for (int z = minZ; z < maxZ; z += sampleStep)
                    {
                        // Get the cell ID at this position
                        int cellId = GetPOICellId(x, z);

                        // If we haven't processed this cell yet, determine if it should have a POI
                        if (!_cellToPOIMap.ContainsKey(cellId))
                        {
                            DeterminePOIForCell(cellId, x, z);
                        }

                        // If this cell has a POI, generate it if it hasn't been generated yet
                        if (_cellToPOIMap.TryGetValue(cellId, out string poiId) && !string.IsNullOrEmpty(poiId))
                        {
                            GeneratePOIForCell(cellId, poiId);
                        }
                    }
                }
            }

            // Clear the influence cache for this chunk to ensure it's recalculated
            // when the chunk is generated
            _chunkPOIInfluenceCache.Remove(chunkPos);
        }

        /// <summary>
        /// Calculates the POI influence map for a chunk
        /// </summary>
        /// <param name="chunkPos">The chunk position</param>
        /// <returns>Dictionary mapping column positions to POI influence data</returns>
        public Dictionary<Vector2I, POIInfluence> CalculatePOIInfluenceMap(Vector2I chunkPos)
        {
            // Check if we have a cached influence map for this chunk
            if (_chunkPOIInfluenceCache.TryGetValue(chunkPos, out var influenceMap))
            {
                return influenceMap;
            }

            // Create a new influence map
            Dictionary<Vector2I, POIInfluence> newInfluenceMap = new Dictionary<Vector2I, POIInfluence>();

            // Get all POIs that might affect this chunk
            List<POIInstance> poiInstances = POIRegistry.Instance.GetPOIsForChunk(chunkPos);

            if (poiInstances.Count == 0)
            {
                // No POIs affect this chunk, cache empty map and return
                _chunkPOIInfluenceCache[chunkPos] = newInfluenceMap;
                return newInfluenceMap;
            }

            // Calculate chunk bounds in world coordinates
            int chunkSize = WorldGenerator.CHUNK_SIZE;
            int minX = chunkPos.X * chunkSize;
            int minZ = chunkPos.Y * chunkSize;
            int maxX = (chunkPos.X + 1) * chunkSize;
            int maxZ = (chunkPos.Y + 1) * chunkSize;

            // Process each column in the chunk
            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    // Calculate world coordinates for this column
                    int worldX = minX + x;
                    int worldZ = minZ + z;
                    Vector2I columnPos = new Vector2I(x, z);

                    // Find the most influential POI for this column
                    POIInfluence bestInfluence = null;
                    float bestInfluenceFactor = 0f;

                    foreach (POIInstance poi in poiInstances)
                    {
                        // Calculate distance from column to POI center
                        float distanceX = Math.Abs(worldX - poi.Position.X);
                        float distanceZ = Math.Abs(worldZ - poi.Position.Z);

                        // Check if this column is within the POI's influence area
                        float halfWidth = poi.Definition.Dimensions.X / 2f;
                        float halfDepth = poi.Definition.Dimensions.Z / 2f;
                        float blendDistance = poi.Definition.TerrainRules.BlendDistance;
                        float clearanceRadius = poi.Definition.FloraRules.ClearanceRadius;

                        // Check if within structure footprint
                        bool isWithinStructure = distanceX <= halfWidth && distanceZ <= halfDepth;

                        // Check if within clearance zone
                        bool isWithinClearanceZone = distanceX <= (halfWidth + clearanceRadius) &&
                                                    distanceZ <= (halfDepth + clearanceRadius);

                        // Calculate influence factor based on distance
                        float influenceFactor = 0f;

                        if (isWithinStructure)
                        {
                            // Within structure footprint - full influence
                            influenceFactor = 1.0f;
                        }
                        else if (distanceX <= (halfWidth + blendDistance) &&
                                distanceZ <= (halfDepth + blendDistance))
                        {
                            // Within blend zone - calculate smooth falloff
                            float xFactor = 1.0f;
                            float zFactor = 1.0f;

                            if (distanceX > halfWidth)
                            {
                                xFactor = 1.0f - ((distanceX - halfWidth) / blendDistance);
                            }

                            if (distanceZ > halfDepth)
                            {
                                zFactor = 1.0f - ((distanceZ - halfDepth) / blendDistance);
                            }

                            // Combine factors (minimum of the two for smoother blending)
                            influenceFactor = Math.Min(xFactor, zFactor);
                        }

                        // If this POI has more influence than the current best, update
                        if (influenceFactor > bestInfluenceFactor)
                        {
                            // Calculate target height for this column
                            int? targetHeight = null;

                            if (poi.Definition.TerrainRules.TargetHeight.HasValue)
                            {
                                // Use the specified target height
                                targetHeight = poi.Definition.TerrainRules.TargetHeight.Value;
                            }
                            else if (isWithinStructure)
                            {
                                // Use the POI's terrain height
                                targetHeight = poi.TerrainHeight;
                            }

                            // Create influence object
                            bestInfluence = new POIInfluence(
                                poi,
                                targetHeight,
                                influenceFactor,
                                isWithinStructure,
                                isWithinClearanceZone
                            );

                            bestInfluenceFactor = influenceFactor;
                        }
                    }

                    // If we found an influence for this column, add it to the map
                    if (bestInfluence != null && bestInfluenceFactor > 0.01f)
                    {
                        newInfluenceMap[columnPos] = bestInfluence;
                    }
                }
            }

            // Cache the influence map for future use
            _chunkPOIInfluenceCache[chunkPos] = newInfluenceMap;

            return newInfluenceMap;
        }

        /// <summary>
        /// Modifies terrain height based on POI influence
        /// </summary>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldZ">World Z coordinate</param>
        /// <param name="naturalHeight">Natural terrain height without POI influence</param>
        /// <returns>Modified terrain height</returns>
        public int ModifyTerrainHeight(int worldX, int worldZ, int naturalHeight)
        {
            // Calculate chunk position
            int chunkSize = WorldGenerator.CHUNK_SIZE;
            Vector2I chunkPos = new Vector2I(
                Mathf.FloorToInt((float)worldX / chunkSize),
                Mathf.FloorToInt((float)worldZ / chunkSize)
            );

            // Calculate local coordinates within the chunk
            int localX = worldX - (chunkPos.X * chunkSize);
            int localZ = worldZ - (chunkPos.Y * chunkSize);
            Vector2I columnPos = new Vector2I(localX, localZ);

            // Get POI influence map for this chunk
            Dictionary<Vector2I, POIInfluence> influenceMap = CalculatePOIInfluenceMap(chunkPos);

            // Check if this column is influenced by a POI
            if (influenceMap.TryGetValue(columnPos, out POIInfluence influence))
            {
                // If the POI specifies a target height, blend towards it
                if (influence.TargetHeight.HasValue)
                {
                    // Blend between natural height and target height based on influence factor
                    return Mathf.RoundToInt(
                        naturalHeight * (1.0f - influence.BlendFactor) +
                        influence.TargetHeight.Value * influence.BlendFactor
                    );
                }
            }

            // No influence or no target height - return natural height
            return naturalHeight;
        }

        /// <summary>
        /// Checks if a position is within a POI's flora clearance zone
        /// </summary>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldZ">World Z coordinate</param>
        /// <returns>True if the position is within a clearance zone</returns>
        public bool IsWithinPOIClearanceZone(int worldX, int worldZ)
        {
            // Calculate chunk position
            int chunkSize = WorldGenerator.CHUNK_SIZE;
            Vector2I chunkPos = new Vector2I(
                Mathf.FloorToInt((float)worldX / chunkSize),
                Mathf.FloorToInt((float)worldZ / chunkSize)
            );

            // Calculate local coordinates within the chunk
            int localX = worldX - (chunkPos.X * chunkSize);
            int localZ = worldZ - (chunkPos.Y * chunkSize);
            Vector2I columnPos = new Vector2I(localX, localZ);

            // Get POI influence map for this chunk
            Dictionary<Vector2I, POIInfluence> influenceMap = CalculatePOIInfluenceMap(chunkPos);

            // Check if this column is influenced by a POI
            if (influenceMap.TryGetValue(columnPos, out POIInfluence influence))
            {
                return influence.IsWithinClearanceZone;
            }

            // No influence - not within a clearance zone
            return false;
        }

        /// <summary>
        /// Gets POI-specific flora for a position
        /// </summary>
        /// <param name="worldX">World X coordinate</param>
        /// <param name="worldZ">World Z coordinate</param>
        /// <returns>Dictionary mapping flora types to spawn probabilities, or null if no POI influence</returns>
        public Dictionary<VoxelType, float> GetPOISpecificFlora(int worldX, int worldZ)
        {
            // Calculate chunk position
            int chunkSize = WorldGenerator.CHUNK_SIZE;
            Vector2I chunkPos = new Vector2I(
                Mathf.FloorToInt((float)worldX / chunkSize),
                Mathf.FloorToInt((float)worldZ / chunkSize)
            );

            // Calculate local coordinates within the chunk
            int localX = worldX - (chunkPos.X * chunkSize);
            int localZ = worldZ - (chunkPos.Y * chunkSize);
            Vector2I columnPos = new Vector2I(localX, localZ);

            // Get POI influence map for this chunk
            Dictionary<Vector2I, POIInfluence> influenceMap = CalculatePOIInfluenceMap(chunkPos);

            // Check if this column is influenced by a POI
            if (influenceMap.TryGetValue(columnPos, out POIInfluence influence))
            {
                // Return POI-specific flora if within structure or clearance zone
                if (influence.IsWithinStructure || influence.IsWithinClearanceZone)
                {
                    return influence.POI.Definition.FloraRules.POISpecificFlora;
                }
            }

            // No influence or not within structure/clearance zone
            return null;
        }

        // Get the cell ID for a world position
        private int GetPOICellId(int worldX, int worldZ)
        {
            // Apply domain warping to the coordinates
            (float warpedX, float warpedZ) = WarpPosition(worldX, worldZ);

            // Get the cell value from Voronoi noise using the warped coordinates
            float cellValue = _poiVoronoiNoise.GetNoise2D(warpedX, warpedZ);

            // Convert to a stable integer cell ID
            return (int)((cellValue + 1.0f) * 1000.0f);
        }

        // Apply domain warping to a position
        private (float, float) WarpPosition(float x, float z)
        {
            // Sample the domain warp noise at the input position
            float warpX = _poiWarpNoise.GetNoise2D(x + 1000, z);
            float warpZ = _poiWarpNoise.GetNoise2D(x, z + 1000);

            // Apply the warp with controlled strength
            return (
                x + warpX * _warpStrength,
                z + warpZ * _warpStrength
            );
        }

        // Determine if a cell should have a POI and what type
        private void DeterminePOIForCell(int cellId, int sampleX, int sampleZ)
        {
            // Use the cell ID to seed a random generator
            Random random = new Random(_worldSeed + cellId);

            // Get the biome at this position
            BiomeType biomeType = WorldGenerator.GetBiomeType(sampleX, sampleZ);

            // Get all POI definitions
            var poiDefinitions = POIRegistry.Instance.Definitions.Values.ToList();

            // Filter POIs by biome compatibility
            var compatiblePOIs = poiDefinitions.Where(poi => poi.ValidBiomes.Contains(biomeType)).ToList();

            if (compatiblePOIs.Count == 0)
            {
                // No compatible POIs for this biome
                _cellToPOIMap[cellId] = "";
                return;
            }

            // Calculate total spawn probability
            float totalProbability = compatiblePOIs.Sum(poi => poi.SpawnProbability);

            // Random value to determine if a POI should spawn
            float randomValue = (float)random.NextDouble();

            // Base chance of a cell having a POI (40% - reduced from 70% for performance)
            if (randomValue > 0.4f)
            {
                // No POI in this cell
                _cellToPOIMap[cellId] = "";
                return;
            }

            // Select a POI based on weighted probabilities
            float cumulativeProbability = 0;
            float normalizedRandom = (float)random.NextDouble() * totalProbability;

            foreach (var poi in compatiblePOIs)
            {
                cumulativeProbability += poi.SpawnProbability;

                if (normalizedRandom <= cumulativeProbability)
                {
                    // This POI is selected
                    _cellToPOIMap[cellId] = poi.Id;
                    return;
                }
            }

            // Fallback - no POI selected
            _cellToPOIMap[cellId] = "";
        }

        // Generate a POI for a cell
        private void GeneratePOIForCell(int cellId, string poiId)
        {
            // Get the POI definition
            POIDefinition poiDefinition = POIRegistry.Instance.GetPOIDefinition(poiId);

            if (poiDefinition == null)
            {
                GD.PrintErr($"POI definition with ID {poiId} not found!");
                return;
            }

            // Check if this POI has already been generated
            bool poiExists = POIRegistry.Instance.Instances.Any(poi =>
                poi.Definition.Id == poiId &&
                Math.Abs(poi.Seed - (_worldSeed + cellId)) < 1000); // Approximate check

            if (poiExists)
            {
                // POI already exists
                return;
            }

            // Use the cell ID to seed a random generator
            Random random = new Random(_worldSeed + cellId);

            // Get the cell center position
            Vector2 cellCenter = GetCellCenter(cellId);

            // Convert to world coordinates
            int worldX = (int)cellCenter.X;
            int worldZ = (int)cellCenter.Y;

            // Get the terrain height at this position
            int terrainHeight = GetTerrainHeightAt(worldX, worldZ);

            // Create the POI instance
            Vector3I position = new Vector3I(worldX, terrainHeight, worldZ);
            int rotation = random.Next(0, 360);

            POIInstance poiInstance = new POIInstance(poiDefinition, position, rotation, _worldSeed);
            poiInstance.TerrainHeight = terrainHeight;

            // Add the POI instance to the registry
            POIRegistry.Instance.AddPOIInstance(poiInstance);

            GD.Print($"Generated POI: {poiDefinition.Name} at position {position}");
        }

        // Get the center position of a cell
        private Vector2 GetCellCenter(int cellId)
        {
            // Create a noise instance with the same settings as the POI noise
            FastNoiseLite noise = new FastNoiseLite();
            noise.Seed = _worldSeed + 12345; // Same seed as _poiVoronoiNoise
            noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
            noise.Frequency = _poiRegionScale;
            noise.CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean;
            noise.CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.CellValue;
            noise.CellularJitter = 0.5f;

            // Use the cell ID to seed a random generator
            Random random = new Random(_worldSeed + cellId);

            // Start with a random position
            float startX = random.Next(-10000, 10000);
            float startZ = random.Next(-10000, 10000);

            // Adjust the position until we find the cell center
            float step = 100.0f;
            Vector2 position = new Vector2(startX, startZ);

            // First, find a point within the target cell
            int attempts = 0;
            int maxAttempts = 100; // Prevent infinite loops

            while (GetPOICellId((int)position.X, (int)position.Y) != cellId && attempts < maxAttempts)
            {
                position.X += step;
                if (position.X > startX + 10000)
                {
                    position.X = startX;
                    position.Y += step;
                }
                attempts++;
            }

            if (attempts >= maxAttempts)
            {
                // Fallback to random position if we can't find the cell
                GD.PrintErr($"Could not find center for cell {cellId}, using random position");
                return new Vector2(startX, startZ);
            }

            // Now, find the approximate center by sampling in a grid
            Vector2 bestPosition = position;
            float bestDistance = 0f;

            // Sample a grid around the found position
            int gridSize = 10;
            int gridStep = 50;

            for (int x = -gridSize; x <= gridSize; x++)
            {
                for (int z = -gridSize; z <= gridSize; z++)
                {
                    Vector2 testPos = new Vector2(
                        position.X + x * gridStep,
                        position.Y + z * gridStep
                    );

                    // Check if this position is in the same cell
                    if (GetPOICellId((int)testPos.X, (int)testPos.Y) == cellId)
                    {
                        // Calculate distance to cell boundary
                        float distance = DistanceToCellBoundary(testPos);

                        // If this is further from the boundary, it's closer to the center
                        if (distance > bestDistance)
                        {
                            bestDistance = distance;
                            bestPosition = testPos;
                        }
                    }
                }
            }

            return bestPosition;
        }

        // Calculate approximate distance to the nearest cell boundary
        private float DistanceToCellBoundary(Vector2 position)
        {
            // Sample points in a small radius and check if they're in a different cell
            int cellId = GetPOICellId((int)position.X, (int)position.Y);
            float minDistance = float.MaxValue;

            for (int x = -10; x <= 10; x += 2)
            {
                for (int z = -10; z <= 10; z += 2)
                {
                    if (x == 0 && z == 0) continue;

                    Vector2 testPos = new Vector2(position.X + x, position.Y + z);
                    int testCellId = GetPOICellId((int)testPos.X, (int)testPos.Y);

                    if (testCellId != cellId)
                    {
                        float distance = (testPos - position).Length();
                        minDistance = Math.Min(minDistance, distance);
                    }
                }
            }

            return minDistance == float.MaxValue ? 0f : minDistance;
        }

        // Cached noise instances for height calculation
        private Dictionary<BiomeType, FastNoiseLite> _heightNoiseCache = new Dictionary<BiomeType, FastNoiseLite>();

        // Get the terrain height at a world position
        private int GetTerrainHeightAt(int worldX, int worldZ)
        {
            // Get the biome at this position
            BiomeType biomeType = WorldGenerator.GetBiomeType(worldX, worldZ);

            // Get or create noise instance for this biome
            if (!_heightNoiseCache.TryGetValue(biomeType, out FastNoiseLite heightNoise))
            {
                // Create and cache a noise instance for this biome
                heightNoise = new FastNoiseLite
                {
                    Seed = _worldSeed,
                    // Set frequency based on biome type
                    Frequency = biomeType switch
                    {
                        BiomeType.Desert => 0.01f,
                        BiomeType.Tundra => 0.012f,
                        BiomeType.Islands => 0.018f,
                        BiomeType.ForestLands => 0.01f,
                        _ => 0.01f
                    }
                };

                _heightNoiseCache[biomeType] = heightNoise;
            }

            // Get noise value
            float noiseValue = heightNoise.GetNoise2D(worldX, worldZ);

            // Convert to height value (simplified version)
            float normalizedNoise = (noiseValue + 1.0f) * 0.5f;
            int height = Mathf.FloorToInt(normalizedNoise * 128); // Assuming max height is 128

            return height;
        }
    }
}
