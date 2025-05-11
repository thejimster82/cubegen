using Godot;
using System;
using CubeGen.Player;
using CubeGen.Player.CharacterParts;

public partial class CharacterFaceCullingTest : Node3D
{
    private Player _player;
    private Button _fixButton;
    private Button _toggleCullingButton;
    private Label _instructions;
    private bool _isFrontCulling = true; // Start with front culling

    public override void _Ready()
    {
        // Get references to nodes
        _player = GetNode<Player>("Player");
        _fixButton = GetNode<Button>("FixButton");
        _toggleCullingButton = GetNode<Button>("ToggleCullingButton");
        _instructions = GetNode<Label>("Instructions");

        // Connect button signals
        _fixButton.Pressed += OnFixButtonPressed;
        _toggleCullingButton.Pressed += OnToggleCullingPressed;

        // Set up camera controls
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void OnFixButtonPressed()
    {
        // Call the method to regenerate character meshes
        _player.RegenerateCharacterMeshes();

        // Update instructions
        _instructions.Text = "Character meshes regenerated with " +
                            (_isFrontCulling ? "front" : "back") + " face culling.\n" +
                            "Current mode: " + (_isFrontCulling ? "Showing inside faces" : "Showing outside faces");
    }

    private void OnToggleCullingPressed()
    {
        // Toggle between front and back culling
        _isFrontCulling = !_isFrontCulling;

        // Find the VoxelCharacter
        var voxelCharacter = _player.GetNode<VoxelCharacter>("VoxelCharacter");
        if (voxelCharacter != null)
        {
            // Update all body parts with the new culling mode
            foreach (var child in voxelCharacter.GetChildren())
            {
                if (child is VoxelBodyPart bodyPart)
                {
                    // Get the mesh instance
                    var meshInstance = bodyPart.GetNode<MeshInstance3D>("MeshInstance3D");
                    if (meshInstance != null && meshInstance.Mesh != null)
                    {
                        // Get the material
                        var material = meshInstance.Mesh.SurfaceGetMaterial(0) as StandardMaterial3D;
                        if (material != null)
                        {
                            // Set the culling mode
                            material.CullMode = _isFrontCulling ?
                                BaseMaterial3D.CullModeEnum.Front :
                                BaseMaterial3D.CullModeEnum.Back;

                            // Update the material
                            meshInstance.Mesh.SurfaceSetMaterial(0, material);
                        }
                    }
                }
            }

            // Update instructions
            _instructions.Text = "Culling mode changed to " +
                                (_isFrontCulling ? "front" : "back") + " face culling.\n" +
                                "Current mode: " + (_isFrontCulling ? "Showing inside faces" : "Showing outside faces");
        }
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
