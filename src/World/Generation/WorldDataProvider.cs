using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
    /// <summary>
    /// Central provider for all world data, independent of chunks.
    /// Acts as the source of truth for what exists at any world coordinate.
    /// </summary>
    public class WorldDataProvider
    {
        // Singleton instance
        private static WorldDataProvider _instance;
        public static WorldDataProvider Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new WorldDataProvider();
                }
                return _instance;
            }
        }

        // World generation parameters
        private int _seed;
        private int _chunkSize;
        private int _chunkHeight;
        private float _voxelScale = 1.0f;
        private float _waterLevel = 0.4f;

        // Noise generators
        private FastNoiseLite _terrainNoise;
        private FastNoiseLite _detailNoise;
        private FastNoiseLite _biomeBlendNoise;

        // Cache for feature positions to ensure consistent generation
        private ConcurrentDictionary<string, FeatureInfo> _featureCache = new ConcurrentDictionary<string, FeatureInfo>();

        // Initialize the provider with world parameters
        public void Initialize(int seed, int chunkSize, int chunkHeight, float voxelScale = 1.0f)
        {
            _seed = seed;
            _chunkSize = chunkSize;
            _chunkHeight = chunkHeight;
            _voxelScale = voxelScale;

            // Initialize noise generators
            InitializeNoiseGenerators();

            GD.Print($"WorldDataProvider initialized with seed {_seed}");
        }

        private void InitializeNoiseGenerators()
        {
            // Main terrain noise
            _terrainNoise = new FastNoiseLite();
            _terrainNoise.Seed = _seed;
            _terrainNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _terrainNoise.Frequency = 0.005f;

            // Detail noise for smaller terrain features
            _detailNoise = new FastNoiseLite();
            _detailNoise.Seed = _seed + 1;
            _detailNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _detailNoise.Frequency = 0.02f;

            // Biome blend noise
            _biomeBlendNoise = new FastNoiseLite();
            _biomeBlendNoise.Seed = _seed + 2;
            _biomeBlendNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _biomeBlendNoise.Frequency = 0.01f;
        }

        /// <summary>
        /// Get the voxel type at any world coordinate
        /// </summary>
        public VoxelType GetVoxelTypeAt(int worldX, int worldY, int worldZ)
        {
            // First check if this position is part of a feature (tree, etc.)
            VoxelType featureVoxel = GetFeatureVoxelAt(worldX, worldY, worldZ);
            if (featureVoxel != VoxelType.Air)
            {
                return featureVoxel;
            }

            // If not part of a feature, determine the terrain voxel
            return GetTerrainVoxelAt(worldX, worldY, worldZ);
        }

        /// <summary>
        /// Get the terrain voxel type at a world coordinate
        /// </summary>
        private VoxelType GetTerrainVoxelAt(int worldX, int worldY, int worldZ)
        {
            // Get the biome at this position
            BiomeType biomeType = GetBiomeType(worldX, worldZ);

            // Get terrain height at this position
            int terrainHeight = GetTerrainHeight(worldX, worldZ);

            // Calculate water level height in voxels
            int waterLevelHeight = Mathf.FloorToInt(_waterLevel * _chunkHeight);

            // Below terrain
            if (worldY < terrainHeight)
            {
                // Determine underground voxel type based on depth and biome
                return DetermineUndergroundVoxelType(worldX, worldY, worldZ, terrainHeight, biomeType);
            }
            // At terrain surface
            else if (worldY == terrainHeight)
            {
                // Determine surface voxel type based on biome
                return DetermineSurfaceVoxelType(worldX, worldY, worldZ, biomeType);
            }
            // Above terrain but below or at water level
            else if (worldY <= waterLevelHeight)
            {
                // Water voxel
                return VoxelType.Water;
            }
            // Above terrain and above water level
            else
            {
                // Air voxel
                return VoxelType.Air;
            }
        }

        /// <summary>
        /// Get the feature voxel type at a world coordinate (trees, structures, etc.)
        /// </summary>
        private VoxelType GetFeatureVoxelAt(int worldX, int worldY, int worldZ)
        {
            // Check if this position is part of a tree
            VoxelType treeVoxel = GetTreeVoxelAt(worldX, worldY, worldZ);
            if (treeVoxel != VoxelType.Air)
            {
                return treeVoxel;
            }

            // Check other features as needed...

            // No feature at this position
            return VoxelType.Air;
        }

        /// <summary>
        /// Get the tree voxel type at a world coordinate
        /// </summary>
        private VoxelType GetTreeVoxelAt(int worldX, int worldY, int worldZ)
        {
            // Check nearby positions that could be tree bases
            // Trees can extend several blocks in any direction, so we need to check a radius
            int checkRadius = 12; // Maximum radius a tree could extend from its base

            // Check all potential tree base positions in the radius
            for (int dx = -checkRadius; dx <= checkRadius; dx++)
            {
                for (int dz = -checkRadius; dz <= checkRadius; dz++)
                {
                    int baseX = worldX + dx;
                    int baseZ = worldZ + dz;

                    // Get the terrain height at this potential base position
                    int baseHeight = GetTerrainHeight(baseX, baseZ);

                    // Check if a tree should be placed at this base position
                    if (ShouldPlaceTreeAt(baseX, baseZ))
                    {
                        // Get the tree type for this position
                        TreeType treeType = GetTreeTypeAt(baseX, baseZ);

                        // Check if the current position is part of this tree
                        VoxelType treeVoxel = GetTreeVoxelTypeForPosition(
                            worldX, worldY, worldZ, baseX, baseZ, baseHeight, treeType);

                        if (treeVoxel != VoxelType.Air)
                        {
                            return treeVoxel;
                        }
                    }
                }
            }

            return VoxelType.Air;
        }

        /// <summary>
        /// Determine if a tree should be placed at the given world coordinates
        /// </summary>
        private bool ShouldPlaceTreeAt(int worldX, int worldZ)
        {
            // Create a unique key for this position
            string posKey = $"tree_{worldX}_{worldZ}";

            // Check if we've already determined if a tree should be here
            if (_featureCache.TryGetValue(posKey, out FeatureInfo featureInfo))
            {
                return featureInfo.ShouldGenerate;
            }

            // Get the biome at this position
            BiomeType biomeType = GetBiomeType(worldX, worldZ);

            // Create a deterministic random generator for this position
            Random random = new Random(_seed + worldX * 10000 + worldZ);

            // Base probability depends on biome
            float treeProbability = GetTreeProbabilityForBiome(biomeType);

            // Add some noise to create natural clusters
            float noiseValue = _detailNoise.GetNoise2D(worldX * 0.05f, worldZ * 0.05f);
            noiseValue = (noiseValue + 1.0f) * 0.5f; // Convert to [0,1] range
            treeProbability *= noiseValue * 2.0f; // Amplify effect

            // Determine if a tree should be placed here
            bool shouldPlaceTree = random.NextDouble() < treeProbability;

            // Check minimum distance to other trees
            if (shouldPlaceTree)
            {
                // Minimum distance between trees depends on biome
                int minDistance = GetMinTreeDistanceForBiome(biomeType);

                // Check nearby positions for existing trees
                for (int dx = -minDistance; dx <= minDistance; dx++)
                {
                    for (int dz = -minDistance; dz <= minDistance; dz++)
                    {
                        // Skip the current position
                        if (dx == 0 && dz == 0) continue;

                        int nearbyX = worldX + dx;
                        int nearbyZ = worldZ + dz;
                        string nearbyKey = $"tree_{nearbyX}_{nearbyZ}";

                        // If there's already a tree nearby, don't place one here
                        if (_featureCache.TryGetValue(nearbyKey, out FeatureInfo nearbyInfo) && nearbyInfo.ShouldGenerate)
                        {
                            shouldPlaceTree = false;
                            break;
                        }
                    }
                    if (!shouldPlaceTree) break;
                }
            }

            // Cache the result
            _featureCache[posKey] = new FeatureInfo
            {
                ShouldGenerate = shouldPlaceTree,
                FeatureType = FeatureType.Tree,
                BiomeType = biomeType,
                Random = random.Next()
            };

            return shouldPlaceTree;
        }

        /// <summary>
        /// Get the tree type for a position
        /// </summary>
        private TreeType GetTreeTypeAt(int worldX, int worldZ)
        {
            // Get the cached feature info
            string posKey = $"tree_{worldX}_{worldZ}";
            if (_featureCache.TryGetValue(posKey, out FeatureInfo featureInfo))
            {
                // Create a deterministic random generator
                Random random = new Random(featureInfo.Random);

                // Determine tree type based on biome
                switch (featureInfo.BiomeType)
                {
                    case BiomeType.ForestLands:
                        return random.NextDouble() < 0.7f ? TreeType.Oak : TreeType.Pine;
                    case BiomeType.Tundra:
                        return TreeType.SnowCovered;
                    case BiomeType.Islands:
                        return TreeType.Palm;
                    case BiomeType.Desert:
                        return TreeType.Cactus;
                    default:
                        return TreeType.Oak;
                }
            }

            return TreeType.Oak; // Default
        }

        /// <summary>
        /// Get the voxel type for a position that might be part of a tree
        /// </summary>
        private VoxelType GetTreeVoxelTypeForPosition(
            int worldX, int worldY, int worldZ,
            int baseX, int baseZ, int baseHeight,
            TreeType treeType)
        {
            // Calculate relative position from the tree base
            int relX = worldX - baseX;
            int relY = worldY - baseHeight;
            int relZ = worldZ - baseZ;

            // Create a deterministic random generator for this tree
            Random random = new Random(_seed + baseX * 10000 + baseZ);

            // Generate tree parameters based on tree type
            switch (treeType)
            {
                case TreeType.Oak:
                    return GetOakTreeVoxel(relX, relY, relZ, random);
                case TreeType.Pine:
                    return GetPineTreeVoxel(relX, relY, relZ, random);
                case TreeType.SnowCovered:
                    return GetSnowTreeVoxel(relX, relY, relZ, random);
                case TreeType.Palm:
                    return GetPalmTreeVoxel(relX, relY, relZ, random);
                case TreeType.Cactus:
                    return GetCactusVoxel(relX, relY, relZ, random);
                default:
                    return VoxelType.Air;
            }
        }

        // Tree voxel generation methods for different tree types
        private VoxelType GetOakTreeVoxel(int relX, int relY, int relZ, Random random)
        {
            // Tree parameters
            int trunkHeight = random.Next(12, 24);
            int trunkThickness = random.Next(1, 3);
            int leafRadius = random.Next(6, 9);
            int leafHeight = random.Next(10, 11);

            // Trunk
            if (relY > 0 && relY <= trunkHeight)
            {
                // Base of trunk is thicker
                if (relY <= 4)
                {
                    if (Math.Abs(relX) <= 1 && Math.Abs(relZ) <= 1)
                    {
                        return VoxelType.Wood;
                    }
                }
                // Upper trunk
                else if (Math.Abs(relX) <= trunkThickness / 2 && Math.Abs(relZ) <= trunkThickness / 2)
                {
                    return VoxelType.Wood;
                }
            }

            // Leaves
            int leafStartHeight = trunkHeight - leafHeight / 2;
            if (relY >= leafStartHeight && relY < leafStartHeight + leafHeight)
            {
                // Calculate distance from center of leaf sphere
                int leafCenterY = leafStartHeight + leafHeight / 2;
                double distance = Math.Sqrt(
                    relX * relX +
                    (relY - leafCenterY) * (relY - leafCenterY) * 1.5 + // Stretch vertically
                    relZ * relZ);

                // Effective radius varies with height (wider in middle)
                float heightFactor = 1.0f - Math.Abs(relY - leafCenterY) / (float)(leafHeight / 2);
                float effectiveRadius = leafRadius * (0.8f + heightFactor * 0.2f);

                if (distance <= effectiveRadius)
                {
                    // Add some randomness to make leaves less uniform
                    if (distance > effectiveRadius - 0.8f && random.NextDouble() < 0.3f)
                    {
                        return VoxelType.Air; // Skip some edge leaves randomly
                    }

                    return VoxelType.Leaves;
                }
            }

            return VoxelType.Air;
        }

        private VoxelType GetPineTreeVoxel(int relX, int relY, int relZ, Random random)
        {
            // Tree parameters
            int trunkHeight = random.Next(14, 26);
            int leafHeight = random.Next(16, 22);

            // Trunk
            if (relY > 0 && relY <= trunkHeight && Math.Abs(relX) <= 0 && Math.Abs(relZ) <= 0)
            {
                return VoxelType.Wood;
            }

            // Conical leaves
            if (relY > trunkHeight / 3 && relY <= trunkHeight + 2)
            {
                // Calculate the radius at this height
                // Radius is larger at the bottom, smaller at the top
                float heightRatio = 1.0f - ((float)(relY - trunkHeight / 3) / (float)(leafHeight));
                int maxRadius = Mathf.FloorToInt(heightRatio * 6.0f) + 1;

                // Calculate distance from trunk
                double distance = Math.Sqrt(relX * relX + relZ * relZ);

                if (distance <= maxRadius)
                {
                    // Add some randomness to make leaves less uniform
                    if (distance > maxRadius - 0.8f && random.NextDouble() < 0.4f)
                    {
                        return VoxelType.Air; // Skip some edge leaves randomly
                    }

                    return VoxelType.Leaves;
                }
            }

            return VoxelType.Air;
        }

        private VoxelType GetSnowTreeVoxel(int relX, int relY, int relZ, Random random)
        {
            // Tree parameters
            int trunkHeight = random.Next(10, 16);
            int leafRadius = random.Next(3, 6);
            int leafHeight = random.Next(6, 10);

            // Trunk
            if (relY > 0 && relY <= trunkHeight && Math.Abs(relX) <= 0 && Math.Abs(relZ) <= 0)
            {
                return VoxelType.Wood;
            }

            // Leaves
            int leafStartHeight = trunkHeight - leafHeight / 2;
            if (relY >= leafStartHeight && relY < leafStartHeight + leafHeight)
            {
                // Calculate distance from center of leaf sphere
                int leafCenterY = leafStartHeight + leafHeight / 2;
                double distance = Math.Sqrt(
                    relX * relX +
                    (relY - leafCenterY) * (relY - leafCenterY) * 1.5 + // Stretch vertically
                    relZ * relZ);

                // Effective radius varies with height (wider in middle)
                float heightFactor = 1.0f - Math.Abs(relY - leafCenterY) / (float)(leafHeight / 2);
                float effectiveRadius = leafRadius * (0.8f + heightFactor * 0.2f);

                if (distance <= effectiveRadius)
                {
                    // Add some randomness to make leaves less uniform
                    if (distance > effectiveRadius - 0.8f && random.NextDouble() < 0.4f)
                    {
                        return VoxelType.Air; // Skip some edge leaves randomly
                    }

                    // Use snow-covered leaves for the top layer, regular leaves for lower layers
                    VoxelType leafType = (relY >= leafStartHeight + leafHeight - 2 ||
                                         (relY >= leafStartHeight + leafHeight - 3 && random.NextDouble() < 0.7f))
                        ? VoxelType.SnowLeaves
                        : VoxelType.Leaves;

                    return leafType;
                }
            }

            return VoxelType.Air;
        }

        private VoxelType GetPalmTreeVoxel(int relX, int relY, int relZ, Random random)
        {
            // Tree parameters
            int trunkHeight = random.Next(12, 18);
            int frondLength = random.Next(6, 10);
            int frondCount = random.Next(5, 8);

            // Trunk bend parameters
            int bendDirection = random.Next(0, 4); // 0=+x, 1=-x, 2=+z, 3=-z
            float bendAmount = 0.2f + (float)random.NextDouble() * 0.3f; // 0.2 to 0.5

            // Calculate bend offset
            float bendFactor = (float)relY / trunkHeight;
            float currentBend = bendAmount * bendFactor * bendFactor; // Quadratic bend (more at top)

            int bendX = 0;
            int bendZ = 0;

            switch (bendDirection)
            {
                case 0: bendX = (int)(currentBend * relY); break;
                case 1: bendX = (int)(-currentBend * relY); break;
                case 2: bendZ = (int)(currentBend * relY); break;
                case 3: bendZ = (int)(-currentBend * relY); break;
            }

            // Adjust coordinates for bend
            int adjustedX = relX - bendX;
            int adjustedZ = relZ - bendZ;

            // Trunk
            if (relY > 0 && relY <= trunkHeight && Math.Abs(adjustedX) <= 0 && Math.Abs(adjustedZ) <= 0)
            {
                return VoxelType.Wood;
            }

            // Palm fronds at the top
            if (relY > trunkHeight - 1 && relY <= trunkHeight + 2)
            {
                // Generate radial fronds
                for (int i = 0; i < frondCount; i++)
                {
                    float angle = i * (2.0f * Mathf.Pi / frondCount);
                    float frondDirX = Mathf.Cos(angle);
                    float frondDirZ = Mathf.Sin(angle);

                    // Check if this position is part of this frond
                    for (int j = 1; j <= frondLength; j++)
                    {
                        int frondX = (int)(j * frondDirX);
                        int frondZ = (int)(j * frondDirZ);

                        // Calculate distance from frond center line
                        float distFromLine = Mathf.Abs(
                            (adjustedX - bendX) * frondDirZ - (adjustedZ - bendZ) * frondDirX);

                        // Frond width decreases with distance from trunk
                        float maxWidth = 1.5f * (1.0f - (float)j / frondLength);

                        if (Math.Abs(frondX - (adjustedX - bendX)) <= 1 &&
                            Math.Abs(frondZ - (adjustedZ - bendZ)) <= 1 &&
                            distFromLine <= maxWidth)
                        {
                            return VoxelType.Leaves;
                        }
                    }
                }
            }

            return VoxelType.Air;
        }

        private VoxelType GetCactusVoxel(int relX, int relY, int relZ, Random random)
        {
            // Cactus parameters
            int mainHeight = random.Next(8, 16);
            bool hasArm = random.NextDouble() < 0.7f;
            int armHeight = hasArm ? random.Next(mainHeight / 2, mainHeight - 2) : 0;
            int armLength = hasArm ? random.Next(3, 6) : 0;
            int armDirection = random.Next(0, 4); // 0=+x, 1=-x, 2=+z, 3=-z

            // Main trunk
            if (relY > 0 && relY <= mainHeight && Math.Abs(relX) <= 0 && Math.Abs(relZ) <= 0)
            {
                return VoxelType.Cactus;
            }

            // Arm
            if (hasArm && relY >= armHeight && relY <= armHeight + 1)
            {
                int armX = 0;
                int armZ = 0;

                switch (armDirection)
                {
                    case 0: armX = 1; break;
                    case 1: armX = -1; break;
                    case 2: armZ = 1; break;
                    case 3: armZ = -1; break;
                }

                // Check if this position is part of the arm
                for (int i = 1; i <= armLength; i++)
                {
                    if (relX == armX * i && relZ == armZ * i)
                    {
                        return VoxelType.Cactus;
                    }
                }

                // Vertical part at the end of the arm
                if (relX == armX * armLength && relZ == armZ * armLength &&
                    relY > armHeight && relY <= armHeight + random.Next(3, 6))
                {
                    return VoxelType.Cactus;
                }
            }

            return VoxelType.Air;
        }

        // Helper methods for terrain generation
        private int GetTerrainHeight(int worldX, int worldZ)
        {
            // Get the biome at this position
            BiomeType biomeType = GetBiomeType(worldX, worldZ);

            // Get biome blend weights if near a boundary
            Dictionary<BiomeType, float> blendWeights = GetBiomeBlendWeights(worldX, worldZ);

            // Base height depends on biome
            float baseHeight = GetBaseHeightForBiome(biomeType);

            // Apply biome blending if needed
            if (blendWeights != null && blendWeights.Count > 1)
            {
                // Calculate weighted average of base heights
                float totalWeight = 0f;
                float weightedHeight = 0f;

                foreach (var kvp in blendWeights)
                {
                    float weight = kvp.Value;
                    BiomeType blendBiome = kvp.Key;
                    float biomeBaseHeight = GetBaseHeightForBiome(blendBiome);

                    weightedHeight += biomeBaseHeight * weight;
                    totalWeight += weight;
                }

                if (totalWeight > 0)
                {
                    baseHeight = weightedHeight / totalWeight;
                }
            }

            // Sample terrain noise
            float terrainNoise = _terrainNoise.GetNoise2D(worldX, worldZ);

            // Apply biome-specific noise amplitude
            float noiseAmplitude = GetNoiseAmplitudeForBiome(biomeType);

            // Calculate final height
            float heightF = baseHeight * _chunkHeight + terrainNoise * noiseAmplitude;

            // Convert to integer height
            return Mathf.FloorToInt(heightF);
        }

        // Biome determination
        public BiomeType GetBiomeType(int worldX, int worldZ)
        {
            return BiomeRegionGenerator.Instance.GetBiomeType(worldX, worldZ);
        }

        // Get biome blend weights for a position
        private Dictionary<BiomeType, float> GetBiomeBlendWeights(int worldX, int worldZ)
        {
            // Check if this position is near a biome boundary
            float boundaryDistance = BiomeRegionGenerator.Instance.GetDistanceToBoundary(worldX, worldZ);
            float blendDistance = 10.0f;

            if (boundaryDistance <= blendDistance)
            {
                // Use the BiomeRegionGenerator's built-in blend weight calculation
                return BiomeRegionGenerator.Instance.CalculateBiomeBlendWeights(worldX, worldZ, blendDistance);
            }

            // If not near a boundary, just use the current biome with weight 1.0
            BiomeType mainBiome = GetBiomeType(worldX, worldZ);
            return new Dictionary<BiomeType, float> { { mainBiome, 1.0f } };
        }

        // Helper methods for biome-specific parameters
        private float GetBaseHeightForBiome(BiomeType biomeType)
        {
            switch (biomeType)
            {
                case BiomeType.ForestLands:
                    return 0.5f;
                case BiomeType.Desert:
                    return 0.45f;
                case BiomeType.Tundra:
                    return 0.55f;
                case BiomeType.Islands:
                    return 0.35f;
                default:
                    return 0.5f;
            }
        }

        private float GetNoiseAmplitudeForBiome(BiomeType biomeType)
        {
            switch (biomeType)
            {
                case BiomeType.ForestLands:
                    return 15.0f;
                case BiomeType.Desert:
                    return 8.0f;
                case BiomeType.Tundra:
                    return 12.0f;
                case BiomeType.Islands:
                    return 5.0f;
                default:
                    return 10.0f;
            }
        }

        private float GetTreeProbabilityForBiome(BiomeType biomeType)
        {
            switch (biomeType)
            {
                case BiomeType.ForestLands:
                    return 0.02f;
                case BiomeType.Desert:
                    return 0.005f;
                case BiomeType.Tundra:
                    return 0.01f;
                case BiomeType.Islands:
                    return 0.015f;
                default:
                    return 0.01f;
            }
        }

        private int GetMinTreeDistanceForBiome(BiomeType biomeType)
        {
            switch (biomeType)
            {
                case BiomeType.ForestLands:
                    return 6;
                case BiomeType.Desert:
                    return 12;
                case BiomeType.Tundra:
                    return 8;
                case BiomeType.Islands:
                    return 7;
                default:
                    return 8;
            }
        }

        private VoxelType DetermineSurfaceVoxelType(int worldX, int worldY, int worldZ, BiomeType biomeType)
        {
            switch (biomeType)
            {
                case BiomeType.ForestLands:
                    return VoxelType.Grass;
                case BiomeType.Desert:
                    return VoxelType.Sand;
                case BiomeType.Tundra:
                    return VoxelType.Snow;
                case BiomeType.Islands:
                    // Islands have sand near water, grass elsewhere
                    int waterLevelHeight = Mathf.FloorToInt(_waterLevel * _chunkHeight);
                    if (worldY <= waterLevelHeight + 2)
                    {
                        return VoxelType.Sand;
                    }
                    return VoxelType.Grass;
                default:
                    return VoxelType.Grass;
            }
        }

        private VoxelType DetermineUndergroundVoxelType(int worldX, int worldY, int worldZ, int terrainHeight, BiomeType biomeType)
        {
            // Bedrock at the bottom
            if (worldY < 3)
            {
                return VoxelType.Bedrock;
            }

            // Top layer depends on surface type
            if (worldY >= terrainHeight - 3)
            {
                switch (biomeType)
                {
                    case BiomeType.Desert:
                        return VoxelType.Sand;
                    case BiomeType.Tundra:
                        return VoxelType.Snow;
                    default:
                        return VoxelType.Dirt;
                }
            }

            // Stone for deeper layers
            return VoxelType.Stone;
        }
    }

    /// <summary>
    /// Information about a feature at a specific position
    /// </summary>
    public class FeatureInfo
    {
        public bool ShouldGenerate { get; set; }
        public FeatureType FeatureType { get; set; }
        public BiomeType BiomeType { get; set; }
        public int Random { get; set; } // For deterministic randomization
    }

    /// <summary>
    /// Types of features that can be generated
    /// </summary>
    public enum FeatureType
    {
        Tree,
        Rock,
        Bush,
        Flower,
        Grass
    }

    /// <summary>
    /// Types of trees that can be generated
    /// </summary>
    public enum TreeType
    {
        Oak,
        Pine,
        SnowCovered,
        Palm,
        Cactus
    }
}
