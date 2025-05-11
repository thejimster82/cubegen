using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CubeGen.World.Common;
using CubeGen.World.Generation;

namespace CubeGen.World.Fauna
{
    /// <summary>
    /// Manages bird spawning and lifecycle in the world
    /// </summary>
    public partial class BirdManager : Node3D
    {
        [Export] public int MaxBirds { get; set; } = 5; // Reduced from 8 to 5
        [Export] public float SpawnHeight { get; set; } = 70.0f; // Increased from 40.0f to 70.0f
        [Export] public float SpawnRadius { get; set; } = 150.0f;
        [Export] public float DespawnDistance { get; set; } = 200.0f;
        [Export] public float UpdateInterval { get; set; } = 5.0f;
        [Export] public PackedScene BirdScene { get; set; }

        // Bird collections
        private List<Bird> _activeBirds = new List<Bird>();
        private ConcurrentBag<Bird> _inactiveBirds = new ConcurrentBag<Bird>();

        // Bird types and colors
        private Dictionary<BirdType, List<BirdColorPair>> _birdColors = new Dictionary<BirdType, List<BirdColorPair>>();

        // Player reference
        private Node3D _player;

        // Chunk manager reference for terrain queries
        private ChunkManager _chunkManager;

        // Random number generator
        private Random _random;

        // Update timer
        private float _updateTimer = 0.0f;

        // Seed for consistent bird generation
        private int _seed;

        // Noise for bird distribution
        private FastNoiseLite _birdDistributionNoise;

        // World generator reference for biome information
        private WorldGenerator _worldGenerator;

        public override void _Ready()
        {
            // Initialize bird colors
            InitializeBirdColors();

            // Create update timer
            _updateTimer = 0.0f;

            // Get player reference
            _player = GetTree().GetFirstNodeInGroup("Player") as Node3D;

            // Get world generator reference
            _worldGenerator = GetNode<WorldGenerator>("/root/World/WorldGenerator");

            // Get chunk manager reference
            _chunkManager = _worldGenerator?.GetNode<ChunkManager>("ChunkManager");

            // Initialize random with world seed
            _seed = _worldGenerator?.Seed ?? 0;
            _random = new Random(_seed);

            // Initialize noise for bird distribution
            InitializeNoise();
        }

        public override void _Process(double delta)
        {
            // Update timer
            _updateTimer += (float)delta;

            // Handle gradual spawning if active
            if (_gradualSpawningActive)
            {
                _gradualSpawnTimer += (float)delta;

                // Check if it's time to spawn another bird
                if (_gradualSpawnTimer >= GRADUAL_SPAWN_INTERVAL)
                {
                    _gradualSpawnTimer = 0.0f;

                    // If we haven't reached the target number of birds, spawn another one
                    if (_activeBirds.Count < MaxBirds)
                    {
                        SpawnBird();
                        GD.Print($"Gradually spawned bird. Active birds: {_activeBirds.Count}/{MaxBirds}");
                    }
                    else
                    {
                        // If we've reached the target, stop gradual spawning
                        _gradualSpawningActive = false;
                        GD.Print("Gradual bird spawning complete");
                    }
                }
            }

            // Perform periodic updates
            if (_updateTimer >= UpdateInterval)
            {
                _updateTimer = 0.0f;

                // Update bird population
                UpdateBirdPopulation();
            }
        }

        /// <summary>
        /// Initialize noise for bird distribution
        /// </summary>
        private void InitializeNoise()
        {
            // Create noise for bird distribution
            _birdDistributionNoise = new FastNoiseLite();
            _birdDistributionNoise.Seed = _seed;
            _birdDistributionNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            _birdDistributionNoise.Frequency = 0.001f; // Very low frequency for large-scale patterns

            GD.Print($"Bird distribution noise initialized with seed {_seed}");
        }

