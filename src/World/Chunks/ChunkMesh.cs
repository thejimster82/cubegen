using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

public partial class ChunkMesh : Node3D
{
    private MeshInstance3D _meshInstance;
    private StaticBody3D _staticBody;
    private CollisionShape3D _collisionShape;

    // Material for different voxel types
    [Export] public Material DefaultMaterial { get; set; }

    // Dictionary to store materials for different voxel types
    private Dictionary<VoxelType, Material> _materials = new Dictionary<VoxelType, Material>();

    public override void _Ready()
    {
        // Create mesh instance
        _meshInstance = new MeshInstance3D();
        AddChild(_meshInstance);

        // Create static body for collision
        _staticBody = new StaticBody3D();
        AddChild(_staticBody);

        // Create collision shape
        _collisionShape = new CollisionShape3D();
        _staticBody.AddChild(_collisionShape);

        // Initialize biome materials
        BiomeMaterials.Initialize();

        // Initialize default materials dictionary
        foreach (VoxelType type in Enum.GetValues(typeof(VoxelType)))
        {
            if (type != VoxelType.Air)
            {
                if (DefaultMaterial != null)
                {
                    _materials[type] = DefaultMaterial;
                }
                else
                {
                    // Use the Plains biome as default if no material is provided
                    _materials[type] = BiomeMaterials.GetMaterial(BiomeType.Plains, type);
                }
            }
        }

        GD.Print("ChunkMesh initialized with biome materials");
    }

    public void GenerateMesh(VoxelChunk chunk)
    {
        // Create a dictionary to group vertices by biome and voxel type
        Dictionary<BiomeType, Dictionary<VoxelType, List<MeshData>>> meshDataByBiomeAndType = new Dictionary<BiomeType, Dictionary<VoxelType, List<MeshData>>>();

        // Generate mesh data
        for (int x = 0; x < chunk.Size; x++)
        {
            for (int y = 0; y < chunk.Height; y++)
            {
                for (int z = 0; z < chunk.Size; z++)
                {
                    VoxelType voxelType = chunk.GetVoxel(x, y, z);

                    // Skip air voxels
                    if (voxelType == VoxelType.Air)
                        continue;

                    // Get biome type for this position
                    int worldX = chunk.Position.X * chunk.Size + x;
                    int worldZ = chunk.Position.Y * chunk.Size + z;
                    BiomeType biomeType = CubeGen.World.Generation.WorldGenerator.GetBiomeType(worldX, worldZ);

                    // Initialize dictionaries if needed
                    if (!meshDataByBiomeAndType.ContainsKey(biomeType))
                    {
                        meshDataByBiomeAndType[biomeType] = new Dictionary<VoxelType, List<MeshData>>();
                    }

                    if (!meshDataByBiomeAndType[biomeType].ContainsKey(voxelType))
                    {
                        meshDataByBiomeAndType[biomeType][voxelType] = new List<MeshData>();
                    }

                    // Create mesh data for this voxel
                    MeshData meshData = new MeshData();

                    // Check each face and add to mesh data
                    AddVoxelFaces(chunk, x, y, z, voxelType, meshData.Vertices, meshData.Normals, meshData.UVs, meshData.Indices);

                    // Add mesh data to the appropriate list if it has any vertices
                    if (meshData.Vertices.Count > 0)
                    {
                        meshDataByBiomeAndType[biomeType][voxelType].Add(meshData);
                    }
                }
            }
        }

        // Create a new ArrayMesh
        ArrayMesh mesh = new ArrayMesh();

        // Create surfaces for each biome and voxel type
        int surfaceIndex = 0;

        // Variables for collision shape
        List<Vector3> allVertices = new List<Vector3>();
        List<int> allIndices = new List<int>();
        int globalVertexOffset = 0;

        foreach (var biomeEntry in meshDataByBiomeAndType)
        {
            BiomeType biomeType = biomeEntry.Key;

            foreach (var typeEntry in biomeEntry.Value)
            {
                VoxelType voxelType = typeEntry.Key;
                List<MeshData> meshDataList = typeEntry.Value;

                if (meshDataList.Count == 0)
                    continue;

                // Combine all mesh data for this biome and voxel type
                List<Vector3> vertices = new List<Vector3>();
                List<Vector3> normals = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> indices = new List<int>();

                int vertexOffset = 0;
                foreach (MeshData meshData in meshDataList)
                {
                    vertices.AddRange(meshData.Vertices);
                    normals.AddRange(meshData.Normals);
                    uvs.AddRange(meshData.UVs);

                    // Adjust indices to account for vertex offset
                    foreach (int index in meshData.Indices)
                    {
                        indices.Add(index + vertexOffset);
                    }

                    vertexOffset += meshData.Vertices.Count;
                }

                // Create surface arrays
                Godot.Collections.Array arrays = new Godot.Collections.Array();
                arrays.Resize((int)Mesh.ArrayType.Max);
                arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
                arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
                arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
                arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

                // Create surface
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

                // Set material based on biome and voxel type
                Material material = BiomeMaterials.GetMaterial(biomeType, voxelType);
                mesh.SurfaceSetMaterial(surfaceIndex, material);

                // Add vertices and indices to collision data
                allVertices.AddRange(vertices);

                // Adjust indices for global vertex offset
                foreach (int index in indices)
                {
                    allIndices.Add(index + globalVertexOffset);
                }

                globalVertexOffset += vertices.Count;
                surfaceIndex++;
            }
        }

        // Set mesh
        _meshInstance.Mesh = mesh;

        // Create collision shape if we have vertices
        if (allVertices.Count > 0)
        {
            ConcavePolygonShape3D collisionShape = new ConcavePolygonShape3D();
            List<Vector3> faces = new List<Vector3>();

            // Convert indices to faces
            for (int i = 0; i < allIndices.Count; i += 3)
            {
                if (i + 2 < allIndices.Count &&
                    allIndices[i] < allVertices.Count &&
                    allIndices[i + 1] < allVertices.Count &&
                    allIndices[i + 2] < allVertices.Count)
                {
                    faces.Add(allVertices[allIndices[i]]);
                    faces.Add(allVertices[allIndices[i + 1]]);
                    faces.Add(allVertices[allIndices[i + 2]]);
                }
            }

            if (faces.Count > 0)
            {
                collisionShape.Data = faces.ToArray();
                _collisionShape.Shape = collisionShape;
            }
            else
            {
                _collisionShape.Shape = null;
            }
        }
        else
        {
            // No voxels to render
            _meshInstance.Mesh = null;
            _collisionShape.Shape = null;
        }
    }

