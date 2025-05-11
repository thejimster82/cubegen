using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;
using CubeGen.World.Generation;

namespace CubeGen.World.Fauna
{
    /// <summary>
    /// Represents a bird that can fly in the sky and land on high places
    /// </summary>
    public partial class Bird : Fauna
    {
        // Bird-specific properties
        [Export] public float FlyingHeight { get; set; } = 20.0f;
        [Export] public float FlyingSpeed { get; set; } = 3.0f;
        [Export] public float WingFlapSpeed { get; set; } = 5.0f;
        [Export] public float PerchDuration { get; set; } = 10.0f;
        [Export] public float LandingDistance { get; set; } = 1.0f;
        [Export] public BirdType Type { get; set; } = BirdType.Small;
        [Export] public Color PrimaryColor { get; set; } = new Color(0.6f, 0.6f, 0.6f); // Gray
        [Export] public Color SecondaryColor { get; set; } = new Color(0.8f, 0.8f, 0.8f); // Light gray

        // Flight pattern properties
        private Vector3 _circleCenter;
        private float _circleRadius = 10.0f;
        private float _circleHeight;
        private float _circleAngle = 0.0f;
        private float _wingFlapAngle = 0.0f;

        // Bird model parts
        private Node3D _body;
        private Node3D _leftWing;
        private Node3D _rightWing;
        private Node3D _tail;
        private Node3D _head;

        // Landing target
        private Vector3 _landingPosition;
        private bool _hasLandingTarget = false;

        public override void _Ready()
        {
            base._Ready();

            // Set bird-specific properties
            FaunaName = "Bird";
            MovementSpeed = FlyingSpeed;

            // Initialize flight pattern
            _circleCenter = GlobalPosition;
            _circleHeight = GlobalPosition.Y;

            // Start in flying state
            ChangeState(FaunaState.Moving);
        }

        protected override void CreateModel()
        {
            // Create bird model based on type
            _model = new Node3D();
            _model.Name = "BirdModel";

            // Create body parts
            CreateBirdParts();

            // Add model to the scene
            AddChild(_model);

            // Scale the model
            _model.Scale = Vector3.One * FaunaScale;
        }

        private void CreateBirdParts()
        {
            // Create body
            _body = CreateVoxelCube(new Vector3(0, 0, 0), new Vector3(0.5f, 0.3f, 0.7f), PrimaryColor);
            _model.AddChild(_body);

            // Create head
            _head = CreateVoxelCube(new Vector3(0, 0.2f, 0.4f), new Vector3(0.3f, 0.3f, 0.3f), PrimaryColor);
            _model.AddChild(_head);

            // Create beak
            Node3D beak = CreateVoxelCube(new Vector3(0, 0.15f, 0.6f), new Vector3(0.1f, 0.1f, 0.2f), new Color(1.0f, 0.7f, 0.0f));
            _model.AddChild(beak);

            // Create wings
            _leftWing = CreateVoxelCube(new Vector3(0.4f, 0.1f, 0), new Vector3(0.4f, 0.1f, 0.5f), SecondaryColor);
            _rightWing = CreateVoxelCube(new Vector3(-0.4f, 0.1f, 0), new Vector3(0.4f, 0.1f, 0.5f), SecondaryColor);
            _model.AddChild(_leftWing);
            _model.AddChild(_rightWing);

            // Create tail
            _tail = CreateVoxelCube(new Vector3(0, 0.1f, -0.4f), new Vector3(0.3f, 0.1f, 0.3f), SecondaryColor);
            _model.AddChild(_tail);

            // Add eyes
            Node3D leftEye = CreateVoxelCube(new Vector3(0.1f, 0.25f, 0.5f), new Vector3(0.05f, 0.05f, 0.05f), Colors.Black);
            Node3D rightEye = CreateVoxelCube(new Vector3(-0.1f, 0.25f, 0.5f), new Vector3(0.05f, 0.05f, 0.05f), Colors.Black);
            _model.AddChild(leftEye);
            _model.AddChild(rightEye);
        }

        private Node3D CreateVoxelCube(Vector3 position, Vector3 size, Color color)
        {
            // Create a mesh instance for the cube
            MeshInstance3D meshInstance = new MeshInstance3D();

            // Create a box mesh
            BoxMesh boxMesh = new BoxMesh();
            boxMesh.Size = size;
            meshInstance.Mesh = boxMesh;

            // Create a material
            StandardMaterial3D material = new StandardMaterial3D();
            material.AlbedoColor = color;
            meshInstance.MaterialOverride = material;

            // Create a container node and add the mesh instance
            Node3D container = new Node3D();
            container.AddChild(meshInstance);
            container.Position = position;

            return container;
        }

