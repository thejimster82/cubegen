using Godot;
using System;
using CubeGen.World.Fauna;
using CubeGen.World.Common;

public partial class BirdTest : Node3D
{
    [Export] public int NumBirds { get; set; } = 10;
    [Export] public float SpawnRadius { get; set; } = 20.0f;
    [Export] public float SpawnHeight { get; set; } = 20.0f;
    [Export] public PackedScene BirdScene { get; set; }

    private Camera3D _camera;
    private Label _infoLabel;

    public override void _Ready()
    {
        // Get camera reference
        _camera = GetNode<Camera3D>("Camera3D");

        // Create info label
        CreateInfoLabel();

        // Spawn birds
        SpawnBirds();
    }

    public override void _Process(double delta)
    {
        // Update info label
        UpdateInfoLabel();

        // Handle camera movement
        HandleCameraMovement(delta);
    }

    private void CreateInfoLabel()
    {
        // Create a Control node for UI
        Control ui = new Control();
        ui.AnchorRight = 1.0f;
        ui.AnchorBottom = 1.0f;
        AddChild(ui);

        // Create info label
        _infoLabel = new Label();
        _infoLabel.Position = new Vector2(20, 20);
        _infoLabel.Text = "Bird Test Scene";
        ui.AddChild(_infoLabel);
    }

    private void UpdateInfoLabel()
    {
        if (_infoLabel != null)
        {
            // Count active birds
            int activeBirds = 0;
            foreach (Node child in GetChildren())
            {
                if (child is Bird)
                {
                    activeBirds++;
                }
            }

            // Update label
            _infoLabel.Text = $"Bird Test Scene\nActive Birds: {activeBirds}\nControls: WASD - Move, Mouse - Look, Esc - Toggle Mouse";
        }
    }

    private void HandleCameraMovement(double delta)
    {
        // Camera movement speed
        float speed = 10.0f;

        // Get input direction
        Vector3 direction = Vector3.Zero;

        if (Input.IsActionPressed("ui_up"))
            direction -= _camera.GlobalTransform.Basis.Z;
        if (Input.IsActionPressed("ui_down"))
            direction += _camera.GlobalTransform.Basis.Z;
        if (Input.IsActionPressed("ui_left"))
            direction -= _camera.GlobalTransform.Basis.X;
        if (Input.IsActionPressed("ui_right"))
            direction += _camera.GlobalTransform.Basis.X;

        // Normalize direction
        if (direction != Vector3.Zero)
            direction = direction.Normalized();

        // Move camera
        _camera.GlobalPosition += direction * speed * (float)delta;
    }

    public override void _Input(InputEvent @event)
    {
        // Mouse look
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            // Rotate camera
            float sensitivity = 0.002f;
            _camera.RotateY(-mouseMotion.Relative.X * sensitivity);
            _camera.RotateObjectLocal(Vector3.Right, -mouseMotion.Relative.Y * sensitivity);
        }

        // Toggle mouse capture
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
                Input.MouseMode = Input.MouseModeEnum.Visible;
            else
                Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        // Spawn a new bird on Space key
        if (@event is InputEventKey spaceKeyEvent && spaceKeyEvent.Pressed && spaceKeyEvent.Keycode == Key.Space)
        {
            SpawnBird();
        }
    }

    private void SpawnBirds()
    {
        for (int i = 0; i < NumBirds; i++)
        {
            SpawnBird();
        }
    }

    private void SpawnBird()
    {
        // Create a new bird
        Bird bird;

        if (BirdScene != null)
        {
            bird = BirdScene.Instantiate<Bird>();
        }
        else
        {
            bird = new Bird();
        }

        // Configure bird
        ConfigureBird(bird);

        // Add to scene
        AddChild(bird);

        // Position the bird
        PositionBird(bird);
    }

    private void ConfigureBird(Bird bird)
    {
        // Random number generator
        Random random = new Random();

        // Determine bird type (weighted towards smaller birds)
        BirdType birdType;
        float typeRoll = (float)random.NextDouble();

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
                bird.FlyingSpeed = 3.0f + (float)random.NextDouble() * 1.0f;
                break;
            case BirdType.Medium:
                bird.FaunaScale = 0.75f;
                bird.FlyingSpeed = 2.5f + (float)random.NextDouble() * 0.8f;
                break;
            case BirdType.Large:
                bird.FaunaScale = 1.0f;
                bird.FlyingSpeed = 2.0f + (float)random.NextDouble() * 0.6f;
                break;
        }

        // Set random colors
        Color[] primaryColors = new Color[]
        {
            new Color(0.6f, 0.6f, 0.6f), // Gray
            new Color(0.6f, 0.3f, 0.1f), // Brown
            new Color(0.2f, 0.4f, 0.8f), // Blue
            new Color(0.1f, 0.5f, 0.1f), // Green
            new Color(0.8f, 0.2f, 0.2f), // Red
        };

        Color[] secondaryColors = new Color[]
        {
            new Color(0.8f, 0.8f, 0.8f), // Light gray
            new Color(0.8f, 0.5f, 0.2f), // Light brown
            new Color(0.4f, 0.6f, 0.9f), // Light blue
            new Color(0.2f, 0.7f, 0.2f), // Light green
            new Color(0.9f, 0.4f, 0.4f), // Light red
        };

        int colorIndex = random.Next(0, primaryColors.Length);
        bird.PrimaryColor = primaryColors[colorIndex];
        bird.SecondaryColor = secondaryColors[colorIndex];

        // Set other random properties
        bird.WingFlapSpeed = 4.0f + (float)random.NextDouble() * 2.0f;
        bird.PerchDuration = 5.0f + (float)random.NextDouble() * 10.0f;
    }

    private void PositionBird(Bird bird)
    {
        // Random number generator
        Random random = new Random();

        // Random angle
        float angle = (float)random.NextDouble() * Mathf.Pi * 2;

        // Random distance from center (within spawn radius)
        float distance = (float)random.NextDouble() * SpawnRadius;

        // Calculate position
        float x = Mathf.Cos(angle) * distance;
        float z = Mathf.Sin(angle) * distance;

        // Set height
        float y = SpawnHeight + (float)random.NextDouble() * 10.0f - 5.0f;

        // Set position
        bird.GlobalPosition = new Vector3(x, y, z);
    }
}
