using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;
using CubeGen.World.Generation;

public partial class BiomeMapVisualizer : Node3D
{
    [Export] public int MapSize { get; set; } = 400; // Increased for better region visualization
    [Export] public float TileSize { get; set; } = 2.5f; // Smaller tiles for higher resolution

    private Dictionary<BiomeType, Color> _biomeColors = new Dictionary<BiomeType, Color>();
    private MeshInstance3D _mapMesh;

    public override void _Ready()
    {
        // Initialize biome colors
        InitializeBiomeColors();

        // Create the map mesh
        CreateMapMesh();

        // Add region boundary visualization
        CreateRegionBoundaries();
    }

    // Method to create visual indicators of region boundaries
    private void CreateRegionBoundaries()
    {
        // Create a new node to hold all boundary lines
        Node3D boundaryContainer = new Node3D();
        boundaryContainer.Name = "RegionBoundaries";
        AddChild(boundaryContainer);

        // Calculate half size for centering
        float halfSize = MapSize * TileSize / 2.0f;

        // Sample the map at a lower resolution to find boundaries
        int sampleStep = 1; // Check every tile for more detailed boundaries

        for (int x = 0; x < MapSize - sampleStep; x += sampleStep)
        {
            for (int z = 0; z < MapSize - sampleStep; z += sampleStep)
            {
                // Calculate world positions
                float worldX = x * TileSize - halfSize;
                float worldZ = z * TileSize - halfSize;

                // Get biome at current position
                BiomeType currentBiome = WorldGenerator.GetBiomeType((int)worldX, (int)worldZ);

                // Check right neighbor
                BiomeType rightBiome = WorldGenerator.GetBiomeType((int)(worldX + sampleStep * TileSize), (int)worldZ);

                // Check bottom neighbor
                BiomeType bottomBiome = WorldGenerator.GetBiomeType((int)worldX, (int)(worldZ + sampleStep * TileSize));

                // If there's a biome change, draw a boundary line
                if (currentBiome != rightBiome)
                {
                    CreateBoundaryLine(
                        boundaryContainer,
                        new Vector3(worldX + sampleStep * TileSize, 0.1f, worldZ),
                        new Vector3(worldX + sampleStep * TileSize, 0.1f, worldZ + sampleStep * TileSize)
                    );
                }

                if (currentBiome != bottomBiome)
                {
                    CreateBoundaryLine(
                        boundaryContainer,
                        new Vector3(worldX, 0.1f, worldZ + sampleStep * TileSize),
                        new Vector3(worldX + sampleStep * TileSize, 0.1f, worldZ + sampleStep * TileSize)
                    );
                }
            }
        }
    }

    // Helper method to create a boundary line
    private void CreateBoundaryLine(Node3D parent, Vector3 start, Vector3 end)
    {
        // Create a mesh for the line
        ImmediateMesh lineMesh = new ImmediateMesh();

        // Create a mesh instance
        MeshInstance3D lineInstance = new MeshInstance3D();
        lineInstance.Mesh = lineMesh;

        // Create a simple material for the line
        StandardMaterial3D material = new StandardMaterial3D();
        // Use a bright color that will stand out against the biome colors
        material.AlbedoColor = new Color(0.1f, 0.1f, 0.1f); // Dark gray for boundaries

        // Make the lines more visible with unshaded mode
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

        // Set material
        lineInstance.MaterialOverride = material;

        // Draw the line as a thin quad instead of a line for better visibility
        lineMesh.ClearSurfaces();
        lineMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

        // Calculate perpendicular vector for width
        Vector3 direction = end - start;
        Vector3 up = new Vector3(0, 1, 0);
        Vector3 perpendicular = direction.Cross(up).Normalized() * 0.1f; // Width of 0.1 units for thinner lines

        // Create quad vertices (two triangles)
        Vector3 v1 = start + perpendicular;
        Vector3 v2 = start - perpendicular;
        Vector3 v3 = end + perpendicular;
        Vector3 v4 = end - perpendicular;

        // First triangle
        lineMesh.SurfaceAddVertex(v1);
        lineMesh.SurfaceAddVertex(v2);
        lineMesh.SurfaceAddVertex(v3);

        // Second triangle
        lineMesh.SurfaceAddVertex(v2);
        lineMesh.SurfaceAddVertex(v4);
        lineMesh.SurfaceAddVertex(v3);

        lineMesh.SurfaceEnd();

        // Add to parent
        parent.AddChild(lineInstance);
    }

    private void InitializeBiomeColors()
    {
        // Use more vibrant colors for better region visualization
        _biomeColors[BiomeType.Plains] = new Color(0.45f, 0.85f, 0.3f);      // Brighter green
        _biomeColors[BiomeType.Forest] = new Color(0.15f, 0.55f, 0.15f);     // Darker green
        _biomeColors[BiomeType.Desert] = new Color(0.98f, 0.85f, 0.45f);     // Brighter sand
        _biomeColors[BiomeType.Mountains] = new Color(0.45f, 0.45f, 0.65f);  // Blueish gray
        _biomeColors[BiomeType.Tundra] = new Color(0.98f, 0.98f, 1.0f);      // Bright white
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