        /// <summary>
        /// Initialize bird color variations
        /// </summary>
        private void InitializeBirdColors()
        {
            // Initialize dictionary for each bird type
            foreach (BirdType type in Enum.GetValues(typeof(BirdType)))
            {
                _birdColors[type] = new List<BirdColorPair>();
            }

            // Small birds
            _birdColors[BirdType.Small].Add(new BirdColorPair(
                new Color(0.6f, 0.6f, 0.6f), // Gray
                new Color(0.8f, 0.8f, 0.8f)  // Light gray
            ));
            _birdColors[BirdType.Small].Add(new BirdColorPair(
                new Color(0.6f, 0.3f, 0.1f), // Brown
                new Color(0.8f, 0.5f, 0.2f)  // Light brown
            ));
            _birdColors[BirdType.Small].Add(new BirdColorPair(
                new Color(0.2f, 0.4f, 0.8f), // Blue
                new Color(0.4f, 0.6f, 0.9f)  // Light blue
            ));

            // Medium birds
            _birdColors[BirdType.Medium].Add(new BirdColorPair(
                new Color(0.1f, 0.5f, 0.1f), // Green
                new Color(0.2f, 0.7f, 0.2f)  // Light green
            ));
            _birdColors[BirdType.Medium].Add(new BirdColorPair(
                new Color(0.8f, 0.2f, 0.2f), // Red
                new Color(0.9f, 0.4f, 0.4f)  // Light red
            ));

            // Large birds
            _birdColors[BirdType.Large].Add(new BirdColorPair(
                new Color(0.1f, 0.1f, 0.1f), // Black
                new Color(0.3f, 0.3f, 0.3f)  // Dark gray
            ));
            _birdColors[BirdType.Large].Add(new BirdColorPair(
                new Color(0.9f, 0.9f, 0.9f), // White
                new Color(1.0f, 1.0f, 0.8f)  // Off-white
            ));
        }

        /// <summary>
        /// Find a position in a specific biome at the target location
        /// </summary>
        private Vector3 FindPositionInBiome(BiomeType targetBiome, Vector3 targetPosition)
        {
            // Define a search area around the target position
            float searchRadius = 50.0f; // Small search radius around the target position

            // Try many positions to find one in the target biome
            const int MAX_ATTEMPTS = 50;

            for (int i = 0; i < MAX_ATTEMPTS; i++)
            {
                // Generate a position near the target position
                float offsetX = (float)(_random.NextDouble() * searchRadius * 2 - searchRadius);
                float offsetZ = (float)(_random.NextDouble() * searchRadius * 2 - searchRadius);

                float mapX = targetPosition.X + offsetX;
                float mapZ = targetPosition.Z + offsetZ;

                // Get the biome type at this position
                BiomeType biomeType = WorldGenerator.GetBiomeType((int)mapX, (int)mapZ);

                // If this is the target biome, use this position
                if (biomeType == targetBiome)
                {
                    // Get terrain height at this position
                    float terrainHeight = GetTerrainHeight(mapX, mapZ);

                    // Calculate bird height above terrain
                    float birdHeight = terrainHeight + SpawnHeight;

                    // Add variation based on noise
                    float heightVariation = 30.0f;
                    float heightNoise = _birdDistributionNoise.GetNoise2D(mapX * 1.5f, mapZ * 1.5f);
                    birdHeight += heightNoise * heightVariation;

                    // Adjust height based on biome (birds fly higher in forests)
                    if (biomeType == BiomeType.ForestLands)
                    {
                        birdHeight += 20.0f;
                    }

                    // Ensure minimum height
                    birdHeight = Mathf.Max(birdHeight, 60.0f);

                    // Create the position with the calculated height
                    Vector3 position = new Vector3(mapX, birdHeight, mapZ);

                    // Debug output
                    GD.Print($"Found suitable position in {targetBiome} biome at ({position.X:F1}, {position.Y:F1}, {position.Z:F1})");

                    return position;
                }
            }

            // If we couldn't find a position in the target biome, try any biome at this location
            float defaultHeight = GetTerrainHeight(targetPosition.X, targetPosition.Z);
            float defaultBirdHeight = defaultHeight + SpawnHeight + 20.0f; // Add extra height to be safe

            // Ensure minimum height
            defaultBirdHeight = Mathf.Max(defaultBirdHeight, 60.0f);

            // Return position at the target location with appropriate height
            Vector3 defaultPosition = new Vector3(targetPosition.X, defaultBirdHeight, targetPosition.Z);

            GD.Print($"Couldn't find {targetBiome} biome at target location, using default position at ({defaultPosition.X:F1}, {defaultPosition.Y:F1}, {defaultPosition.Z:F1})");

            return defaultPosition;
        }

