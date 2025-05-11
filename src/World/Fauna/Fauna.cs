using Godot;
using System;
using CubeGen.World.Common;

namespace CubeGen.World.Fauna
{
    /// <summary>
    /// Base class for all non-interactive fauna in the world
    /// </summary>
    public partial class Fauna : Node3D
    {
        // Fauna properties
        [Export] public string FaunaName { get; set; } = "Unknown";
        [Export] public float MovementSpeed { get; set; } = 1.0f;
        [Export] public float FaunaScale { get; set; } = 1.0f;
        [Export] public BiomeType PreferredBiome { get; set; } = BiomeType.ForestLands;

        // State tracking
        protected FaunaState _currentState = FaunaState.Idle;
        protected Vector3 _targetPosition;
        protected float _stateTimer = 0.0f;
        protected Random _random;

        // Visual representation
        protected Node3D _model;

        // Called when the node enters the scene tree for the first time
        public override void _Ready()
        {
            // Initialize random with a unique seed based on position
            _random = new Random(GetInstanceId().GetHashCode());

            // Set initial target position to current position
            _targetPosition = GlobalPosition;

            // Create visual representation
            CreateModel();
        }

        // Called every frame
        public override void _Process(double delta)
        {
            // Update state timer
            _stateTimer += (float)delta;

            // Process current state
            ProcessState(delta);

            // Ensure the fauna is always visible
            Visible = true;
        }

        /// <summary>
        /// Process the current state
        /// </summary>
        protected virtual void ProcessState(double delta)
        {
            switch (_currentState)
            {
                case FaunaState.Idle:
                    ProcessIdleState(delta);
                    break;
                case FaunaState.Moving:
                    ProcessMovingState(delta);
                    break;
                case FaunaState.Perched:
                    ProcessPerchedState(delta);
                    break;
                case FaunaState.TakingOff:
                    ProcessTakingOffState(delta);
                    break;
                default:
                    ProcessIdleState(delta);
                    break;
            }
        }

        /// <summary>
        /// Process the idle state
        /// </summary>
        protected virtual void ProcessIdleState(double delta)
        {
            // Default implementation: do nothing in idle state
            // Derived classes should override this
        }

        /// <summary>
        /// Process the moving state
        /// </summary>
        protected virtual void ProcessMovingState(double delta)
        {
            // Default implementation: move towards target position
            // Derived classes should override this for specific movement patterns
        }

        /// <summary>
        /// Process the perched state
        /// </summary>
        protected virtual void ProcessPerchedState(double delta)
        {
            // Default implementation: stay perched for a while
            // Derived classes should override this
        }

        /// <summary>
        /// Process the taking off state
        /// </summary>
        protected virtual void ProcessTakingOffState(double delta)
        {
            // Default implementation: transition to moving state
            // Derived classes should override this
        }

        /// <summary>
        /// Change to a new state
        /// </summary>
        protected virtual void ChangeState(FaunaState newState)
        {
            _currentState = newState;
            _stateTimer = 0.0f;
        }

        /// <summary>
        /// Create the visual model for this fauna
        /// </summary>
        protected virtual void CreateModel()
        {
            // Default implementation: create a simple placeholder model
            // Derived classes should override this with specific models
            _model = new Node3D();
            AddChild(_model);
        }

        /// <summary>
        /// Check if this fauna is visible to the player
        /// </summary>
        public virtual bool IsVisibleToPlayer(Vector3 playerPosition, float maxDistance)
        {
            // Simple distance-based visibility check
            return GlobalPosition.DistanceTo(playerPosition) <= maxDistance;
        }

        /// <summary>
        /// Get the current state of this fauna
        /// </summary>
        public FaunaState GetCurrentState()
        {
            return _currentState;
        }
    }

    /// <summary>
    /// Enum representing different fauna states
    /// </summary>
    public enum FaunaState
    {
        Idle,
        Moving,
        Perched,
        TakingOff
    }
}
