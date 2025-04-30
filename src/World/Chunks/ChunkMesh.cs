using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

public partial class ChunkMesh : Node3D
{
    private MeshInstance3D _meshInstance;
    private StaticBody3D _staticBody;
    private CollisionShape3D _collisionShape;
    private VoxelChunk _chunk; // Store reference to the chunk

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

        // GD.Print("ChunkMesh initialized with biome materials");
    }

    public void GenerateMesh(VoxelChunk chunk)
    {
        // Store reference to the chunk
        _chunk = chunk;
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
                    AddVoxelFaces(chunk, x, y, z, voxelType, meshData);

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
                List<Color> colors = new List<Color>(); // For storing AO values as vertex colors

                int vertexOffset = 0;
                foreach (MeshData meshData in meshDataList)
                {
                    vertices.AddRange(meshData.Vertices);
                    normals.AddRange(meshData.Normals);
                    uvs.AddRange(meshData.UVs);

                    // Convert AO values to colors (grayscale)
                    foreach (float ao in meshData.AmbientOcclusion)
                    {
                        // Create a color with RGB all set to the AO value
                        // Use a more subtle effect by blending with white
                        // This makes the AO less pronounced but still visible
                        float blendedAO = 0.7f + (ao * 0.3f); // Scale AO to be between 0.7 and 1.0
                        colors.Add(new Color(blendedAO, blendedAO, blendedAO));
                    }

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
                arrays[(int)Mesh.ArrayType.Color] = colors.ToArray(); // Add vertex colors for AO
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

    private void AddVoxelFaces(VoxelChunk chunk, int x, int y, int z, VoxelType voxelType, MeshData meshData)
    {
        // Check each of the 6 faces

        // Top face (Y+)
        if (y == chunk.Height - 1 || !chunk.IsVoxelSolid(x, y + 1, z))
        {
            AddFace(FaceDirection.Top, new Vector3(x, y, z), voxelType, meshData, chunk);
        }

        // Bottom face (Y-)
        if (y == 0 || !chunk.IsVoxelSolid(x, y - 1, z))
        {
            AddFace(FaceDirection.Bottom, new Vector3(x, y, z), voxelType, meshData, chunk);
        }

        // Front face (Z+)
        // Always add a face at chunk boundaries or if the neighbor is not solid
        if (z == chunk.Size - 1 || !chunk.IsVoxelSolid(x, y, z + 1))
        {
            AddFace(FaceDirection.Front, new Vector3(x, y, z), voxelType, meshData, chunk);
        }

        // Back face (Z-)
        // Always add a face at chunk boundaries or if the neighbor is not solid
        if (z == 0 || !chunk.IsVoxelSolid(x, y, z - 1))
        {
            AddFace(FaceDirection.Back, new Vector3(x, y, z), voxelType, meshData, chunk);
        }

        // Right face (X+)
        // Always add a face at chunk boundaries or if the neighbor is not solid
        if (x == chunk.Size - 1 || !chunk.IsVoxelSolid(x + 1, y, z))
        {
            AddFace(FaceDirection.Right, new Vector3(x, y, z), voxelType, meshData, chunk);
        }

        // Left face (X-)
        // Always add a face at chunk boundaries or if the neighbor is not solid
        if (x == 0 || !chunk.IsVoxelSolid(x - 1, y, z))
        {
            AddFace(FaceDirection.Left, new Vector3(x, y, z), voxelType, meshData, chunk);
        }
    }

    private void AddFace(FaceDirection direction, Vector3 position, VoxelType voxelType,
                        MeshData meshData, VoxelChunk chunk)
    {
        // Get current vertex count
        int vertexCount = meshData.Vertices.Count;

        // Get UV coordinates based on voxel type
        Vector2[] faceUVs = GetUVsForVoxelType(voxelType, direction);

        // Add vertices, normals, and UVs based on face direction
        // Get scale from the chunk
        float scale = chunk.Scale;
        int x = (int)position.X;
        int y = (int)position.Y;
        int z = (int)position.Z;

        // Calculate AO values for each vertex of this face
        float[] aoValues = new float[4];
        for (int i = 0; i < 4; i++)
        {
            aoValues[i] = CalculateAO(chunk, x, y, z, direction, i);
        }

        // Check if we need to flip the triangulation to avoid AO artifacts
        bool flipTriangulation = false;

        // At chunk boundaries, use a consistent triangulation pattern
        // to avoid seams between chunks
        bool isAtBoundary = (x == 0 || x == chunk.Size - 1 || z == 0 || z == chunk.Size - 1);
        if (isAtBoundary)
        {
            // Use a deterministic pattern based on position
            // This ensures the same triangulation is used on both sides of a chunk boundary
            flipTriangulation = ((x + z) % 2 == 0);
        }
        else
        {
            // For interior faces, use the AO-based triangulation
            if (Math.Abs(aoValues[0] - aoValues[2]) < Math.Abs(aoValues[1] - aoValues[3]))
            {
                flipTriangulation = true;
            }
        }

        switch (direction)
        {
            case FaceDirection.Top:
                meshData.Vertices.Add(new Vector3(0, 1, 0) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(1, 1, 0) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(1, 1, 1) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(0, 1, 1) * scale + position * scale);
                for (int i = 0; i < 4; i++) meshData.Normals.Add(Vector3.Up);
                break;

            case FaceDirection.Bottom:
                meshData.Vertices.Add(new Vector3(0, 0, 0) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(0, 0, 1) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(1, 0, 1) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(1, 0, 0) * scale + position * scale);
                for (int i = 0; i < 4; i++) meshData.Normals.Add(Vector3.Down);
                break;

            case FaceDirection.Front:
                meshData.Vertices.Add(new Vector3(0, 0, 1) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(0, 1, 1) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(1, 1, 1) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(1, 0, 1) * scale + position * scale);
                for (int i = 0; i < 4; i++) meshData.Normals.Add(Vector3.Forward);
                break;

            case FaceDirection.Back:
                meshData.Vertices.Add(new Vector3(0, 0, 0) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(1, 0, 0) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(1, 1, 0) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(0, 1, 0) * scale + position * scale);
                for (int i = 0; i < 4; i++) meshData.Normals.Add(Vector3.Back);
                break;

            case FaceDirection.Right:
                meshData.Vertices.Add(new Vector3(1, 0, 0) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(1, 0, 1) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(1, 1, 1) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(1, 1, 0) * scale + position * scale);
                for (int i = 0; i < 4; i++) meshData.Normals.Add(Vector3.Right);
                break;

            case FaceDirection.Left:
                meshData.Vertices.Add(new Vector3(0, 0, 0) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(0, 1, 0) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(0, 1, 1) * scale + position * scale);
                meshData.Vertices.Add(new Vector3(0, 0, 1) * scale + position * scale);
                for (int i = 0; i < 4; i++) meshData.Normals.Add(Vector3.Left);
                break;
        }

        // Add UVs
        for (int i = 0; i < 4; i++)
        {
            meshData.UVs.Add(faceUVs[i]);
            meshData.AmbientOcclusion.Add(aoValues[i]);
        }

        // Add indices (two triangles to form a quad)
        // Use flipped triangulation if needed to avoid AO artifacts
        if (flipTriangulation)
        {
            // Flipped triangulation (v0-v3-v1, v1-v3-v2)
            meshData.Indices.Add(vertexCount);
            meshData.Indices.Add(vertexCount + 1);
            meshData.Indices.Add(vertexCount + 2);

            meshData.Indices.Add(vertexCount);
            meshData.Indices.Add(vertexCount + 2);
            meshData.Indices.Add(vertexCount + 3);
            //TODO: fix flipped triangulation faces
            // meshData.Indices.Add(vertexCount);
            // meshData.Indices.Add(vertexCount + 3);
            // meshData.Indices.Add(vertexCount + 1);

            // meshData.Indices.Add(vertexCount + 1);
            // meshData.Indices.Add(vertexCount + 3);
            // meshData.Indices.Add(vertexCount + 2);
        }
        else
        {
            // Standard triangulation (v0-v1-v2, v0-v2-v3)
            meshData.Indices.Add(vertexCount);
            meshData.Indices.Add(vertexCount + 1);
            meshData.Indices.Add(vertexCount + 2);

            meshData.Indices.Add(vertexCount);
            meshData.Indices.Add(vertexCount + 2);
            meshData.Indices.Add(vertexCount + 3);
        }
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

    private float CalculateAO(VoxelChunk chunk, int x, int y, int z, FaceDirection faceDirection, int vertexIndex)
    {
        // Define the side and corner voxels to check based on face direction and vertex index
        Vector3I side1, side2, corner;

        // Determine which neighboring voxels to check based on face direction and vertex index
        switch (faceDirection)
        {
            case FaceDirection.Top:
                switch (vertexIndex)
                {
                    case 0: // Bottom-left vertex (0,1,0)
                        side1 = new Vector3I(x - 1, y, z);     // Left
                        side2 = new Vector3I(x, y, z - 1);     // Back
                        corner = new Vector3I(x - 1, y, z - 1); // Left-Back
                        break;
                    case 1: // Bottom-right vertex (1,1,0)
                        side1 = new Vector3I(x + 1, y, z);     // Right
                        side2 = new Vector3I(x, y, z - 1);     // Back
                        corner = new Vector3I(x + 1, y, z - 1); // Right-Back
                        break;
                    case 2: // Top-right vertex (1,1,1)
                        side1 = new Vector3I(x + 1, y, z);     // Right
                        side2 = new Vector3I(x, y, z + 1);     // Front
                        corner = new Vector3I(x + 1, y, z + 1); // Right-Front
                        break;
                    case 3: // Top-left vertex (0,1,1)
                        side1 = new Vector3I(x - 1, y, z);     // Left
                        side2 = new Vector3I(x, y, z + 1);     // Front
                        corner = new Vector3I(x - 1, y, z + 1); // Left-Front
                        break;
                    default:
                        return 1.0f;
                }
                break;

            case FaceDirection.Bottom:
                switch (vertexIndex)
                {
                    case 0: // Bottom-left vertex (0,0,0)
                        side1 = new Vector3I(x - 1, y, z);     // Left
                        side2 = new Vector3I(x, y, z - 1);     // Back
                        corner = new Vector3I(x - 1, y, z - 1); // Left-Back
                        break;
                    case 1: // Bottom-right vertex (0,0,1)
                        side1 = new Vector3I(x - 1, y, z);     // Left
                        side2 = new Vector3I(x, y, z + 1);     // Front
                        corner = new Vector3I(x - 1, y, z + 1); // Left-Front
                        break;
                    case 2: // Top-right vertex (1,0,1)
                        side1 = new Vector3I(x + 1, y, z);     // Right
                        side2 = new Vector3I(x, y, z + 1);     // Front
                        corner = new Vector3I(x + 1, y, z + 1); // Right-Front
                        break;
                    case 3: // Top-left vertex (1,0,0)
                        side1 = new Vector3I(x + 1, y, z);     // Right
                        side2 = new Vector3I(x, y, z - 1);     // Back
                        corner = new Vector3I(x + 1, y, z - 1); // Right-Back
                        break;
                    default:
                        return 1.0f;
                }
                break;

            case FaceDirection.Front:
                switch (vertexIndex)
                {
                    case 0: // Bottom-left vertex (0,0,1)
                        side1 = new Vector3I(x - 1, y, z);     // Left
                        side2 = new Vector3I(x, y - 1, z);     // Bottom
                        corner = new Vector3I(x - 1, y - 1, z); // Left-Bottom
                        break;
                    case 1: // Top-left vertex (0,1,1)
                        side1 = new Vector3I(x - 1, y, z);     // Left
                        side2 = new Vector3I(x, y + 1, z);     // Top
                        corner = new Vector3I(x - 1, y + 1, z); // Left-Top
                        break;
                    case 2: // Top-right vertex (1,1,1)
                        side1 = new Vector3I(x + 1, y, z);     // Right
                        side2 = new Vector3I(x, y + 1, z);     // Top
                        corner = new Vector3I(x + 1, y + 1, z); // Right-Top
                        break;
                    case 3: // Bottom-right vertex (1,0,1)
                        side1 = new Vector3I(x + 1, y, z);     // Right
                        side2 = new Vector3I(x, y - 1, z);     // Bottom
                        corner = new Vector3I(x + 1, y - 1, z); // Right-Bottom
                        break;
                    default:
                        return 1.0f;
                }
                break;

            case FaceDirection.Back:
                switch (vertexIndex)
                {
                    case 0: // Bottom-left vertex (0,0,0)
                        side1 = new Vector3I(x - 1, y, z);     // Left
                        side2 = new Vector3I(x, y - 1, z);     // Bottom
                        corner = new Vector3I(x - 1, y - 1, z); // Left-Bottom
                        break;
                    case 1: // Bottom-right vertex (1,0,0)
                        side1 = new Vector3I(x + 1, y, z);     // Right
                        side2 = new Vector3I(x, y - 1, z);     // Bottom
                        corner = new Vector3I(x + 1, y - 1, z); // Right-Bottom
                        break;
                    case 2: // Top-right vertex (1,1,0)
                        side1 = new Vector3I(x + 1, y, z);     // Right
                        side2 = new Vector3I(x, y + 1, z);     // Top
                        corner = new Vector3I(x + 1, y + 1, z); // Right-Top
                        break;
                    case 3: // Top-left vertex (0,1,0)
                        side1 = new Vector3I(x - 1, y, z);     // Left
                        side2 = new Vector3I(x, y + 1, z);     // Top
                        corner = new Vector3I(x - 1, y + 1, z); // Left-Top
                        break;
                    default:
                        return 1.0f;
                }
                break;

            case FaceDirection.Right:
                switch (vertexIndex)
                {
                    case 0: // Bottom-left vertex (1,0,0)
                        side1 = new Vector3I(x, y - 1, z);     // Bottom
                        side2 = new Vector3I(x, y, z - 1);     // Back
                        corner = new Vector3I(x, y - 1, z - 1); // Bottom-Back
                        break;
                    case 1: // Bottom-right vertex (1,0,1)
                        side1 = new Vector3I(x, y - 1, z);     // Bottom
                        side2 = new Vector3I(x, y, z + 1);     // Front
                        corner = new Vector3I(x, y - 1, z + 1); // Bottom-Front
                        break;
                    case 2: // Top-right vertex (1,1,1)
                        side1 = new Vector3I(x, y + 1, z);     // Top
                        side2 = new Vector3I(x, y, z + 1);     // Front
                        corner = new Vector3I(x, y + 1, z + 1); // Top-Front
                        break;
                    case 3: // Top-left vertex (1,1,0)
                        side1 = new Vector3I(x, y + 1, z);     // Top
                        side2 = new Vector3I(x, y, z - 1);     // Back
                        corner = new Vector3I(x, y + 1, z - 1); // Top-Back
                        break;
                    default:
                        return 1.0f;
                }
                break;

            case FaceDirection.Left:
                switch (vertexIndex)
                {
                    case 0: // Bottom-left vertex (0,0,0)
                        side1 = new Vector3I(x, y - 1, z);     // Bottom
                        side2 = new Vector3I(x, y, z - 1);     // Back
                        corner = new Vector3I(x, y - 1, z - 1); // Bottom-Back
                        break;
                    case 1: // Top-left vertex (0,1,0)
                        side1 = new Vector3I(x, y + 1, z);     // Top
                        side2 = new Vector3I(x, y, z - 1);     // Back
                        corner = new Vector3I(x, y + 1, z - 1); // Top-Back
                        break;
                    case 2: // Top-right vertex (0,1,1)
                        side1 = new Vector3I(x, y + 1, z);     // Top
                        side2 = new Vector3I(x, y, z + 1);     // Front
                        corner = new Vector3I(x, y + 1, z + 1); // Top-Front
                        break;
                    case 3: // Bottom-right vertex (0,0,1)
                        side1 = new Vector3I(x, y - 1, z);     // Bottom
                        side2 = new Vector3I(x, y, z + 1);     // Front
                        corner = new Vector3I(x, y - 1, z + 1); // Bottom-Front
                        break;
                    default:
                        return 1.0f;
                }
                break;

            default:
                return 1.0f;
        }

        // Convert to world coordinates for consistent lookup across chunk boundaries
        int worldX1 = chunk.Position.X * chunk.Size + side1.X;
        int worldY1 = side1.Y;
        int worldZ1 = chunk.Position.Y * chunk.Size + side1.Z;

        int worldX2 = chunk.Position.X * chunk.Size + side2.X;
        int worldY2 = side2.Y;
        int worldZ2 = chunk.Position.Y * chunk.Size + side2.Z;

        int worldXC = chunk.Position.X * chunk.Size + corner.X;
        int worldYC = corner.Y;
        int worldZC = chunk.Position.Y * chunk.Size + corner.Z;

        // Use the World's IsVoxelSolid method to check across chunk boundaries
        bool side1Solid = false;
        bool side2Solid = false;
        bool cornerSolid = false;

        // Special case for Y bounds
        if (worldY1 < 0) side1Solid = true;
        else if (worldY1 >= chunk.Height) side1Solid = false;
        else if (IsInBounds(chunk, side1.X, side1.Y, side1.Z))
            side1Solid = chunk.GetVoxel(side1.X, side1.Y, side1.Z) != VoxelType.Air && chunk.GetVoxel(side1.X, side1.Y, side1.Z) != VoxelType.Water;

        if (worldY2 < 0) side2Solid = true;
        else if (worldY2 >= chunk.Height) side2Solid = false;
        else if (IsInBounds(chunk, side2.X, side2.Y, side2.Z))
            side2Solid = chunk.GetVoxel(side2.X, side2.Y, side2.Z) != VoxelType.Air && chunk.GetVoxel(side2.X, side2.Y, side2.Z) != VoxelType.Water;

        if (worldYC < 0) cornerSolid = true;
        else if (worldYC >= chunk.Height) cornerSolid = false;
        else if (IsInBounds(chunk, corner.X, corner.Y, corner.Z))
            cornerSolid = chunk.GetVoxel(corner.X, corner.Y, corner.Z) != VoxelType.Air && chunk.GetVoxel(corner.X, corner.Y, corner.Z) != VoxelType.Water;

        // For chunk boundaries, we'll still calculate AO but with a more consistent approach
        bool isAtBoundary = (x == 0 || x == chunk.Size - 1 || z == 0 || z == chunk.Size - 1);
        if (isAtBoundary)
        {
            // Calculate occlusion level based on what we can see
            int boundaryOcclusionLevel = 0;

            // Count solid neighbors
            if (side1Solid) boundaryOcclusionLevel++;
            if (side2Solid) boundaryOcclusionLevel++;
            if (cornerSolid) boundaryOcclusionLevel++;

            // Use a more subtle AO effect at boundaries
            switch (boundaryOcclusionLevel)
            {
                case 0: return 1.0f;      // No occlusion
                case 1: return 0.9f;      // Slight occlusion
                case 2: return 0.8f;      // Medium occlusion
                case 3: return 0.7f;      // Maximum occlusion at boundaries
                default: return 1.0f;
            }
        }

        // Calculate occlusion level for non-boundary vertices
        int occlusionLevel = 0;

        // If both sides are solid, this is a strong occlusion case
        if (side1Solid && side2Solid)
        {
            occlusionLevel = 3; // Maximum occlusion
        }
        else
        {
            // Add occlusion for each solid neighbor
            if (side1Solid) occlusionLevel++;
            if (side2Solid) occlusionLevel++;
            if (cornerSolid && !(side1Solid && side2Solid)) occlusionLevel++; // Only count corner if not already in a corner
        }

        // Map occlusion level to AO value (higher value = less occlusion)
        float aoValue = 1.0f;
        switch (occlusionLevel)
        {
            case 1: aoValue = 0.85f; break; // One blocker - slightly less dark (was 0.8f)
            case 2: aoValue = 0.7f; break; // Two blockers - slightly less dark (was 0.65f)
            case 3: aoValue = 0.55f; break; // Three blockers or corner case - slightly less dark (was 0.5f)
        }

        return aoValue;
    }

    public void UpdateMesh(VoxelChunk chunk)
    {
        // Regenerate mesh
        GenerateMesh(chunk);
    }

    public void SetMeshFromArrayMesh(ArrayMesh mesh, List<Vector3> collisionFaces, VoxelChunk chunk = null)
    {
        // Store the chunk reference if provided
        if (chunk != null)
        {
            _chunk = chunk;
        }

        // Set the mesh
        _meshInstance.Mesh = mesh;

        // Create collision shape
        if (collisionFaces.Count > 0)
        {
            ConcavePolygonShape3D collisionShape = new ConcavePolygonShape3D();
            collisionShape.Data = collisionFaces.ToArray();
            _collisionShape.Shape = collisionShape;
        }
        else
        {
            _collisionShape.Shape = null;
        }
    }

    public void UpdateMeshFromArrayMesh(ArrayMesh mesh, List<Vector3> collisionFaces, VoxelChunk chunk = null)
    {
        // Update the mesh
        SetMeshFromArrayMesh(mesh, collisionFaces, chunk);
    }

    // Get the chunk associated with this mesh
    public VoxelChunk GetChunk()
    {
        return _chunk;
    }

    // Helper method to check if coordinates are within chunk bounds
    private static bool IsInBounds(VoxelChunk chunk, int x, int y, int z)
    {
        return x >= 0 && x < chunk.Size && y >= 0 && y < chunk.Height && z >= 0 && z < chunk.Size;
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
        public List<float> AmbientOcclusion { get; set; } = new List<float>();
    }
}
