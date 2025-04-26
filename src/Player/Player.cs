using Godot;
using System;

public partial class Player : CharacterBody3D
{
    [Export] public float Speed { get; set; } = 4.0f; // Increased from 2.7f for better gameplay feel
    [Export] public float JumpVelocity { get; set; } = 3.5f; // Kept the same
    [Export] public float MouseSensitivity { get; set; } = 0.002f;
    [Export] public float CameraRotationSpeed { get; set; } = 3.0f;
    [Export] public float CameraDistance { get; set; } = 3.0f; // Increased from 1.5f to zoom out the camera
    [Export] public float CameraHeight { get; set; } = 0.75f; // Increased from 0.375f for better view with larger character
    [Export] public float Friction { get; set; } = 0.1f; // Ground friction
    [Export] public float Acceleration { get; set; } = 0.25f; // Movement acceleration
    [Export] public float MaxStepHeight { get; set; } = 0.5f; // Increased from 0.3125f for better stepping with larger character

    private Node3D _head;
    private Node3D _cameraMount;
    private Camera3D _camera;
    private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
    private float _cameraRotation = 0.0f;

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        _cameraMount = GetNode<Node3D>("CameraMount");
        _camera = GetNode<Camera3D>("CameraMount/Camera3D");

        // Initialize camera position
        UpdateCameraPosition();

        // Capture mouse
        Input.MouseMode = Input.MouseModeEnum.Captured;

        // Set up physics properties for better movement
        FloorStopOnSlope = true;
        FloorMaxAngle = 1.0f; // About 60 degrees - even steeper angle for better climbing
        FloorSnapLength = 1.0f; // Increased for larger character (was 0.5f)

        // Set up step climbing properties
        UpDirection = Vector3.Up;
        FloorConstantSpeed = true; // Maintain constant speed on slopes
        FloorBlockOnWall = false; // Allow sliding along walls