    private void AddVoxelFaces(VoxelChunk chunk, int x, int y, int z, VoxelType voxelType,
                              List<Vector3> vertices, List<Vector3> normals,
                              List<Vector2> uvs, List<int> indices)
    {
        // Check each of the 6 faces

        // Top face (Y+)
        if (y == chunk.Height - 1 || !chunk.IsVoxelSolid(x, y + 1, z))
        {
            AddFace(FaceDirection.Top, new Vector3(x, y, z), voxelType, vertices, normals, uvs, indices, chunk);
        }

        // Bottom face (Y-)
        if (y == 0 || !chunk.IsVoxelSolid(x, y - 1, z))
        {
            AddFace(FaceDirection.Bottom, new Vector3(x, y, z), voxelType, vertices, normals, uvs, indices, chunk);
        }

        // Front face (Z+)
        if (z == chunk.Size - 1 || !chunk.IsVoxelSolid(x, y, z + 1))
        {
            AddFace(FaceDirection.Front, new Vector3(x, y, z), voxelType, vertices, normals, uvs, indices, chunk);
        }

        // Back face (Z-)
        if (z == 0 || !chunk.IsVoxelSolid(x, y, z - 1))
        {
            AddFace(FaceDirection.Back, new Vector3(x, y, z), voxelType, vertices, normals, uvs, indices, chunk);
        }

        // Right face (X+)
        if (x == chunk.Size - 1 || !chunk.IsVoxelSolid(x + 1, y, z))
        {
            AddFace(FaceDirection.Right, new Vector3(x, y, z), voxelType, vertices, normals, uvs, indices, chunk);
        }

        // Left face (X-)
        if (x == 0 || !chunk.IsVoxelSolid(x - 1, y, z))
        {
            AddFace(FaceDirection.Left, new Vector3(x, y, z), voxelType, vertices, normals, uvs, indices, chunk);
        }
    }