        // Flag to track if gradual spawning is active
        private bool _gradualSpawningActive = false;
        // Timer for gradual spawning
        private float _gradualSpawnTimer = 0.0f;
        // Interval between gradual spawns
        private const float GRADUAL_SPAWN_INTERVAL = 8.0f; // Increased from 4.0f to 8.0f

        /// <summary>
        /// Update bird population based on player position
        /// </summary>
        private void UpdateBirdPopulation()
        {
            if (_player == null)
                return;

            Vector3 playerPosition = _player.GlobalPosition;

            // Store the current active birds in a temporary list to avoid modification issues
            List<Bird> currentBirds = new List<Bird>(_activeBirds);

            // Track birds to despawn
            List<Bird> birdsToRemove = new List<Bird>();

            // Check for birds that are too far away
            foreach (Bird bird in currentBirds)
            {
                // Skip birds that are already marked for removal
                if (birdsToRemove.Contains(bird))
                    continue;

                float distanceToPlayer = bird.GlobalPosition.DistanceTo(playerPosition);

                // Only despawn birds if they're either:
                // 1. Extremely far away (regardless of state)
                // 2. Perched and beyond the normal despawn distance
                bool shouldDespawn = false;

                if (distanceToPlayer > DespawnDistance * 1.5f)
                {
                    // Birds that are extremely far away should always despawn
                    shouldDespawn = true;
                }
                else if (distanceToPlayer > DespawnDistance && bird.GetCurrentState() == FaunaState.Perched)
                {
                    // Perched birds can despawn at the normal distance
                    shouldDespawn = true;
                }

                if (shouldDespawn)
                {
                    // Mark bird for despawning
                    birdsToRemove.Add(bird);

                    // Debug output
                    GD.Print($"Marked bird for despawn at distance {distanceToPlayer:F1}, state: {bird.GetCurrentState()}");
                }
            }

            // Now safely remove and despawn the birds
            foreach (Bird bird in birdsToRemove)
            {
                // Despawn bird
                DespawnBird(bird);
                _activeBirds.Remove(bird);
            }

            // Birds are now spawned by the FaunaSpawner based on chunks
            // No need to manually spawn birds here anymore
            GD.Print($"Active birds: {_activeBirds.Count}/{MaxBirds}");
        }

        /// <summary>
        /// Get a random preferred biome for birds
        /// </summary>
        private BiomeType GetRandomPreferredBiome()
        {
            // Define biome types to focus on with their weights
            BiomeType[] preferredBiomes = new BiomeType[]
            {
                BiomeType.ForestLands,   // Forest (highest priority)
                BiomeType.ForestLands,   // Added twice for higher weight
                BiomeType.Islands,       // Islands
                BiomeType.Islands,       // Added twice for higher weight
                BiomeType.Desert,        // Desert
                BiomeType.Tundra         // Tundra
            };

            // Pick a random biome from the weighted list
            int index = _random.Next(0, preferredBiomes.Length);
            return preferredBiomes[index];
        }

