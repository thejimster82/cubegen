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
        [Export] public int MaxBirds { get; set; } = 8; // Reduced from 20 to 8
        [Export] public float SpawnHeight { get; set; } = 40.0f;
        [Export] public float SpawnRadius { get; set; } = 150.0f; // Increased from 100.0f to 150.0f
        [Export] public float DespawnDistance { get; set; } = 200.0f; // Increased from 150.0f to 200.0f
        [Export] public float UpdateInterval { get; set; } = 1.0f;
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

        public override void _Ready()
        {
            // Initialize bird colors
            InitializeBirdColors();

            // Create update timer
            _updateTimer = 0.0f;

            // Get player reference
            _player = GetTree().GetFirstNodeInGroup("Player") as Node3D;

            // Get chunk manager reference
            _chunkManager = GetNode<ChunkManager>("/root/World/WorldGenerator/ChunkManager");

            // Initialize random with world seed
            _seed = GetNode<WorldGenerator>("/root/World/WorldGenerator").Seed;
            _random = new Random(_seed);

            // Initial bird spawning
            SpawnInitialBirds();
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
        /// Spawn initial birds in the world
        /// </summary>
        private void SpawnInitialBirds()
        {
            // Spawn just a couple of birds to start (to avoid sudden appearance)
            int initialBirdCount = Mathf.Min(2, MaxBirds / 6); // Reduced from 5 to 2

            // Only spawn initial birds if we have a player reference
            if (_player != null)
            {
                for (int i = 0; i < initialBirdCount; i++)
                {
                    SpawnBird();
                }

                GD.Print($"Spawned {initialBirdCount} initial birds");
            }
            else
            {
                GD.Print("No player reference, delaying initial bird spawning");
                initialBirdCount = 0;
            }

            // We'll use a different approach to avoid Timer issues
            // Instead of using a separate timer, we'll use the existing update timer
            // and a flag to indicate gradual spawning is in progress
            _gradualSpawningActive = true;
            _gradualSpawnTimer = 0.0f;

            GD.Print("Gradual bird spawning started");
        }

        // Flag to track if gradual spawning is active
        private bool _gradualSpawningActive = false;
        // Timer for gradual spawning
        private float _gradualSpawnTimer = 0.0f;
        // Interval between gradual spawns
        private const float GRADUAL_SPAWN_INTERVAL = 4.0f; // Increased from 2.0f to 4.0f

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

            // Spawn new birds if needed, but do it gradually
            // Only spawn one bird per update to avoid sudden appearance of many birds
            // Reduced probability to make spawning even more gradual
            if (_activeBirds.Count < MaxBirds && _random.NextDouble() < 0.2f)
            {
                SpawnBird();

                // Debug output
                GD.Print($"Spawned new bird. Active birds: {_activeBirds.Count}/{MaxBirds}");
            }
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
            if (_birdColors.ContainsKey(birdType) && _birdColors[birdType].Count > 0)
            {
                int colorIndex = _random.Next(0, _birdColors[birdType].Count);
                BirdColorPair colorPair = _birdColors[birdType][colorIndex];

                bird.PrimaryColor = colorPair.Primary;
                bird.SecondaryColor = colorPair.Secondary;
            }

            // Set other random properties
            bird.WingFlapSpeed = 4.0f + (float)_random.NextDouble() * 2.0f;
            bird.PerchDuration = 5.0f + (float)_random.NextDouble() * 10.0f;
        }

        /// <summary>
        /// Position a bird in the world
        /// </summary>
        private void PositionBird(Bird bird)
        {
            if (_player == null)
                return;

            // Get player position
            Vector3 playerPosition = _player.GlobalPosition;

            // Random angle
            float angle = (float)_random.NextDouble() * Mathf.Pi * 2;

            // Random distance from player (within spawn radius, but not too close)
            // Use a much larger minimum distance to prevent birds from spawning too close to the player
            float minDistance = SpawnRadius * 0.7f;  // Increased from 0.4f to 0.7f
            float maxDistance = SpawnRadius * 0.95f; // Increased from 0.8f to 0.95f
            float distance = minDistance + (float)_random.NextDouble() * (maxDistance - minDistance);

            // Calculate position
            float x = playerPosition.X + Mathf.Cos(angle) * distance;
            float z = playerPosition.Z + Mathf.Sin(angle) * distance;

            // Set height - make it more consistent but with some variation
            // Use the player's height as a reference point
            float baseHeight = playerPosition.Y + SpawnHeight;
            float heightVariation = 10.0f; // Less variation than before
            float y = baseHeight + (float)_random.NextDouble() * heightVariation - (heightVariation / 2);

            // Ensure minimum height above ground
            y = Mathf.Max(y, 30.0f);

            // Set position
            bird.GlobalPosition = new Vector3(x, y, z);

            // Debug output
            GD.Print($"Positioned bird at ({x:F1}, {y:F1}, {z:F1}), distance from player: {distance:F1}");
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
