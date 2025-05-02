using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using CubeGen.World.Common;

public class ChunkMeshGenerator
{
    private ConcurrentQueue<VoxelChunk> _chunksToProcess = new ConcurrentQueue<VoxelChunk>();
    private ConcurrentQueue<(VoxelChunk, ArrayMesh, List<Vector3>)> _completedMeshes = new ConcurrentQueue<(VoxelChunk, ArrayMesh, List<Vector3>)>();
    private readonly Thread _workerThread;
    private bool _isRunning = true;
    private readonly ChunkManager _chunkManager; // Reference to the ChunkManager for cross-chunk lookups

    public ChunkMeshGenerator(ChunkManager chunkManager)
    {
        _chunkManager = chunkManager;

        // Start the worker thread
        _workerThread = new Thread(ProcessChunks);
        _workerThread.Start();
    }

    public void QueueChunk(VoxelChunk chunk)
    {
        _chunksToProcess.Enqueue(chunk);
    }

    public bool HasCompletedMeshes()
    {
        return !_completedMeshes.IsEmpty;
    }

    public (VoxelChunk, ArrayMesh, List<Vector3>) GetNextCompletedMesh()
    {
        (VoxelChunk, ArrayMesh, List<Vector3>) result = (null, null, null);
        if (_completedMeshes.TryDequeue(out result))
        {
            return result;
        }
        return (null, null, null);
    }

    public void Stop()
    {
        _isRunning = false;
        _workerThread.Join();
    }

    private void ProcessChunks()
    {
        // Dictionary to track chunks that are waiting for neighbors
        Dictionary<Vector2I, int> waitingChunks = new();

        while (_isRunning)
        {
            bool didWork = false;

            // Try to get the next chunk to process
            if (_chunksToProcess.TryDequeue(out VoxelChunk chunkToProcess))
            {
                didWork = true;

                // Check if all neighboring chunks are available for proper AO calculation
                bool canProcessChunk = AreNeighboringChunksAvailable(chunkToProcess);

                if (canProcessChunk)
                {
                    // Generate the mesh data
                    (ArrayMesh mesh, List<Vector3> collisionFaces) = GenerateMesh(chunkToProcess);

                    // Queue the completed mesh - ConcurrentQueue is thread-safe
                    _completedMeshes.Enqueue((chunkToProcess, mesh, collisionFaces));

                    // Remove from waiting chunks if it was there
                    waitingChunks.Remove(chunkToProcess.Position);
                }
                else
                {
                    // Track how many times we've tried to process this chunk
                    if (!waitingChunks.TryGetValue(chunkToProcess.Position, out int attempts))
                    {
                        attempts = 0;
                    }

                    // If we've tried too many times, process it anyway without all neighbors
                    if (attempts >= 5)
                    {
                        // Generate the mesh data anyway
                        (ArrayMesh mesh, List<Vector3> collisionFaces) = GenerateMesh(chunkToProcess);

                        // Queue the completed mesh
                        _completedMeshes.Enqueue((chunkToProcess, mesh, collisionFaces));

                        // Remove from waiting chunks
                        waitingChunks.Remove(chunkToProcess.Position);
                    }
                    else
                    {
                        // Increment attempts and re-queue
                        waitingChunks[chunkToProcess.Position] = attempts + 1;

                        // Re-queue the chunk for later processing when neighbors are available
                        _chunksToProcess.Enqueue(chunkToProcess);
                    }
                }
            }

            // Sleep only if we didn't do any work
            if (!didWork)
            {
                // Use a shorter sleep time to be more responsive
                Thread.Sleep(5);
            }
        }
    }

