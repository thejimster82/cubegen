using Godot;
using System;

public partial class Player : CharacterBody3D
{
    [Export] public float Speed { get; set; } = 5.0f;
    [Export] public float JumpVelocity { get; set; } = 4.5f;
    [Export] public float MouseSensitivity { get; set; } = 0.002f;
    
    private Node3D _head;
    private Camera3D _camera;
    private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
    
    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        _camera = GetNode<Camera3D>("Head/Camera3D");
        
        // Capture mouse
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }
    
    public override void _Input(InputEvent @event)
    {
        // Mouse look
        if (@event is InputEventMouseMotion mouseMotion)
        {
            // Rotate head (up/down)
            _head.RotateX(-mouseMotion.Relative.Y * MouseSensitivity);
            _head.Rotation = new Vector3(
                Mathf.Clamp(_head.Rotation.X, -Mathf.Pi / 2, Mathf.Pi / 2),
                _head.Rotation.Y,
                _head.Rotation.Z
            );
            
            // Rotate body (left/right)
            RotateY(-mouseMotion.Relative.X * MouseSensitivity);
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
    
    public override void _PhysicsProcess(double delta)
    {
        Vector3 velocity = Velocity;
        
        // Add gravity
        if (!IsOnFloor())
            velocity.Y -= _gravity * (float)delta;
            
        // Handle jump
        if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
            velocity.Y = JumpVelocity;
            
        // Get movement direction
        Vector2 inputDir = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        
        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * Speed;
            velocity.Z = direction.Z * Speed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
            velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
        }
        
        Velocity = velocity;
        MoveAndSlide();
    }
}
