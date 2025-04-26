using Godot;
using System;

public partial class BiomeMapCamera : Camera3D
{
    [Export] public float MapHeight { get; set; } = 500.0f;
    [Export] public float MapMoveSpeed { get; set; } = 50.0f;
    [Export] public float ZoomSpeed { get; set; } = 50.0f;
    [Export] public float MinZoom { get; set; } = 100.0f;
    [Export] public float MaxZoom { get; set; } = 1000.0f;
    
    private Vector3 _targetPosition;
    private Label _biomeLabel;
    private Label _controlsLabel;
    private WorldGenerator _worldGenerator;
    
    public override void _Ready()
    {
        // Initialize position
        _targetPosition = new Vector3(0, MapHeight, 0);
        Position = _targetPosition;
        
        // Set up orthogonal projection for map view
        Projection = ProjectionType.Orthogonal;
        Size = 200.0f;
        
        // Look straight down
        Rotation = new Vector3(-Mathf.Pi/2, 0, 0);
        
        // Find the world generator
        _worldGenerator = GetNode<WorldGenerator>("/root/World/WorldGenerator");
        
        // Create UI elements
        CreateUI();
    }
    
    private void CreateUI()
    {
        // Create a Control node for UI
        Control uiControl = new Control();
        uiControl.AnchorRight = 1.0f;
        uiControl.AnchorBottom = 1.0f;
        AddChild(uiControl);
        
        // Create biome label
        _biomeLabel = new Label();
        _biomeLabel.Position = new Vector2(20, 20);
        _biomeLabel.Text = "Biome: Unknown";
        uiControl.AddChild(_biomeLabel);
        
        // Create controls label
        _controlsLabel = new Label();
        _controlsLabel.Position = new Vector2(20, 50);
        _controlsLabel.Text = "WASD: Move | Mouse Wheel: Zoom | M: Exit Map";
        uiControl.AddChild(_controlsLabel);
    }
    
    public override void _Process(double delta)
    {
        // Handle movement
        Vector3 moveDirection = Vector3.Zero;
        
        if (Input.IsActionPressed("ui_up"))
            moveDirection.Z -= 1;
        if (Input.IsActionPressed("ui_down"))
            moveDirection.Z += 1;
        if (Input.IsActionPressed("ui_left"))
            moveDirection.X -= 1;
        if (Input.IsActionPressed("ui_right"))
            moveDirection.X += 1;
        
        if (moveDirection != Vector3.Zero)
        {
            moveDirection = moveDirection.Normalized();
            _targetPosition += moveDirection * MapMoveSpeed * (float)delta;
            Position = _targetPosition;
        }
        
        // Update biome label
        if (_worldGenerator != null)
        {
            int worldX = (int)Position.X;
            int worldZ = (int)Position.Z;
            BiomeType biomeType = WorldGenerator.GetBiomeType(worldX, worldZ);
            _biomeLabel.Text = $"Biome: {biomeType}";
        }
    }
    
    public override void _Input(InputEvent @event)
    {
        // Handle zooming with mouse wheel
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
            {
                // Zoom in
                Size = Mathf.Max(Size - ZoomSpeed, MinZoom);
            }
            else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
            {
                // Zoom out
                Size = Mathf.Min(Size + ZoomSpeed, MaxZoom);
            }
        }
    }
}
