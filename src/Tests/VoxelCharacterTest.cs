using Godot;
using System;
using CubeGen.Player;
using CubeGen.Player.CharacterParts;

public partial class VoxelCharacterTest : Node3D
{
    private VoxelCharacter _character;
    private Camera3D _camera;
    private Label _infoLabel;
    private float _rotationY = 0.0f;

    // Character customization properties
    private Color _skinColor = new Color(0.9f, 0.75f, 0.65f);
    private Color _hairColor = new Color(0.6f, 0.4f, 0.2f);
    private Color _shirtColor = new Color(0.2f, 0.4f, 0.8f);
    private Color _pantsColor = new Color(0.3f, 0.3f, 0.7f);
    private HairStyle _hairStyle = HairStyle.Short;

    // Animation properties
    private bool _isWalking = false;
    private bool _isJumping = false;
    private float _walkCycle = 0.0f;
    private float _walkSpeed = 5.0f;

    public override void _Ready()
    {
        // Create character
        _character = new VoxelCharacter();
        _character.Name = "VoxelCharacter";
        AddChild(_character);

        // Create camera
        _camera = new Camera3D();
        _camera.Name = "Camera";
        _camera.Position = new Vector3(0, 1.0f, 3.0f);
        _camera.LookAt(new Vector3(0, 0.5f, 0));
        AddChild(_camera);

        // Create UI
        CreateUI();

        // Create environment
        CreateEnvironment();
    }

    private void CreateUI()
    {
        // Create canvas layer
        var canvasLayer = new CanvasLayer();
        canvasLayer.Name = "UI";
        AddChild(canvasLayer);

        // Create info label
        _infoLabel = new Label();
        _infoLabel.Name = "InfoLabel";
        _infoLabel.Text = "Voxel Character Test\n\nControls:\nW/A/S/D - Toggle walking\nSpace - Toggle jumping\nR - Rotate character\n1-5 - Change hair style\nQ/E - Cycle shirt color\nZ/X - Cycle pants color\nC/V - Cycle skin color\nB/N - Cycle hair color";
        _infoLabel.Position = new Vector2(20, 20);
        _infoLabel.Size = new Vector2(400, 200);
        canvasLayer.AddChild(_infoLabel);
    }

    private void CreateEnvironment()
    {
        // Create a floor
        var floor = new MeshInstance3D();
        floor.Name = "Floor";
        var planeMesh = new PlaneMesh();
        planeMesh.Size = new Vector2(10, 10);
        floor.Mesh = planeMesh;

        // Create material for floor
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(0.3f, 0.3f, 0.3f);
        floor.MaterialOverride = material;

        // Position floor
        floor.Position = new Vector3(0, -0.5f, 0);

        // Add floor to scene
        AddChild(floor);

        // Create directional light
        var light = new DirectionalLight3D();
        light.Name = "DirectionalLight";
        light.Position = new Vector3(0, 5, 0);
        light.RotationDegrees = new Vector3(-45, 45, 0);
        light.ShadowEnabled = true;
        AddChild(light);
    }

    public override void _Process(double delta)
    {
        // Update character animation
        if (_isWalking)
        {
            _walkCycle += (float)delta * _walkSpeed;

            // Keep walk cycle between 0 and 2π
            if (_walkCycle > Mathf.Pi * 2)
            {
                _walkCycle -= Mathf.Pi * 2;
            }
        }

        // Update character animation
        _character.UpdateAnimation(delta, _isWalking, _isJumping, Vector3.Forward);
    }

    public override void _Input(InputEvent @event)
    {
        // Toggle walking
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            switch (keyEvent.Keycode)
            {
                case Key.W:
                case Key.A:
                case Key.S:
                case Key.D:
                    _isWalking = !_isWalking;
                    break;

                case Key.Space:
                    _isJumping = !_isJumping;
                    break;

                case Key.R:
                    // Rotate character
                    _rotationY += Mathf.Pi / 4;
                    _character.RotationDegrees = new Vector3(0, _rotationY * (180.0f / Mathf.Pi), 0);
                    break;

                // Hair style
                case Key.Key1:
                    _hairStyle = HairStyle.None;
                    UpdateCharacter();
                    break;
                case Key.Key2:
                    _hairStyle = HairStyle.Short;
                    UpdateCharacter();
                    break;
                case Key.Key3:
                    _hairStyle = HairStyle.Long;
                    UpdateCharacter();
                    break;
                case Key.Key4:
                    _hairStyle = HairStyle.Mohawk;
                    UpdateCharacter();
                    break;
                case Key.Key5:
                    _hairStyle = HairStyle.Bald;
                    UpdateCharacter();
                    break;

                // Shirt color
                case Key.Q:
                    CycleColor(ref _shirtColor, -0.2f);
                    UpdateCharacter();
                    break;
                case Key.E:
                    CycleColor(ref _shirtColor, 0.2f);
                    UpdateCharacter();
                    break;

                // Pants color
                case Key.Z:
                    CycleColor(ref _pantsColor, -0.2f);
                    UpdateCharacter();
                    break;
                case Key.X:
                    CycleColor(ref _pantsColor, 0.2f);
                    UpdateCharacter();
                    break;

                // Skin color
                case Key.C:
                    CycleColor(ref _skinColor, -0.1f);
                    UpdateCharacter();
                    break;
                case Key.V:
                    CycleColor(ref _skinColor, 0.1f);
                    UpdateCharacter();
                    break;

                // Hair color
                case Key.B:
                    CycleColor(ref _hairColor, -0.2f);
                    UpdateCharacter();
                    break;
                case Key.N:
                    CycleColor(ref _hairColor, 0.2f);
                    UpdateCharacter();
                    break;
            }
        }
    }

    private void CycleColor(ref Color color, float amount)
    {
        // Simple color cycling by adjusting RGB values
        float r = color.R;
        float g = color.G;
        float b = color.B;

        // Shift colors
        if (amount > 0)
        {
            float temp = r;
            r = g;
            g = b;
            b = temp;
        }
        else
        {
            float temp = b;
            b = g;
            g = r;
            r = temp;
        }

        // Apply a small random variation
        r += (float)GD.Randf() * 0.1f - 0.05f;
        g += (float)GD.Randf() * 0.1f - 0.05f;
        b += (float)GD.Randf() * 0.1f - 0.05f;

        // Clamp values
        r = Mathf.Clamp(r, 0.0f, 1.0f);
        g = Mathf.Clamp(g, 0.0f, 1.0f);
        b = Mathf.Clamp(b, 0.0f, 1.0f);

        // Create new color
        color = new Color(r, g, b);
    }

    private void UpdateCharacter()
    {
        _character.Customize(_skinColor, _hairColor, _shirtColor, _pantsColor, _hairStyle);
    }
}