        /// <summary>
        /// Spawn a new bird in the world
        /// </summary>
        private Bird SpawnBird()
        {
            Bird bird;

            // Always create a new bird instead of reusing
            // This ensures each bird has a clean state
            if (BirdScene != null)
            {
                bird = BirdScene.Instantiate<Bird>();
            }
            else
            {
                // Create bird directly if no scene is provided
                bird = new Bird();
            }

            // Set bird properties
            ConfigureBird(bird);

            // Position the bird
            PositionBird(bird);

            // Add the bird to the scene tree
            AddChild(bird);

            // Ensure the bird is visible
            bird.Visible = true;

            // Add to active birds
            _activeBirds.Add(bird);

            GD.Print("Created and added new bird to scene tree");

            return bird;
        }

        /// <summary>
        /// Spawn a bird at a specific position with specific properties
        /// Used by the FaunaSpawner for chunk-based spawning
        /// </summary>
        public Bird SpawnBirdAtPosition(Vector3 position, BirdType birdType, BiomeType biomeType)
        {
            // Check if we've reached the maximum number of birds
            if (_activeBirds.Count >= MaxBirds)
            {
                GD.Print("Maximum number of birds reached, not spawning new bird");
                return null;
            }

            Bird bird;

            // Create a new bird
            if (BirdScene != null)
            {
                bird = BirdScene.Instantiate<Bird>();
            }
            else
            {
                bird = new Bird();
            }

            // Configure the bird with the specified type
            ConfigureBirdWithType(bird, birdType);

            // Set the bird's position
            bird.GlobalPosition = position;

            // Add the bird to the scene tree
            AddChild(bird);

            // Ensure the bird is visible
            bird.Visible = true;

            // Add to active birds
            _activeBirds.Add(bird);

            GD.Print($"Spawned {birdType} bird at position ({position.X:F1}, {position.Y:F1}, {position.Z:F1}) in {biomeType} biome");

            return bird;
        }

        /// <summary>
        /// Configure a bird with random properties
        /// </summary>
        private void ConfigureBird(Bird bird)
        {
            // Determine bird type (weighted towards smaller birds)
            BirdType birdType;
            float typeRoll = (float)_random.NextDouble();

            if (typeRoll < 0.7f)
                birdType = BirdType.Small;
            else if (typeRoll < 0.9f)
                birdType = BirdType.Medium;
            else
                birdType = BirdType.Large;

            // Configure the bird with the determined type
            ConfigureBirdWithType(bird, birdType);
        }

        /// <summary>
        /// Configure a bird with a specific type
        /// </summary>
        private void ConfigureBirdWithType(Bird bird, BirdType birdType)
        {
            // Set bird type
            bird.Type = birdType;

            // Set scale based on type
            switch (birdType)
            {
                case BirdType.Small:
                    bird.FaunaScale = 0.5f;
                    bird.FlyingSpeed = 3.0f + (float)_random.NextDouble() * 1.0f;
                    break;
                case BirdType.Medium:
                    bird.FaunaScale = 0.75f;
                    bird.FlyingSpeed = 2.5f + (float)_random.NextDouble() * 0.8f;
                    break;
                case BirdType.Large:
                    bird.FaunaScale = 1.0f;
                    bird.FlyingSpeed = 2.0f + (float)_random.NextDouble() * 0.6f;
                    break;
            }

            // Set random colors from available options for this bird type
            if (_birdColors.TryGetValue(birdType, out List<BirdColorPair> colorOptions) && colorOptions.Count > 0)
            {
                int colorIndex = _random.Next(0, colorOptions.Count);
                BirdColorPair colorPair = colorOptions[colorIndex];

                bird.PrimaryColor = colorPair.Primary;
                bird.SecondaryColor = colorPair.Secondary;
            }

            // Set other random properties
            bird.WingFlapSpeed = 4.0f + (float)_random.NextDouble() * 2.0f;
            bird.PerchDuration = 5.0f + (float)_random.NextDouble() * 10.0f;
        }

