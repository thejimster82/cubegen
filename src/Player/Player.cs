using Godot;
using System;

public partial class Player : CharacterBody3D
{
    [Export] public float Speed { get; set; } = 15.0f; // Increased speed for larger player
    [Export] public float JumpVelocity { get; set; } = 12.0f; // Increased jump for larger player
    [Export] public float MouseSensitivity { get; set; } = 0.002f;
    [Export] public float CameraRotationSpeed { get; set; } = 3.0f;
    [Export] public float CameraDistance { get; set; } = 8.0f;
    [Export] public float CameraHeight { get; set; } = 4.0f;
    [Export] public float Friction { get; set; } = 0.1f; // Ground friction
    [Export] public float Acceleration { get; set; } = 0.25f; // Movement acceleration

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

        // Set up physics properties
        FloorStopOnSlope = true;
        FloorMaxAngle = 0.8f; // About 45 degrees
        FloorSnapLength = 0.5f;
    }

    public override void _Input(InputEvent @event)
    {
        // Mouse look
        if (@event is InputEventMouseMotion mouseMotion)
        {
            // Rotate player (left/right)
            RotateY(-mouseMotion.Relative.X * MouseSensitivity);

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
        float height = CameraHeight + _cameraRotation * 4.0f; // Adjust height based on rotation
        float distance = CameraDistance - _cameraRotation * 2.0f; // Adjust distance based on rotation

        // Set camera transform
        _camera.Transform = new Transform3D(
            Basis.Identity,
            new Vector3(0, height, distance)
        );

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

            // Only manually rotate player when there's significant input
            if (inputMagnitude > 0.1f)
            {
                // Calculate the target angle based on input direction, not the resulting velocity
                // This is more stable and prevents unwanted rotation
                float targetAngle = Mathf.Atan2(-inputDir.X, -inputDir.Y);

                // Add the camera's rotation to get the correct world orientation
                targetAngle += _cameraMount.GlobalRotation.Y;

                // Get current angle
                float currentAngle = Rotation.Y;

                // Use a very gentle rotation speed to prevent spinning
                float rotationSpeed = 2.0f;

                // Interpolate rotation for smooth turning
                Rotation = new Vector3(
                    Rotation.X,
                    Mathf.LerpAngle(currentAngle, targetAngle, (float)delta * rotationSpeed),
                    Rotation.Z
                );
            }
        }
        else if (IsOnFloor())
        {
            // Apply friction when on ground and not actively moving
            velocity.X = Mathf.Lerp(velocity.X, 0, Friction);
            velocity.Z = Mathf.Lerp(velocity.Z, 0, Friction);
        }

        Velocity = velocity;
        MoveAndSlide();
    }
}
