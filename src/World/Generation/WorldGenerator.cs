using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
public partial class WorldGenerator : Node3D
{
	public int Seed { get; set; } = 0;
	[Export] public Vector2I WorldSize { get; set; } = new Vector2I(16, 16); // Size in chunks
	[Export] public int ChunkSize { get; set; } = 16; // Size of each chunk in voxels
	[Export] public int ChunkHeight { get; set; } = 128; // Maximum height of the world
	[Export] public float VoxelScale { get; set; } = 0.5f; // Scale of each voxel (0.5 = double resolution)
	[Export] public float WaterLevel { get; set; } = 0.18f; // Water level as a fraction of chunk height
	public int ViewDistance { get; set; } = 5;

	// Public constant for chunk size to be used by other classes
	public const int CHUNK_SIZE = 16;

	private ChunkManager _chunkManager;

	public override void _Ready()
	{
	}

	public void Initialize(int seed, int viewDistance)
	{
		// Explicitly initialize the BiomeRegionGenerator first
		// This ensures it's properly initialized before any biome queries
		Seed = seed;
		ViewDistance = viewDistance;
		BiomeRegionGenerator.Instance.Initialize(Seed);
		GD.Print($"BiomeRegionGenerator initialized with seed: {Seed}");

		// Initialize the WorldDataProvider
		WorldDataProvider.Instance.Initialize(Seed, ChunkSize, ChunkHeight, VoxelScale);
		GD.Print($"WorldDataProvider initialized with seed: {Seed}");

		// Initialize the FaunaSpawner
		var birdManager = GetNode<CubeGen.World.Fauna.BirdManager>("/root/World/BirdManager");
		if (birdManager != null)
		{
			CubeGen.World.Fauna.FaunaSpawner.Instance.Initialize(Seed, birdManager);
			GD.Print("FaunaSpawner initialized");
		}
		else
		{
			GD.PrintErr("BirdManager not found for FaunaSpawner initialization!");
		}

		InitializeNoise();
		_chunkManager = GetNode<ChunkManager>("ChunkManager");

		if (_chunkManager != null)
		{
			GD.Print("ChunkManager found, initializing...");
			_chunkManager.Initialize(ChunkSize, ChunkHeight);
			GenerateInitialChunks(ViewDistance);
		}
		else
		{
			GD.PrintErr("ChunkManager not found!");
		}
	}

	private void InitializeNoise()
		{
			// We're now using biome-specific noise instances in GenerateTerrainHeight
			// so we don't need to initialize _terrainNoise here anymore

			// Initialize static noise for use by other classes
			InitializeStaticNoise(Seed);
		}

	private void GenerateInitialChunks(int viewDistance)
	{
		// Generate chunks around origin
		GD.Print($"Generating initial chunks with view distance: {viewDistance}");

		// Use a circular pattern for better visual appearance
		int viewDistanceSquared = viewDistance * viewDistance;

		// Create a list of chunks to generate, sorted by distance from center
		List<(Vector2I, float)> chunksToGenerate = new List<(Vector2I, float)>();

		// First, collect all chunks within view distance
		for (int x = -viewDistance; x <= viewDistance; x++)
		{
			for (int z = -viewDistance; z <= viewDistance; z++)
			{
				// Calculate distance squared from center
				float distanceSquared = x * x + z * z;

				// Use distance squared for a more circular pattern
				if (distanceSquared <= viewDistanceSquared)
				{
					Vector2I chunkPos = new Vector2I(x, z);
					chunksToGenerate.Add((chunkPos, distanceSquared));
				}
			}
		}

		// Sort chunks by distance from center (closest first)
		chunksToGenerate.Sort((a, b) => a.Item2.CompareTo(b.Item2));

		// Generate chunks in order of distance from center
		foreach ((Vector2I chunkPos, float distance) in chunksToGenerate)
		{
			GD.Print($"Generating initial chunk at: {chunkPos}, distance: {Math.Sqrt(distance):F2}");
			GenerateChunk(chunkPos);
		}

		GD.Print("Initial chunk generation complete");
	}

	public void GenerateChunk(Vector2I chunkPos)
	{
		if (_chunkManager == null) return;

		// Create chunk data
		VoxelChunk chunk = new VoxelChunk(ChunkSize, ChunkHeight, chunkPos, VoxelScale);

		// Populate the chunk from the world data provider
		// This is the key change - the chunk now gets all its data from the provider
		// instead of generating it internally
		chunk.PopulateFromWorldProvider();

		// Process the chunk for fauna spawning
		// Note: In a full implementation, fauna would also be handled by the WorldDataProvider
		CubeGen.World.Fauna.FaunaSpawner.Instance.ProcessChunkForFauna(chunk);

		// Add the chunk data to the chunk manager
		// This makes the data available for neighboring chunks' AO calculations
		_chunkManager.AddChunk(chunk);
	}

	// Static biome noise for use by other classes
	private static FastNoiseLite _staticBiomeNoise;

	// Initialize static noise
	private static void InitializeStaticNoise(int seed)
	{
		if (_staticBiomeNoise == null)
		{
			_staticBiomeNoise = new FastNoiseLite();
			_staticBiomeNoise.Seed = seed + 1000; // Different seed for biome variation
			_staticBiomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
			_staticBiomeNoise.Frequency = 0.006f; // Doubled frequency for higher resolution (was 0.003f)
		}

		// Initialize the BiomeSubRegions with the same seed
		BiomeSubRegions.Initialize(seed);
	}

	/// <summary>
	/// Get the voxel type at a specific world position
	/// </summary>
	public static VoxelType GetVoxelTypeAtPosition(int worldX, int worldY, int worldZ)
	{
		// Use the WorldDataProvider as the source of truth
		return WorldDataProvider.Instance.GetVoxelTypeAt(worldX, worldY, worldZ);
	}

	// Get biome type for a world position - instance method
	private BiomeType GetBiomeTypeForChunk(int worldX, int worldZ)
	{
		// Use the BiomeRegionGenerator for biome determination
		return BiomeRegionGenerator.Instance.GetBiomeType(worldX, worldZ);
	}

	// Get biome type for a world position - static method for use by other classes
	public static BiomeType GetBiomeType(int worldX, int worldZ)
	{
		// Use the BiomeRegionGenerator for biome determination
		return BiomeRegionGenerator.Instance.GetBiomeType(worldX, worldZ);
	}

	// Helper method to convert noise value to biome type (kept for backward compatibility)
	private static BiomeType GetBiomeTypeFromNoise(float biomeValue)
	{
		// Simple biome distribution based on noise value
		if (biomeValue < -0.5f)
			return BiomeType.Desert;
		else if (biomeValue < 0.5f)
			return BiomeType.ForestLands;
		else
			return BiomeType.Tundra;
	}

