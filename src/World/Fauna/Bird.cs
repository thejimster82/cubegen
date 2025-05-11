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
        [Export] public float FlyingHeight { get; set; } = 50.0f; // Increased from 20.0f to 50.0f
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

            // Initialize flight pattern with a unique random seed
            _random = new Random(GetInstanceId().GetHashCode() + (int)(GlobalPosition.X * 1000) + (int)(GlobalPosition.Z * 1000));
            _circleCenter = GlobalPosition;
            _circleHeight = GlobalPosition.Y;

            // Initialize circle radius with some randomness
            _circleRadius = 5.0f + (float)_random.NextDouble() * 15.0f;

            // Initialize circle angle with some randomness to prevent birds from flying in sync
            _circleAngle = (float)_random.NextDouble() * Mathf.Pi * 2;

            // Start in flying state
            ChangeState(FaunaState.Moving);

            // Debug output
            GD.Print($"Bird initialized at {GlobalPosition}, circle center: {_circleCenter}, height: {_circleHeight}");
        }

        public override void _PhysicsProcess(double delta)
        {
            // This ensures the bird movement is processed every physics frame
            // This is crucial for consistent movement
            if (_currentState == FaunaState.Moving)
            {
                // Process movement in physics process for more consistent motion
                if (_hasLandingTarget)
                {
                    MoveTowardsLanding(delta);
                }
                else
                {
                    FlyInCircularPattern(delta);
                }
            }
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
            // Create body - make it face the -Z direction (forward)
            _body = CreateVoxelCube(new Vector3(0, 0, 0), new Vector3(0.5f, 0.3f, 0.7f), PrimaryColor);
            _model.AddChild(_body);

            // Create head - positioned at the front (-Z) of the body
            _head = CreateVoxelCube(new Vector3(0, 0.2f, -0.4f), new Vector3(0.3f, 0.3f, 0.3f), PrimaryColor);
            _model.AddChild(_head);

            // Create beak - positioned at the front of the head
            Node3D beak = CreateVoxelCube(new Vector3(0, 0.15f, -0.6f), new Vector3(0.1f, 0.1f, 0.2f), new Color(1.0f, 0.7f, 0.0f));
            _model.AddChild(beak);

            // Create wings - make them much larger for better visibility and more realistic flight
            _leftWing = CreateVoxelCube(new Vector3(0.6f, 0.1f, 0), new Vector3(0.8f, 0.1f, 0.6f), SecondaryColor);
            _rightWing = CreateVoxelCube(new Vector3(-0.6f, 0.1f, 0), new Vector3(0.8f, 0.1f, 0.6f), SecondaryColor);
            _model.AddChild(_leftWing);
            _model.AddChild(_rightWing);

            // Create tail - positioned at the back (+Z) of the body
            _tail = CreateVoxelCube(new Vector3(0, 0.1f, 0.4f), new Vector3(0.3f, 0.1f, 0.3f), SecondaryColor);
            _model.AddChild(_tail);

            // Add eyes - positioned at the front of the head
            Node3D leftEye = CreateVoxelCube(new Vector3(0.1f, 0.25f, -0.5f), new Vector3(0.05f, 0.05f, 0.05f), Colors.Black);
            Node3D rightEye = CreateVoxelCube(new Vector3(-0.1f, 0.25f, -0.5f), new Vector3(0.05f, 0.05f, 0.05f), Colors.Black);
            _model.AddChild(leftEye);
            _model.AddChild(rightEye);

            // Rotate the entire model to face the correct direction
            _model.RotationDegrees = new Vector3(0, 180, 0);
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
            // We now handle the actual movement in _PhysicsProcess for more consistent motion
            // Here we just handle state changes and occasional actions

            // Occasionally look for landing spots
            if (_stateTimer > 5.0f && _random.NextDouble() < 0.01f)
            {
                LookForLandingSpot();
            }

            // Occasionally change flight parameters for more varied movement
            if (_random.NextDouble() < 0.005f)
            {
                // Change radius - more variation for interesting patterns
                _circleRadius = 5.0f + (float)_random.NextDouble() * 20.0f;

                // Change height - keep within a reasonable range
                _circleHeight = FlyingHeight + (float)_random.NextDouble() * 15.0f - 7.5f;

                // Occasionally change the circle center for more varied flight paths
                if (_random.NextDouble() < 0.2f)
                {
                    // Shift the circle center slightly
                    float centerShiftX = (float)_random.NextDouble() * 10.0f - 5.0f;
                    float centerShiftZ = (float)_random.NextDouble() * 10.0f - 5.0f;
                    _circleCenter.X += centerShiftX;
                    _circleCenter.Z += centerShiftZ;
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
            // Store current position to calculate direction
            Vector3 oldPosition = GlobalPosition;

            // Update circle angle
            _circleAngle += FlyingSpeed * (float)delta * 0.2f;

            // Calculate new position on circle
            float x = _circleCenter.X + Mathf.Cos(_circleAngle) * _circleRadius;
            float z = _circleCenter.Z + Mathf.Sin(_circleAngle) * _circleRadius;

            // Add some vertical movement - more natural wave pattern
            float y = _circleHeight + Mathf.Sin(_circleAngle * 0.5f) * 2.0f;

            // Set new position
            Vector3 newPosition = new Vector3(x, y, z);

            // Calculate movement direction
            Vector3 direction = newPosition - oldPosition;

            // Only update rotation if we're actually moving
            if (direction.Length() > 0.01f)
            {
                // Look in the direction of movement
                // We need to look in the direction we're moving, not at the position itself
                Vector3 lookTarget = GlobalPosition + direction;
                LookAt(lookTarget, Vector3.Up);

                // Add a slight bank angle when turning (tilt towards the center of the circle)
                Vector3 toCenter = _circleCenter - GlobalPosition;
                toCenter.Y = 0; // Keep it horizontal

                if (toCenter.Length() > 0.01f)
                {
                    // Calculate the cross product of forward direction and up vector
                    // This gives us the right vector
                    Vector3 forward = -GlobalTransform.Basis.Z;
                    Vector3 right = forward.Cross(Vector3.Up);

                    // Calculate the dot product to determine if we're turning left or right
                    float dot = right.Dot(toCenter.Normalized());

                    // Apply a bank angle (roll) based on the turn direction
                    // This makes the bird tilt into the turn
                    float bankAngle = dot * 0.3f; // Adjust the multiplier for more or less banking
                    Rotation = new Vector3(Rotation.X, Rotation.Y, bankAngle);
                }
            }

            // Move to new position
            GlobalPosition = newPosition;

            // We now handle parameter changes in ProcessMovingState
            // This ensures the movement is consistent and not interrupted
        }

        private void MoveTowardsLanding(double delta)
        {
            // Store current position to calculate direction
            Vector3 oldPosition = GlobalPosition;

            // Calculate direction to landing position
            Vector3 direction = (_landingPosition - GlobalPosition).Normalized();

            // Move towards landing position
            GlobalPosition += direction * FlyingSpeed * (float)delta;

            // Calculate actual movement direction
            Vector3 actualDirection = GlobalPosition - oldPosition;

            // Only update rotation if we're actually moving
            if (actualDirection.Length() > 0.01f)
            {
                // Look in the direction of movement
                Vector3 lookTarget = GlobalPosition + actualDirection;
                LookAt(lookTarget, Vector3.Up);
            }

            // As we get closer to landing, slow down and start to descend more vertically
            float distanceToLanding = GlobalPosition.DistanceTo(_landingPosition);
            if (distanceToLanding < 5.0f)
            {
                // Slow down as we approach the landing spot
                float slowdownFactor = Mathf.Clamp(distanceToLanding / 5.0f, 0.3f, 1.0f);

                // Adjust height to approach from above
                if (GlobalPosition.Y < _landingPosition.Y + 2.0f)
                {
                    // If we're below the landing spot + buffer, move up
                    GlobalPosition = new Vector3(
                        GlobalPosition.X,
                        Mathf.Lerp(GlobalPosition.Y, _landingPosition.Y + 2.0f, 0.1f),
                        GlobalPosition.Z
                    );
                }

                // Flap wings more slowly as we prepare to land
                WingFlapSpeed = Mathf.Lerp(WingFlapSpeed, 2.0f, 0.1f);
            }

            // Check if we've reached the landing position
            if (distanceToLanding < LandingDistance)
            {
                // Land
                GlobalPosition = _landingPosition;

                // Rotate to face a random direction when perched
                float randomRotation = (float)_random.NextDouble() * Mathf.Pi * 2;
                Rotation = new Vector3(0, randomRotation, 0);

                // Change to perched state
                ChangeState(FaunaState.Perched);

                // Reset wings
                ResetWings();

                GD.Print($"Bird landed at {_landingPosition}");
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

            // Calculate wing rotation - increase amplitude for more visible flapping
            float wingRotation = Mathf.Sin(_wingFlapAngle) * 0.8f;

            // Apply rotation to wings - use Z rotation for up/down movement
            if (_leftWing != null && _rightWing != null)
            {
                // For birds, wings should move up and down (Z rotation)
                // Negative rotation for left wing makes it go up when right wing goes down
                _leftWing.Rotation = new Vector3(0, 0, -wingRotation);
                _rightWing.Rotation = new Vector3(0, 0, wingRotation);

                // Also add a slight Y rotation to make the wings twist a bit during flapping
                // This creates a more natural flapping motion
                float twistAmount = Mathf.Sin(_wingFlapAngle) * 0.2f;
                _leftWing.Rotation += new Vector3(0, twistAmount, 0);
                _rightWing.Rotation += new Vector3(0, -twistAmount, 0);
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