        /// <summary>
        /// Position a bird in the world using noise for distribution
        /// </summary>
        private void PositionBird(Bird bird)
        {
            if (_player == null)
                return;

            // Get player position as a reference point
            Vector3 playerPosition = _player.GlobalPosition;

            // Use noise to find a suitable position across the map
            Vector3 birdPosition = FindPositionUsingNoise(playerPosition);

            // Set the bird's position
            bird.GlobalPosition = birdPosition;

            // Debug output
            float distanceFromPlayer = birdPosition.DistanceTo(playerPosition);
            GD.Print($"Positioned bird at ({birdPosition.X:F1}, {birdPosition.Y:F1}, {birdPosition.Z:F1}), distance from player: {distanceFromPlayer:F1}");
        }

        /// <summary>
        /// Find a suitable position for a bird using noise and biome information
        /// </summary>
        private Vector3 FindPositionUsingNoise(Vector3 playerPosition)
        {
            if (_worldGenerator == null)
            {
                // Fallback if world generator is not available
                return FallbackPosition(playerPosition);
            }

            // Define a very large search area to cover the entire map
            // This is much larger than the player's view distance
            float mapSearchRadius = 1000.0f;

            // Try many positions to find one with good biome and noise value
            const int MAX_ATTEMPTS = 30;
            float bestScore = -1.0f;
            Vector3 bestPosition = Vector3.Zero;

            for (int i = 0; i < MAX_ATTEMPTS; i++)
            {
                // Generate a completely random position on the map
                // This is NOT centered on the player
                float mapX = (float)(_random.NextDouble() * mapSearchRadius * 2 - mapSearchRadius);
                float mapZ = (float)(_random.NextDouble() * mapSearchRadius * 2 - mapSearchRadius);

                // Get the biome type at this position
                BiomeType biomeType = WorldGenerator.GetBiomeType((int)mapX, (int)mapZ);

                // Calculate biome score - forests and islands have higher scores
                float biomeScore = GetBiomeScore(biomeType);

                // Sample noise at this position
                float noiseValue = _birdDistributionNoise.GetNoise2D(mapX, mapZ);

                // Convert noise from [-1,1] to [0,1] range
                noiseValue = (noiseValue + 1.0f) * 0.5f;

                // Calculate combined score (biome + noise)
                float combinedScore = biomeScore * 0.7f + noiseValue * 0.3f;

                // If this position has a better score, use it
                if (combinedScore > bestScore)
                {
                    // Get terrain height at this position
                    float terrainHeight = GetTerrainHeight(mapX, mapZ);

                    // Calculate bird height above terrain
                    float birdHeight = terrainHeight + SpawnHeight;

                    // Add variation based on noise and biome
                    float heightVariation = 30.0f;
                    float heightNoise = _birdDistributionNoise.GetNoise2D(mapX * 1.5f, mapZ * 1.5f);
                    birdHeight += heightNoise * heightVariation;

                    // Adjust height based on biome (birds fly higher in forests)
                    if (biomeType == BiomeType.ForestLands)
                    {
                        birdHeight += 20.0f;
                    }

                    // Ensure minimum height
                    birdHeight = Mathf.Max(birdHeight, 60.0f);

                    // Save this position
                    bestPosition = new Vector3(mapX, birdHeight, mapZ);
                    bestScore = combinedScore;
                }
            }

            // If we couldn't find a good position, use a fallback
            if (bestScore < 0.3f)
            {
                return FallbackPosition(playerPosition);
            }

            // Check if the position is too far from the player
            float distanceToPlayer = bestPosition.DistanceTo(playerPosition);
            if (distanceToPlayer > SpawnRadius * 5.0f)
            {
                // If too far, find a position in the same direction but closer
                Vector3 direction = (bestPosition - playerPosition).Normalized();
                float adjustedDistance = SpawnRadius * 3.0f;

                // Calculate adjusted position
                Vector3 adjustedPosition = playerPosition + direction * adjustedDistance;

                // Keep the same height
                adjustedPosition.Y = bestPosition.Y;

                return adjustedPosition;
            }

            return bestPosition;
        }