	/// <summary>
	/// Generates terrain height for a specific biome
	/// </summary>
	private int GenerateTerrainHeight(int worldX, int worldZ, BiomeType biomeType)
	{
		// Create a temporary noise instance for biome-specific noise settings
		FastNoiseLite biomeNoise = new FastNoiseLite();
		biomeNoise.Seed = Seed;

		// Apply a consistent base height for all biomes
		float baseHeight = 0.2f;
		float noiseContribution = 0.4f; // Increased from 0.25f for greater height variation

		// Define water level height in voxels (used consistently throughout the code)
		int waterLevelHeight = Mathf.FloorToInt(WaterLevel * ChunkHeight);



		// Set biome-specific noise characteristics
		switch (biomeType)
		{
			case BiomeType.Desert:
				// Get the sub-biome type for this position
				BiomeSubRegions.DesertSubRegion desertSubBiome = BiomeSubRegions.GetDesertSubRegion(worldX, worldZ);

				// Default noise settings (will be blended)
				biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
				biomeNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;

				// Get blend factors for each sub-region
				float dunesBlend = BiomeSubRegions.GetDesertBlendFactor(worldX, worldZ, BiomeSubRegions.DesertSubRegion.Dunes);
				float rockyBlend = BiomeSubRegions.GetDesertBlendFactor(worldX, worldZ, BiomeSubRegions.DesertSubRegion.Rocky);
				float oasisBlend = BiomeSubRegions.GetDesertBlendFactor(worldX, worldZ, BiomeSubRegions.DesertSubRegion.Oasis);

				// Normalize blend factors
				float totalBlend = dunesBlend + rockyBlend + oasisBlend;
				if (totalBlend > 0)
				{
					dunesBlend /= totalBlend;
					rockyBlend /= totalBlend;
					oasisBlend /= totalBlend;
				}

				// Blend noise parameters
				// Frequency
				float frequency =
					(0.008f * dunesBlend) +  // Dunes: Low frequency
					(0.012f * rockyBlend) +  // Rocky: Higher frequency
					(0.01f * oasisBlend);    // Oasis: Medium frequency
				biomeNoise.Frequency = frequency;

				// Octaves
				float octaves =
					(1.0f * dunesBlend) +    // Dunes: Low octaves
					(2.0f * rockyBlend) +    // Rocky: More octaves
					(2.0f * oasisBlend);     // Oasis: Medium octaves
				biomeNoise.FractalOctaves = Mathf.RoundToInt(octaves);

				// Lacunarity
				float lacunarity =
					(2.0f * dunesBlend) +    // Dunes
					(2.2f * rockyBlend) +    // Rocky
					(1.8f * oasisBlend);     // Oasis
				biomeNoise.FractalLacunarity = lacunarity;

				// Gain
				float gain =
					(0.6f * dunesBlend) +    // Dunes: Increased from 0.5f
					(0.8f * rockyBlend) +    // Rocky: Increased from 0.6f
					(0.5f * oasisBlend);     // Oasis: Increased from 0.4f
				biomeNoise.FractalGain = gain;

				// Blend height adjustments
				// Rocky areas are higher, oasis areas are lower
				baseHeight += (0.08f * rockyBlend) - (0.06f * oasisBlend); // Increased difference

				break;

			case BiomeType.Tundra:
				// Default noise settings (will be blended)
				biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
				biomeNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;

				// Get blend factors for each sub-region
				float snowyBlend = BiomeSubRegions.GetTundraBlendFactor(worldX, worldZ, BiomeSubRegions.TundraSubRegion.Snowy);
				float frozenBlend = BiomeSubRegions.GetTundraBlendFactor(worldX, worldZ, BiomeSubRegions.TundraSubRegion.Frozen);
				float alpineBlend = BiomeSubRegions.GetTundraBlendFactor(worldX, worldZ, BiomeSubRegions.TundraSubRegion.Alpine);

				// Normalize blend factors
				float totalTundraBlend = snowyBlend + frozenBlend + alpineBlend;
				if (totalTundraBlend > 0)
				{
					snowyBlend /= totalTundraBlend;
					frozenBlend /= totalTundraBlend;
					alpineBlend /= totalTundraBlend;
				}

				// Blend noise parameters
				// Frequency
				float tundraFrequency =
					(0.011f * snowyBlend) +  // Snowy: Medium frequency
					(0.008f * frozenBlend) + // Frozen: Lower frequency
					(0.014f * alpineBlend);  // Alpine: Higher frequency
				biomeNoise.Frequency = tundraFrequency;

				// Octaves
				float tundraOctaves =
					(2.0f * snowyBlend) +    // Snowy: Medium octaves
					(1.0f * frozenBlend) +   // Frozen: Low octaves
					(3.0f * alpineBlend);    // Alpine: Higher octaves
				biomeNoise.FractalOctaves = Mathf.RoundToInt(tundraOctaves);

				// Lacunarity
				float tundraLacunarity =
					(1.8f * snowyBlend) +    // Snowy
					(1.5f * frozenBlend) +   // Frozen
					(2.0f * alpineBlend);    // Alpine
				biomeNoise.FractalLacunarity = tundraLacunarity;

				// Gain
				float tundraGain =
					(0.4f * snowyBlend) +    // Snowy: Increased from 0.3f
					(0.25f * frozenBlend) +  // Frozen: Increased from 0.2f
					(0.7f * alpineBlend);    // Alpine: Increased from 0.5f for more dramatic mountains
				biomeNoise.FractalGain = tundraGain;

				// Blend height adjustments
				// Frozen areas are lower, alpine areas are higher
				baseHeight += (0.12f * alpineBlend) - (0.05f * frozenBlend); // Increased difference

				break;

			case BiomeType.Islands:
				// Default noise settings (will be blended)
				biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
				biomeNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;

				// Get blend factors for each sub-region
				float beachBlend = BiomeSubRegions.GetIslandsBlendFactor(worldX, worldZ, BiomeSubRegions.IslandsSubRegion.Beach);
				float jungleBlend = BiomeSubRegions.GetIslandsBlendFactor(worldX, worldZ, BiomeSubRegions.IslandsSubRegion.Jungle);
				float lagoonBlend = BiomeSubRegions.GetIslandsBlendFactor(worldX, worldZ, BiomeSubRegions.IslandsSubRegion.Lagoon);

				// Normalize blend factors
				float totalIslandsBlend = beachBlend + jungleBlend + lagoonBlend;
				if (totalIslandsBlend > 0)
				{
					beachBlend /= totalIslandsBlend;
					jungleBlend /= totalIslandsBlend;
					lagoonBlend /= totalIslandsBlend;
				}

				// Blend noise parameters
				// Frequency
				float islandsFrequency =
					(0.018f * beachBlend) +  // Beach: Medium frequency
					(0.022f * jungleBlend) + // Jungle: Higher frequency
					(0.015f * lagoonBlend);  // Lagoon: Lower frequency
				biomeNoise.Frequency = islandsFrequency;

				// Octaves
				float islandsOctaves =
					(2.0f * beachBlend) +    // Beach: Medium octaves
					(3.0f * jungleBlend) +   // Jungle: Higher octaves
					(2.0f * lagoonBlend);    // Lagoon: Medium octaves
				biomeNoise.FractalOctaves = Mathf.RoundToInt(islandsOctaves);

				// Lacunarity
				float islandsLacunarity =
					(1.8f * beachBlend) +    // Beach
					(2.1f * jungleBlend) +   // Jungle
					(1.6f * lagoonBlend);    // Lagoon
				biomeNoise.FractalLacunarity = islandsLacunarity;

				// Gain
				float islandsGain =
					(0.5f * beachBlend) +    // Beach: Increased from 0.4f
					(0.7f * jungleBlend) +   // Jungle: Increased from 0.55f for more varied terrain
					(0.35f * lagoonBlend);   // Lagoon: Increased from 0.3f
				biomeNoise.FractalGain = islandsGain;

				// Blend height adjustments
				// Beaches are slightly lower, jungle is higher, lagoons are much lower
				baseHeight += (0.07f * jungleBlend) - (0.03f * beachBlend) - (0.08f * lagoonBlend); // Increased differences

				break;

			case BiomeType.ForestLands:
			default:
				// Default noise settings (will be blended)
				biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;

				// Get blend factors for each sub-region
				float plainsBlend = BiomeSubRegions.GetForestLandsBlendFactor(worldX, worldZ, BiomeSubRegions.ForestLandsSubRegion.Plains);
				float forestBlend = BiomeSubRegions.GetForestLandsBlendFactor(worldX, worldZ, BiomeSubRegions.ForestLandsSubRegion.Forest);
				float mountainsBlend = BiomeSubRegions.GetForestLandsBlendFactor(worldX, worldZ, BiomeSubRegions.ForestLandsSubRegion.Mountains);

				// Normalize blend factors
				float totalForestBlend = plainsBlend + forestBlend + mountainsBlend;
				if (totalForestBlend > 0)
				{
					plainsBlend /= totalForestBlend;
					forestBlend /= totalForestBlend;
					mountainsBlend /= totalForestBlend;
				}

				// Blend noise parameters
				// Fractal type - special case since we can't blend enum values
				// Use Ridged fractal if mountains blend is dominant, otherwise use Fbm
				biomeNoise.FractalType = (mountainsBlend > 0.5f) ?
					FastNoiseLite.FractalTypeEnum.Ridged :
					FastNoiseLite.FractalTypeEnum.Fbm;

				// Frequency
				float forestFrequency =
					(0.01f * plainsBlend) +   // Plains: Medium-low frequency
					(0.012f * forestBlend) +  // Forest: Medium frequency
					(0.015f * mountainsBlend); // Mountains: Medium-high frequency
				biomeNoise.Frequency = forestFrequency;

				// Octaves
				float forestOctaves =
					(2.0f * plainsBlend) +    // Plains: Low octaves
					(3.0f * forestBlend) +    // Forest: Medium octaves
					(4.0f * mountainsBlend);  // Mountains: Higher octaves
				biomeNoise.FractalOctaves = Mathf.RoundToInt(forestOctaves);

				// Lacunarity
				float forestLacunarity =
					(2.0f * plainsBlend) +    // Plains
					(2.0f * forestBlend) +    // Forest
					(2.2f * mountainsBlend);  // Mountains
				biomeNoise.FractalLacunarity = forestLacunarity;

				// Gain
				float forestGain =
					(0.45f * plainsBlend) +   // Plains: Increased from 0.4f
					(0.6f * forestBlend) +    // Forest: Increased from 0.5f
					(0.8f * mountainsBlend);  // Mountains: Increased from 0.6f for more dramatic peaks
				biomeNoise.FractalGain = forestGain;

				// Blend height adjustments
				// Mountains are higher, plains slightly lower
				baseHeight += (0.15f * mountainsBlend) - (0.02f * plainsBlend); // Increased mountain height

				break;
		}

		// Get noise value with biome-specific settings
		float heightNoise = biomeNoise.GetNoise2D(worldX, worldZ);

		// Convert noise from [-1, 1] to [0, 1]
		heightNoise = (heightNoise + 1f) * 0.5f;

		// Special handling for Islands biome
		if (biomeType == BiomeType.Islands)
		{
			// Create islands by thresholding the noise
			// Values below threshold become water, above become land
			float islandThreshold = 0.55f; // Adjust to control island size

			if (heightNoise >= islandThreshold)
			{
				// Above threshold - scale the remaining range to create island terrain
				// Scale the noise to create more pronounced islands
				float scaledNoise = ((heightNoise - islandThreshold) / (1.0f - islandThreshold));

				// Apply a stronger curve to make islands more pronounced
				scaledNoise = scaledNoise * scaledNoise * 2.0f; // Increased from 1.5f

				// Increased maximum height for taller island peaks
				scaledNoise = Mathf.Min(scaledNoise, 1.0f); // Increased from 0.8f

				// Calculate final height
				heightNoise = baseHeight + scaledNoise * noiseContribution;
			}
			else
			{
				// Below threshold - generate normal underwater terrain
				// Use a lower height for underwater areas
				heightNoise = heightNoise * 0.3f;
			}

			// Convert to actual height value and return
			return Mathf.FloorToInt(heightNoise * ChunkHeight);
		}
		else
		{
			// Standard terrain generation for other biomes
			heightNoise = baseHeight + (heightNoise * noiseContribution);
		}

		// Convert to actual height value
		int height = Mathf.FloorToInt(heightNoise * ChunkHeight);

		return height;
	}

