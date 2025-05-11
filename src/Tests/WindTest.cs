using Godot;
using System;
using CubeGen.World.Common;
using CubeGen.World.Environment;
using CubeGen.World.Materials;

public partial class WindTest : Node3D
{
    [Export] public int NumGrassPatches { get; set; } = 20;
    [Export] public int NumFlowerPatches { get; set; } = 10;

    private WindSystem _windSystem;

    public override void _Ready()
    {
        // Initialize materials
        BiomeMaterials.Initialize();
        WindMaterials.Initialize();

        // Get the wind system
        _windSystem = GetNode<WindSystem>("WindSystem");

        // Create test grass and flower patches
        CreateTestPatches();

        // Add UI for wind controls
        CreateWindControls();
    }

    private void CreateTestPatches()
    {
        // Create a parent node for all patches
        Node3D patchesParent = new Node3D();
        patchesParent.Name = "Patches";
        AddChild(patchesParent);

        Random random = new Random();

        // Create grass patches
        for (int i = 0; i < NumGrassPatches; i++)
        {
            // Create a mesh instance for the grass
            MeshInstance3D grassMesh = new MeshInstance3D();
            grassMesh.Name = $"GrassPatch_{i}";

            // Create a simple cube mesh - make it taller and thinner for more visible wind effect
            BoxMesh boxMesh = new BoxMesh();
            boxMesh.Size = new Vector3(0.2f, 3.0f, 0.2f);
            grassMesh.Mesh = boxMesh;

            // Position randomly in a grid
            float x = (random.Next(10) - 5) * 2.0f;
            float z = (random.Next(10) - 5) * 2.0f;
            grassMesh.Position = new Vector3(x, 1.0f, z);

            // Apply wind material
            Material grassMaterial = WindMaterials.GetWindMaterial(BiomeType.ForestLands, VoxelType.TallGrass);
            grassMesh.MaterialOverride = grassMaterial;

            // Debug output
            if (i == 0) // Only for the first grass patch to avoid spam
            {
                GD.Print($"Applied grass material: {grassMaterial.GetType().Name}");
                if (grassMaterial is ShaderMaterial shaderMat)
                {
                    GD.Print($"  - Shader: {(shaderMat.Shader != null ? "Loaded" : "NULL")}");
                    GD.Print($"  - Wind strength: {shaderMat.GetShaderParameter("wind_strength")}");
                }
            }

            // Add to scene
            patchesParent.AddChild(grassMesh);
        }

        // Create flower patches
        for (int i = 0; i < NumFlowerPatches; i++)
        {
            // Create a mesh instance for the flower
            MeshInstance3D flowerMesh = new MeshInstance3D();
            flowerMesh.Name = $"FlowerPatch_{i}";

            // Create a simple cube mesh - make it taller and thinner for more visible wind effect
            BoxMesh boxMesh = new BoxMesh();
            boxMesh.Size = new Vector3(0.2f, 2.5f, 0.2f);
            flowerMesh.Mesh = boxMesh;

            // Position randomly in a grid
            float x = (random.Next(10) - 5) * 2.0f;
            float z = (random.Next(10) - 5) * 2.0f;
            flowerMesh.Position = new Vector3(x, 0.75f, z);

            // Apply wind material
            flowerMesh.MaterialOverride = WindMaterials.GetWindMaterial(BiomeType.ForestLands, VoxelType.Flower);

            // Add to scene
            patchesParent.AddChild(flowerMesh);
        }

        // Create a ground plane
        MeshInstance3D ground = new MeshInstance3D();
        ground.Name = "Ground";

        // Create a plane mesh
        PlaneMesh planeMesh = new PlaneMesh();
        planeMesh.Size = new Vector2(30.0f, 30.0f);
        ground.Mesh = planeMesh;

        // Create a material for the ground
        StandardMaterial3D groundMaterial = new StandardMaterial3D();
        groundMaterial.AlbedoColor = new Color(0.3f, 0.5f, 0.2f); // Green for grass
        ground.MaterialOverride = groundMaterial;

        // Add to scene
        patchesParent.AddChild(ground);
    }

    private void CreateWindControls()
    {
        // Create a control panel
        Control panel = new Control();
        panel.Name = "WindControls";
        panel.AnchorRight = 1.0f;
        panel.AnchorBottom = 1.0f;
        AddChild(panel);

        // Create labels and sliders for wind parameters
        CreateWindControl(panel, "Wind Strength", 0.0f, 1.0f, _windSystem.WindStrength, 20,
            (value) => _windSystem.WindStrength = value);

        CreateWindControl(panel, "Wind Gustiness", 0.0f, 1.0f, _windSystem.WindGustiness, 60,
            (value) => _windSystem.WindGustiness = value);

        CreateWindControl(panel, "Wind Speed", 0.1f, 3.0f, _windSystem.WindSpeed, 100,
            (value) => _windSystem.WindSpeed = value);
    }

    private void CreateWindControl(Control parent, string labelText, float min, float max, float value,
        float yPosition, Action<float> onValueChanged)
    {
        // Create label
        Label label = new Label();
        label.Text = $"{labelText}: {value:F2}";
        label.Position = new Vector2(20, yPosition);
        parent.AddChild(label);

        // Create slider
        HSlider slider = new HSlider();
        slider.MinValue = min;
        slider.MaxValue = max;
        slider.Value = value;
        slider.Position = new Vector2(200, yPosition);
        slider.Size = new Vector2(200, 20);
        parent.AddChild(slider);

        // Connect value changed signal
        slider.ValueChanged += (double newValue) =>
        {
            float floatValue = (float)newValue;
            onValueChanged(floatValue);
            label.Text = $"{labelText}: {floatValue:F2}";
        };
    }
}
