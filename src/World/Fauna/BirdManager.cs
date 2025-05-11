using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;
using CubeGen.World.Generation;

namespace CubeGen.World.Fauna
{
    /// <summary>
    /// Manages bird spawning and lifecycle in the world
    /// </summary>
    public partial class BirdManager : Node3D
    {
        [Export] public float UpdateInterval { get; set; } = 5.0f;
        [Export] public PackedScene BirdScene { get; set; }

        // Bird collection
        private List<Bird> _activeBirds = new List<Bird>();

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

            // Perform periodic updates
            if (_updateTimer >= UpdateInterval)
            {
                _updateTimer = 0.0f;

                // Update bird population (only handles despawning now)
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

        // Position finding methods have been moved to FaunaSpawner

        // No longer using gradual spawning with chunk-based system

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

                // Calculate despawn distance based on view distance
                float despawnDistance;

                // Get the view distance from the WorldGenerator
                if (_worldGenerator != null)
                {
                    // Convert view distance (in chunks) to world units
                    despawnDistance = _worldGenerator.ViewDistance * _worldGenerator.ChunkSize * _worldGenerator.VoxelScale;

                    // Add a small buffer to avoid birds popping in and out at the edge of view
                    despawnDistance *= 1.1f;
                }
                else
                {
                    // Fallback if WorldGenerator is not available
                    despawnDistance = 200.0f;
                }

                // Only despawn birds if they're either:
                // 1. Extremely far away (regardless of state)
                // 2. Perched and beyond the normal despawn distance
                bool shouldDespawn = false;

                if (distanceToPlayer > despawnDistance)
                {
                    // Birds that are extremely far away should always despawn
                    shouldDespawn = true;
                }
                else if (distanceToPlayer > despawnDistance && bird.GetCurrentState() == FaunaState.Perched)
                {
                    // Perched birds can despawn at the normal distance
                    shouldDespawn = true;
                }

                if (shouldDespawn)
                {
                    // Mark bird for despawning
                    birdsToRemove.Add(bird);

                    // Debug output
                    GD.Print($"Marked bird for despawn at distance {distanceToPlayer:F1} (despawn distance: {despawnDistance:F1}), state: {bird.GetCurrentState()}");
                }
            }

            // Now safely remove and despawn the birds
            foreach (Bird bird in birdsToRemove)
            {
                // Despawn bird
                DespawnBird(bird);
                _activeBirds.Remove(bird);
            }
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
        /// Spawn a new bird in the world - this is now only used for testing
        /// Birds are normally spawned by SpawnBirdAtPosition from FaunaSpawner
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

            // Add the bird to the scene tree first
            AddChild(bird);

            // Position the bird at a default position above the player
            if (_player != null)
            {
                Vector3 playerPos = _player.GlobalPosition;
                bird.GlobalPosition = new Vector3(playerPos.X, playerPos.Y + 50.0f, playerPos.Z);
            }

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

            // Add the bird to the scene tree first
            AddChild(bird);

            // Set the bird's position after adding to the scene tree
            bird.GlobalPosition = position;

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

        // Position finding methods have been moved to FaunaSpawner

        // Position finding and terrain methods have been moved to FaunaSpawner

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

                    // No longer need biome type for landing spots

                    // Get the highest voxel at this position
                    int highestVoxel = FindHighestVoxel(checkX, checkZ);

                    // Check if this is a suitable landing spot
                    if (IsSuitableLandingSpot(checkX, highestVoxel, checkZ))
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
        private bool IsSuitableLandingSpot(int x, int y, int z)
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