	/// <summary>
	/// Generates terrain height by blending multiple biomes based on blend weights
	/// </summary>
	/// <param name="worldX">World X coordinate</param>
	/// <param name="worldZ">World Z coordinate</param>
	/// <param name="biomeBlendWeights">Dictionary mapping BiomeType to blend weight (0.0-1.0)</param>
	/// <returns>Blended terrain height</returns>
	private int GenerateBlendedTerrainHeight(int worldX, int worldZ, Dictionary<BiomeType, float> biomeBlendWeights)
	{
		// Fast path: If there's only one biome with weight 1.0, use the standard method
		if (biomeBlendWeights.Count == 1)
		{
			BiomeType biomeType = biomeBlendWeights.Keys.First();
			return GenerateTerrainHeight(worldX, worldZ, biomeType);
		}

		// Fast path: If there are only two biomes and one has a weight of 1.0
		if (biomeBlendWeights.Count == 2)
		{
			bool hasFullWeight = false;
			BiomeType fullWeightBiome = BiomeType.ForestLands; // Default, will be overwritten

			foreach (var entry in biomeBlendWeights)
			{
				if (Math.Abs(entry.Value - 1.0f) < 0.001f) // Check if weight is very close to 1.0
				{
					hasFullWeight = true;
					fullWeightBiome = entry.Key;
					break;
				}
			}

			if (hasFullWeight)
			{
				return GenerateTerrainHeight(worldX, worldZ, fullWeightBiome);
			}
		}

		// Calculate weighted height from all contributing biomes
		float totalHeight = 0f;
		float totalWeight = 0f;

		foreach (var biomeEntry in biomeBlendWeights)
		{
			BiomeType biomeType = biomeEntry.Key;
			float weight = biomeEntry.Value;

			if (weight <= 0.001f) // Skip very small weights
				continue;

			// Get height for this biome
			int biomeHeight = GenerateTerrainHeight(worldX, worldZ, biomeType);

			// Add weighted contribution
			totalHeight += biomeHeight * weight;
			totalWeight += weight;
		}

		// Normalize if needed
		if (totalWeight > 0f)
		{
			totalHeight /= totalWeight;
		}
		else
		{
			// Fallback to default biome if no weights
			return GenerateTerrainHeight(worldX, worldZ, BiomeType.ForestLands);
		}

		// Convert to integer height
		int blendedHeight = Mathf.RoundToInt(totalHeight);

		// Debug output for the first chunk to help understand terrain height
		if (worldX == 0 && worldZ == 0)
		{
			string biomeInfo = string.Join(", ", biomeBlendWeights.Select(b => $"{b.Key}:{b.Value:F2}"));
			GD.Print($"Blended terrain height at origin: {blendedHeight}, biomes: {biomeInfo}");
		}

		return blendedHeight;
	}

	/// <summary>
	/// Interpolates blend weights from a pre-calculated grid
	/// </summary>
	/// <param name="x">X coordinate within the chunk</param>
	/// <param name="z">Z coordinate within the chunk</param>
	/// <param name="blendWeightsGrid">Pre-calculated grid of blend weights</param>
	/// <param name="gridSize">Size of the grid (e.g., 4 for a 4x4 grid)</param>
	/// <param name="chunkSize">Size of the chunk in voxels</param>
	/// <returns>Interpolated blend weights for the given position</returns>
	private static Dictionary<BiomeType, float> InterpolateBlendWeights(
		int x, int z,
		Dictionary<BiomeType, float>[,] blendWeightsGrid,
		int gridSize, int chunkSize)
	{
		// Convert x,z to normalized coordinates in the grid (0.0 to 1.0)
		float normalizedX = x / (float)(chunkSize - 1);
		float normalizedZ = z / (float)(chunkSize - 1);

		// Convert to grid coordinates
		float gridX = normalizedX * (gridSize - 1);
		float gridZ = normalizedZ * (gridSize - 1);

		// Get the four surrounding grid points
		int gridX1 = Mathf.FloorToInt(gridX);
		int gridZ1 = Mathf.FloorToInt(gridZ);
		int gridX2 = Mathf.Min(gridX1 + 1, gridSize - 1);
		int gridZ2 = Mathf.Min(gridZ1 + 1, gridSize - 1);

		// Calculate interpolation factors
		float factorX = gridX - gridX1;
		float factorZ = gridZ - gridZ1;

		// Get the four corner weights
		var weights11 = blendWeightsGrid[gridX1, gridZ1];
		var weights12 = blendWeightsGrid[gridX1, gridZ2];
		var weights21 = blendWeightsGrid[gridX2, gridZ1];
		var weights22 = blendWeightsGrid[gridX2, gridZ2];

		// Collect all biome types from the four corners
		var allBiomes = new HashSet<BiomeType>(weights11.Keys);
		allBiomes.UnionWith(weights12.Keys);
		allBiomes.UnionWith(weights21.Keys);
		allBiomes.UnionWith(weights22.Keys);

		// Create result dictionary
		var result = new Dictionary<BiomeType, float>();

		// Bilinear interpolation for each biome
		foreach (var biome in allBiomes)
		{
			// Get weights from each corner (default to 0 if not present)
			weights11.TryGetValue(biome, out float w11);
			weights12.TryGetValue(biome, out float w12);
			weights21.TryGetValue(biome, out float w21);
			weights22.TryGetValue(biome, out float w22);

			// Bilinear interpolation
			float topInterp = Mathf.Lerp(w11, w21, factorX);
			float bottomInterp = Mathf.Lerp(w12, w22, factorX);
			float finalWeight = Mathf.Lerp(topInterp, bottomInterp, factorZ);

			// Only add biomes with significant weight
			if (finalWeight > 0.001f)
			{
				result[biome] = finalWeight;
			}
		}

		// Normalize weights to ensure they sum to 1.0
		float totalWeight = 0f;
		foreach (var weight in result.Values)
		{
			totalWeight += weight;
		}

		if (totalWeight > 0f)
		{
			foreach (var biome in result.Keys.ToArray())
			{
				result[biome] /= totalWeight;
			}
		}

		return result;
	}

