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


	private FastNoiseLite _terrainNoise;
	private FastNoiseLite _biomeNoise;
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
		// Initialize terrain noise with flatter settings
		_terrainNoise = new FastNoiseLite();
		_terrainNoise.Seed = Seed;
		_terrainNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_terrainNoise.FractalType = FastNoiseLite.FractalTypeEnum.Ridged;
		_terrainNoise.Frequency = 0.01f; // Doubled frequency for higher resolution (was 0.005f)
		_terrainNoise.FractalOctaves = 2; // Fewer octaves for less detail and flatter terrain

		// Initialize biome noise (different settings for variety)
		_biomeNoise = new FastNoiseLite();
		_biomeNoise.Seed = Seed + 1000; // Different seed for biome variation
		_biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_biomeNoise.Frequency = 0.006f; // Doubled frequency for higher resolution (was 0.003f)

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
		float biomeValue = _biomeNoise.GetNoise2D(worldX, worldZ);
		return GetBiomeTypeFromNoise(biomeValue);
	}

	// Get biome type for a world position - static method for use by other classes
	public static BiomeType GetBiomeType(int worldX, int worldZ)
	{
		if (_staticBiomeNoise == null)
		{
			// Use a default seed if not initialized
			InitializeStaticNoise(0);
		}

		float biomeValue = _staticBiomeNoise.GetNoise2D(worldX, worldZ);
		return GetBiomeTypeFromNoise(biomeValue);
	}

	// Helper method to convert noise value to biome type
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
		// Base terrain height from noise
		float heightNoise = _terrainNoise.GetNoise2D(worldX, worldZ);

		// Convert noise from [-1, 1] to [0, 1]
		heightNoise = (heightNoise + 1f) * 0.5f;

		// Apply biome-specific height modifications - much flatter for all biomes
		// Add a consistent base height to ensure terrain is at a predictable level
		float baseHeight = 0.3f; // Consistent base height for all terrain

		switch (biomeType)
		{
			case BiomeType.Desert:
				heightNoise = heightNoise * 0.1f + baseHeight; // Very flat, low
				break;
			case BiomeType.Plains:
				heightNoise = heightNoise * 0.15f + baseHeight; // Very flat
				break;
			case BiomeType.Forest:
				heightNoise = heightNoise * 0.2f + baseHeight; // Slightly more varied but still flat
				break;
			case BiomeType.Mountains:
				heightNoise = heightNoise * 0.3f + baseHeight; // Less mountainous, more like hills
				break;
			case BiomeType.Tundra:
				heightNoise = heightNoise * 0.15f + baseHeight; // Very flat
				break;
		}

		// Convert to actual height value
		int height = Mathf.FloorToInt(heightNoise * ChunkHeight);

		// Debug output for the first chunk to help understand terrain height
		if (worldX == 0 && worldZ == 0)
		{
			GD.Print($"Terrain height at origin: {height}");
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

		for (int x = 0; x < chunkSize; x++)
		{
			for (int z = 0; z < chunkSize; z++)
			{
				int worldX = chunkPos.X * chunkSize + x;
				int worldZ = chunkPos.Y * chunkSize + z;

				BiomeType biomeType = GetBiomeType(worldX, worldZ);

				// Only add trees in Forest biome with reduced probability (more spaced out)
				if (biomeType == BiomeType.Forest && random.NextDouble() < 0.008) // Reduced from 0.02 to 0.008
				{
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

					if (surfaceHeight >= 0 && chunk.GetVoxel(x, surfaceHeight, z) == VoxelType.Grass)
					{
						// Calculate max leaf radius for this tree (for boundary check)
						int maxLeafRadius = random.Next(4, 8);

						// Check if tree is too close to chunk boundary
						int safeDistance = maxLeafRadius + 2; // Add a small buffer

						// Only generate trees that are safely away from chunk boundaries
						if (x >= safeDistance && x < (chunkSize - safeDistance) &&
							z >= safeDistance && z < (chunkSize - safeDistance))
						{
							// Generate a detailed tree with trunk and leaves
							GenerateDetailedTree(chunk, x, z, surfaceHeight, random);
						}
					}
				}
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
}
}