        /// <summary>
        /// Get a score for a biome type based on bird preference
        /// </summary>
        private float GetBiomeScore(BiomeType biomeType)
        {
            switch (biomeType)
            {
                case BiomeType.ForestLands:
                    return 1.0f; // Forests have the highest score
                case BiomeType.Islands:
                    return 0.9f; // Islands are also good
                case BiomeType.Desert:
                    return 0.4f; // Deserts have fewer birds
                case BiomeType.Tundra:
                    return 0.3f; // Tundra has even fewer birds
                default:
                    return 0.5f; // Default score for other biomes
            }
        }

        /// <summary>
        /// Get the terrain height at a specific position
        /// </summary>
        private float GetTerrainHeight(float x, float z)
        {
            if (_chunkManager == null)
            {
                // Fallback if chunk manager is not available
                return 30.0f;
            }

            // Convert to integer coordinates
            int worldX = (int)x;
            int worldZ = (int)z;

            // Start from a reasonable height and search downward
            int startY = 100;

            for (int y = startY; y >= 0; y--)
            {
                VoxelType voxelType = _chunkManager.GetVoxelType(worldX, y, worldZ);

                // If we find a non-air voxel, this is the highest point
                if (voxelType != VoxelType.Air && voxelType != VoxelType.Water)
                {
                    return y;
                }
            }

            // If we couldn't find terrain, return a default height
            return 30.0f;
        }

        /// <summary>
        /// Fallback position calculation if other methods fail
        /// </summary>
        private Vector3 FallbackPosition(Vector3 playerPosition)
        {
            // Generate a position in a large radius around the player
            float angle = (float)_random.NextDouble() * Mathf.Pi * 2;
            float distance = SpawnRadius * 2.0f;
            float x = playerPosition.X + Mathf.Cos(angle) * distance;
            float z = playerPosition.Z + Mathf.Sin(angle) * distance;

            // Calculate height with some noise variation
            float heightNoise = _birdDistributionNoise.GetNoise2D(x * 1.5f, z * 1.5f);
            float y = playerPosition.Y + SpawnHeight + heightNoise * 20.0f;

            // Ensure minimum height
            y = Mathf.Max(y, 60.0f);

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Despawn a bird and add it to the inactive pool
        /// </summary>
        private void DespawnBird(Bird bird)
        {
            // Make sure the bird is properly removed from the scene
            if (bird.IsInsideTree())
            {
                // Remove the bird from its parent
                bird.GetParent()?.RemoveChild(bird);

                // Debug output
                GD.Print("Bird removed from scene tree");
            }

            // Completely free the bird to avoid any issues with reuse
            // This is more reliable than trying to reuse birds
            bird.QueueFree();

            // We no longer add to inactive birds pool
            // This avoids issues with reusing birds that might have state problems
            // _inactiveBirds.Add(bird);

            GD.Print("Bird properly despawned and queued for deletion");
        }

        /// <summary>
        /// Find high points in the world suitable for birds to land
        /// </summary>
        public Vector3[] FindLandingSpots(Vector3 center, float radius, int count)
        {
            if (_chunkManager == null)
            {
                // If chunk manager is not available, return random high points
                return FindRandomHighPoints(center, radius, count);
            }

            List<Vector3> landingSpots = new List<Vector3>();

            // Search for high points in the world
            // Look for trees, cacti, or other tall structures

            // Check a larger area around the center position
            int searchSize = (int)radius;
            int centerX = (int)center.X;
            int centerZ = (int)center.Z;

            // Sample points in a grid pattern
            int sampleStep = 5; // Check every 5 blocks

            for (int dx = -searchSize; dx <= searchSize; dx += sampleStep)
            {
                for (int dz = -searchSize; dz <= searchSize; dz += sampleStep)
                {
                    int checkX = centerX + dx;
                    int checkZ = centerZ + dz;

                    // Calculate distance from center
                    float distanceSquared = dx * dx + dz * dz;
                    if (distanceSquared > radius * radius)
                        continue; // Skip points outside the radius

                    // Get the biome type for this position
                    BiomeType biomeType = WorldGenerator.GetBiomeType(checkX, checkZ);

                    // Get the highest voxel at this position
                    int highestVoxel = FindHighestVoxel(checkX, checkZ);

                    // Check if this is a suitable landing spot
                    if (IsSuitableLandingSpot(checkX, highestVoxel, checkZ, biomeType))
                    {
                        // Add to landing spots
                        landingSpots.Add(new Vector3(checkX, highestVoxel + 1, checkZ));

                        // If we have enough spots, return them
                        if (landingSpots.Count >= count)
                            break;
                    }
                }

                // If we have enough spots, return them
                if (landingSpots.Count >= count)
                    break;
            }

            // If we didn't find enough spots, add some random high points
            if (landingSpots.Count < count)
            {
                Vector3[] randomSpots = FindRandomHighPoints(center, radius, count - landingSpots.Count);
                landingSpots.AddRange(randomSpots);
            }

            return landingSpots.ToArray();
        }

        /// <summary>
        /// Find random high points in the world
        /// </summary>
        private Vector3[] FindRandomHighPoints(Vector3 center, float radius, int count)
        {
            Vector3[] spots = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                // Random position within radius
                float angle = (float)_random.NextDouble() * Mathf.Pi * 2;
                float distance = (float)_random.NextDouble() * radius;

                float x = center.X + Mathf.Cos(angle) * distance;
                float z = center.Z + Mathf.Sin(angle) * distance;

                // Set height to a high value (simulating a tree or cactus)
                float y = 30.0f + (float)_random.NextDouble() * 10.0f;

                spots[i] = new Vector3(x, y, z);
            }

            return spots;
        }