	private VoxelType DetermineVoxelType(int y, int terrainHeight, BiomeType biomeType)
	{
		// Calculate water level height in voxels
		int waterLevelHeight = Mathf.FloorToInt(WaterLevel * ChunkHeight);

		// Bedrock at bottom
		if (y == 0)
			return VoxelType.Bedrock;



		// For Islands biome, handle water and islands
		if (biomeType == BiomeType.Islands)
		{
			// If this is at the terrain surface
			if (y == terrainHeight - 1)
			{
				// If the terrain is at or below water level, use sand (beach)
				if (terrainHeight <= waterLevelHeight)
				{
					// For underwater terrain, use sand at the bottom
					return VoxelType.Sand; // Islands have sandy beaches
				}
				else
				{
					// Check if we're near the water's edge (within 5 blocks of water level)
					// This creates a sandy beach around the island
					if (terrainHeight <= waterLevelHeight + 5)
					{
						return VoxelType.Sand; // Create sandy beaches around islands
					}
					else
					{
						return VoxelType.Grass; // Islands have grass on top above water level
					}
				}
			}
			// Layers just below surface
			else if (y >= terrainHeight - 4)
			{
				// If the terrain is at or below water level, use sand
				if (terrainHeight <= waterLevelHeight + 5) // Extended sand area to match surface
				{
					return VoxelType.Sand; // More sand under islands and beaches
				}
				else
				{
					return VoxelType.Dirt; // Dirt under grass
				}
			}
			// Stone for deeper layers
			else if (y < terrainHeight * 0.6f)
			{
				return VoxelType.Stone;
			}
			else
			{
				return VoxelType.Dirt;
			}
		}

		// Surface layer and layers just below for other biomes
		if (y == terrainHeight - 1)
		{
			// Top layer depends on biome
			switch (biomeType)
			{
				case BiomeType.Desert:
					return VoxelType.Sand;
				case BiomeType.Tundra:
					return VoxelType.Snow;
				case BiomeType.ForestLands:
				default:
					// We don't have worldX/worldZ here, so we need to use a consistent approach
					// Use the terrainHeight as a seed for the sub-biome determination
					// This ensures consistent sub-biome detection for the same terrain height
					int subBiomeSeed = terrainHeight * 100 + Seed;
					Random subBiomeRandom = new Random(subBiomeSeed);
					float subBiomeValue = (float)subBiomeRandom.NextDouble();

					// Determine sub-biome type based on the random value
					if (subBiomeValue < 0.333f)
					{
						// Plains sub-biome (equivalent to BiomeSubRegions.ForestLandsSubRegion.Plains)
						return VoxelType.Grass;
					}
					else if (subBiomeValue < 0.667f)
					{
						// Forest sub-biome (equivalent to BiomeSubRegions.ForestLandsSubRegion.Forest)
						return VoxelType.Grass;
					}
					else
					{
						// Mountains sub-biome (equivalent to BiomeSubRegions.ForestLandsSubRegion.Mountains)
						// Use a secondary noise to determine if this should be stone or grass
						FastNoiseLite localNoise = new FastNoiseLite();
						localNoise.Seed = Seed + 7890;
						localNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
						localNoise.Frequency = 0.02f;

						float localValue = localNoise.GetNoise2D(terrainHeight * 10, y * 5);
						localValue = (localValue + 1f) * 0.5f;

						// Higher chance of stone in mountainous areas
						if (localValue > 0.6f)
						{
							return VoxelType.Stone;
						}
						else
						{
							return VoxelType.Grass;
						}
					}
			}
		}
		else if (y >= terrainHeight - 4)
		{
			// Layers just below surface
			switch (biomeType)
			{
				case BiomeType.Desert:
					return VoxelType.Sand;
				case BiomeType.Tundra:
					return VoxelType.Dirt;
				case BiomeType.ForestLands:
				default:
					// Use the same sub-biome determination as for the surface layer
					int subBiomeSeed = terrainHeight * 100 + Seed;
					Random subBiomeRandom = new Random(subBiomeSeed);
					float subBiomeValue = (float)subBiomeRandom.NextDouble();

					// Determine sub-biome type based on the random value
					if (subBiomeValue < 0.333f)
					{
						// Plains sub-biome (equivalent to BiomeSubRegions.ForestLandsSubRegion.Plains)
						return VoxelType.Dirt;
					}
					else if (subBiomeValue < 0.667f)
					{
						// Forest sub-biome (equivalent to BiomeSubRegions.ForestLandsSubRegion.Forest)
						return VoxelType.Dirt;
					}
					else
					{
						// Mountains sub-biome (equivalent to BiomeSubRegions.ForestLandsSubRegion.Mountains)
						// Use a secondary noise to determine if this should be stone or dirt
						FastNoiseLite localNoise = new FastNoiseLite();
						localNoise.Seed = Seed + 7890;
						localNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
						localNoise.Frequency = 0.02f;

						float localValue = localNoise.GetNoise2D(terrainHeight * 10, y * 5);
						localValue = (localValue + 1f) * 0.5f;

						// Higher chance of stone in mountainous areas
						if (localValue > 0.5f && y < terrainHeight - 2)
						{
							return VoxelType.Stone;
						}
						else
						{
							return VoxelType.Dirt;
						}
					}
			}
		}

		// Stone for deeper layers
		if (y < terrainHeight * 0.6f)
			return VoxelType.Stone;

		return VoxelType.Dirt;
	}

