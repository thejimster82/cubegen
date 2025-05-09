using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
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
		private float _regionScale = 0.00015f; // Controls the size of regions (smaller value = larger regions)
		private float _warpStrength = 50.0f; // Controls how much the domain is warped

		// THREAD SAFETY: Use ConcurrentDictionary for thread-safe access
		private ConcurrentDictionary<int, BiomeType> _cellToBiomeMap = new ConcurrentDictionary<int, BiomeType>();

		// Lock object for synchronizing access to biome map
		private readonly object _biomeLock = new object();

		private static BiomeRegionGenerator _instance;
		private bool _isProperlyInitialized = false; // Flag to track if initialized with the game's seed

		// Singleton instance
		public static BiomeRegionGenerator Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new BiomeRegionGenerator();
					// We don't initialize with a default seed here anymore
					// The proper seed will be set by the WorldGenerator
				}
				return _instance;
			}
			set
			{
				_instance = value;
				GD.Print($"BiomeRegionGenerator instance set to: {_instance.GetType().Name}");
			}
		}

		// Initialize with a specific seed
		public virtual void Initialize(int seed)
		{
			// Store the seed
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

			// Mark as properly initialized
			_isProperlyInitialized = true;
		}

		// Dictionary to track neighboring cell relationships
		// THREAD SAFETY: Use ConcurrentDictionary for thread-safe access
		private ConcurrentDictionary<int, List<int>> _cellNeighbors = new ConcurrentDictionary<int, List<int>>();

		// Lock object for synchronizing access to neighbor lists
		private readonly object _neighborLock = new object();

		// Get biome type for a world position using domain warping
		public virtual BiomeType GetBiomeType(int worldX, int worldZ)
		{
			// Check if properly initialized
			if (!_isProperlyInitialized)
			{
				// GD.PrintErr("BiomeRegionGenerator not properly initialized in GetBiomeType. Waiting for proper initialization.");
				// Return a default biome type instead of initializing with a default seed
				return BiomeType.Plains;
			}

			// Apply domain warping to the coordinates
			(float warpedX, float warpedZ) = WarpPosition(worldX, worldZ);

			// Get the cell value from Voronoi noise using the warped coordinates
			float cellValue = _voronoiNoise.GetNoise2D(warpedX, warpedZ);

			// Convert to a stable integer cell ID
			// The cell value from FastNoiseLite is in range [-1,1], so we scale and convert to int
			int cellId = (int)((cellValue + 1.0f) * 1000.0f);

			// THREAD SAFETY: Use GetOrAdd to safely get or create the biome for this cell
			return _cellToBiomeMap.GetOrAdd(cellId, id => {
				// If the biome doesn't exist yet, assign it
				// This lambda will only be called if the key doesn't exist

				// Use the cell ID to seed a new random generator
				Random random = new Random(_seed + id);

				// Get all biome types
				Array biomeTypesArray = Enum.GetValues(typeof(BiomeType));
				List<BiomeType> availableBiomes = new List<BiomeType>();

				// Convert to list for easier manipulation
				foreach (BiomeType biomeType in biomeTypesArray)
				{
					availableBiomes.Add(biomeType);
				}

				// Find neighboring cells by sampling points around this cell
				List<int> neighbors = FindNeighboringCells(id);

				// Store the neighbors for future reference
				_cellNeighbors.TryAdd(id, neighbors);

				// Remove biome types that are already used by neighbors
				foreach (int neighborId in neighbors)
				{
					BiomeType neighborBiome;
					if (_cellToBiomeMap.TryGetValue(neighborId, out neighborBiome))
					{
						availableBiomes.Remove(neighborBiome);
					}
				}

				// Make sure we have at least one biome available
				if (availableBiomes.Count == 0)
				{
					// If all biomes are used by neighbors, just use any biome
					availableBiomes.AddRange(Enum.GetValues(typeof(BiomeType)).Cast<BiomeType>());
				}

				// Select a random biome from the available ones
				return availableBiomes[random.Next(availableBiomes.Count)];
			});
		}

		// Apply domain warping to a position
		private (float, float) WarpPosition(float x, float z)
		{
			// Check if properly initialized
			if (!_isProperlyInitialized)
			{
				// If not properly initialized, return the original coordinates without warping
				return (x, z);
			}

			// Sample the domain warp noise at the input position
			float warpX = _domainWarpNoise.GetNoise2D(x + 1000, z);
			float warpZ = _domainWarpNoise.GetNoise2D(x, z + 1000);

			// Apply the warp with controlled strength
			return (
				x + warpX * _warpStrength,
				z + warpZ * _warpStrength
			);
		}

		// NOTE: AssignBiomeToCell method has been replaced with thread-safe inline code in GetBiomeType and GetBiomeTypeForCell

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
			// Check if properly initialized
			if (!_isProperlyInitialized)
			{
				// GD.PrintErr("BiomeRegionGenerator not properly initialized in GetCellValue. Returning default value.");
				return 0.0f;
			}

			// Apply domain warping to the coordinates
			(float warpedX, float warpedZ) = WarpPosition(worldX, worldZ);

			return _voronoiNoise.GetNoise2D(warpedX, warpedZ);
		}

		// Check if a position is near a region boundary
		public bool IsNearBoundary(int worldX, int worldZ, float threshold = 0.05f)
		{
			// Check if properly initialized
			if (!_isProperlyInitialized)
			{
				// GD.PrintErr("BiomeRegionGenerator not properly initialized in IsNearBoundary. Returning default value.");
				return false;
			}

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

		// Get distance to the nearest region boundary - more efficient implementation
		public float GetDistanceToBoundary(int worldX, int worldZ)
		{
			// Check if properly initialized
			if (!_isProperlyInitialized)
			{
				// GD.PrintErr("BiomeRegionGenerator not properly initialized in GetDistanceToBoundary. Returning default distance.");
				return 100.0f; // Return a large value to indicate "far from boundary"
			}

			// Apply domain warping to the coordinates
			(float warpedX, float warpedZ) = WarpPosition(worldX, worldZ);

			// Get the cell value at this position
			float cellValue = _voronoiNoise.GetNoise2D(warpedX, warpedZ);
			int cellId = (int)((cellValue + 1.0f) * 1000.0f);

			// Sample in a circle to find the nearest boundary
			float minDistance = float.MaxValue;
			int sampleCount = 24; // Increased for better accuracy
			int maxRadius = 30; // Increased max search radius

			// Use binary search to find the boundary more efficiently
			int minRadius = 1;
			int maxSearchRadius = maxRadius;

			while (minRadius <= maxSearchRadius)
			{
				int radius = (minRadius + maxSearchRadius) / 2;
				bool foundBoundary = false;
				float closestDistance = float.MaxValue;

				for (int i = 0; i < sampleCount; i++)
				{
					float angle = i * (2.0f * Mathf.Pi / sampleCount);
					int dx = (int)(radius * Mathf.Cos(angle));
					int dz = (int)(radius * Mathf.Sin(angle));

					// Apply domain warping to the sample position
					(float sampleWarpedX, float sampleWarpedZ) = WarpPosition(worldX + dx, worldZ + dz);
					float sampleCellValue = _voronoiNoise.GetNoise2D(sampleWarpedX, sampleWarpedZ);
					int sampleCellId = (int)((sampleCellValue + 1.0f) * 1000.0f);

					// If we've crossed a boundary
					if (sampleCellId != cellId)
					{
						float distance = Mathf.Sqrt(dx * dx + dz * dz);
						closestDistance = Mathf.Min(closestDistance, distance);
						foundBoundary = true;
					}
				}

				if (foundBoundary)
				{
					minDistance = Mathf.Min(minDistance, closestDistance);
					maxSearchRadius = radius - 1; // Search closer
				}
				else
				{
					minRadius = radius + 1; // Search farther
				}
			}

			// If we didn't find a boundary, return a large value
			if (minDistance == float.MaxValue)
			{
				return maxRadius;
			}

			return minDistance;
		}

		/// <summary>
		/// Gets the cell ID for a world position
		/// </summary>
		public virtual int GetCellId(int worldX, int worldZ)
		{
			// Check if properly initialized
			if (!_isProperlyInitialized)
			{
				// GD.PrintErr("BiomeRegionGenerator not properly initialized in GetCellId. Returning default cell ID.");
				// Return a consistent default cell ID
				return 500; // Arbitrary but consistent value
			}

			// Apply domain warping to the coordinates
			(float warpedX, float warpedZ) = WarpPosition(worldX, worldZ);

			// Get the cell value from Voronoi noise using the warped coordinates
			float cellValue = _voronoiNoise.GetNoise2D(warpedX, warpedZ);

			// Convert to a stable integer cell ID
			return (int)((cellValue + 1.0f) * 1000.0f);
		}

		/// <summary>
		/// Gets the biome type for a cell ID
		/// </summary>
		public virtual BiomeType GetBiomeTypeForCell(int cellId)
		{
			// Check if properly initialized
			if (!_isProperlyInitialized)
			{
				// GD.PrintErr("BiomeRegionGenerator not properly initialized in GetBiomeTypeForCell. Returning default biome.");
				return BiomeType.Plains;
			}

			// THREAD SAFETY: Use GetOrAdd to safely get or create the biome for this cell
			return _cellToBiomeMap.GetOrAdd(cellId, id => {
				// Use the cell ID to seed a new random generator
				Random random = new Random(_seed + id);

				// Get all biome types
				Array biomeTypesArray = Enum.GetValues(typeof(BiomeType));
				List<BiomeType> availableBiomes = new List<BiomeType>();

				// Convert to list for easier manipulation
				foreach (BiomeType biomeType in biomeTypesArray)
				{
					availableBiomes.Add(biomeType);
				}

				// Find neighboring cells by sampling points around this cell
				List<int> neighbors = FindNeighboringCells(id);

				// Store the neighbors for future reference
				_cellNeighbors.TryAdd(id, neighbors);

				// Remove biome types that are already used by neighbors
				foreach (int neighborId in neighbors)
				{
					BiomeType neighborBiome;
					if (_cellToBiomeMap.TryGetValue(neighborId, out neighborBiome))
					{
						availableBiomes.Remove(neighborBiome);
					}
				}

				// Make sure we have at least one biome available
				if (availableBiomes.Count == 0)
				{
					// If all biomes are used by neighbors, just use any biome
					availableBiomes.AddRange(Enum.GetValues(typeof(BiomeType)).Cast<BiomeType>());
				}

				// Select a random biome from the available ones
				return availableBiomes[random.Next(availableBiomes.Count)];
			});
		}

		/// <summary>
		/// Gets the nearest region center for a specific biome type
		/// </summary>
		/// <param name="worldX">World X coordinate</param>
		/// <param name="worldZ">World Z coordinate</param>
		/// <param name="biomeType">The biome type to find the center for</param>
		/// <returns>Vector2 containing the region center coordinates</returns>
		public Vector2 GetNearestRegionCenter(int worldX, int worldZ, BiomeType biomeType)
		{
			// Check if properly initialized
			if (!_isProperlyInitialized)
			{
				// GD.PrintErr("BiomeRegionGenerator not properly initialized in GetNearestRegionCenter. Using default center.");
				return new Vector2(worldX, worldZ); // Return the input position as fallback
			}

			// Apply domain warping to the input coordinates
			(float warpedX, float warpedZ) = WarpPosition(worldX, worldZ);

			// Get the cell ID for the warped position
			float cellValue = _voronoiNoise.GetNoise2D(warpedX, warpedZ);
			int cellId = (int)((cellValue + 1.0f) * 1000.0f);

			// THREAD SAFETY: Use GetBiomeTypeForCell to safely get or create the biome for this cell
			// This will automatically assign a biome if one doesn't exist yet
			GetBiomeTypeForCell(cellId);

			// Check if this cell is the requested biome type
			BiomeType cellBiome;
			if (_cellToBiomeMap.TryGetValue(cellId, out cellBiome) && cellBiome == biomeType)
			{
				// This cell is already the requested biome, return its center
				return GetCellCenter(cellId);
			}

			// If this cell is not the requested biome, find the nearest cell with that biome
			// First, get the neighboring cells
			List<int> neighbors = GetCellNeighbors(cellId);

			// Check if any direct neighbors are the requested biome
			foreach (int neighborId in neighbors)
			{
				BiomeType neighborBiome;
				if (_cellToBiomeMap.TryGetValue(neighborId, out neighborBiome) && neighborBiome == biomeType)
				{
					return GetCellCenter(neighborId);
				}
			}

			// If no direct neighbors match, search in a wider radius
			// This is a simplified approach - in a real game, you might want a more efficient search
			float closestDistance = float.MaxValue;
			Vector2 closestCenter = new Vector2(worldX, worldZ); // Default to current position

			// Search through all known cells (this could be optimized further)
			foreach (var entry in _cellToBiomeMap)
			{
				if (entry.Value == biomeType)
				{
					Vector2 center = GetCellCenter(entry.Key);
					float distance = (center - new Vector2(worldX, worldZ)).LengthSquared();

					if (distance < closestDistance)
					{
						closestDistance = distance;
						closestCenter = center;
					}
				}
			}

			return closestCenter;
		}

		/// <summary>
		/// Gets the center coordinates of a cell
		/// </summary>
		/// <param name="cellId">The cell ID</param>
		/// <returns>Vector2 containing the cell center coordinates</returns>
		private Vector2 GetCellCenter(int cellId)
		{
			// Use the cell ID to seed a random generator for this cell
			Random random = new Random(_seed + cellId);

			// Generate a position within this cell
			// The scale factor should match the one used in the noise frequency
			float sampleX = random.Next(-10000, 10000);
			float sampleZ = random.Next(-10000, 10000);

			// Return the cell center
			return new Vector2(sampleX, sampleZ);
		}

		/// <summary>
		/// Gets the neighboring cells for a given cell ID
		/// </summary>
		/// <param name="cellId">The cell ID</param>
		/// <returns>List of neighboring cell IDs</returns>
		private List<int> GetCellNeighbors(int cellId)
		{
			// THREAD SAFETY: Use GetOrAdd for thread-safe access
			return _cellNeighbors.GetOrAdd(cellId, id => {
				// This lambda will only be called if the key doesn't exist
				return FindNeighboringCells(id);
			});
		}

		/// <summary>
		/// Checks if a chunk is near any biome boundary
		/// </summary>
		/// <param name="chunkPosX">Chunk X position</param>
		/// <param name="chunkPosZ">Chunk Z position</param>
		/// <param name="chunkSize">Size of the chunk in blocks</param>
		/// <param name="blendDistance">Maximum distance to blend (in blocks)</param>
		/// <returns>True if any part of the chunk is near a biome boundary</returns>
		public virtual bool IsChunkNearBiomeBoundary(int chunkPosX, int chunkPosZ, int chunkSize, float blendDistance)
		{
			// Check if properly initialized
			if (!_isProperlyInitialized)
			{
				// GD.PrintErr("BiomeRegionGenerator not properly initialized in IsChunkNearBiomeBoundary. Returning false.");
				return false;
			}
			// Check corners and center of the chunk
			int worldX, worldZ;

			// Check center
			worldX = chunkPosX * chunkSize + chunkSize / 2;
			worldZ = chunkPosZ * chunkSize + chunkSize / 2;
			if (GetDistanceToBoundary(worldX, worldZ) <= blendDistance)
				return true;

			// Check corners
			// Top-left
			worldX = chunkPosX * chunkSize;
			worldZ = chunkPosZ * chunkSize;
			if (GetDistanceToBoundary(worldX, worldZ) <= blendDistance)
				return true;

			// Top-right
			worldX = chunkPosX * chunkSize + chunkSize - 1;
			worldZ = chunkPosZ * chunkSize;
			if (GetDistanceToBoundary(worldX, worldZ) <= blendDistance)
				return true;

			// Bottom-left
			worldX = chunkPosX * chunkSize;
			worldZ = chunkPosZ * chunkSize + chunkSize - 1;
			if (GetDistanceToBoundary(worldX, worldZ) <= blendDistance)
				return true;

			// Bottom-right
			worldX = chunkPosX * chunkSize + chunkSize - 1;
			worldZ = chunkPosZ * chunkSize + chunkSize - 1;
			if (GetDistanceToBoundary(worldX, worldZ) <= blendDistance)
				return true;

			// Check midpoints of edges
			// Top edge
			worldX = chunkPosX * chunkSize + chunkSize / 2;
			worldZ = chunkPosZ * chunkSize;
			if (GetDistanceToBoundary(worldX, worldZ) <= blendDistance)
				return true;

			// Bottom edge
			worldX = chunkPosX * chunkSize + chunkSize / 2;
			worldZ = chunkPosZ * chunkSize + chunkSize - 1;
			if (GetDistanceToBoundary(worldX, worldZ) <= blendDistance)
				return true;

			// Left edge
			worldX = chunkPosX * chunkSize;
			worldZ = chunkPosZ * chunkSize + chunkSize / 2;
			if (GetDistanceToBoundary(worldX, worldZ) <= blendDistance)
				return true;

			// Right edge
			worldX = chunkPosX * chunkSize + chunkSize - 1;
			worldZ = chunkPosZ * chunkSize + chunkSize / 2;
			if (GetDistanceToBoundary(worldX, worldZ) <= blendDistance)
				return true;

			// If none of the sample points are near a boundary, the chunk is not near a boundary
			return false;
		}

		/// <summary>
		/// Gets neighboring biomes for a world position
		/// </summary>
		/// <param name="worldX">World X coordinate</param>
		/// <param name="worldZ">World Z coordinate</param>
		/// <param name="maxDistance">Maximum distance to search for neighbors</param>
		/// <returns>Dictionary mapping BiomeType to distance from the position</returns>
		public Dictionary<BiomeType, float> GetNeighboringBiomes(int worldX, int worldZ, int maxDistance = 30)
		{
			Dictionary<BiomeType, float> biomesWithDistances = new Dictionary<BiomeType, float>();

			// Check if properly initialized
			if (!_isProperlyInitialized)
			{
				// GD.PrintErr("BiomeRegionGenerator not properly initialized in GetNeighboringBiomes. Returning default biome only.");
				biomesWithDistances[BiomeType.Plains] = 0f;
				return biomesWithDistances;
			}

			// Get the current cell and biome
			int cellId = GetCellId(worldX, worldZ);
			BiomeType currentBiome = GetBiomeTypeForCell(cellId);

			// Add the current biome with distance 0
			biomesWithDistances[currentBiome] = 0f;

			// Sample in a circle to find neighboring biomes
			int sampleCount = 24;

			for (int radius = 1; radius <= maxDistance; radius++)
			{
				for (int i = 0; i < sampleCount; i++)
				{
					float angle = i * (2.0f * Mathf.Pi / sampleCount);
					int dx = (int)(radius * Mathf.Cos(angle));
					int dz = (int)(radius * Mathf.Sin(angle));

					int sampleX = worldX + dx;
					int sampleZ = worldZ + dz;

					// Get the biome at this sample point
					int sampleCellId = GetCellId(sampleX, sampleZ);

					// Skip if it's the same cell
					if (sampleCellId == cellId)
						continue;

					BiomeType sampleBiome = GetBiomeTypeForCell(sampleCellId);

					// Skip if we already found this biome closer
					float existingDistance;
					if (biomesWithDistances.TryGetValue(sampleBiome, out existingDistance) && existingDistance <= radius)
						continue;

					// Add or update the biome with its distance
					biomesWithDistances[sampleBiome] = radius;
				}
			}

			return biomesWithDistances;
		}

		// THREAD SAFETY: Use ConcurrentDictionary for thread-safe biome blend weights caching
		private ConcurrentDictionary<(int x, int z), Dictionary<BiomeType, float>> _blendWeightsCache = new ConcurrentDictionary<(int x, int z), Dictionary<BiomeType, float>>();
		private const int BLEND_CACHE_SIZE_LIMIT = 5000; // Limit cache size to prevent memory issues
		private readonly object _blendCacheLock = new object(); // Lock for cache clearing operations

		/// <summary>
		/// Calculates blend weights for biomes at a world position
		/// </summary>
		/// <param name="worldX">World X coordinate</param>
		/// <param name="worldZ">World Z coordinate</param>
		/// <param name="blendDistance">Maximum distance to blend (in blocks)</param>
		/// <returns>Dictionary mapping BiomeType to blend weight (0.0-1.0)</returns>
		public Dictionary<BiomeType, float> CalculateBiomeBlendWeights(int worldX, int worldZ, float blendDistance = 10f)
		{
			// OPTIMIZATION: Round coordinates to reduce unique positions and increase cache hits
			int roundedX = (worldX / 4) * 4;
			int roundedZ = (worldZ / 4) * 4;

			// Create cache key
			var cacheKey = (roundedX, roundedZ);

			// THREAD SAFETY: Use thread-safe TryGetValue
			if (_blendWeightsCache.TryGetValue(cacheKey, out Dictionary<BiomeType, float> cachedWeights))
			{
				return cachedWeights;
			}

			// THREAD SAFETY: Use lock for cache size check and clearing
			// This prevents multiple threads from clearing the cache simultaneously
			if (_blendWeightsCache.Count > BLEND_CACHE_SIZE_LIMIT)
			{
				lock (_blendCacheLock)
				{
					// Double-check inside lock to avoid multiple clears
					if (_blendWeightsCache.Count > BLEND_CACHE_SIZE_LIMIT)
					{
						// Create a new dictionary instead of clearing
						// This is safer for concurrent access
						_blendWeightsCache = new ConcurrentDictionary<(int x, int z), Dictionary<BiomeType, float>>();
					}
				}
			}

			Dictionary<BiomeType, float> blendWeights = new Dictionary<BiomeType, float>();

			// Check if properly initialized
			if (!_isProperlyInitialized)
			{
				// GD.PrintErr("BiomeRegionGenerator not properly initialized in CalculateBiomeBlendWeights. Returning default biome only.");
				blendWeights[BiomeType.Plains] = 1.0f;
				_blendWeightsCache[cacheKey] = blendWeights;
				return blendWeights;
			}

			// OPTIMIZATION: Use cached cell ID to avoid recalculating warped position
			int cellId = GetCellId(worldX, worldZ);
			BiomeType currentBiome = GetBiomeTypeForCell(cellId);

			// OPTIMIZATION: Fast path - just use current biome with weight 1.0
			// This avoids the expensive boundary distance calculation in most cases
			// We'll use a simple check to see if we're likely near a boundary
			if (!IsLikelyNearBoundary(worldX, worldZ))
			{
				blendWeights[currentBiome] = 1.0f;
				_blendWeightsCache[cacheKey] = blendWeights;
				return blendWeights;
			}

			// Get distance to boundary - only if we need it
			float distanceToBoundary = GetDistanceToBoundary(worldX, worldZ);

			// If we're far from any boundary, just use the current biome with weight 1.0
			if (distanceToBoundary > blendDistance)
			{
				blendWeights[currentBiome] = 1.0f;
				_blendWeightsCache[cacheKey] = blendWeights;
				return blendWeights;
			}

			// OPTIMIZATION: Calculate blend factor based on distance to boundary
			// This avoids the expensive GetNeighboringBiomes call when we're close to a boundary
			// but not right at it
			if (distanceToBoundary > blendDistance * 0.3f)
			{
				// We're in the outer blend zone - just blend with the current biome
				float blendFactor = (distanceToBoundary - (blendDistance * 0.3f)) / (blendDistance * 0.7f);
				blendFactor = Mathf.Clamp(blendFactor, 0.0f, 1.0f);

				// Get the primary neighboring biome (most likely the one across the boundary)
				BiomeType neighborBiome = GetPrimaryNeighborBiome(worldX, worldZ, cellId);

				// Add weights for current biome and neighbor
				blendWeights[currentBiome] = blendFactor;
				blendWeights[neighborBiome] = 1.0f - blendFactor;

				_blendWeightsCache[cacheKey] = blendWeights;
				return blendWeights;
			}

			// Only do full neighbor calculation when very close to boundary
			Dictionary<BiomeType, float> neighboringBiomes = GetNeighboringBiomes(worldX, worldZ);

			// Calculate blend weights based on distance
			float totalWeight = 0f;

			foreach (var biomeEntry in neighboringBiomes)
			{
				BiomeType biome = biomeEntry.Key;
				float distance = biomeEntry.Value;

				// Calculate weight based on distance (closer = higher weight)
				// Use a smooth falloff function
				float weight;

				if (distance <= 0)
				{
					// Current biome gets full weight at center
					weight = 1.0f;
				}
				else if (distance > blendDistance)
				{
					// Skip biomes that are too far away
					continue;
				}
				else
				{
					// OPTIMIZATION: Simplified falloff calculation
					// Linear falloff is much faster than cosine
					weight = 1.0f - (distance / blendDistance);
				}

				blendWeights[biome] = weight;
				totalWeight += weight;
			}

			// Normalize weights so they sum to 1.0
			if (totalWeight > 0f)
			{
				foreach (var biome in blendWeights.Keys.ToArray())
				{
					blendWeights[biome] /= totalWeight;
				}
			}
			else
			{
				// Fallback if no weights were calculated
				blendWeights.Clear();
				blendWeights[currentBiome] = 1.0f;
			}

			// THREAD SAFETY: Use thread-safe GetOrAdd to avoid race conditions
			// This ensures only one thread can add a value for a given key
			return _blendWeightsCache.GetOrAdd(cacheKey, _ => blendWeights);
		}

		// OPTIMIZATION: Quick check to see if a position is likely near a boundary
		// This avoids the expensive GetDistanceToBoundary calculation in most cases
		private bool IsLikelyNearBoundary(int worldX, int worldZ)
		{
			// Check a few points around the position to see if any have a different cell ID
			int cellId = GetCellId(worldX, worldZ);

			// Check in 4 cardinal directions at a small distance
			int[] dx = { 5, 0, -5, 0 };
			int[] dz = { 0, 5, 0, -5 };

			for (int i = 0; i < 4; i++)
			{
				int sampleX = worldX + dx[i];
				int sampleZ = worldZ + dz[i];

				int sampleCellId = GetCellId(sampleX, sampleZ);

				if (sampleCellId != cellId)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Gets the primary neighboring biome across the nearest boundary
		/// </summary>
		/// <param name="worldX">World X coordinate</param>
		/// <param name="worldZ">World Z coordinate</param>
		/// <param name="cellId">Current cell ID (optional, will be calculated if not provided)</param>
		/// <returns>The most likely neighboring biome</returns>
		private BiomeType GetPrimaryNeighborBiome(int worldX, int worldZ, int cellId = -1)
		{
			// Get current cell ID if not provided
			if (cellId == -1)
			{
				cellId = GetCellId(worldX, worldZ);
			}

			// Get current biome
			BiomeType currentBiome = GetBiomeTypeForCell(cellId);

			// Find the direction to the nearest boundary
			// Sample in 8 directions to find the closest different cell
			int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
			int[] dz = { 0, 1, 1, 1, 0, -1, -1, -1 };

			float closestDistance = float.MaxValue;
			int closestDifferentCellId = -1;

			for (int i = 0; i < 8; i++)
			{
				// Start with a small step and increase until we find a different cell
				for (int step = 1; step <= 10; step++)
				{
					int sampleX = worldX + dx[i] * step;
					int sampleZ = worldZ + dz[i] * step;

					int sampleCellId = GetCellId(sampleX, sampleZ);

					if (sampleCellId != cellId)
					{
						float distance = step;
						if (distance < closestDistance)
						{
							closestDistance = distance;
							closestDifferentCellId = sampleCellId;
						}
						break;
					}
				}
			}

			// If we found a different cell, return its biome
			if (closestDifferentCellId != -1)
			{
				return GetBiomeTypeForCell(closestDifferentCellId);
			}

			// Fallback to current biome if no neighbor found
			return currentBiome;
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
