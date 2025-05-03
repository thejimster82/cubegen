using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
public partial class WorldGenerator : Node3D
{
	[Export] public int Seed { get; set; } = 0;
	[Export] public Vector2I WorldSize { get; set; } = new Vector2I(16, 16); // Size in chunks
	[Export] public int ChunkSize { get; set; } = 16; // Size of each chunk in voxels
	[Export] public int ChunkHeight { get; set; } = 128; // Maximum height of the world
	[Export] public float VoxelScale { get; set; } = 0.5f; // Scale of each voxel (0.5 = double resolution)
	public int ViewDistance { get; set; } = 5;

	// Public constant for chunk size to be used by other classes
	public const int CHUNK_SIZE = 16;

	private ChunkManager _chunkManager;

	public override void _Ready()
	{
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

		// Generate terrain for the chunk
		for (int x = 0; x < ChunkSize; x++)
		{
			for (int z = 0; z < ChunkSize; z++)
			{
				// Get world coordinates
				int worldX = chunkPos.X * ChunkSize + x;
				int worldZ = chunkPos.Y * ChunkSize + z;

				// Get biome type based on noise
				BiomeType biomeType = GetBiomeTypeForChunk(worldX, worldZ);

				// Generate terrain height based on noise
				int terrainHeight = GenerateTerrainHeight(worldX, worldZ, biomeType);

				// Fill voxels from bottom to terrain height
				for (int y = 0; y < terrainHeight && y < ChunkHeight; y++)
				{
					VoxelType voxelType = DetermineVoxelType(y, terrainHeight, biomeType);
					chunk.SetVoxel(x, y, z, voxelType);
				}
			}
		}

		// Add objects like trees based on biome
		AddBiomeObjects(chunk, chunkPos, ChunkSize);

		// First, add the chunk data to the chunk manager
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
		else if (biomeValue < -0.2f)
			return BiomeType.Plains;
		else if (biomeValue < 0.2f)
			return BiomeType.Forest;
		else if (biomeValue < 0.5f)
			return BiomeType.Mountains;
		else
			return BiomeType.Tundra;
	}

	private int GenerateTerrainHeight(int worldX, int worldZ, BiomeType biomeType)
	{
		// Create a temporary noise instance for biome-specific noise settings
		FastNoiseLite biomeNoise = new FastNoiseLite();
		biomeNoise.Seed = Seed;

		// Set biome-specific noise characteristics
		switch (biomeType)
		{
			case BiomeType.Desert:
				// Desert: Low frequency, low octaves for smooth dunes
				biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
				biomeNoise.Frequency = 0.008f;
				biomeNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
				biomeNoise.FractalOctaves = 1;
				biomeNoise.FractalLacunarity = 2.0f;
				biomeNoise.FractalGain = 0.5f;
				break;

			case BiomeType.Plains:
				// Plains: Medium-low frequency, low octaves for gentle rolling hills
				biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
				biomeNoise.Frequency = 0.01f;
				biomeNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
				biomeNoise.FractalOctaves = 2;
				biomeNoise.FractalLacunarity = 2.0f;
				biomeNoise.FractalGain = 0.4f;
				break;

			case BiomeType.Forest:
				// Forest: Medium frequency, medium octaves for varied terrain
				biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
				biomeNoise.Frequency = 0.012f;
				biomeNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
				biomeNoise.FractalOctaves = 3;
				biomeNoise.FractalLacunarity = 2.0f;
				biomeNoise.FractalGain = 0.5f;
				break;

			case BiomeType.Mountains:
				// Mountains: Medium-high frequency, ridged fractal for more dramatic terrain
				biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
				biomeNoise.Frequency = 0.015f;
				biomeNoise.FractalType = FastNoiseLite.FractalTypeEnum.Ridged;
				biomeNoise.FractalOctaves = 4;
				biomeNoise.FractalLacunarity = 2.2f;
				biomeNoise.FractalGain = 0.6f;
				break;

			case BiomeType.Tundra:
				// Tundra: Medium frequency, low gain for flatter terrain with occasional features
				biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
				biomeNoise.Frequency = 0.011f;
				biomeNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
				biomeNoise.FractalOctaves = 2;
				biomeNoise.FractalLacunarity = 1.8f;
				biomeNoise.FractalGain = 0.3f;
				break;
		}

		// Get noise value with biome-specific settings
		float heightNoise = biomeNoise.GetNoise2D(worldX, worldZ);

		// Convert noise from [-1, 1] to [0, 1]
		heightNoise = (heightNoise + 1f) * 0.5f;

		// Apply a consistent base height for all biomes
		float baseHeight = 0.3f;
		float noiseContribution = 0.15f; // How much the noise affects the final height

		// Combine base height with noise contribution
		heightNoise = baseHeight + (heightNoise * noiseContribution);

		// Convert to actual height value
		int height = Mathf.FloorToInt(heightNoise * ChunkHeight);

		// Debug output for the first chunk to help understand terrain height
		if (worldX == 0 && worldZ == 0)
		{
			GD.Print($"Terrain height at origin: {height}, biome: {biomeType}");
		}

		return height;
	}