	private void AddBiomeObjects(VoxelChunk chunk, Vector2I chunkPos, int chunkSize)
	{
		// Generate trees and other biome objects
		Random random = new Random(Seed + chunkPos.X * 10000 + chunkPos.Y);

		// Initialize decoration clusters for this chunk
		DecorationClusters.InitializeChunkClusters(chunkPos, chunkSize, random);

		// Create a 2D array to track where features have been placed
		// true = feature exists at this position, false = no feature
		bool[,] featureMap = new bool[chunkSize, chunkSize];

		for (int x = 0; x < chunkSize; x++)
		{
			for (int z = 0; z < chunkSize; z++)
			{
				int worldX = chunkPos.X * chunkSize + x;
				int worldZ = chunkPos.Y * chunkSize + z;

				BiomeType biomeType = GetBiomeType(worldX, worldZ);

				// Find surface height
				int surfaceHeight = -1;
				for (int y = ChunkHeight - 1; y >= 0; y--)
				{
					if (chunk.GetVoxel(x, y, z) != VoxelType.Air)
					{
						surfaceHeight = y;
						break;
					}
				}

				// Only proceed if we found a surface
				if (surfaceHeight < 0)
					continue;

				// Add decorations based on clusters
				if (chunk.GetVoxel(x, surfaceHeight, z) != VoxelType.Air && surfaceHeight + 1 < chunk.Height)
				{
					// Only place decorations if the block above is air
					if (chunk.GetVoxel(x, surfaceHeight + 1, z) == VoxelType.Air)
					{
						// Use the already calculated world position
						Vector2I worldPos = new Vector2I(worldX, worldZ);

						// Check if this position should have a decoration based on clusters
						DecorationClusters.DecorationPlacement placement;
						if (DecorationClusters.ShouldPlaceDecoration(worldPos, out placement, random))
						{
							// Check if the decoration is appropriate for the surface block
							bool canPlace = false;

							switch (placement.Type)
							{
								case VoxelType.TallGrass:
								case VoxelType.Flower:
									// Grass and flowers only on grass blocks
									canPlace = chunk.GetVoxel(x, surfaceHeight, z) == VoxelType.Grass;
									break;

								case VoxelType.Mushroom:
									// Mushrooms on grass or dirt
									canPlace = chunk.GetVoxel(x, surfaceHeight, z) == VoxelType.Grass ||
											  chunk.GetVoxel(x, surfaceHeight, z) == VoxelType.Dirt;
									break;

								case VoxelType.Rock:
									// Rocks can go on any surface
									canPlace = true;
									break;

								case VoxelType.Stick:
									// Sticks primarily in forests on grass or dirt
									canPlace = chunk.GetVoxel(x, surfaceHeight, z) == VoxelType.Grass ||
											  chunk.GetVoxel(x, surfaceHeight, z) == VoxelType.Dirt;
									break;

								case VoxelType.Seashell:
									// Seashells only on sand
									canPlace = chunk.GetVoxel(x, surfaceHeight, z) == VoxelType.Sand;
									break;

								default:
									canPlace = false;
									break;
							}

							// Place the decoration if appropriate
							if (canPlace)
							{
								// Store the decoration type in the voxel data
								chunk.SetVoxel(x, surfaceHeight + 1, z, placement.Type);

								// Store the placement information in the chunk's metadata
								// This will be used by the mesh generator to position the decoration
								chunk.SetDecorationPlacement(x, surfaceHeight + 1, z, placement);
							}
						}
					}
				}

				// Add biome-specific features with appropriate probability
				// Check position is safely away from chunk boundaries
				int safeDistance = 4; // Reduced safe distance to allow more features
				if (x >= safeDistance && x < (chunkSize - safeDistance) &&
					z >= safeDistance && z < (chunkSize - safeDistance))
				{
					switch (biomeType)
					{
						case BiomeType.Desert:
							// Add cacti in Desert biome
							if (random.NextDouble() < 0.008) // Adjusted to a more reasonable value
							{
								if (surfaceHeight >= 0)
								{
									// Check if we can place a cactus here (no overlap with other features)
									// Cacti need a medium radius (4) to prevent overlap
									if (CanPlaceFeature(featureMap, x, z, 4, chunkSize))
									{
										// More lenient check - allow cacti on any solid surface in desert biome
										GenerateCactus(chunk, x, z, surfaceHeight, random);

										// Mark the area as occupied
										MarkFeaturePosition(featureMap, x, z, 4, chunkSize);
									}
								}
							}
							// Add rock formations in Desert biome
							else if (random.NextDouble() < 0.006) // Adjusted to a more reasonable value
							{
								if (surfaceHeight >= 0)
								{
									// Check if we can place a rock formation here (no overlap with other features)
									// Rock formations need a medium radius (5) to prevent overlap
									if (CanPlaceFeature(featureMap, x, z, 5, chunkSize))
									{
										// More lenient check - allow rock formations on any solid surface in desert biome
										GenerateRockFormation(chunk, x, z, surfaceHeight, random);

										// Mark the area as occupied
										MarkFeaturePosition(featureMap, x, z, 5, chunkSize);
									}
								}
							}
							break;

						case BiomeType.Tundra:
							// Add ice formations in Tundra biome
							if (random.NextDouble() < 0.008) // Adjusted to a more reasonable value
							{
								if (surfaceHeight >= 0)
								{
									// Check if we can place an ice formation here (no overlap with other features)
									// Ice formations need a medium radius (4) to prevent overlap
									if (CanPlaceFeature(featureMap, x, z, 4, chunkSize))
									{
										// More lenient check - allow ice formations on any solid surface in tundra biome
										GenerateIceFormation(chunk, x, z, surfaceHeight, random);

										// Mark the area as occupied
										MarkFeaturePosition(featureMap, x, z, 4, chunkSize);
									}
								}
							}
							// Add snow-covered trees in Tundra biome
							else if (random.NextDouble() < 0.006) // Adjusted to a more reasonable value
							{
								if (surfaceHeight >= 0)
								{
									// Check if we can place a snow tree here (no overlap with other features)
									// Snow trees need a larger radius (6) to prevent overlap
									if (CanPlaceFeature(featureMap, x, z, 6, chunkSize))
									{
										// More lenient check - allow snow trees on any solid surface in tundra biome
										GenerateSnowTree(chunk, x, z, surfaceHeight, random);

										// Mark the area as occupied
										MarkFeaturePosition(featureMap, x, z, 6, chunkSize);
									}
								}
							}
							break;



						case BiomeType.Islands:
							// Add palm trees on islands
							if (random.NextDouble() < 0.02) // Increased chance for palm trees (2%)
							{
								if (surfaceHeight >= 0)
								{
									// Check if we can place a palm tree here (no overlap with other features)
									// Palm trees need a larger radius (5) to prevent overlap
									if (CanPlaceFeature(featureMap, x, z, 5, chunkSize))
									{
										// Calculate water level height in voxels
										int waterLevelHeight = Mathf.FloorToInt(WaterLevel * ChunkHeight);

										// Place palm trees on sand (beach areas) or on grass near the beach
										VoxelType surfaceType = chunk.GetVoxel(x, surfaceHeight, z);

										if (surfaceType == VoxelType.Sand ||
											(surfaceType == VoxelType.Grass && surfaceHeight <= waterLevelHeight + 8))
										{
											GeneratePalmTree(chunk, x, z, surfaceHeight, random);

											// Mark the area as occupied
											MarkFeaturePosition(featureMap, x, z, 5, chunkSize);
										}
									}
								}
							}
							// Add seashells on beaches
							else if (random.NextDouble() < 0.03) // Higher chance for seashells
							{
								if (surfaceHeight >= 0)
								{
									// Check if we can place a seashell here (no overlap with other features)
									// Seashells need a small radius (2) to prevent overlap
									if (CanPlaceFeature(featureMap, x, z, 2, chunkSize))
									{
										// Only place seashells on sand
										if (chunk.GetVoxel(x, surfaceHeight, z) == VoxelType.Sand)
										{
											// Use the decoration system to place seashells
											Vector2I worldPos = new Vector2I(worldX, worldZ);
											DecorationClusters.DecorationPlacement seashellPlacement = new DecorationClusters.DecorationPlacement(
												VoxelType.Seashell,
												new Vector2(random.Next(-20, 20) / 100.0f, random.Next(-20, 20) / 100.0f),
												random.Next(0, 360),
												0.8f + (float)random.NextDouble() * 0.4f
											);

											// Place the seashell
											chunk.SetVoxel(x, surfaceHeight + 1, z, VoxelType.Seashell);
											chunk.SetDecorationPlacement(x, surfaceHeight + 1, z, seashellPlacement);

											// Mark the area as occupied
											MarkFeaturePosition(featureMap, x, z, 2, chunkSize);
										}
									}
								}
							}
							break;

						case BiomeType.ForestLands:
							// Get the sub-biome type for this position
							BiomeSubRegions.ForestLandsSubRegion subBiome = BiomeSubRegions.GetForestLandsSubRegion(worldX, worldZ);

							// Add features based on sub-biome type
							switch (subBiome)
							{
								case BiomeSubRegions.ForestLandsSubRegion.Plains:
									// PLAINS SUB-BIOME
									// Add small bushes
									if (random.NextDouble() < 0.015)
									{
										if (surfaceHeight >= 0 && CanPlaceFeature(featureMap, x, z, 3, chunkSize))
										{
											GenerateBush(chunk, x, z, surfaceHeight, random);
											MarkFeaturePosition(featureMap, x, z, 3, chunkSize);
										}
									}
									// Add occasional lone trees
									else if (random.NextDouble() < 0.006)
									{
										if (surfaceHeight >= 0 && CanPlaceFeature(featureMap, x, z, 6, chunkSize))
										{
											GenerateDetailedTree(chunk, x, z, surfaceHeight, random);
											MarkFeaturePosition(featureMap, x, z, 6, chunkSize);
										}
									}
									break;

								case BiomeSubRegions.ForestLandsSubRegion.Forest:
									// FOREST SUB-BIOME
									// Add trees with higher density
									if (random.NextDouble() < 0.02)
									{
										if (surfaceHeight >= 0 && CanPlaceFeature(featureMap, x, z, 6, chunkSize))
										{
											GenerateDetailedTree(chunk, x, z, surfaceHeight, random);
											MarkFeaturePosition(featureMap, x, z, 6, chunkSize);
										}
									}
									// Add bushes between trees
									else if (random.NextDouble() < 0.012)
									{
										if (surfaceHeight >= 0 && CanPlaceFeature(featureMap, x, z, 3, chunkSize))
										{
											GenerateBush(chunk, x, z, surfaceHeight, random);
											MarkFeaturePosition(featureMap, x, z, 3, chunkSize);
										}
									}
									break;

								case BiomeSubRegions.ForestLandsSubRegion.Mountains:
									// MOUNTAINS SUB-BIOME
									// Add rock spires
									if (random.NextDouble() < 0.01)
									{
										if (surfaceHeight >= 0 && CanPlaceFeature(featureMap, x, z, 5, chunkSize))
										{
											GenerateRockSpire(chunk, x, z, surfaceHeight, random);
											MarkFeaturePosition(featureMap, x, z, 5, chunkSize);
										}
									}
									// Add boulders
									else if (random.NextDouble() < 0.015)
									{
										if (surfaceHeight >= 0 && CanPlaceFeature(featureMap, x, z, 4, chunkSize))
										{
											GenerateBoulder(chunk, x, z, surfaceHeight, random);
											MarkFeaturePosition(featureMap, x, z, 4, chunkSize);
										}
									}
									// Add occasional trees even in mountainous areas
									else if (random.NextDouble() < 0.005)
									{
										if (surfaceHeight >= 0 && CanPlaceFeature(featureMap, x, z, 6, chunkSize))
										{
											GenerateDetailedTree(chunk, x, z, surfaceHeight, random);
											MarkFeaturePosition(featureMap, x, z, 6, chunkSize);
										}
									}
									break;
							}
							break;
					}
				}
			}
		}
	}

