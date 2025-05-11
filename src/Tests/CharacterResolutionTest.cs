using Godot;
using System;
using CubeGen.Player;
using CubeGen.Player.CharacterParts;

public partial class CharacterResolutionTest : Node3D
{
    private Player _player;
    private Button _doubleResolutionButton;
    private Button _regenerateButton;
    private Label _instructions;

    public override void _Ready()
    {
        // Get references to nodes
        _player = GetNode<Player>("Player");
        _doubleResolutionButton = GetNode<Button>("DoubleResolutionButton");
        _regenerateButton = GetNode<Button>("RegenerateButton");
        _instructions = GetNode<Label>("Instructions");

        // Connect button signals
        _doubleResolutionButton.Pressed += OnDoubleResolutionButtonPressed;
        _regenerateButton.Pressed += OnRegenerateButtonPressed;

        // Set up camera controls
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void OnDoubleResolutionButtonPressed()
    {
        // Recreate the character with the new voxel size
        _player.RecreateCharacterWithNewVoxelSize();
        
        // Update instructions
        _instructions.Text = "Character recreated with doubled resolution (1/16 voxel size).\n" +
                            "The character model now has more detailed voxels.";
    }
    
    private void OnRegenerateButtonPressed()
    {
        // Regenerate the character meshes
        _player.RegenerateCharacterMeshes();
        
        // Update instructions
        _instructions.Text = "Character meshes regenerated with current voxel size.";
    }

    public override void _Process(double delta)
    {
        // Allow exiting with Escape key
        if (Input.IsActionJustPressed("ui_cancel"))
        {
            GetTree().Quit();
        }
    }
}