        // Additional physics properties
        WallMinSlideAngle = 0.1f; // Allow sliding on very slight walls
        MaxSlides = 10; // Increase maximum slides for better movement around obstacles
    }

    public override void _Input(InputEvent @event)
    {
        // Mouse look
        if (@event is InputEventMouseMotion mouseMotion)
        {
            // Rotate camera mount (left/right)
            _cameraMount.RotateY(-mouseMotion.Relative.X * MouseSensitivity);

            // Adjust camera height (up/down) with limits
            _cameraRotation -= mouseMotion.Relative.Y * MouseSensitivity * CameraRotationSpeed;
            _cameraRotation = Mathf.Clamp(_cameraRotation, -0.5f, 0.5f);

            // Update camera position
            UpdateCameraPosition();
        }

        // Toggle mouse capture
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
                Input.MouseMode = Input.MouseModeEnum.Visible;
            else
                Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    private void UpdateCameraPosition()
    {
        // Calculate camera position based on rotation and distance
        float height = CameraHeight + _cameraRotation * 2.0f; // Adjust height based on rotation (reduced multiplier)
        float distance = CameraDistance - _cameraRotation * 1.0f; // Adjust distance based on rotation (reduced multiplier)

        // Set camera transform - position only, keep the rotation from camera mount
        _camera.Position = new Vector3(0, height, distance);

        // Make camera look at player's head
        _camera.LookAt(_head.GlobalPosition);
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 velocity = Velocity;

        // Add gravity
        if (!IsOnFloor())
            velocity.Y -= _gravity * (float)delta;

        // Handle jump
        if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
            velocity.Y = JumpVelocity;

        // Store the original Y velocity for stair stepping
        float originalY = velocity.Y;

        // Get movement input
        Vector2 inputDir = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");

        // Store the input magnitude for use in rotation logic
        float inputMagnitude = inputDir.Length();

        // Get camera's forward direction (ignoring Y component)
        Vector3 cameraForward = -_camera.GlobalTransform.Basis.Z;
        cameraForward.Y = 0;
        cameraForward = cameraForward.Normalized();

        // Get camera's right direction (ignoring Y component)
        Vector3 cameraRight = _camera.GlobalTransform.Basis.X;
        cameraRight.Y = 0;
        cameraRight = cameraRight.Normalized();

        // Calculate movement direction relative to camera
        // Invert the Y input to fix the backwards movement (W/S keys)
        Vector3 direction = Vector3.Zero;
        if (inputMagnitude > 0.1f)
        {
            direction = (cameraForward * -inputDir.Y + cameraRight * inputDir.X).Normalized();
        }

        // Handle movement with acceleration and friction
        if (direction != Vector3.Zero)
        {
            // Calculate target velocity
            Vector3 targetVelocity = direction * Speed;

            // Apply acceleration
            if (IsOnFloor())
            {
                // Smoother acceleration on ground
                velocity.X = Mathf.Lerp(velocity.X, targetVelocity.X, Acceleration);
                velocity.Z = Mathf.Lerp(velocity.Z, targetVelocity.Z, Acceleration);
            }
            else
            {
                // Less control in air
                velocity.X = Mathf.Lerp(velocity.X, targetVelocity.X, Acceleration * 0.5f);
                velocity.Z = Mathf.Lerp(velocity.Z, targetVelocity.Z, Acceleration * 0.5f);
            }

            // No player rotation - movement is purely relative to camera direction
        }
        else if (IsOnFloor())
        {
            // Apply friction when on ground and not actively moving
            velocity.X = Mathf.Lerp(velocity.X, 0, Friction);
            velocity.Z = Mathf.Lerp(velocity.Z, 0, Friction);
        }

        Velocity = velocity;
        MoveAndSlide();

        // Enhanced stair stepping implementation
        if (IsOnFloor() && inputDir.Length() > 0.1f)
        {
            // Get the horizontal velocity (movement direction)
            Vector3 horizontalVelocity = new Vector3(Velocity.X, 0, Velocity.Z);

            if (horizontalVelocity.Length() > 0.1f)
            {
                // Normalize and scale to check ahead (slightly longer distance)
                Vector3 stepCheckDirection = horizontalVelocity.Normalized() * 0.25f; // Increased for larger character (was 0.125f)

                // Store current position for potential rollback
                Vector3 originalPosition = Position;

                // First, check if there's an obstacle in front of us
                PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
                    Position + new Vector3(0, 0.05f, 0), // Increased for larger character (was 0.025f)
                    Position + new Vector3(0, 0.05f, 0) + stepCheckDirection, // Check in movement direction
                    1, // Collision mask (adjust as needed)
                    new Godot.Collections.Array<Godot.Rid>() // Exclude nothing
                );

                // Cast the ray
                var result = GetWorld3D().DirectSpaceState.IntersectRay(query);

                // If we hit something, it might be a step
                if (result.Count > 0)
                {
                    // Get the collision point
                    Vector3 collisionPoint = (Vector3)result["position"];

                    // Calculate how far we need to check above the collision
                    float checkHeight = MaxStepHeight + 0.1f; // Increased for larger character (was 0.05f)

                    // Check if there's space above the obstacle (for the player's height)
                    for (float h = 0.125f; h <= checkHeight; h += 0.125f) // Increased for larger character (was 0.0625f)
                    {
                        // Cast a ray from above the obstacle
                        query = PhysicsRayQueryParameters3D.Create(
                            new Vector3(collisionPoint.X, Position.Y + h, collisionPoint.Z), // Start above the collision
                            new Vector3(collisionPoint.X, Position.Y + h, collisionPoint.Z) + stepCheckDirection * 0.25f, // Increased for larger character (was 0.125f)
                            1, // Collision mask
                            new Godot.Collections.Array<Godot.Rid>() // Exclude nothing
                        );

                        var upperResult = GetWorld3D().DirectSpaceState.IntersectRay(query);

                        // If there's nothing blocking at this height, we can try to step up
                        if (upperResult.Count == 0)
                        {
                            // Try to move the player up to this height
                            Position = new Vector3(Position.X, Position.Y + h - 0.025f, Position.Z); // Increased for larger character (was 0.0125f)

                            // Apply a small forward impulse to help get over the step
                            velocity = horizontalVelocity.Normalized() * Speed * 1.1f; // Keep the same impulse multiplier
                            velocity.Y = 0; // Don't apply gravity during the step

                            Velocity = velocity;
                            MoveAndSlide();

                            // Check if we successfully moved forward
                            Vector3 newHorizontalPos = new Vector3(Position.X, 0, Position.Z);
                            Vector3 oldHorizontalPos = new Vector3(originalPosition.X, 0, originalPosition.Z);

                            if ((newHorizontalPos - oldHorizontalPos).Length() < 0.1f)
                            {
                                // We didn't move forward enough, try a higher step
                                continue;
                            }

                            // We successfully stepped up
                            GD.Print("Successfully stepped up by " + h + " units");
                            return;
                        }
                    }

                    // If we get here, we couldn't step up, so restore original position
                    Position = originalPosition;
                    velocity.Y = originalY; // Restore original Y velocity
                    Velocity = velocity;
                }
            }
        }
    }
}
