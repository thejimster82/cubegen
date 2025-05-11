using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation.POI
{
    /// <summary>
    /// Represents an Axis-Aligned Bounding Box for a POI structure
    /// </summary>
    public struct POIAABB
    {
        public int MinX;
        public int MaxX;
        public int MinY;
        public int MaxY;
        public int MinZ;
        public int MaxZ;

        public POIAABB(int minX, int maxX, int minY, int maxY, int minZ, int maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        /// <summary>
        /// Check if this AABB contains a point
        /// </summary>
        public bool Contains(int x, int y, int z)
        {
            return x >= MinX && x <= MaxX &&
                   y >= MinY && y <= MaxY &&
                   z >= MinZ && z <= MaxZ;
        }

        /// <summary>
        /// Get all chunk positions that this AABB overlaps with
        /// </summary>
        public HashSet<Vector2I> GetOverlappingChunks(int chunkSize)
        {
            HashSet<Vector2I> chunks = new HashSet<Vector2I>();

            int minChunkX = MinX / chunkSize;
            int maxChunkX = MaxX / chunkSize;
            int minChunkZ = MinZ / chunkSize;
            int maxChunkZ = MaxZ / chunkSize;

            for (int cx = minChunkX; cx <= maxChunkX; cx++)
            {
                for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
                {
                    chunks.Add(new Vector2I(cx, cz));
                }
            }

            return chunks;
        }
    }

    /// <summary>
    /// Handles direct voxel modifications for Points of Interest
    /// This approach ensures consistent POI generation across chunk boundaries
    /// </summary>
    public class POIVoxelModifier
    {
        // Cache of POI voxel modifications to ensure consistency across chunks
        // Key is world position (x, z), value is a dictionary of y-coordinate to voxel type
        private static Dictionary<Vector2I, Dictionary<int, VoxelType>> _voxelModifications = new Dictionary<Vector2I, Dictionary<int, VoxelType>>();

        // Cache of POI terrain height modifications
        // Key is world position (x, z), value is the modified terrain height
        private static Dictionary<Vector2I, int> _terrainHeightModifications = new Dictionary<Vector2I, int>();

        // Track which chunks are affected by each POI
        // Key is POI position, value is a set of chunk positions
        private static Dictionary<Vector2I, HashSet<Vector2I>> _poiAffectedChunks = new Dictionary<Vector2I, HashSet<Vector2I>>();

        // Track which POIs have been processed
        private static HashSet<Vector2I> _processedPOIs = new HashSet<Vector2I>();

        // Store the AABB for each POI
        // Key is POI position, value is the AABB
        private static Dictionary<Vector2I, POIAABB> _poiAABBs = new Dictionary<Vector2I, POIAABB>();

        // Debug counter for POIs processed
        private static int _poisProcessed = 0;

        /// <summary>
        /// Generate all voxel modifications for a POI
        /// This is called once when the POI is first encountered
        /// </summary>
        public static void GeneratePOIVoxels(PointOfInterest poi, int chunkHeight, float waterLevel)
        {
            // Check if we've already processed this POI
            Vector2I poiPos = new Vector2I(poi.Position.X, poi.Position.Y);
            if (_processedPOIs.Contains(poiPos))
            {
                return;
            }

            // Skip if this POI has already been processed
            if (_voxelModifications.ContainsKey(poi.Position))
            {
                return;
            }

            GD.Print($"Generating voxel modifications for POI {poi.Type} at {poi.Position}");

            // Mark this POI as processed
            _processedPOIs.Add(poiPos);
            _poisProcessed++;

            // Initialize the set of affected chunks for this POI
            if (!_poiAffectedChunks.ContainsKey(poiPos))
            {
                _poiAffectedChunks[poiPos] = new HashSet<Vector2I>();
            }

            // Generate voxel modifications based on POI type
            switch (poi.Type)
            {
                case POIType.Tower:
                    GenerateTower(poi, chunkHeight, waterLevel);
                    break;
                case POIType.RockFormation:
                    GenerateRockFormation(poi, chunkHeight, waterLevel);
                    break;
                case POIType.Ruin:
                    GenerateRuin(poi, chunkHeight, waterLevel);
                    break;
                case POIType.Waterfall:
                    GenerateWaterfall(poi, chunkHeight, waterLevel);
                    break;
                default:
                    // Default simple structure
                    GenerateDefaultStructure(poi, chunkHeight, waterLevel);
                    break;
            }

            // Mark all chunks within the POI's influence radius as affected
            MarkAffectedChunks(poi);
        }

        /// <summary>
        /// Mark all chunks that are affected by a POI
        /// </summary>
        private static void MarkAffectedChunks(PointOfInterest poi)
        {
            Vector2I poiPos = new Vector2I(poi.Position.X, poi.Position.Y);
            int chunkSize = 16; // Standard chunk size

            // If we have an AABB for this POI, use it to determine affected chunks
            if (_poiAABBs.TryGetValue(poiPos, out POIAABB aabb))
            {
                // Get all chunks that overlap with the AABB
                HashSet<Vector2I> overlappingChunks = aabb.GetOverlappingChunks(chunkSize);

                // Add all these chunks to the affected chunks set
                foreach (Vector2I chunkPos in overlappingChunks)
                {
                    _poiAffectedChunks[poiPos].Add(chunkPos);
                }

                GD.Print($"POI at {poiPos} affects {overlappingChunks.Count} chunks based on AABB");
            }
            else
            {
                // Fallback to using influence radius if no AABB is available
                // Calculate the range of chunks affected by this POI
                int minChunkX = (poi.Position.X - poi.InfluenceRadius) / chunkSize;
                int maxChunkX = (poi.Position.X + poi.InfluenceRadius) / chunkSize;
                int minChunkZ = (poi.Position.Y - poi.InfluenceRadius) / chunkSize;
                int maxChunkZ = (poi.Position.Y + poi.InfluenceRadius) / chunkSize;

                // Mark all chunks in this range as affected by this POI
                for (int cx = minChunkX; cx <= maxChunkX; cx++)
                {
                    for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
                    {
                        Vector2I chunkPos = new Vector2I(cx, cz);
                        _poiAffectedChunks[poiPos].Add(chunkPos);
                    }
                }

                GD.Print($"POI at {poiPos} affects chunks based on influence radius (no AABB available)");
            }
        }

        /// <summary>
        /// Check if a chunk is affected by any POI
        /// </summary>
        public static bool IsChunkAffectedByPOI(Vector2I chunkPos)
        {
            foreach (var poiEntry in _poiAffectedChunks)
            {
                if (poiEntry.Value.Contains(chunkPos))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get the AABB for a POI if it exists
        /// </summary>
        public static bool TryGetPOIAABB(Vector2I poiPos, out POIAABB aabb)
        {
            return _poiAABBs.TryGetValue(poiPos, out aabb);
        }

        /// <summary>
        /// Get all POIs that affect a chunk
        /// </summary>
        public static List<Vector2I> GetPOIsAffectingChunk(Vector2I chunkPos)
        {
            List<Vector2I> affectingPOIs = new List<Vector2I>();

            foreach (var poiEntry in _poiAffectedChunks)
            {
                if (poiEntry.Value.Contains(chunkPos))
                {
                    affectingPOIs.Add(poiEntry.Key);
                }
            }

            return affectingPOIs;
        }

        /// <summary>
        /// Get the modified terrain height at a specific world position
        /// </summary>
        public static int GetModifiedTerrainHeight(int worldX, int worldZ, int originalHeight)
        {
            Vector2I pos = new Vector2I(worldX, worldZ);

            if (_terrainHeightModifications.TryGetValue(pos, out int modifiedHeight))
            {
                return modifiedHeight;
            }

            return originalHeight;
        }

        /// <summary>
        /// Get the modified voxel type at a specific world position
        /// </summary>
        public static VoxelType GetModifiedVoxelType(int worldX, int worldY, int worldZ, VoxelType originalType)
        {
            Vector2I pos = new Vector2I(worldX, worldZ);

            if (_voxelModifications.TryGetValue(pos, out Dictionary<int, VoxelType> yModifications))
            {
                if (yModifications.TryGetValue(worldY, out VoxelType modifiedType))
                {
                    return modifiedType;
                }
            }

            return originalType;
        }

        /// <summary>
        /// Set a voxel modification at a specific world position
        /// </summary>
        private static void SetVoxel(int worldX, int worldY, int worldZ, VoxelType voxelType)
        {
            Vector2I pos = new Vector2I(worldX, worldZ);

            if (!_voxelModifications.TryGetValue(pos, out Dictionary<int, VoxelType> yModifications))
            {
                yModifications = new Dictionary<int, VoxelType>();
                _voxelModifications[pos] = yModifications;
            }

            yModifications[worldY] = voxelType;
        }

        /// <summary>
        /// Set a terrain height modification at a specific world position
        /// </summary>
        private static void SetTerrainHeight(int worldX, int worldZ, int height)
        {
            Vector2I pos = new Vector2I(worldX, worldZ);
            _terrainHeightModifications[pos] = height;
        }

        /// <summary>
        /// Calculate and store the AABB for a POI structure
        /// </summary>
        public static void CalculateAndStoreAABB(PointOfInterest poi, int minX, int maxX, int minY, int maxY, int minZ, int maxZ)
        {
            Vector2I poiPos = new Vector2I(poi.Position.X, poi.Position.Y);

            // Create the AABB
            POIAABB aabb = new POIAABB(minX, maxX, minY, maxY, minZ, maxZ);

            // Store it
            _poiAABBs[poiPos] = aabb;

            GD.Print($"Calculated AABB for POI at {poiPos}: X({minX}-{maxX}), Y({minY}-{maxY}), Z({minZ}-{maxZ})");
        }

        /// <summary>
        /// Generate a tower structure
        /// </summary>
        private static void GenerateTower(PointOfInterest poi, int chunkHeight, float waterLevel)
        {
            // Create a random generator with the POI's seed
            Random random = new Random(poi.Seed);

            // Tower parameters - ensure they fit within influence radius
            int maxRadius = poi.InfluenceRadius / 4; // Ensure structure fits well within influence radius
            int baseRadius = Math.Min(6, maxRadius);

            // Adjust tower height based on influence radius
            int maxHeight = poi.InfluenceRadius / 2;
            int towerHeight = Math.Min(30 + random.Next(20), maxHeight); // 30-50 blocks tall, but capped by influence radius

            int baseHeight = Mathf.FloorToInt(waterLevel * chunkHeight) + 2; // Slightly above water level

            // Create a flat base for the tower
            int minX = poi.Position.X - baseRadius * 2;
            int maxX = poi.Position.X + baseRadius * 2;
            int minZ = poi.Position.Y - baseRadius * 2;
            int maxZ = poi.Position.Y + baseRadius * 2;

            // Calculate the AABB for this tower
            int aabbMinX = minX;
            int aabbMaxX = maxX;
            int aabbMinY = baseHeight;
            int aabbMaxY = baseHeight + towerHeight;
            int aabbMinZ = minZ;
            int aabbMaxZ = maxZ;

            // Store the AABB
            CalculateAndStoreAABB(poi, aabbMinX, aabbMaxX, aabbMinY, aabbMaxY, aabbMinZ, aabbMaxZ);

            // Flatten terrain around the tower
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    int dx = x - poi.Position.X;
                    int dz = z - poi.Position.Y;
                    float distanceSquared = dx * dx + dz * dz;

                    // Ensure we're within the POI's influence radius
                    if (distanceSquared <= baseRadius * baseRadius * 4 &&
                        distanceSquared <= poi.InfluenceRadius * poi.InfluenceRadius)
                    {
                        // Set terrain height to base height
                        SetTerrainHeight(x, z, baseHeight);

                        // Set the surface to stone
                        SetVoxel(x, baseHeight, z, VoxelType.Stone);
                    }
                }
            }

            // Build the tower
            for (int y = baseHeight + 1; y <= baseHeight + towerHeight; y++)
            {
                // Tower gets slightly narrower as it goes up
                float heightFactor = 1.0f - ((float)(y - baseHeight) / towerHeight * 0.3f);
                int currentRadius = Mathf.FloorToInt(baseRadius * heightFactor);

                // Add floors every 5 blocks
                bool isFloor = (y - baseHeight) % 5 == 0;

                for (int x = poi.Position.X - currentRadius; x <= poi.Position.X + currentRadius; x++)
                {
                    for (int z = poi.Position.Y - currentRadius; z <= poi.Position.Y + currentRadius; z++)
                    {
                        int dx = x - poi.Position.X;
                        int dz = z - poi.Position.Y;
                        float distanceSquared = dx * dx + dz * dz;

                        // Create walls (hollow tower) and ensure we're within the POI's influence radius
                        if (distanceSquared <= currentRadius * currentRadius &&
                            (distanceSquared >= (currentRadius - 1) * (currentRadius - 1) || isFloor) &&
                            distanceSquared <= poi.InfluenceRadius * poi.InfluenceRadius)
                        {
                            // Use different materials for variety
                            VoxelType material = VoxelType.Stone;

                            // Add some snow at the top
                            if (y > baseHeight + towerHeight * 0.8f)
                            {
                                material = VoxelType.Snow;
                            }

                            SetVoxel(x, y, z, material);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generate a rock formation
        /// </summary>
        private static void GenerateRockFormation(PointOfInterest poi, int chunkHeight, float waterLevel)
        {
            // Create a random generator with the POI's seed
            Random random = new Random(poi.Seed);

            // Rock formation parameters - ensure they fit within influence radius
            int maxRadius = poi.InfluenceRadius / 4; // Ensure structure fits well within influence radius
            int baseRadius = Math.Min(8, maxRadius);

            // Adjust height based on influence radius
            int maxHeight = poi.InfluenceRadius / 3;
            int height = Math.Min(15 + random.Next(10), maxHeight); // 15-25 blocks tall, but capped by influence radius

            int baseHeight = Mathf.FloorToInt(waterLevel * chunkHeight) + 1; // Slightly above water level

            // Calculate the AABB for this rock formation
            int aabbMinX = poi.Position.X - baseRadius;
            int aabbMaxX = poi.Position.X + baseRadius;
            int aabbMinY = baseHeight;
            int aabbMaxY = baseHeight + height;
            int aabbMinZ = poi.Position.Y - baseRadius;
            int aabbMaxZ = poi.Position.Y + baseRadius;

            // Store the AABB
            CalculateAndStoreAABB(poi, aabbMinX, aabbMaxX, aabbMinY, aabbMaxY, aabbMinZ, aabbMaxZ);

            // Create the rock formation
            for (int y = baseHeight; y <= baseHeight + height; y++)
            {
                // Formation gets narrower as it goes up
                float heightFactor = 1.0f - ((float)(y - baseHeight) / height * 0.7f);
                int currentRadius = Mathf.FloorToInt(baseRadius * heightFactor);

                for (int x = poi.Position.X - currentRadius; x <= poi.Position.X + currentRadius; x++)
                {
                    for (int z = poi.Position.Y - currentRadius; z <= poi.Position.Y + currentRadius; z++)
                    {
                        int dx = x - poi.Position.X;
                        int dz = z - poi.Position.Y;
                        float distanceSquared = dx * dx + dz * dz;

                        // Create a somewhat irregular shape
                        float noise = (float)random.NextDouble() * 0.3f;
                        float radiusSquared = (currentRadius * (1.0f - noise)) * (currentRadius * (1.0f - noise));

                        // Ensure we're within the POI's influence radius
                        if (distanceSquared <= radiusSquared &&
                            distanceSquared <= poi.InfluenceRadius * poi.InfluenceRadius)
                        {
                            // Set terrain height
                            if (y == baseHeight)
                            {
                                SetTerrainHeight(x, z, baseHeight);
                            }

                            // Use different materials for variety
                            VoxelType material = VoxelType.Stone;

                            // Add some snow at the top
                            if (y > baseHeight + height * 0.7f)
                            {
                                material = VoxelType.Snow;
                            }

                            SetVoxel(x, y, z, material);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generate ruins
        /// </summary>
        private static void GenerateRuin(PointOfInterest poi, int chunkHeight, float waterLevel)
        {
            // Implementation for ruins
            // Similar to tower but with more randomness and gaps
            // Create a random generator with the POI's seed
            Random random = new Random(poi.Seed);

            // Ruin parameters - ensure they fit within influence radius
            int maxRadius = poi.InfluenceRadius / 4; // Ensure structure fits well within influence radius
            int baseRadius = Math.Min(7, maxRadius);

            // Adjust height based on influence radius
            int maxHeight = poi.InfluenceRadius / 3;
            int height = Math.Min(10 + random.Next(8), maxHeight); // 10-18 blocks tall, but capped by influence radius

            int baseHeight = Mathf.FloorToInt(waterLevel * chunkHeight) + 1; // Slightly above water level

            // Create a flat base for the ruin
            int minX = poi.Position.X - baseRadius * 2;
            int maxX = poi.Position.X + baseRadius * 2;
            int minZ = poi.Position.Y - baseRadius * 2;
            int maxZ = poi.Position.Y + baseRadius * 2;

            // Calculate the AABB for this ruin
            int aabbMinX = minX;
            int aabbMaxX = maxX;
            int aabbMinY = baseHeight;
            int aabbMaxY = baseHeight + height;
            int aabbMinZ = minZ;
            int aabbMaxZ = maxZ;

            // Store the AABB
            CalculateAndStoreAABB(poi, aabbMinX, aabbMaxX, aabbMinY, aabbMaxY, aabbMinZ, aabbMaxZ);

            // Flatten terrain around the ruin
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    int dx = x - poi.Position.X;
                    int dz = z - poi.Position.Y;
                    float distanceSquared = dx * dx + dz * dz;

                    // Ensure we're within the POI's influence radius
                    if (distanceSquared <= baseRadius * baseRadius * 3 &&
                        distanceSquared <= poi.InfluenceRadius * poi.InfluenceRadius)
                    {
                        // Set terrain height to base height
                        SetTerrainHeight(x, z, baseHeight);

                        // Set the surface to sand or stone
                        SetVoxel(x, baseHeight, z, VoxelType.Sand);
                    }
                }
            }

            // Build the ruin
            for (int y = baseHeight + 1; y <= baseHeight + height; y++)
            {
                // Ruin gets narrower as it goes up
                float heightFactor = 1.0f - ((float)(y - baseHeight) / height * 0.4f);
                int currentRadius = Mathf.FloorToInt(baseRadius * heightFactor);

                for (int x = poi.Position.X - currentRadius; x <= poi.Position.X + currentRadius; x++)
                {
                    for (int z = poi.Position.Y - currentRadius; z <= poi.Position.Y + currentRadius; z++)
                    {
                        int dx = x - poi.Position.X;
                        int dz = z - poi.Position.Y;
                        float distanceSquared = dx * dx + dz * dz;

                        // Create walls with gaps for a ruined look and ensure we're within the POI's influence radius
                        if (distanceSquared <= currentRadius * currentRadius &&
                            distanceSquared >= (currentRadius - 1) * (currentRadius - 1) &&
                            distanceSquared <= poi.InfluenceRadius * poi.InfluenceRadius)
                        {
                            // Add random gaps to make it look ruined
                            float ruinChance = (float)(y - baseHeight) / height; // More gaps higher up
                            if (random.NextDouble() > ruinChance * 0.7f)
                            {
                                SetVoxel(x, y, z, VoxelType.Sand);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generate a waterfall
        /// </summary>
        private static void GenerateWaterfall(PointOfInterest poi, int chunkHeight, float waterLevel)
        {
            // Implementation for waterfall
            // Create a random generator with the POI's seed
            Random random = new Random(poi.Seed);

            // Waterfall parameters - ensure they fit within influence radius
            int maxRadius = poi.InfluenceRadius / 5; // Ensure structure fits well within influence radius
            int baseRadius = Math.Min(5, maxRadius);

            // Adjust height based on influence radius
            int maxHeight = poi.InfluenceRadius / 2;
            int height = Math.Min(20 + random.Next(10), maxHeight); // 20-30 blocks tall, but capped by influence radius

            int baseHeight = Mathf.FloorToInt(waterLevel * chunkHeight) - 2; // Below water level for the pool

            // Create a pool at the bottom
            int poolRadius = Math.Min(baseRadius * 2, poi.InfluenceRadius / 3);

            // Calculate the AABB for this waterfall
            int aabbMinX = poi.Position.X - poolRadius;
            int aabbMaxX = poi.Position.X + poolRadius;
            int aabbMinY = baseHeight;
            int aabbMaxY = baseHeight + height;
            int aabbMinZ = poi.Position.Y - poolRadius;
            int aabbMaxZ = poi.Position.Y + poolRadius;

            // Store the AABB
            CalculateAndStoreAABB(poi, aabbMinX, aabbMaxX, aabbMinY, aabbMaxY, aabbMinZ, aabbMaxZ);

            for (int x = poi.Position.X - poolRadius; x <= poi.Position.X + poolRadius; x++)
            {
                for (int z = poi.Position.Y - poolRadius; z <= poi.Position.Y + poolRadius; z++)
                {
                    int dx = x - poi.Position.X;
                    int dz = z - poi.Position.Y;
                    float distanceSquared = dx * dx + dz * dz;

                    // Ensure we're within the POI's influence radius
                    if (distanceSquared <= poolRadius * poolRadius &&
                        distanceSquared <= poi.InfluenceRadius * poi.InfluenceRadius)
                    {
                        // Set terrain height to base height
                        SetTerrainHeight(x, z, baseHeight);

                        // Fill with water up to water level
                        for (int y = baseHeight + 1; y <= Mathf.FloorToInt(waterLevel * chunkHeight); y++)
                        {
                            SetVoxel(x, y, z, VoxelType.Water);
                        }
                    }
                }
            }

            // Create the waterfall
            for (int y = baseHeight + 1; y <= baseHeight + height; y++)
            {
                // Waterfall gets narrower as it goes up
                float heightFactor = 1.0f - ((float)(y - baseHeight) / height * 0.5f);
                int currentRadius = Mathf.FloorToInt(baseRadius * heightFactor);

                for (int x = poi.Position.X - currentRadius; x <= poi.Position.X + currentRadius; x++)
                {
                    for (int z = poi.Position.Y - currentRadius; z <= poi.Position.Y + currentRadius; z++)
                    {
                        int dx = x - poi.Position.X;
                        int dz = z - poi.Position.Y;
                        float distanceSquared = dx * dx + dz * dz;

                        // Create the water stream and ensure we're within the POI's influence radius
                        if (distanceSquared <= currentRadius * currentRadius &&
                            distanceSquared <= poi.InfluenceRadius * poi.InfluenceRadius)
                        {
                            // Set water voxels
                            SetVoxel(x, y, z, VoxelType.Water);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generate a default simple structure
        /// </summary>
        private static void GenerateDefaultStructure(PointOfInterest poi, int chunkHeight, float waterLevel)
        {
            // Create a random generator with the POI's seed
            Random random = new Random(poi.Seed);

            // Structure parameters - ensure they fit within influence radius
            int maxRadius = poi.InfluenceRadius / 3; // Ensure structure fits well within influence radius
            int baseRadius = Math.Min(4, maxRadius);
            int height = 8 + random.Next(5); // 8-13 blocks tall
            int baseHeight = Mathf.FloorToInt(waterLevel * chunkHeight) + 1; // Slightly above water level

            // Calculate the AABB for this structure
            int aabbMinX = poi.Position.X - baseRadius;
            int aabbMaxX = poi.Position.X + baseRadius;
            int aabbMinY = baseHeight;
            int aabbMaxY = baseHeight + height;
            int aabbMinZ = poi.Position.Y - baseRadius;
            int aabbMaxZ = poi.Position.Y + baseRadius;

            // Store the AABB
            CalculateAndStoreAABB(poi, aabbMinX, aabbMaxX, aabbMinY, aabbMaxY, aabbMinZ, aabbMaxZ);

            // Create a simple structure
            for (int y = baseHeight; y <= baseHeight + height; y++)
            {
                // Structure gets narrower as it goes up
                float heightFactor = 1.0f - ((float)(y - baseHeight) / height * 0.5f);
                int currentRadius = Mathf.FloorToInt(baseRadius * heightFactor);

                for (int x = poi.Position.X - currentRadius; x <= poi.Position.X + currentRadius; x++)
                {
                    for (int z = poi.Position.Y - currentRadius; z <= poi.Position.Y + currentRadius; z++)
                    {
                        int dx = x - poi.Position.X;
                        int dz = z - poi.Position.Y;
                        float distanceSquared = dx * dx + dz * dz;

                        // Ensure we're within the POI's influence radius
                        if (distanceSquared <= currentRadius * currentRadius &&
                            distanceSquared <= poi.InfluenceRadius * poi.InfluenceRadius)
                        {
                            // Set terrain height
                            if (y == baseHeight)
                            {
                                SetTerrainHeight(x, z, baseHeight);
                            }

                            // Use stone for the structure
                            SetVoxel(x, y, z, VoxelType.Stone);
                        }
                    }
                }
            }
        }
    }
}