	private VoxelType DetermineVoxelType(int y, int terrainHeight, BiomeType biomeType)
	{
		// Bedrock at bottom
		if (y == 0)
			return VoxelType.Bedrock;

		// Surface layer and layers just below
		if (y == terrainHeight - 1)
		{
			// Top layer depends on biome
			switch (biomeType)
			{
				case BiomeType.Desert:
					return VoxelType.Sand;
				case BiomeType.Tundra:
					return VoxelType.Snow;
				default:
					return VoxelType.Grass;
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
				default:
					return VoxelType.Dirt;
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
						case BiomeType.Forest:
							// Add trees in Forest biome
							if (random.NextDouble() < 0.01) // Adjusted to a more reasonable value
							{
								if (surfaceHeight >= 0)
								{
									// Check if we can place a tree here (no overlap with other features)
									// Trees need a larger radius (6) to prevent overlap
									if (CanPlaceFeature(featureMap, x, z, 6, chunkSize))
									{
										// More lenient check - allow trees on any solid surface in forest biome
										GenerateDetailedTree(chunk, x, z, surfaceHeight, random);

										// Mark the area as occupied
										MarkFeaturePosition(featureMap, x, z, 6, chunkSize);
									}
								}
							}
							break;

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

						case BiomeType.Plains:
							// Add small bushes in Plains biome
							if (random.NextDouble() < 0.01) // Adjusted to a more reasonable value
							{
								if (surfaceHeight >= 0)
								{
									// Check if we can place a bush here (no overlap with other features)
									// Bushes need a small radius (3) to prevent overlap
									if (CanPlaceFeature(featureMap, x, z, 3, chunkSize))
									{
										// More lenient check - allow bushes on any solid surface in plains biome
										GenerateBush(chunk, x, z, surfaceHeight, random);

										// Mark the area as occupied
										MarkFeaturePosition(featureMap, x, z, 3, chunkSize);
									}
								}
							}
							// Add occasional lone trees in Plains biome
							else if (random.NextDouble() < 0.004) // Adjusted to a more reasonable value
							{
								if (surfaceHeight >= 0)
								{
									// Check if we can place a tree here (no overlap with other features)
									// Trees need a larger radius (6) to prevent overlap
									if (CanPlaceFeature(featureMap, x, z, 6, chunkSize))
									{
										// More lenient check - allow trees on any solid surface in plains biome
										GenerateDetailedTree(chunk, x, z, surfaceHeight, random);

										// Mark the area as occupied
										MarkFeaturePosition(featureMap, x, z, 6, chunkSize);
									}
								}
							}
							break;

						case BiomeType.Mountains:
							// Add rock spires in Mountains biome
							if (random.NextDouble() < 0.007) // Adjusted to a more reasonable value
							{
								if (surfaceHeight >= 0)
								{
									// Check if we can place a rock spire here (no overlap with other features)
									// Rock spires need a medium radius (5) to prevent overlap
									if (CanPlaceFeature(featureMap, x, z, 5, chunkSize))
									{
										// More lenient check - allow rock spires on any solid surface in mountains biome
										GenerateRockSpire(chunk, x, z, surfaceHeight, random);

										// Mark the area as occupied
										MarkFeaturePosition(featureMap, x, z, 5, chunkSize);
									}
								}
							}
							// Add boulders in Mountains biome
							else if (random.NextDouble() < 0.01) // Adjusted to a more reasonable value
							{
								if (surfaceHeight >= 0)
								{
									// Check if we can place a boulder here (no overlap with other features)
									// Boulders need a medium radius (4) to prevent overlap
									if (CanPlaceFeature(featureMap, x, z, 4, chunkSize))
									{
										GenerateBoulder(chunk, x, z, surfaceHeight, random);

										// Mark the area as occupied
										MarkFeaturePosition(featureMap, x, z, 4, chunkSize);
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
		int trunkHeight = random.Next(12, 20); // Doubled height for higher resolution (was 6-10)
		int leafRadius = random.Next(4, 8);    // Doubled radius for higher resolution (was 2-4)
		int leafHeight = random.Next(8, 12);   // Doubled height for higher resolution (was 4-6)

		// Generate trunk with more detail
		// Make the trunk thicker at the base (2x2 for first few blocks)
		for (int y = 1; y <= Math.Min(3, trunkHeight); y++)
		{
			// Create a 2x2 trunk base if there's room in the chunk
			for (int dx = 0; dx <= 1; dx++)
			{
				for (int dz = 0; dz <= 1; dz++)
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

		// Continue with a 1x1 trunk for the rest of the height
		for (int y = 4; y <= trunkHeight; y++)
		{
			if (surfaceHeight + y < chunk.Height)
			{
				chunk.SetVoxel(x, surfaceHeight + y, z, VoxelType.Wood);
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
								if (distance > effectiveRadius - 0.8f && random.NextDouble() < 0.3f)
								{
									// Skip some edge leaves randomly
									continue;
								}

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
}
}