        /// <summary>
        /// Find the highest voxel at a given position
        /// </summary>
        private int FindHighestVoxel(int x, int z)
        {
            if (_chunkManager == null)
                return 0;

            // Start from a reasonable height and search downward
            int startY = 100;

            for (int y = startY; y >= 0; y--)
            {
                VoxelType voxelType = _chunkManager.GetVoxelType(x, y, z);

                // If we find a non-air voxel, this is the highest point
                if (voxelType != VoxelType.Air)
                {
                    return y;
                }
            }

            return 0;
        }

        /// <summary>
        /// Check if a position is suitable for birds to land
        /// </summary>
        private bool IsSuitableLandingSpot(int x, int y, int z, BiomeType biomeType)
        {
            if (_chunkManager == null)
                return false;

            // Get the voxel type at this position
            VoxelType voxelType = _chunkManager.GetVoxelType(x, y, z);

            // Check if this is a suitable landing spot based on voxel type
            switch (voxelType)
            {
                case VoxelType.Leaves:
                case VoxelType.SnowLeaves:
                    // Birds can land on tree leaves
                    return true;

                case VoxelType.Wood:
                    // Birds can land on tree trunks if they're high enough
                    // Check if this is the top of a tree
                    return y > 20 && _chunkManager.GetVoxelType(x, y + 1, z) == VoxelType.Air;

                case VoxelType.Cactus:
                    // Birds can land on cacti
                    return _chunkManager.GetVoxelType(x, y + 1, z) == VoxelType.Air;

                default:
                    // For other voxel types, only land if they're high enough and have air above
                    return y > 20 && _chunkManager.GetVoxelType(x, y + 1, z) == VoxelType.Air;
            }
        }
    }

    /// <summary>
    /// Struct to hold a pair of bird colors
    /// </summary>
    public struct BirdColorPair
    {
        public Color Primary;
        public Color Secondary;

        public BirdColorPair(Color primary, Color secondary)
        {
            Primary = primary;
            Secondary = secondary;
        }
    }
}