	// Helper method to check if a feature can be placed at a position
	private bool CanPlaceFeature(bool[,] featureMap, int x, int z, int radius, int chunkSize)
	{
		// Check if the position is already occupied
		if (featureMap[x, z])
			return false;

		// Check surrounding area based on radius
		int minX = Math.Max(0, x - radius);
		int maxX = Math.Min(chunkSize - 1, x + radius);
		int minZ = Math.Max(0, z - radius);
		int maxZ = Math.Min(chunkSize - 1, z + radius);

		for (int dx = minX; dx <= maxX; dx++)
		{
			for (int dz = minZ; dz <= maxZ; dz++)
			{
				if (featureMap[dx, dz])
					return false;
			}
		}

		return true;
	}

	// Helper method to mark a feature position and its surrounding area
	private void MarkFeaturePosition(bool[,] featureMap, int x, int z, int radius, int chunkSize)
	{
		// Mark the center position
		featureMap[x, z] = true;

		// Mark surrounding area based on radius
		int minX = Math.Max(0, x - radius);
		int maxX = Math.Min(chunkSize - 1, x + radius);
		int minZ = Math.Max(0, z - radius);
		int maxZ = Math.Min(chunkSize - 1, z + radius);

		for (int dx = minX; dx <= maxX; dx++)
		{
			for (int dz = minZ; dz <= maxZ; dz++)
			{
				// Mark as occupied
				featureMap[dx, dz] = true;
			}
		}
	}

	private static void GenerateDetailedTree(VoxelChunk chunk, int x, int z, int surfaceHeight, Random random)
	{
		// Tree parameters - doubled for higher resolution
		int trunkHeight = random.Next(12, 24); // Doubled height for higher resolution (was 6-10)
		int trunkThickness = random.Next(1, 3); // Doubled height for higher resolution (was 6-10)
		int trunkThicknessBase = random.Next(6, 8); // Doubled height for higher resolution (was 6-10)
		int leafRadius = random.Next(6, 9);    // Doubled radius for higher resolution (was 2-4)
		int leafHeight = random.Next(10, 11);   // Doubled height for higher resolution (was 4-6)
		int trunkShiftX = random.Next(-6, 6);
		int trunkShiftZ = random.Next(-6, 6);

		// Generate trunk with more detail
		// Make the trunk thicker at the base (2x2 for first few blocks)
		for (int y = 1; y <= Math.Min(10, trunkHeight); y++)
		{
			for (int dx = Math.Min(-1, -trunkThicknessBase / 2 + y / 2); dx <= Math.Max(1, trunkThicknessBase / 2 - y / 2); dx++)
			{
				for (int dz = Math.Min(-1, -trunkThicknessBase / 2 + y / 2); dz <= Math.Max(1, trunkThicknessBase / 2 - y / 2); dz++)
				{
					int nx = x + dx;
					int nz = z + dz;

					// We already checked chunk boundaries in AddBiomeObjects, so this should be safe
					if (random.NextDouble() > 0.2f && surfaceHeight + y < chunk.Height)
					{
						chunk.SetVoxel(nx, surfaceHeight + y, nz, VoxelType.Wood);
					}
				}
			}
		}

		// Continue with a 1x1 trunk for the rest of the height
		float xFloat = x;
		float zFloat = z;
		for (int y = 4; y <= trunkHeight; y++)
		{
			xFloat += (float)trunkShiftX / (float)trunkHeight;
			zFloat += (float)trunkShiftZ / (float)trunkHeight;
			x = (int)xFloat;
			z = (int)zFloat;
			// Create a 2x2 trunk base if there's room in the chunk
			for (int dx = 0; dx <= trunkThickness; dx++)
			{
				for (int dz = 0; dz <= trunkThickness; dz++)
				{
					int nx = x + dx;
					int nz = z + dz;

					// We already checked chunk boundaries in AddBiomeObjects, so this should be safe
					if (surfaceHeight + y < chunk.Height)
					{
						chunk.SetVoxel(nx, surfaceHeight + y, nz, VoxelType.Wood);
					}
				}
			}
		}

		// Generate leaf canopy (spherical/rounded shape)
		int leafStartHeight = surfaceHeight + trunkHeight - leafHeight / 2;

		// Create a more balanced, symmetrical leaf structure
		for (int y = 0; y < leafHeight; y++)
		{
			// Calculate the radius at this height (ellipsoid shape)
			// Maximum radius at the middle, smaller at top and bottom
			float heightFactor = 1.0f - Math.Abs((y - leafHeight / 2.0f) / (leafHeight / 2.0f));
			int currentRadius = (int)Math.Ceiling(leafRadius * heightFactor);

			// Add some randomness to the leaf shape but keep it symmetrical
			float randomFactor = 0.8f + (float)random.NextDouble() * 0.4f; // 0.8 to 1.2
			currentRadius = (int)Math.Ceiling(currentRadius * randomFactor);

			for (int dx = -currentRadius; dx <= currentRadius; dx++)
			{
				for (int dz = -currentRadius; dz <= currentRadius; dz++)
				{
					// Create a rounded shape by checking distance from center
					float distance = (float)Math.Sqrt(dx * dx + dz * dz);

					// Add some noise to the leaf edge for a more natural look
					float edgeNoise = (float)random.NextDouble() * 0.8f;
					float effectiveRadius = currentRadius + edgeNoise;

					if (distance <= effectiveRadius)
					{
						int nx = x + dx;
						int nz = z + dz;
						int ny = leafStartHeight + y;

						// We already checked chunk boundaries in AddBiomeObjects, so this should be safe
						if (ny < chunk.Height)
						{
							// Don't overwrite the trunk
							if (chunk.GetVoxel(nx, ny, nz) != VoxelType.Wood)
							{
								// Add some randomness to make leaves less uniform
								// But ensure the tree still looks full and balanced
								// if (distance > effectiveRadius - 0.8f && random.NextDouble() < 0.3f)

								chunk.SetVoxel(nx, ny, nz, VoxelType.Leaves);
							}
						}
					}
				}
			}
		}
	}

