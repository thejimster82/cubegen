using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.Player.CharacterParts
{
    /// <summary>
    /// Base class for voxel-based character body parts
    /// </summary>
    public partial class VoxelBodyPart : Node3D
    {
        // Body part properties
        [Export] public Vector3 Size { get; set; } = new Vector3(1, 1, 1);
        [Export] public Color BaseColor { get; set; } = new Color(1, 1, 1);
        [Export] public string PartName { get; set; } = "Part";
        [Export] public bool GenerateCollider { get; set; } = false;

        // Mesh instance for rendering
        protected MeshInstance3D _meshInstance;

        // Collision shape (optional)
        protected CollisionShape3D _collisionShape;

        // Voxel data
        protected VoxelType[,,] _voxels;

        // Default voxel size (can be overridden)
        protected float _voxelSize = 0.125f; // 1/8 size voxels for more detail

        public override void _Ready()
        {
            // Create mesh instance
            _meshInstance = new MeshInstance3D();
            _meshInstance.Name = $"{PartName}Mesh";
            AddChild(_meshInstance);

            // Create collision shape if needed
            if (GenerateCollider)
            {
                _collisionShape = new CollisionShape3D();
                _collisionShape.Name = $"{PartName}Collision";
                AddChild(_collisionShape);
            }

            // Initialize voxel data
            InitializeVoxelData();

            // Generate mesh
            GenerateMesh();
        }

        /// <summary>
        /// Initialize the voxel data for this body part
        /// </summary>
        protected virtual void InitializeVoxelData()
        {
            // Calculate dimensions in voxels
            int sizeX = Mathf.CeilToInt(Size.X / _voxelSize);
            int sizeY = Mathf.CeilToInt(Size.Y / _voxelSize);
            int sizeZ = Mathf.CeilToInt(Size.Z / _voxelSize);

            // Initialize voxel array
            _voxels = new VoxelType[sizeX, sizeY, sizeZ];

            // Fill with default voxel type (solid)
            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        _voxels[x, y, z] = VoxelType.Stone; // Default solid type
                    }
                }
            }
        }

        /// <summary>
        /// Generate the mesh for this body part
        /// </summary>
        public virtual void GenerateMesh()
        {
            // Create mesh data
            ArrayMesh mesh = new ArrayMesh();
            List<Vector3> collisionFaces = new List<Vector3>();

            // Generate mesh data
            (mesh, collisionFaces) = GenerateMeshData();

            // Set mesh
            _meshInstance.Mesh = mesh;

            // Set collision shape if needed
            if (GenerateCollider && _collisionShape != null && collisionFaces.Count > 0)
            {
                ConcavePolygonShape3D collisionShape = new ConcavePolygonShape3D();
                collisionShape.Data = collisionFaces.ToArray();
                _collisionShape.Shape = collisionShape;
            }
        }

        /// <summary>
        /// Generate mesh data for this body part
        /// </summary>
        protected virtual (ArrayMesh, List<Vector3>) GenerateMeshData()
        {
            // Create mesh arrays
            Godot.Collections.Array arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);

            // Lists for mesh data
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> indices = new List<int>();
            List<Vector3> collisionFaces = new List<Vector3>();

            // Get dimensions
            int sizeX = _voxels.GetLength(0);
            int sizeY = _voxels.GetLength(1);
            int sizeZ = _voxels.GetLength(2);

            // Generate mesh for each voxel
            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        // Skip air voxels
                        if (_voxels[x, y, z] == VoxelType.Air)
                            continue;

                        // Add voxel faces
                        AddVoxelFaces(x, y, z, vertices, normals, colors, indices, collisionFaces);
                    }
                }
            }

            // Create mesh
            ArrayMesh mesh = new ArrayMesh();

            // Set mesh data
            if (vertices.Count > 0)
            {
                arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
                arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
                arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
                arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

                // Create surface
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

                // Create material
                StandardMaterial3D material = new StandardMaterial3D();
                material.VertexColorUseAsAlbedo = true;
                material.Roughness = 0.8f;

                // Fix the face culling to show outside faces instead of inside faces
                material.CullMode = BaseMaterial3D.CullModeEnum.Front; // Cull front faces to show the inside faces

                // Apply material
                mesh.SurfaceSetMaterial(0, material);
            }

            return (mesh, collisionFaces);
        }

        /// <summary>
        /// Add faces for a voxel at the specified position
        /// </summary>
        protected virtual void AddVoxelFaces(int x, int y, int z, List<Vector3> vertices, List<Vector3> normals,
            List<Color> colors, List<int> indices, List<Vector3> collisionFaces)
        {
            // Check each face direction
            for (int face = 0; face < 6; face++)
            {
                // Skip faces that are not visible
                if (!IsFaceVisible(x, y, z, face))
                    continue;

                // Add face
                AddFace(x, y, z, face, vertices, normals, colors, indices, collisionFaces);
            }
        }

        /// <summary>
        /// Check if a face is visible (not occluded by another voxel)
        /// </summary>
        protected virtual bool IsFaceVisible(int x, int y, int z, int face)
        {
            // Get neighbor position
            int nx = x, ny = y, nz = z;

            // Adjust position based on face
            switch (face)
            {
                case 0: nx++; break; // Right
                case 1: nx--; break; // Left
                case 2: ny++; break; // Top
                case 3: ny--; break; // Bottom
                case 4: nz++; break; // Front
                case 5: nz--; break; // Back
            }

            // Check if neighbor is out of bounds
            if (nx < 0 || nx >= _voxels.GetLength(0) ||
                ny < 0 || ny >= _voxels.GetLength(1) ||
                nz < 0 || nz >= _voxels.GetLength(2))
            {
                return true; // Visible if neighbor is out of bounds
            }

            // Check if neighbor is air
            return _voxels[nx, ny, nz] == VoxelType.Air;
        }

        /// <summary>
        /// Add a face to the mesh
        /// </summary>
        protected virtual void AddFace(int x, int y, int z, int face, List<Vector3> vertices, List<Vector3> normals,
            List<Color> colors, List<int> indices, List<Vector3> collisionFaces)
        {
            // Face vertices (cube corners)
            Vector3[] faceVertices = new Vector3[4];

            // Face normal
            Vector3 normal = Vector3.Zero;

            // Calculate face vertices and normal based on face index
            CalculateFaceVertices(x, y, z, face, out faceVertices, out normal);

            // Add vertices
            int vertexOffset = vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                vertices.Add(faceVertices[i]);
                normals.Add(normal);
                colors.Add(BaseColor);
            }

            // Add indices (two triangles per face)
            indices.Add(vertexOffset);
            indices.Add(vertexOffset + 1);
            indices.Add(vertexOffset + 2);

            indices.Add(vertexOffset);
            indices.Add(vertexOffset + 2);
            indices.Add(vertexOffset + 3);

            // Add collision faces
            if (GenerateCollider)
            {
                collisionFaces.Add(faceVertices[0]);
                collisionFaces.Add(faceVertices[1]);
                collisionFaces.Add(faceVertices[2]);

                collisionFaces.Add(faceVertices[0]);
                collisionFaces.Add(faceVertices[2]);
                collisionFaces.Add(faceVertices[3]);
            }
        }

        /// <summary>
        /// Calculate vertices and normal for a face
        /// </summary>
        protected virtual void CalculateFaceVertices(int x, int y, int z, int face, out Vector3[] vertices, out Vector3 normal)
        {
            // Initialize vertices array and normal
            vertices = new Vector3[4];
            normal = Vector3.Zero; // Default initialization

            // Calculate voxel position
            float px = (x - _voxels.GetLength(0) / 2.0f) * _voxelSize;
            float py = (y - _voxels.GetLength(1) / 2.0f) * _voxelSize;
            float pz = (z - _voxels.GetLength(2) / 2.0f) * _voxelSize;

            // Half size of voxel
            float hs = _voxelSize / 2.0f;

            // Calculate vertices and normal based on face
            switch (face)
            {
                case 0: // Right face (+X)
                    vertices[0] = new Vector3(px + hs, py - hs, pz - hs);
                    vertices[1] = new Vector3(px + hs, py + hs, pz - hs);
                    vertices[2] = new Vector3(px + hs, py + hs, pz + hs);
                    vertices[3] = new Vector3(px + hs, py - hs, pz + hs);
                    normal = Vector3.Right;
                    break;
                case 1: // Left face (-X)
                    vertices[0] = new Vector3(px - hs, py - hs, pz + hs);
                    vertices[1] = new Vector3(px - hs, py + hs, pz + hs);
                    vertices[2] = new Vector3(px - hs, py + hs, pz - hs);
                    vertices[3] = new Vector3(px - hs, py - hs, pz - hs);
                    normal = Vector3.Left;
                    break;
                case 2: // Top face (+Y)
                    vertices[0] = new Vector3(px - hs, py + hs, pz - hs);
                    vertices[1] = new Vector3(px - hs, py + hs, pz + hs);
                    vertices[2] = new Vector3(px + hs, py + hs, pz + hs);
                    vertices[3] = new Vector3(px + hs, py + hs, pz - hs);
                    normal = Vector3.Up;
                    break;
                case 3: // Bottom face (-Y)
                    vertices[0] = new Vector3(px - hs, py - hs, pz + hs);
                    vertices[1] = new Vector3(px - hs, py - hs, pz - hs);
                    vertices[2] = new Vector3(px + hs, py - hs, pz - hs);
                    vertices[3] = new Vector3(px + hs, py - hs, pz + hs);
                    normal = Vector3.Down;
                    break;
                case 4: // Front face (+Z)
                    vertices[0] = new Vector3(px - hs, py - hs, pz + hs);
                    vertices[1] = new Vector3(px + hs, py - hs, pz + hs);
                    vertices[2] = new Vector3(px + hs, py + hs, pz + hs);
                    vertices[3] = new Vector3(px - hs, py + hs, pz + hs);
                    normal = Vector3.Forward;
                    break;
                case 5: // Back face (-Z)
                    vertices[0] = new Vector3(px + hs, py - hs, pz - hs);
                    vertices[1] = new Vector3(px - hs, py - hs, pz - hs);
                    vertices[2] = new Vector3(px - hs, py + hs, pz - hs);
                    vertices[3] = new Vector3(px + hs, py + hs, pz - hs);
                    normal = Vector3.Back;
                    break;
            }
        }

        /// <summary>
        /// Set a voxel at the specified position
        /// </summary>
        public virtual void SetVoxel(int x, int y, int z, VoxelType type)
        {
            // Check bounds
            if (x < 0 || x >= _voxels.GetLength(0) ||
                y < 0 || y >= _voxels.GetLength(1) ||
                z < 0 || z >= _voxels.GetLength(2))
            {
                return;
            }

            // Set voxel
            _voxels[x, y, z] = type;
        }

        /// <summary>
        /// Get a voxel at the specified position
        /// </summary>
        public virtual VoxelType GetVoxel(int x, int y, int z)
        {
            // Check bounds
            if (x < 0 || x >= _voxels.GetLength(0) ||
                y < 0 || y >= _voxels.GetLength(1) ||
                z < 0 || z >= _voxels.GetLength(2))
            {
                return VoxelType.Air; // Out of bounds is air
            }

            // Get voxel
            return _voxels[x, y, z];
        }

        /// <summary>
        /// Regenerate the mesh with the current voxel data
        /// </summary>
        public virtual void RegenerateMesh()
        {
            // Clear existing mesh
            if (_meshInstance.Mesh != null)
            {
                _meshInstance.Mesh.Dispose();
                _meshInstance.Mesh = null;
            }

            // Generate new mesh
            GenerateMesh();
        }
    }
}