    private void AddFace(FaceDirection direction, Vector3 position, VoxelType voxelType,
                        List<Vector3> vertices, List<Vector3> normals,
                        List<Vector2> uvs, List<int> indices, VoxelChunk chunk)
    {
        // Get current vertex count
        int vertexCount = vertices.Count;

        // Get UV coordinates based on voxel type
        Vector2[] faceUVs = GetUVsForVoxelType(voxelType, direction);

        // Add vertices, normals, and UVs based on face direction
        // Get scale from the chunk
        float scale = chunk.Scale;

        switch (direction)
        {
            case FaceDirection.Top:
                vertices.Add(new Vector3(0, 1, 0) * scale + position * scale);
                vertices.Add(new Vector3(1, 1, 0) * scale + position * scale);
                vertices.Add(new Vector3(1, 1, 1) * scale + position * scale);
                vertices.Add(new Vector3(0, 1, 1) * scale + position * scale);
                for (int i = 0; i < 4; i++) normals.Add(Vector3.Up);
                break;

            case FaceDirection.Bottom:
                vertices.Add(new Vector3(0, 0, 0) * scale + position * scale);
                vertices.Add(new Vector3(0, 0, 1) * scale + position * scale);
                vertices.Add(new Vector3(1, 0, 1) * scale + position * scale);
                vertices.Add(new Vector3(1, 0, 0) * scale + position * scale);
                for (int i = 0; i < 4; i++) normals.Add(Vector3.Down);
                break;

            case FaceDirection.Front:
                vertices.Add(new Vector3(0, 0, 1) * scale + position * scale);
                vertices.Add(new Vector3(0, 1, 1) * scale + position * scale);
                vertices.Add(new Vector3(1, 1, 1) * scale + position * scale);
                vertices.Add(new Vector3(1, 0, 1) * scale + position * scale);
                for (int i = 0; i < 4; i++) normals.Add(Vector3.Forward);
                break;

            case FaceDirection.Back:
                vertices.Add(new Vector3(0, 0, 0) * scale + position * scale);
                vertices.Add(new Vector3(1, 0, 0) * scale + position * scale);
                vertices.Add(new Vector3(1, 1, 0) * scale + position * scale);
                vertices.Add(new Vector3(0, 1, 0) * scale + position * scale);
                for (int i = 0; i < 4; i++) normals.Add(Vector3.Back);
                break;

            case FaceDirection.Right:
                vertices.Add(new Vector3(1, 0, 0) * scale + position * scale);
                vertices.Add(new Vector3(1, 0, 1) * scale + position * scale);
                vertices.Add(new Vector3(1, 1, 1) * scale + position * scale);
                vertices.Add(new Vector3(1, 1, 0) * scale + position * scale);
                for (int i = 0; i < 4; i++) normals.Add(Vector3.Right);
                break;

            case FaceDirection.Left:
                vertices.Add(new Vector3(0, 0, 0) * scale + position * scale);
                vertices.Add(new Vector3(0, 1, 0) * scale + position * scale);
                vertices.Add(new Vector3(0, 1, 1) * scale + position * scale);
                vertices.Add(new Vector3(0, 0, 1) * scale + position * scale);
                for (int i = 0; i < 4; i++) normals.Add(Vector3.Left);
                break;
        }

        // Add UVs
        for (int i = 0; i < 4; i++)
        {
            uvs.Add(faceUVs[i]);
        }

        // Add indices (two triangles to form a quad)
        indices.Add(vertexCount);
        indices.Add(vertexCount + 1);
        indices.Add(vertexCount + 2);

        indices.Add(vertexCount);
        indices.Add(vertexCount + 2);
        indices.Add(vertexCount + 3);
    }

    private Vector2[] GetUVsForVoxelType(VoxelType type, FaceDirection direction)
    {
        // In a real implementation, you'd have a texture atlas and return UVs based on voxel type and face
        // For now, we'll just return basic UVs
        return new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0)
        };
    }

    public void UpdateMesh(VoxelChunk chunk)
    {
        // Regenerate mesh
        GenerateMesh(chunk);
    }

    private enum FaceDirection
    {
        Top,
        Bottom,
        Front,
        Back,
        Right,
        Left
    }

    private class MeshData
    {
        public List<Vector3> Vertices { get; set; } = new List<Vector3>();
        public List<Vector3> Normals { get; set; } = new List<Vector3>();
        public List<Vector2> UVs { get; set; } = new List<Vector2>();
        public List<int> Indices { get; set; } = new List<int>();
    }
}
