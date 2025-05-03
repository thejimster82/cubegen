using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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

		// Get distance to the nearest region boundary - more efficient implementation
		public float GetDistanceToBoundary(int worldX, int worldZ)
		{
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
		public int GetCellId(int worldX, int worldZ)
		{
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
		public BiomeType GetBiomeTypeForCell(int cellId)
		{
			// If we haven't assigned a biome to this cell yet, do so now
			if (!_cellToBiomeMap.ContainsKey(cellId))
			{
				AssignBiomeToCell(cellId);
			}

			return _cellToBiomeMap[cellId];
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
					if (biomesWithDistances.ContainsKey(sampleBiome) && biomesWithDistances[sampleBiome] <= radius)
						continue;

					// Add or update the biome with its distance
					biomesWithDistances[sampleBiome] = radius;
				}
			}

			return biomesWithDistances;
		}

		/// <summary>
		/// Calculates blend weights for biomes at a world position
		/// </summary>
		/// <param name="worldX">World X coordinate</param>
		/// <param name="worldZ">World Z coordinate</param>
		/// <param name="blendDistance">Maximum distance to blend (in blocks)</param>
		/// <returns>Dictionary mapping BiomeType to blend weight (0.0-1.0)</returns>
		public Dictionary<BiomeType, float> CalculateBiomeBlendWeights(int worldX, int worldZ, float blendDistance = 10f)
		{
			Dictionary<BiomeType, float> blendWeights = new Dictionary<BiomeType, float>();

			// Get the current biome
			BiomeType currentBiome = GetBiomeType(worldX, worldZ);

			// Get distance to boundary
			float distanceToBoundary = GetDistanceToBoundary(worldX, worldZ);

			// If we're far from any boundary, just use the current biome with weight 1.0
			if (distanceToBoundary > blendDistance)
			{
				blendWeights[currentBiome] = 1.0f;
				return blendWeights;
			}

			// Get neighboring biomes with their distances
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
					// Smooth falloff from 1.0 at distance 0 to 0.0 at blendDistance
					// Using a cosine interpolation for smoother transition
					float t = distance / blendDistance;
					weight = Mathf.Cos(t * Mathf.Pi * 0.5f);
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

			return blendWeights;
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