        protected override void ProcessState(double delta)
        {
            base.ProcessState(delta);

            // Always animate wings when flying
            if (_currentState == FaunaState.Moving || _currentState == FaunaState.TakingOff)
            {
                AnimateWings(delta);
            }
        }

        protected override void ProcessIdleState(double delta)
        {
            // In idle state, look for a new target or start flying
            if (_stateTimer > 2.0f)
            {
                if (_random.NextDouble() < 0.7f)
                {
                    // Start flying
                    ChangeState(FaunaState.TakingOff);
                }
                else
                {
                    // Reset timer and stay idle
                    _stateTimer = 0.0f;
                }
            }
        }

        protected override void ProcessMovingState(double delta)
        {
            // Check if we have a landing target
            if (_hasLandingTarget)
            {
                // Move towards landing position
                MoveTowardsLanding(delta);
            }
            else
            {
                // Fly in a circular pattern
                FlyInCircularPattern(delta);

                // Occasionally look for landing spots
                if (_stateTimer > 5.0f && _random.NextDouble() < 0.02f)
                {
                    LookForLandingSpot();
                }
            }
        }

        protected override void ProcessPerchedState(double delta)
        {
            // Stay perched for a while
            if (_stateTimer > PerchDuration)
            {
                // Take off
                ChangeState(FaunaState.TakingOff);
            }
        }

        protected override void ProcessTakingOffState(double delta)
        {
            // Animate taking off
            if (_stateTimer < 1.0f)
            {
                // Move upward
                GlobalPosition += Vector3.Up * FlyingSpeed * 0.5f * (float)delta;
            }
            else
            {
                // Reset landing target
                _hasLandingTarget = false;

                // Start flying
                ChangeState(FaunaState.Moving);

                // Set new circle center
                _circleCenter = new Vector3(GlobalPosition.X, 0, GlobalPosition.Z);
                _circleHeight = GlobalPosition.Y;
            }
        }

        private void FlyInCircularPattern(double delta)
        {
            // Update circle angle
            _circleAngle += FlyingSpeed * (float)delta * 0.2f;

            // Calculate new position on circle
            float x = _circleCenter.X + Mathf.Cos(_circleAngle) * _circleRadius;
            float z = _circleCenter.Z + Mathf.Sin(_circleAngle) * _circleRadius;

            // Add some vertical movement
            float y = _circleHeight + Mathf.Sin(_circleAngle * 0.5f) * 2.0f;

            // Set new position
            Vector3 newPosition = new Vector3(x, y, z);

            // Look in the direction of movement
            LookAt(newPosition, Vector3.Up);

            // Move to new position
            GlobalPosition = newPosition;

            // Occasionally change circle parameters
            if (_random.NextDouble() < 0.005f)
            {
                // Change radius
                _circleRadius = 5.0f + (float)_random.NextDouble() * 15.0f;

                // Change height
                _circleHeight = FlyingHeight + (float)_random.NextDouble() * 10.0f - 5.0f;
            }
        }

        private void MoveTowardsLanding(double delta)
        {
            // Calculate direction to landing position
            Vector3 direction = (_landingPosition - GlobalPosition).Normalized();

            // Move towards landing position
            GlobalPosition += direction * FlyingSpeed * (float)delta;

            // Look in the direction of movement
            LookAt(_landingPosition, Vector3.Up);

            // Check if we've reached the landing position
            if (GlobalPosition.DistanceTo(_landingPosition) < LandingDistance)
            {
                // Land
                GlobalPosition = _landingPosition;

                // Change to perched state
                ChangeState(FaunaState.Perched);

                // Reset wings
                ResetWings();
            }
        }

