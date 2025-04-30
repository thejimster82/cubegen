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
    private ChunkManager _chunkManager; // Reference to the ChunkManager for cross-chunk lookups

    public ChunkMeshGenerator(ChunkManager chunkManager)
    {
        _chunkManager = chunkManager;

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
                // Check if all neighboring chunks are available for proper AO calculation
                bool canProcessChunk = AreNeighboringChunksAvailable(chunkToProcess);

                if (canProcessChunk)
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
                    // Re-queue the chunk for later processing when neighbors are available
                    _queueMutex.WaitOne();
                    try
                    {
                        // Put it at the back of the queue
                        _chunksToProcess.Enqueue(chunkToProcess);
                    }
                    finally
                    {
                        _queueMutex.ReleaseMutex();
                    }

                    // Sleep a bit to avoid busy re-queueing
                    Thread.Sleep(50);
                }
            }
            else
            {
                // Sleep a bit to avoid busy waiting
                Thread.Sleep(10);
            }
        }
    }

    // Check if all neighboring chunks needed for AO calculation are available
    private bool AreNeighboringChunksAvailable(VoxelChunk chunk)
    {
        // Get the position of the chunk
        Vector2I pos = chunk.Position;

        // Check all 8 neighboring chunks
        Vector2I[] neighbors = new Vector2I[]
        {
            new Vector2I(pos.X - 1, pos.Y - 1), // Bottom-left
            new Vector2I(pos.X - 1, pos.Y),     // Left
            new Vector2I(pos.X - 1, pos.Y + 1), // Top-left
            new Vector2I(pos.X, pos.Y - 1),     // Bottom
            new Vector2I(pos.X, pos.Y + 1),     // Top
            new Vector2I(pos.X + 1, pos.Y - 1), // Bottom-right
            new Vector2I(pos.X + 1, pos.Y),     // Right
            new Vector2I(pos.X + 1, pos.Y + 1)  // Top-right
        };

        // Check if all neighbors exist
        foreach (Vector2I neighborPos in neighbors)
        {
            // Skip chunks that are too far away (outside the world bounds)
            if (Math.Abs(neighborPos.X - pos.X) > 1 || Math.Abs(neighborPos.Y - pos.Y) > 1)
                continue;

            // Check if the neighbor data exists in the chunk manager
            // We only need the chunk data for AO calculation, not the full mesh
            if (!_chunkManager.HasChunkData(neighborPos))
            {
                // If any neighbor data is missing, return false
                return false;
            }
        }

        // All necessary neighbors are available
        return true;
    }

    private (ArrayMesh, List<Vector3>) GenerateMesh(VoxelChunk chunk)
    {
        // Create a dictionary to group vertices by biome and voxel type
        Dictionary<BiomeType, Dictionary<VoxelType, List<MeshData>>> meshDataByBiomeAndType =
            new Dictionary<BiomeType, Dictionary<VoxelType, List<MeshData>>>();

        // First, find the surface height for each x,z coordinate
        int[,] terrainSurfaceHeights = new int[chunk.Size, chunk.Size];
        int[,] depthNeeded = new int[chunk.Size, chunk.Size];

        // Initialize with -1 to indicate no surface found
        for (int x = 0; x < chunk.Size; x++)
        {
            for (int z = 0; z < chunk.Size; z++)
            {
                terrainSurfaceHeights[x, z] = -1;
                depthNeeded[x, z] = 1; // Default to 1 block below surface
            }
        }

        // Find the highest solid voxel for each x,z coordinate
        for (int x = 0; x < chunk.Size; x++)
        {
            for (int z = 0; z < chunk.Size; z++)
            {
                // Scan from top to bottom to find the first solid voxel
                for (int y = chunk.Height - 1; y >= 0; y--)
                {
                    VoxelType voxelType = chunk.GetVoxel(x, y, z);
                    if (voxelType != VoxelType.Air && voxelType != VoxelType.Water)
                    {
                        terrainSurfaceHeights[x, z] = y;

                        // Determine depth needed based on voxel type
                        // Trees and other structures need more depth
                        if (voxelType == VoxelType.Leaves || voxelType == VoxelType.Wood)
                        {
                            depthNeeded[x, z] = 0; // Process all voxels for trees
                        }
                        else if (voxelType == VoxelType.Grass || voxelType == VoxelType.Sand || voxelType == VoxelType.Snow)
                        {
                            depthNeeded[x, z] = 3; // Process a few blocks below surface for terrain
                        }
                        else
                        {
                            depthNeeded[x, z] = 1; // Default depth for other voxel types
                        }

                        break;
                    }
                }
            }
        }

        // Now process all voxels in the chunk
        // We'll use two approaches:
        // 1. For terrain voxels, we'll only process down to the required depth
        // 2. For non-terrain voxels (trees, etc.), we'll process all of them

        // First, create a set to track which voxels we've already processed
        HashSet<(int, int, int)> processedVoxels = new HashSet<(int, int, int)>();

        // Process terrain voxels with depth optimization
        for (int x = 0; x < chunk.Size; x++)
        {
            for (int z = 0; z < chunk.Size; z++)
            {
                int surfaceY = terrainSurfaceHeights[x, z];

                // Skip if no surface was found at this x,z coordinate
                if (surfaceY == -1)
                    continue;

                int depth = depthNeeded[x, z];

                // If depth is 0, process all voxels in this column
                int minY = (depth == 0) ? 0 : Math.Max(0, surfaceY - depth);

                // Process voxels from surface down to minY
                for (int y = surfaceY; y >= minY; y--)
                {
                    VoxelType voxelType = chunk.GetVoxel(x, y, z);

                    // Skip air voxels
                    if (voxelType == VoxelType.Air)
                        continue;

                    // Skip if this voxel is completely surrounded by solid voxels
                    // This is a significant optimization for underground voxels
                    if (y < surfaceY - 1 && // Not the surface or just below it
                        IsVoxelSurrounded(chunk, x, y, z))
                    {
                        continue;
                    }

                    // Mark this voxel as processed
                    processedVoxels.Add((x, y, z));

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

        // Now process any remaining voxels that weren't processed in the terrain pass
        // This handles trees, structures, and other non-terrain voxels
        for (int x = 0; x < chunk.Size; x++)
        {
            for (int y = 0; y < chunk.Height; y++)
            {
                for (int z = 0; z < chunk.Size; z++)
                {
                    // Skip if already processed
                    if (processedVoxels.Contains((x, y, z)))
                        continue;

                    VoxelType voxelType = chunk.GetVoxel(x, y, z);

                    // Skip air voxels
                    if (voxelType == VoxelType.Air)
                        continue;

                    // Skip if this voxel is completely surrounded by solid voxels
                    if (IsVoxelSurrounded(chunk, x, y, z))
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

                // Set material based on biome and voxel type
                Material material = BiomeMaterials.GetMaterial(biomeType, voxelType);
                mesh.SurfaceSetMaterial(surfaceIndex, material);

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
        if (y == chunk.Height - 1 || !IsVoxelSolidForAO(chunk, x, y + 1, z))
        {
            AddFace(FaceDirection.Top, new Vector3(x, y, z), voxelType, meshData, chunk);
        }

        // Bottom face (Y-)
        if (y == 0 || !IsVoxelSolidForAO(chunk, x, y - 1, z))
        {
            AddFace(FaceDirection.Bottom, new Vector3(x, y, z), voxelType, meshData, chunk);
        }

        // Front face (Z+)
        if (z == chunk.Size - 1 || !IsVoxelSolidForAO(chunk, x, y, z + 1))
        {
            AddFace(FaceDirection.Front, new Vector3(x, y, z), voxelType, meshData, chunk);
        }

        // Back face (Z-)
        if (z == 0 || !IsVoxelSolidForAO(chunk, x, y, z - 1))
        {
            AddFace(FaceDirection.Back, new Vector3(x, y, z), voxelType, meshData, chunk);
        }

        // Right face (X+)
        if (x == chunk.Size - 1 || !IsVoxelSolidForAO(chunk, x + 1, y, z))
        {
            AddFace(FaceDirection.Right, new Vector3(x, y, z), voxelType, meshData, chunk);
        }

        // Left face (X-)
        if (x == 0 || !IsVoxelSolidForAO(chunk, x - 1, y, z))
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

        // Use a consistent triangulation pattern for all faces
        // This is critical for avoiding seams at chunk boundaries
        // We'll use a deterministic pattern based on world position, not local position
        // This ensures the same triangulation is used on both sides of a chunk boundary
        int worldX = chunk.Position.X * chunk.Size + x;
        int worldZ = chunk.Position.Y * chunk.Size + z;

        // Use a deterministic pattern based on world position
        flipTriangulation = ((worldX + worldZ) % 2 == 0);

        // For interior faces, we could use AO-based triangulation, but for consistency
        // we'll use the same deterministic pattern everywhere
        // This sacrifices some AO quality for seamless boundaries

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

        // We'll use the same AO calculation for both boundaries and interior
        // to ensure consistent shading across chunk boundaries
        // This is important to avoid visible seams

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
        // First check if the coordinates are within this chunk
        if (x >= 0 && x < chunk.Size && y >= 0 && y < chunk.Height && z >= 0 && z < chunk.Size)
        {
            // Use the chunk's own data for efficiency
            return chunk.IsVoxelSolid(x, y, z);
        }

        // For coordinates outside this chunk, convert to world coordinates
        int worldX = chunk.Position.X * chunk.Size + x;
        int worldY = y;
        int worldZ = chunk.Position.Y * chunk.Size + z;

        // Special case for Y bounds
        if (worldY < 0)
        {
            // Below the world is solid (ground)
            return true;
        }
        else if (worldY >= chunk.Height)
        {
            // Above the world is air
            return false;
        }

        // Use the ChunkManager to check if the voxel is solid in world coordinates
        // This will handle cross-chunk lookups consistently
        return _chunkManager.IsVoxelSolid(worldX, worldY, worldZ);
    }

    // Check if a voxel is completely surrounded by solid voxels
    // If it is, we can skip generating mesh data for it since it won't be visible
    private bool IsVoxelSurrounded(VoxelChunk chunk, int x, int y, int z)
    {
        // Check all 6 faces
        bool topSolid = IsVoxelSolidForAO(chunk, x, y + 1, z);
        bool bottomSolid = IsVoxelSolidForAO(chunk, x, y - 1, z);
        bool frontSolid = IsVoxelSolidForAO(chunk, x, y, z + 1);
        bool backSolid = IsVoxelSolidForAO(chunk, x, y, z - 1);
        bool rightSolid = IsVoxelSolidForAO(chunk, x + 1, y, z);
        bool leftSolid = IsVoxelSolidForAO(chunk, x - 1, y, z);

        // If all faces are covered by solid voxels, this voxel is not visible
        return topSolid && bottomSolid && frontSolid && backSolid && rightSolid && leftSolid;
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
