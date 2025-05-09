using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;
using CubeGen.World.Generation;

public partial class BiomeMapVisualizer : Node3D
{
    [Export] public int MapSize { get; set; } = 100;
    [Export] public float TileSize { get; set; } = 10.0f;

    private Dictionary<BiomeType, Color> _biomeColors = new Dictionary<BiomeType, Color>();
    private MeshInstance3D _mapMesh;

    public override void _Ready()
    {
        // Initialize biome colors
        InitializeBiomeColors();

        // Create the map mesh
        CreateMapMesh();
    }

    private void InitializeBiomeColors()
    {
        // Use colors that match the biome materials
        _biomeColors[BiomeType.ForestLands] = new Color(0.25f, 0.65f, 0.25f);
        _biomeColors[BiomeType.Desert] = new Color(0.95f, 0.85f, 0.5f);
        _biomeColors[BiomeType.Tundra] = new Color(0.95f, 0.97f, 1.0f);
        _biomeColors[BiomeType.Islands] = new Color(0.3f, 0.8f, 0.3f);
    }

    private void CreateMapMesh()
    {
        // Create a new ArrayMesh
        ArrayMesh mesh = new ArrayMesh();

        // Create surface arrays
        Godot.Collections.Array arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        // Create vertices, colors, and indices
        List<Vector3> vertices = new List<Vector3>();
        List<Color> colors = new List<Color>();
        List<int> indices = new List<int>();

        // Calculate half size for centering
        float halfSize = MapSize * TileSize / 2.0f;

        // Create a grid of quads
        for (int x = 0; x < MapSize; x++)
        {
            for (int z = 0; z < MapSize; z++)
            {
                // Calculate world position
                float worldX = x * TileSize - halfSize;
                float worldZ = z * TileSize - halfSize;

                // Get biome type for this position
                int sampleX = (int)(worldX);
                int sampleZ = (int)(worldZ);
                BiomeType biomeType = WorldGenerator.GetBiomeType(sampleX, sampleZ);

                // Get color for this biome
                Color biomeColor = _biomeColors[biomeType];

                // Add vertices for a quad (flat on XZ plane)
                int baseIndex = vertices.Count;

                vertices.Add(new Vector3(worldX, 0, worldZ));
                vertices.Add(new Vector3(worldX + TileSize, 0, worldZ));
                vertices.Add(new Vector3(worldX + TileSize, 0, worldZ + TileSize));
                vertices.Add(new Vector3(worldX, 0, worldZ + TileSize));

                // Add colors for each vertex
                colors.Add(biomeColor);
                colors.Add(biomeColor);
                colors.Add(biomeColor);
                colors.Add(biomeColor);

                // Add indices for two triangles to form a quad
                indices.Add(baseIndex);
                indices.Add(baseIndex + 1);
                indices.Add(baseIndex + 2);

                indices.Add(baseIndex);
                indices.Add(baseIndex + 2);
                indices.Add(baseIndex + 3);
            }
        }

        // Set arrays
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

        // Create surface
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        // Create mesh instance
        _mapMesh = new MeshInstance3D();
        _mapMesh.Mesh = mesh;

        // Create material
        StandardMaterial3D material = new StandardMaterial3D();
        material.VertexColorUseAsAlbedo = true;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

        // Set material
        _mapMesh.MaterialOverride = material;

        // Add to scene
        AddChild(_mapMesh);
    }
}
