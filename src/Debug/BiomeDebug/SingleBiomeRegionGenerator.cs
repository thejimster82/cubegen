using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;
using CubeGen.World.Generation;

namespace CubeGen.Debug.BiomeDebug
{
	/// <summary>
	/// A modified BiomeRegionGenerator that always returns a single biome type.
	/// Used for debugging and testing individual biomes.
	/// </summary>
	public class SingleBiomeRegionGenerator : BiomeRegionGenerator
	{
		private BiomeType _targetBiome;
		private static SingleBiomeRegionGenerator _instance;
		private bool _isProperlyInitialized = false;
		private int _seed;
		private FastNoiseLite _noise;

		// Constructor
		public SingleBiomeRegionGenerator(BiomeType targetBiome)
		{
			_targetBiome = targetBiome;
			GD.Print($"Created SingleBiomeRegionGenerator for biome: {targetBiome}");
		}

		// Set the singleton instance
		public static void SetInstance(SingleBiomeRegionGenerator instance)
		{
			_instance = instance;

			// Also set the BiomeRegionGenerator.Instance to this instance
			// This ensures the WorldGenerator uses our SingleBiomeRegionGenerator
			BiomeRegionGenerator.Instance = instance;

			GD.Print($"SingleBiomeRegionGenerator set as BiomeRegionGenerator.Instance for biome: {instance._targetBiome}");
		}

		// Override the singleton instance property
		public new static SingleBiomeRegionGenerator Instance
		{
			get
			{
				if (_instance == null)
				{
					GD.PrintErr("SingleBiomeRegionGenerator instance not set!");
					// Create a default instance with Plains biome
					_instance = new SingleBiomeRegionGenerator(BiomeType.Plains);
				}
				return _instance;
			}
		}

		// Initialize with a seed
		public override void Initialize(int seed)
		{
			_seed = seed;

			// Initialize noise for consistent behavior
			_noise = new FastNoiseLite();
			_noise.Seed = seed;
			_noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
			_noise.Frequency = 0.01f;

			_isProperlyInitialized = true;
			GD.Print($"Initialized SingleBiomeRegionGenerator with seed: {seed}, target biome: {_targetBiome}");
		}

		// Always return the target biome type
		public override BiomeType GetBiomeType(int worldX, int worldZ)
		{
			if (!_isProperlyInitialized)
			{
				GD.PrintErr("SingleBiomeRegionGenerator not properly initialized in GetBiomeType.");
				return BiomeType.Plains;
			}

			return _targetBiome;
		}

		// Return a consistent cell ID based on position
		public override int GetCellId(int worldX, int worldZ)
		{
			if (!_isProperlyInitialized)
			{
				GD.PrintErr("SingleBiomeRegionGenerator not properly initialized in GetCellId.");
				return 500;
			}

			// Use noise to create a consistent cell ID
			float cellValue = _noise.GetNoise2D(worldX * 0.001f, worldZ * 0.001f);
			return (int)((cellValue + 1.0f) * 1000.0f);
		}

		// Return the target biome for any cell
		public override BiomeType GetBiomeTypeForCell(int cellId)
		{
			if (!_isProperlyInitialized)
			{
				GD.PrintErr("SingleBiomeRegionGenerator not properly initialized in GetBiomeTypeForCell.");
				return BiomeType.Plains;
			}

			return _targetBiome;
		}

		// For debug purposes, always return false to avoid blend calculations
		public override bool IsChunkNearBiomeBoundary(int chunkX, int chunkZ, int chunkSize, float blendDistance)
		{
			return false;
		}
	}
}