        private void LookForLandingSpot()
        {
            // Try to find a suitable landing spot in the world
            // First, get the chunk manager
            ChunkManager chunkManager = GetNode<ChunkManager>("/root/World/WorldGenerator/ChunkManager");

            if (chunkManager == null)
            {
                // If we can't find the chunk manager, use a random high point
                UseRandomLandingSpot();
                return;
            }

            // Search for high points in the world
            // Look for trees, cacti, or other tall structures

            // Random position near current position
            float searchRadius = 50.0f;
            float x = GlobalPosition.X + ((float)_random.NextDouble() * searchRadius * 2.0f - searchRadius);
            float z = GlobalPosition.Z + ((float)_random.NextDouble() * searchRadius * 2.0f - searchRadius);

            // Convert to world coordinates
            int worldX = (int)x;
            int worldZ = (int)z;

            // Get the biome type for this position
            BiomeType biomeType = WorldGenerator.GetBiomeType(worldX, worldZ);

            // Try to find a high point in this area
            bool foundSpot = false;
            Vector3 highestPoint = Vector3.Zero;
            float highestY = 0;

            // Check a small area around the target position
            int searchSize = 10;
            for (int dx = -searchSize; dx <= searchSize; dx += 2)
            {
                for (int dz = -searchSize; dz <= searchSize; dz += 2)
                {
                    int checkX = worldX + dx;
                    int checkZ = worldZ + dz;

                    // Get the highest voxel at this position
                    int highestVoxel = FindHighestVoxel(chunkManager, checkX, checkZ);

                    // Check if this is higher than our current highest point
                    if (highestVoxel > highestY)
                    {
                        // Check if this is a suitable landing spot (tree, cactus, etc.)
                        if (IsSuitableLandingSpot(chunkManager, checkX, highestVoxel, checkZ, biomeType))
                        {
                            highestY = highestVoxel;
                            highestPoint = new Vector3(checkX, highestVoxel + 1, checkZ);
                            foundSpot = true;
                        }
                    }
                }
            }

            if (foundSpot)
            {
                // Set landing position
                _landingPosition = highestPoint;
                _hasLandingTarget = true;
                GD.Print($"Bird found landing spot at {_landingPosition}");
            }
            else
            {
                // If we couldn't find a suitable spot, use a random high point
                UseRandomLandingSpot();
            }
        }

        private void UseRandomLandingSpot()
        {
            // Random position near current position
            float x = GlobalPosition.X + ((float)_random.NextDouble() * 40.0f - 20.0f);
            float z = GlobalPosition.Z + ((float)_random.NextDouble() * 40.0f - 20.0f);

            // Set landing height to a high value (simulating a tree or cactus)
            float y = 30.0f + (float)_random.NextDouble() * 10.0f;

            // Set landing position
            _landingPosition = new Vector3(x, y, z);
            _hasLandingTarget = true;
        }

        private int FindHighestVoxel(ChunkManager chunkManager, int x, int z)
        {
            if (chunkManager == null)
                return 0;

            // Start from a reasonable height and search downward
            int startY = 100;

            for (int y = startY; y >= 0; y--)
            {
                VoxelType voxelType = chunkManager.GetVoxelType(x, y, z);

                // If we find a non-air voxel, this is the highest point
                if (voxelType != VoxelType.Air)
                {
                    return y;
                }
            }

            return 0;
        }

        private bool IsSuitableLandingSpot(ChunkManager chunkManager, int x, int y, int z, BiomeType biomeType)
        {
            if (chunkManager == null)
                return false;

            // Get the voxel type at this position
            VoxelType voxelType = chunkManager.GetVoxelType(x, y, z);

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
                    return y > 20 && chunkManager.GetVoxelType(x, y + 1, z) == VoxelType.Air;

                case VoxelType.Cactus:
                    // Birds can land on cacti
                    return chunkManager.GetVoxelType(x, y + 1, z) == VoxelType.Air;

                default:
                    // For other voxel types, only land if they're high enough and have air above
                    return y > 20 && chunkManager.GetVoxelType(x, y + 1, z) == VoxelType.Air;
            }
        }

        private void AnimateWings(double delta)
        {
            // Update wing flap angle
            _wingFlapAngle += WingFlapSpeed * (float)delta;

            // Keep angle in reasonable range
            if (_wingFlapAngle > Mathf.Pi * 2)
            {
                _wingFlapAngle -= Mathf.Pi * 2;
            }

            // Calculate wing rotation
            float wingRotation = Mathf.Sin(_wingFlapAngle) * 0.5f;

            // Apply rotation to wings
            if (_leftWing != null && _rightWing != null)
            {
                _leftWing.Rotation = new Vector3(0, 0, -wingRotation);
                _rightWing.Rotation = new Vector3(0, 0, wingRotation);
            }
        }

        private void ResetWings()
        {
            // Reset wing rotation
            if (_leftWing != null && _rightWing != null)
            {
                _leftWing.Rotation = Vector3.Zero;
                _rightWing.Rotation = Vector3.Zero;
            }
        }
    }

    /// <summary>
    /// Enum representing different bird types
    /// </summary>
    public enum BirdType
    {
        Small,
        Medium,
        Large
    }
}
