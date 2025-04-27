using Godot;
using System;
using System.Collections.Generic;
using System.Threading;
using CubeGen.World.Common;

public class ChunkMeshGenerator
{
    private Queue<VoxelChunk> _chunksToProcess = new Queue<VoxelChunk>();
    private Queue<(VoxelChunk, ArrayMesh, List<Vector3>)> _completedMeshes = new Queue<(VoxelChunk, ArrayMesh, List<Vector3>)>();
    private System.Threading.Mutex _queueMutex = new System.Threading.Mutex();
    private System.Threading.Mutex _resultMutex = new System.Threading.Mutex();
    private Thread _workerThread;
    private bool _isRunning = true;

    public ChunkMeshGenerator()
    {
        // Start the worker thread
        _workerThread = new Thread(ProcessChunks);
        _workerThread.Start();
    }

    public void QueueChunk(VoxelChunk chunk)
    {
        _queueMutex.WaitOne();
        try
        {
            _chunksToProcess.Enqueue(chunk);
        }
        finally
        {
            _queueMutex.ReleaseMutex();
        }
    }

    public bool HasCompletedMeshes()
    {
        bool hasCompleted = false;
        _resultMutex.WaitOne();
        try
        {
            hasCompleted = _completedMeshes.Count > 0;
        }
        finally
        {
            _resultMutex.ReleaseMutex();
        }
        return hasCompleted;
    }

    public (VoxelChunk, ArrayMesh, List<Vector3>) GetNextCompletedMesh()
    {
        (VoxelChunk, ArrayMesh, List<Vector3>) result = (null, null, null);
        _resultMutex.WaitOne();
        try
        {
            if (_completedMeshes.Count > 0)
            {
                result = _completedMeshes.Dequeue();
            }
        }
        finally
        {
            _resultMutex.ReleaseMutex();
        }
        return result;
    }

    public void Stop()
    {
        _isRunning = false;
        _workerThread.Join();
    }

    private void ProcessChunks()
    {
        while (_isRunning)
        {
            VoxelChunk chunkToProcess = null;

            // Get the next chunk to process
            _queueMutex.WaitOne();
            try
            {
                if (_chunksToProcess.Count > 0)
                {
                    chunkToProcess = _chunksToProcess.Dequeue();
                }
            }
            finally
            {
                _queueMutex.ReleaseMutex();
            }

            // Process the chunk if we have one
            if (chunkToProcess != null)
            {
                // Generate the mesh data
                (ArrayMesh mesh, List<Vector3> collisionFaces) = GenerateMesh(chunkToProcess);

                // Queue the completed mesh
                _resultMutex.WaitOne();
                try
                {
                    _completedMeshes.Enqueue((chunkToProcess, mesh, collisionFaces));
                }
                finally
                {
                    _resultMutex.ReleaseMutex();
                }
            }
            else
            {
                // Sleep a bit to avoid busy waiting
                Thread.Sleep(10);
            }
        }
    }