	private static void GenerateCactus(VoxelChunk chunk, int x, int z, int surfaceHeight, Random random)
	{
		// Cactus parameters
		int mainHeight = random.Next(8, 16); // Height of the main trunk
		bool hasArms = random.NextDouble() < 0.7f; // 70% chance to have arms

		// Generate main trunk
		for (int y = 1; y <= mainHeight; y++)
		{
			if (surfaceHeight + y < chunk.Height)
			{
				chunk.SetVoxel(x, surfaceHeight + y, z, VoxelType.Cactus);
			}
		}

		// Add arms if needed
		if (hasArms)
		{
			// Determine number of arms (1-2)
			int numArms = random.Next(1, 3);

			for (int arm = 0; arm < numArms; arm++)
			{
				// Determine arm direction (0=+x, 1=-x, 2=+z, 3=-z)
				int direction = random.Next(0, 4);

				// Determine arm height (somewhere in the middle of the cactus)
				int armHeight = random.Next(mainHeight / 3, mainHeight * 2 / 3);

				// Determine arm length
				int armLength = random.Next(3, 7);

				// Calculate direction offsets
				int dx = 0, dz = 0;
				switch (direction)
				{
					case 0: dx = 1; break;
					case 1: dx = -1; break;
					case 2: dz = 1; break;
					case 3: dz = -1; break;
				}

				// Generate the arm
				for (int i = 1; i <= armLength; i++)
				{
					int nx = x + dx * i;
					int nz = z + dz * i;
					int ny = surfaceHeight + armHeight;

					// Check chunk boundaries
					if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size && ny < chunk.Height)
					{
						chunk.SetVoxel(nx, ny, nz, VoxelType.Cactus);

						// Add a small vertical part at the end of the arm
						if (i == armLength)
						{
							int verticalHeight = random.Next(2, 5);
							for (int v = 1; v <= verticalHeight; v++)
							{
								if (ny + v < chunk.Height)
								{
									chunk.SetVoxel(nx, ny + v, nz, VoxelType.Cactus);
								}
							}
						}
					}
				}
			}
		}
	}

	private static void GenerateRockFormation(VoxelChunk chunk, int x, int z, int surfaceHeight, Random random)
	{
		// Rock formation parameters
		int baseRadius = random.Next(3, 6);
		int height = random.Next(5, 10);

		// Generate a rounded rock formation
		for (int y = 0; y < height; y++)
		{
			// Rocks get smaller as they go up
			float heightFactor = 1.0f - (y / (float)height);
			int currentRadius = (int)Math.Ceiling(baseRadius * heightFactor);

			for (int dx = -currentRadius; dx <= currentRadius; dx++)
			{
				for (int dz = -currentRadius; dz <= currentRadius; dz++)
				{
					// Create a rounded shape
					float distance = (float)Math.Sqrt(dx * dx + dz * dz);

					// Add some noise to the edge
					float edgeNoise = (float)random.NextDouble() * 0.5f;
					float effectiveRadius = currentRadius - edgeNoise;

					if (distance <= effectiveRadius)
					{
						int nx = x + dx;
						int nz = z + dz;
						int ny = surfaceHeight + y + 1; // Start one block above surface

						// Check chunk boundaries
						if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size && ny < chunk.Height)
						{
							// Add some randomness to make rocks less uniform
							if (distance > effectiveRadius - 0.8f && random.NextDouble() < 0.4f)
							{
								// Skip some edge blocks randomly
								continue;
							}

							chunk.SetVoxel(nx, ny, nz, VoxelType.Stone);
						}
					}
				}
			}
		}
	}

	private static void GenerateBush(VoxelChunk chunk, int x, int z, int surfaceHeight, Random random)
	{
		// Bush parameters
		int radius = random.Next(2, 5);
		int height = random.Next(2, 4);

		// Generate a small bush (just leaves, no trunk)
		for (int y = 0; y < height; y++)
		{
			// Calculate radius at this height (fuller at bottom, thinner at top)
			float heightFactor = 1.0f - (y / (float)height) * 0.5f; // Less reduction with height
			int currentRadius = (int)Math.Ceiling(radius * heightFactor);

			for (int dx = -currentRadius; dx <= currentRadius; dx++)
			{
				for (int dz = -currentRadius; dz <= currentRadius; dz++)
				{
					// Create a rounded shape
					float distance = (float)Math.Sqrt(dx * dx + dz * dz);

					// Add some noise to the edge
					float edgeNoise = (float)random.NextDouble() * 0.7f;
					float effectiveRadius = currentRadius + edgeNoise;

					if (distance <= effectiveRadius)
					{
						int nx = x + dx;
						int nz = z + dz;
						int ny = surfaceHeight + y + 1; // Start one block above surface

						// Check chunk boundaries
						if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size && ny < chunk.Height)
						{
							// Add some randomness to make bushes less uniform
							if (distance > effectiveRadius - 0.8f && random.NextDouble() < 0.4f)
							{
								// Skip some edge blocks randomly
								continue;
							}

							chunk.SetVoxel(nx, ny, nz, VoxelType.Leaves);
						}
					}
				}
			}
		}
	}

	private static void GenerateRockSpire(VoxelChunk chunk, int x, int z, int surfaceHeight, Random random)
	{
		// Rock spire parameters
		int baseRadius = random.Next(3, 5);
		int height = random.Next(12, 20);

		// Generate a tall, thin rock spire
		for (int y = 0; y < height; y++)
		{
			// Spires get much thinner as they go up
			float heightFactor = 1.0f - (y / (float)height) * 0.8f;
			int currentRadius = (int)Math.Ceiling(baseRadius * heightFactor);

			// Ensure we have at least a 1x1 column at the top
			currentRadius = Math.Max(1, currentRadius);

			for (int dx = -currentRadius; dx <= currentRadius; dx++)
			{
				for (int dz = -currentRadius; dz <= currentRadius; dz++)
				{
					// Create a rounded shape
					float distance = (float)Math.Sqrt(dx * dx + dz * dz);

					// Add some noise to the edge
					float edgeNoise = (float)random.NextDouble() * 0.3f;
					float effectiveRadius = currentRadius - edgeNoise;

					if (distance <= effectiveRadius)
					{
						int nx = x + dx;
						int nz = z + dz;
						int ny = surfaceHeight + y + 1; // Start one block above surface

						// Check chunk boundaries
						if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size && ny < chunk.Height)
						{
							// Add some randomness to make spires less uniform
							if (distance > effectiveRadius - 0.5f && random.NextDouble() < 0.3f && y > height / 2)
							{
								// Skip some edge blocks randomly, more at the top
								continue;
							}

							chunk.SetVoxel(nx, ny, nz, VoxelType.Stone);
						}
					}
				}
			}
		}
	}

	private static void GenerateBoulder(VoxelChunk chunk, int x, int z, int surfaceHeight, Random random)
	{
		// Boulder parameters
		int radius = random.Next(3, 6);

		// Generate a roughly spherical boulder
		for (int dy = -radius; dy <= radius; dy++)
		{
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dz = -radius; dz <= radius; dz++)
				{
					// Create a spherical shape
					float distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

					// Add some noise to the edge
					float edgeNoise = (float)random.NextDouble() * 0.5f;
					float effectiveRadius = radius - edgeNoise;

					if (distance <= effectiveRadius)
					{
						int nx = x + dx;
						int nz = z + dz;
						int ny = surfaceHeight + dy + radius / 2; // Position boulder half-embedded in ground

						// Check chunk boundaries and ensure we're not placing blocks underground
						if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size &&
							ny >= 0 && ny < chunk.Height && ny > surfaceHeight - radius / 2)
						{
							// Add some randomness to make boulders less uniform
							if (distance > effectiveRadius - 0.8f && random.NextDouble() < 0.3f)
							{
								// Skip some edge blocks randomly
								continue;
							}

							chunk.SetVoxel(nx, ny, nz, VoxelType.Stone);
						}
					}
				}
			}
		}
	}

	private static void GenerateIceFormation(VoxelChunk chunk, int x, int z, int surfaceHeight, Random random)
	{
		// Ice formation parameters
		int baseRadius = random.Next(2, 4);
		int height = random.Next(6, 12);

		// Generate a crystalline ice formation
		for (int y = 0; y < height; y++)
		{
			// Ice formations get thinner as they go up
			float heightFactor = 1.0f - (y / (float)height) * 0.7f;
			int currentRadius = (int)Math.Ceiling(baseRadius * heightFactor);

			for (int dx = -currentRadius; dx <= currentRadius; dx++)
			{
				for (int dz = -currentRadius; dz <= currentRadius; dz++)
				{
					// Create a more angular, crystalline shape
					// Use Manhattan distance for a more angular look
					float distance = Math.Abs(dx) + Math.Abs(dz);
					float maxDistance = currentRadius * 2;

					if (distance <= maxDistance)
					{
						// Add some randomness for crystal-like protrusions
						if (random.NextDouble() < 0.7f || distance <= maxDistance * 0.5f)
						{
							int nx = x + dx;
							int nz = z + dz;
							int ny = surfaceHeight + y + 1; // Start one block above surface

							// Check chunk boundaries
							if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size && ny < chunk.Height)
							{
								chunk.SetVoxel(nx, ny, nz, VoxelType.IceBlock);
							}
						}
					}
				}
			}
		}
	}

	private static void GenerateSnowTree(VoxelChunk chunk, int x, int z, int surfaceHeight, Random random)
	{
		// Snow tree parameters - similar to regular trees but with snow-covered leaves
		int trunkHeight = random.Next(10, 16); // Slightly shorter than regular trees
		int leafRadius = random.Next(3, 6);    // Slightly smaller than regular trees
		int leafHeight = random.Next(6, 10);   // Slightly smaller than regular trees

		// Generate trunk
		for (int y = 1; y <= trunkHeight; y++)
		{
			if (surfaceHeight + y < chunk.Height)
			{
				chunk.SetVoxel(x, surfaceHeight + y, z, VoxelType.Wood);
			}
		}

		// Generate leaf canopy with snow on top
		int leafStartHeight = surfaceHeight + trunkHeight - leafHeight / 2;

		for (int y = 0; y < leafHeight; y++)
		{
			// Calculate the radius at this height (conical shape for snow trees)
			// Wider at bottom, narrower at top
			float heightFactor = 1.0f - (y / (float)leafHeight);
			int currentRadius = (int)Math.Ceiling(leafRadius * heightFactor);

			for (int dx = -currentRadius; dx <= currentRadius; dx++)
			{
				for (int dz = -currentRadius; dz <= currentRadius; dz++)
				{
					// Create a rounded shape
					float distance = (float)Math.Sqrt(dx * dx + dz * dz);

					// Add some noise to the edge
					float edgeNoise = (float)random.NextDouble() * 0.5f;
					float effectiveRadius = currentRadius + edgeNoise;

					if (distance <= effectiveRadius)
					{
						int nx = x + dx;
						int nz = z + dz;
						int ny = leafStartHeight + y;

						// Check chunk boundaries
						if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size && ny < chunk.Height)
						{
							// Don't overwrite the trunk
							if (chunk.GetVoxel(nx, ny, nz) != VoxelType.Wood)
							{
								// Use snow-covered leaves for the top layer, regular leaves for lower layers
								VoxelType leafType = (y == leafHeight - 1 || (y >= leafHeight - 2 && random.NextDouble() < 0.7f))
									? VoxelType.SnowLeaves
									: VoxelType.Leaves;

								// Add some randomness to make leaves less uniform
								if (distance > effectiveRadius - 0.8f && random.NextDouble() < 0.4f)
								{
									// Skip some edge leaves randomly
									continue;
								}

								chunk.SetVoxel(nx, ny, nz, leafType);
							}
						}
					}
				}
			}
		}
	}

	private static void GeneratePalmTree(VoxelChunk chunk, int x, int z, int surfaceHeight, Random random)
	{
		// Palm tree parameters
		int trunkHeight = random.Next(12, 18); // Taller than regular trees
		int frondLength = random.Next(6, 10);  // Length of palm fronds
		int frondCount = random.Next(5, 8);    // Number of fronds

		// Trunk bend parameters
		int bendDirection = random.Next(0, 4); // 0=+x, 1=-x, 2=+z, 3=-z
		float bendAmount = 0.2f + (float)random.NextDouble() * 0.3f; // 0.2 to 0.5

		// Generate trunk with bend
		float xOffset = 0;
		float zOffset = 0;

		for (int y = 1; y <= trunkHeight; y++)
		{
			// Calculate bend offset
			float bendFactor = (float)y / trunkHeight;
			float currentBend = bendAmount * bendFactor * bendFactor; // Quadratic bend (more at top)

			switch (bendDirection)
			{
				case 0: xOffset = currentBend * y; break;
				case 1: xOffset = -currentBend * y; break;
				case 2: zOffset = currentBend * y; break;
				case 3: zOffset = -currentBend * y; break;
			}

			int nx = x + (int)xOffset;
			int nz = z + (int)zOffset;

			// Check chunk boundaries
			if (nx >= 0 && nx < chunk.Size && nz >= 0 && nz < chunk.Size && surfaceHeight + y < chunk.Height)
			{
				chunk.SetVoxel(nx, surfaceHeight + y, nz, VoxelType.Wood);
			}
		}

		// Calculate top of trunk position
		int topX = x + (int)(xOffset);
		int topZ = z + (int)(zOffset);
		int topY = surfaceHeight + trunkHeight;

		// Generate palm fronds (leaves)
		for (int i = 0; i < frondCount; i++)
		{
			// Calculate frond direction
			float angle = (float)i / frondCount * 2 * Mathf.Pi;
			float dirX = Mathf.Cos(angle);
			float dirZ = Mathf.Sin(angle);

			// Generate frond
			for (int j = 1; j <= frondLength; j++)
			{
				// Calculate position along frond
				int frondX = topX + (int)(dirX * j);
				int frondZ = topZ + (int)(dirZ * j);

				// Calculate height - fronds curve downward
				float heightOffset = -0.5f * j * j / frondLength + j * 0.5f;
				int frondY = topY + (int)heightOffset;

				// Check chunk boundaries
				if (frondX >= 0 && frondX < chunk.Size && frondZ >= 0 && frondZ < chunk.Size &&
					frondY >= 0 && frondY < chunk.Height)
				{
					// Add leaves
					chunk.SetVoxel(frondX, frondY, frondZ, VoxelType.Leaves);

					// Add some width to the frond
					if (j > 1 && j < frondLength - 1)
					{
						// Add leaves to sides of the frond
						int sideX1 = frondX + (int)(dirZ);
						int sideZ1 = frondZ - (int)(dirX);
						int sideX2 = frondX - (int)(dirZ);
						int sideZ2 = frondZ + (int)(dirX);

						// Check chunk boundaries for side leaves
						if (sideX1 >= 0 && sideX1 < chunk.Size && sideZ1 >= 0 && sideZ1 < chunk.Size &&
							frondY >= 0 && frondY < chunk.Height && random.NextDouble() < 0.7f)
						{
							chunk.SetVoxel(sideX1, frondY, sideZ1, VoxelType.Leaves);
						}

						if (sideX2 >= 0 && sideX2 < chunk.Size && sideZ2 >= 0 && sideZ2 < chunk.Size &&
							frondY >= 0 && frondY < chunk.Height && random.NextDouble() < 0.7f)
						{
							chunk.SetVoxel(sideX2, frondY, sideZ2, VoxelType.Leaves);
						}
					}
				}
			}
		}

		// Add coconuts at the top
		int coconutCount = random.Next(0, 3);
		for (int i = 0; i < coconutCount; i++)
		{
			int coconutX = topX + random.Next(-1, 2);
			int coconutZ = topZ + random.Next(-1, 2);
			int coconutY = topY - random.Next(0, 2);

			// Check chunk boundaries
			if (coconutX >= 0 && coconutX < chunk.Size && coconutZ >= 0 && coconutZ < chunk.Size &&
				coconutY >= 0 && coconutY < chunk.Height)
			{
				// Use wood voxel type for coconuts
				chunk.SetVoxel(coconutX, coconutY, coconutZ, VoxelType.Wood);
			}
		}
	}
}
}