    // Check if all neighboring chunks needed for AO calculation are available
    private bool AreNeighboringChunksAvailable(VoxelChunk chunk)
    {
        // Get the position of the chunk
        Vector2I pos = chunk.Position;

        // Only check the 4 direct neighbors (left, right, top, bottom)
        // This is a compromise that still gives decent AO but loads faster
        Vector2I[] neighbors = new[]
        {
            new Vector2I(pos.X - 1, pos.Y),     // Left
            new Vector2I(pos.X + 1, pos.Y),     // Right
            new Vector2I(pos.X, pos.Y - 1),     // Bottom
            new Vector2I(pos.X, pos.Y + 1),     // Top
        };

        // Count how many neighbors exist
        int availableNeighbors = 0;

        foreach (Vector2I neighborPos in neighbors)
        {
            // Check if the neighbor data exists in the chunk manager
            if (_chunkManager.HasChunkData(neighborPos))
            {
                availableNeighbors++;
            }
        }

        // If at least 3 of 4 neighbors are available, we can proceed
        // This is a compromise that allows faster loading while still
        // maintaining decent AO quality
        return availableNeighbors >= 3;
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

                    // Special handling for decoration types
                    bool isDecoration = VoxelProperties.IsDecoration(voxelType);

                    // Check each face and add to mesh data
                    if (isDecoration)
                    {
                        // For decoration types, we'll create a cross-shaped mesh instead of a cube
                        AddDecorationFaces(chunk, x, y, z, voxelType, meshData);
                    }
                    else
                    {
                        // Regular voxel faces for non-decoration types
                        AddVoxelFaces(chunk, x, y, z, voxelType, meshData);
                    }

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

                    // Special handling for decoration types
                    bool isDecoration = VoxelProperties.IsDecoration(voxelType);

                    // Check each face and add to mesh data
                    if (isDecoration)
                    {
                        // For decoration types, we'll create a cross-shaped mesh instead of a cube
                        AddDecorationFaces(chunk, x, y, z, voxelType, meshData);
                    }
                    else
                    {
                        // Regular voxel faces for non-decoration types
                        AddVoxelFaces(chunk, x, y, z, voxelType, meshData);
                    }

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
                try
                {
                    // Initialize BiomeMaterials if needed
                    BiomeMaterials.Initialize();

                    // Get material for this biome and voxel type
                    Material material = BiomeMaterials.GetMaterial(biomeType, voxelType);

                    // Apply the material to the surface
                    if (material != null)
                    {
                        mesh.SurfaceSetMaterial(surfaceIndex, material);
                    }
                    else
                    {
                        GD.PrintErr($"Failed to get material for biome {biomeType} and voxel type {voxelType}");

                        // Create a fallback material with a distinctive color
                        StandardMaterial3D fallbackMaterial = new StandardMaterial3D();
                        fallbackMaterial.AlbedoColor = new Color(1.0f, 0.0f, 1.0f); // Magenta
                        fallbackMaterial.VertexColorUseAsAlbedo = true;
                        mesh.SurfaceSetMaterial(surfaceIndex, fallbackMaterial);
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Error setting material: {ex.Message}");

                    // Create an emergency fallback material
                    StandardMaterial3D emergencyMaterial = new StandardMaterial3D();
                    emergencyMaterial.AlbedoColor = new Color(1.0f, 0.0f, 0.0f); // Red for error
                    emergencyMaterial.VertexColorUseAsAlbedo = true;
                    mesh.SurfaceSetMaterial(surfaceIndex, emergencyMaterial);
                }

                // Add vertices and indices to collision data
                // Skip collision data for decoration types
                bool hasCollider = VoxelProperties.HasCollider(voxelType);

                if (hasCollider)
                {
                    // Only add collision data for voxels that should have colliders
                    allVertices.AddRange(vertices);
                    foreach (int index in indices)
                    {
                        allIndices.Add(index + globalVertexOffset);
                    }
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
        float chunkScale = chunk.Scale;
        int x = (int)position.X;
        int y = (int)position.Y;
        int z = (int)position.Z;

        // Get voxel-specific scale factor
        float voxelScaleFactor = VoxelScaleHelper.GetScaleFactor(voxelType);

        // Calculate the final scale (chunk scale * voxel scale factor)
        float finalScale = chunkScale * voxelScaleFactor;

        // Calculate offset to center smaller voxels within the full voxel space
        Vector3 centeringOffset = Vector3.Zero;
        if (voxelScaleFactor < 1.0f)
        {
            centeringOffset = VoxelScaleHelper.GetCenteringOffset(voxelType);
        }

        // Calculate the base position with chunk scale
        Vector3 basePosition = position * chunkScale;

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
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X, 1, centeringOffset.Z) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X + voxelScaleFactor, 1, centeringOffset.Z) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X + voxelScaleFactor, 1, centeringOffset.Z + voxelScaleFactor) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X, 1, centeringOffset.Z + voxelScaleFactor) * finalScale));
                for (int i = 0; i < 4; i++) meshData.Normals.Add(Vector3.Up);
                break;
            case FaceDirection.Bottom:
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X, 0, centeringOffset.Z) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X, 0, centeringOffset.Z + voxelScaleFactor) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X + voxelScaleFactor, 0, centeringOffset.Z + voxelScaleFactor) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X + voxelScaleFactor, 0, centeringOffset.Z) * finalScale));
                for (int i = 0; i < 4; i++) meshData.Normals.Add(Vector3.Down);
                break;
            case FaceDirection.Front:
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X, 0, centeringOffset.Z + voxelScaleFactor) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X, 1, centeringOffset.Z + voxelScaleFactor) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X + voxelScaleFactor, 1, centeringOffset.Z + voxelScaleFactor) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X + voxelScaleFactor, 0, centeringOffset.Z + voxelScaleFactor) * finalScale));
                for (int i = 0; i < 4; i++) meshData.Normals.Add(Vector3.Forward);
                break;
            case FaceDirection.Back:
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X, 0, centeringOffset.Z) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X + voxelScaleFactor, 0, centeringOffset.Z) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X + voxelScaleFactor, 1, centeringOffset.Z) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X, 1, centeringOffset.Z) * finalScale));
                for (int i = 0; i < 4; i++) meshData.Normals.Add(Vector3.Back);
                break;
            case FaceDirection.Right:
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X + voxelScaleFactor, 0, centeringOffset.Z) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X + voxelScaleFactor, 0, centeringOffset.Z + voxelScaleFactor) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X + voxelScaleFactor, 1, centeringOffset.Z + voxelScaleFactor) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X + voxelScaleFactor, 1, centeringOffset.Z) * finalScale));
                for (int i = 0; i < 4; i++) meshData.Normals.Add(Vector3.Right);
                break;
            case FaceDirection.Left:
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X, 0, centeringOffset.Z) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X, 1, centeringOffset.Z) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X, 1, centeringOffset.Z + voxelScaleFactor) * finalScale));
                meshData.Vertices.Add(basePosition + (new Vector3(centeringOffset.X, 0, centeringOffset.Z + voxelScaleFactor) * finalScale));
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
            // Get the voxel type
            VoxelType voxelType = chunk.GetVoxel(x, y, z);

            // Special case: don't consider decoration types as solid for AO calculations
            // This ensures blocks remain visible even with decorations on top
            if (VoxelProperties.IsDecoration(voxelType))
            {
                return false;
            }

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

    private void AddDecorationFaces(VoxelChunk chunk, int x, int y, int z, VoxelType voxelType, MeshData meshData)
    {
        // Get current vertex count
        int vertexCount = meshData.Vertices.Count;

        // Get scale from the chunk
        float chunkScale = chunk.Scale;

        // Get voxel-specific scale factor
        float voxelScaleFactor = VoxelProperties.GetScaleFactor(voxelType);

        // Calculate the final scale (chunk scale * voxel scale factor)
        float finalScale = chunkScale * voxelScaleFactor;

        // Calculate the base position with chunk scale
        Vector3 basePosition = new Vector3(x, y, z) * chunkScale;

        // Calculate offset to center smaller voxels within the full voxel space
        Vector3 centeringOffset = VoxelProperties.GetCenteringOffset(voxelType);

        // Get biome type for this position
        int worldX = chunk.Position.X * chunk.Size + x;
        int worldZ = chunk.Position.Y * chunk.Size + z;
        BiomeType biomeType = CubeGen.World.Generation.WorldGenerator.GetBiomeType(worldX, worldZ);

        // Get the material color for this voxel type and biome
        Color baseColor = GetColorForVoxelType(voxelType, biomeType);

        // Get the decoration model
        List<DecorationModels.DecorationVoxel> decorationVoxels = DecorationModels.GetDecorationModel(voxelType, baseColor);

        // Add each voxel in the decoration model
        foreach (var decorationVoxel in decorationVoxels)
        {
            // Calculate the position for this decoration voxel
            Vector3 voxelPosition = basePosition + centeringOffset + decorationVoxel.Position * finalScale;

            // Add a cube for this decoration voxel
            AddDecorationCube(meshData, voxelPosition, decorationVoxel.Scale * finalScale, decorationVoxel.Color, vertexCount);

            // Update vertex count for the next cube
            vertexCount += 24; // 24 vertices per cube (4 vertices per face * 6 faces)
        }
    }

    // Helper method to get the color for a voxel type in a specific biome
    private Color GetColorForVoxelType(VoxelType voxelType, BiomeType biomeType)
    {
        // Default color (white)
        Color color = new Color(1.0f, 1.0f, 1.0f);

        // Get the material for this voxel type and biome
        Material material = BiomeMaterials.GetMaterial(biomeType, voxelType);

        // Try to get the albedo color from the material
        if (material is StandardMaterial3D standardMaterial)
        {
            color = standardMaterial.AlbedoColor;
        }

        return color;
    }

    // Add a cube for a decoration voxel
    private void AddDecorationCube(MeshData meshData, Vector3 position, float scale, Color color, int vertexOffset)
    {
        // Calculate half size for the cube
        float halfSize = scale * 0.5f;

        // Define the 8 corners of the cube
        Vector3 v0 = position + new Vector3(-halfSize, -halfSize, -halfSize); // Bottom back left
        Vector3 v1 = position + new Vector3(halfSize, -halfSize, -halfSize);  // Bottom back right
        Vector3 v2 = position + new Vector3(halfSize, -halfSize, halfSize);   // Bottom front right
        Vector3 v3 = position + new Vector3(-halfSize, -halfSize, halfSize);  // Bottom front left
        Vector3 v4 = position + new Vector3(-halfSize, halfSize, -halfSize);  // Top back left
        Vector3 v5 = position + new Vector3(halfSize, halfSize, -halfSize);   // Top back right
        Vector3 v6 = position + new Vector3(halfSize, halfSize, halfSize);    // Top front right
        Vector3 v7 = position + new Vector3(-halfSize, halfSize, halfSize);   // Top front left

        // Define the 6 faces of the cube (each face has 4 vertices)
        // Bottom face
        AddQuad(meshData, v0, v1, v2, v3, color, vertexOffset);

        // Top face
        AddQuad(meshData, v7, v6, v5, v4, color, vertexOffset + 4);

        // Front face
        AddQuad(meshData, v3, v2, v6, v7, color, vertexOffset + 8);

        // Back face
        AddQuad(meshData, v4, v5, v1, v0, color, vertexOffset + 12);

        // Left face
        AddQuad(meshData, v0, v3, v7, v4, color, vertexOffset + 16);

        // Right face
        AddQuad(meshData, v1, v5, v6, v2, color, vertexOffset + 20);
    }

    // Add a quad (4 vertices and 2 triangles)
    private void AddQuad(MeshData meshData, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color color, int vertexOffset)
    {
        // Add vertices
        meshData.Vertices.Add(v0);
        meshData.Vertices.Add(v1);
        meshData.Vertices.Add(v2);
        meshData.Vertices.Add(v3);

        // Calculate normal from vertices
        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v3 - v0;
        Vector3 normal = edge1.Cross(edge2).Normalized();

        // Add normals
        meshData.Normals.Add(normal);
        meshData.Normals.Add(normal);
        meshData.Normals.Add(normal);
        meshData.Normals.Add(normal);

        // Add colors
        meshData.Colors.Add(color);
        meshData.Colors.Add(color);
        meshData.Colors.Add(color);
        meshData.Colors.Add(color);

        // Add UVs (simple mapping)
        meshData.UVs.Add(new Vector2(0, 0));
        meshData.UVs.Add(new Vector2(1, 0));
        meshData.UVs.Add(new Vector2(1, 1));
        meshData.UVs.Add(new Vector2(0, 1));

        // Add ambient occlusion values (full brightness for decorations)
        meshData.AmbientOcclusion.Add(1.0f);
        meshData.AmbientOcclusion.Add(1.0f);
        meshData.AmbientOcclusion.Add(1.0f);
        meshData.AmbientOcclusion.Add(1.0f);

        // Add indices for two triangles
        meshData.Indices.Add(vertexOffset);
        meshData.Indices.Add(vertexOffset + 1);
        meshData.Indices.Add(vertexOffset + 2);

        meshData.Indices.Add(vertexOffset);
        meshData.Indices.Add(vertexOffset + 2);
        meshData.Indices.Add(vertexOffset + 3);
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
    public List<Color> Colors { get; private set; } = new List<Color>();
}