    private (ArrayMesh, List<Vector3>) GenerateMesh(VoxelChunk chunk)
    {
        // Create a dictionary to group vertices by biome and voxel type
        Dictionary<BiomeType, Dictionary<VoxelType, List<MeshData>>> meshDataByBiomeAndType =
            new Dictionary<BiomeType, Dictionary<VoxelType, List<MeshData>>>();

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

        // Variables for collision shape
        List<Vector3> allVertices = new List<Vector3>();
        List<int> allIndices = new List<int>();
        int globalVertexOffset = 0;

        // Create surfaces for each biome and voxel type
        int surfaceIndex = 0;

        foreach (var biomeEntry in meshDataByBiomeAndType)
        {
            BiomeType biomeType = biomeEntry.Key;
            var voxelTypeDict = biomeEntry.Value;

            foreach (var voxelEntry in voxelTypeDict)
            {
                VoxelType voxelType = voxelEntry.Key;
                List<MeshData> meshDataList = voxelEntry.Value;

                // Combine all mesh data for this biome and voxel type
                List<Vector3> vertices = new List<Vector3>();
                List<Vector3> normals = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<Color> colors = new List<Color>();
                List<int> indices = new List<int>();

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

                // Skip if there are no vertices
                if (vertices.Count == 0 || indices.Count == 0)
                {
                    continue;
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

                // Add vertices and indices to collision data
                allVertices.AddRange(vertices);
                foreach (int index in indices)
                {
                    allIndices.Add(index + globalVertexOffset);
                }
                globalVertexOffset += vertices.Count;

                surfaceIndex++;
            }
        }

        // Create collision faces
        List<Vector3> collisionFaces = new List<Vector3>();
        for (int i = 0; i < allIndices.Count; i += 3)
        {
            if (i + 2 < allIndices.Count &&
                allIndices[i] < allVertices.Count &&
                allIndices[i + 1] < allVertices.Count &&
                allIndices[i + 2] < allVertices.Count)
            {
                collisionFaces.Add(allVertices[allIndices[i]]);
                collisionFaces.Add(allVertices[allIndices[i + 1]]);
                collisionFaces.Add(allVertices[allIndices[i + 2]]);
            }
        }

        return (mesh, collisionFaces);
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
        if (z == chunk.Size - 1 || !chunk.IsVoxelSolid(x, y, z + 1))
        {
            AddFace(FaceDirection.Front, new Vector3(x, y, z), voxelType, meshData, chunk);
        }

        // Back face (Z-)
        if (z == 0 || !chunk.IsVoxelSolid(x, y, z - 1))
        {
            AddFace(FaceDirection.Back, new Vector3(x, y, z), voxelType, meshData, chunk);
        }

        // Right face (X+)
        if (x == chunk.Size - 1 || !chunk.IsVoxelSolid(x + 1, y, z))
        {
            AddFace(FaceDirection.Right, new Vector3(x, y, z), voxelType, meshData, chunk);
        }

        // Left face (X-)
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

        // Add vertices based on face direction
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
        if (flipTriangulation)
        {
            // Flipped triangulation (v0-v1-v2, v0-v2-v3)
            meshData.Indices.Add(vertexCount);
            meshData.Indices.Add(vertexCount + 1);
            meshData.Indices.Add(vertexCount + 2);

            meshData.Indices.Add(vertexCount);
            meshData.Indices.Add(vertexCount + 2);
            meshData.Indices.Add(vertexCount + 3);
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
        // Determine which neighboring voxels to check based on face direction and vertex index
        Vector3I side1 = Vector3I.Zero;
        Vector3I side2 = Vector3I.Zero;
        Vector3I corner = Vector3I.Zero;

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
                }
                break;
            // Similar cases for other face directions...
            default:
                // Default case to avoid compiler warning
                break;
        }

        // Check if the neighboring voxels are solid
        bool side1Solid = IsVoxelSolidForAO(chunk, side1.X, side1.Y, side1.Z);
        bool side2Solid = IsVoxelSolidForAO(chunk, side2.X, side2.Y, side2.Z);
        bool cornerSolid = IsVoxelSolidForAO(chunk, corner.X, corner.Y, corner.Z);

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

        // Calculate occlusion level
        int occlusionLevel = 0;

        // If both sides are solid, the corner doesn't matter - it's fully occluded
        if (side1Solid && side2Solid)
        {
            occlusionLevel = 3;
        }
        else
        {
            // Count solid neighbors
            if (side1Solid) occlusionLevel++;
            if (side2Solid) occlusionLevel++;
            if (cornerSolid && !side1Solid && !side2Solid) occlusionLevel++; // Corner only matters if sides aren't solid
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

    private bool IsVoxelSolidForAO(VoxelChunk chunk, int x, int y, int z)
    {
        // Check if the voxel is within bounds
        if (x < 0 || x >= chunk.Size || y < 0 || y >= chunk.Height || z < 0 || z >= chunk.Size)
        {
            // For out-of-bounds positions in the Y direction
            if (y < 0)
            {
                // Below the chunk is solid (ground)
                return true;
            }
            else if (y >= chunk.Height)
            {
                // Above the chunk is air
                return false;
            }

            // For out-of-bounds positions in X and Z, we'll assume air
            // In a full implementation, you would query the world for the neighboring chunk
            return false;
        }

        // For in-bounds voxels, check if it's solid
        VoxelType type = chunk.GetVoxel(x, y, z);
        return type != VoxelType.Air && type != VoxelType.Water;
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
}

public class MeshData
{
    public List<Vector3> Vertices { get; private set; } = new List<Vector3>();
    public List<Vector3> Normals { get; private set; } = new List<Vector3>();
    public List<Vector2> UVs { get; private set; } = new List<Vector2>();
    public List<int> Indices { get; private set; } = new List<int>();
    public List<float> AmbientOcclusion { get; private set; } = new List<float>();
}
