using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using CubeGen.World.Common;

namespace CubeGen.World.Generation.POI
{
    /// <summary>
    /// Manages the generation and tracking of Points of Interest in the world
    /// Uses a direct noise-based approach for more efficient POI generation
    /// </summary>
    public class POIManager
    {
        // Singleton instance
        private static POIManager _instance;
        public static POIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new POIManager();
                }
                return _instance;
            }
        }

        // World seed for consistent generation
        private int _worldSeed;

        // Maximum influence radius of any POI (used for search optimization)
        private const int MAX_POI_INFLUENCE_RADIUS = 150; // Increased significantly to ensure we don't miss any POIs

        // Cache for POIs affecting chunks to avoid recalculation
        // Key is chunk position, value is list of POIs affecting that chunk
        private ConcurrentDictionary<Vector2I, List<PointOfInterest>> _chunkPOICache = new ConcurrentDictionary<Vector2I, List<PointOfInterest>>();

        // Cache for POI positions to ensure consistent POI generation across chunks
        // This helps ensure that all chunks recognize the same POIs
        private ConcurrentDictionary<Vector2I, PointOfInterest> _poiPositionCache = new ConcurrentDictionary<Vector2I, PointOfInterest>();

        // Private constructor for singleton
        private POIManager()
        {
        }

        /// <summary>
        /// Initialize the POI manager with the world seed
        /// </summary>
        public void Initialize(int worldSeed)
        {
            _worldSeed = worldSeed;

            // Initialize the POI generator with the same seed
            POIGenerator.Instance.Initialize(worldSeed);

            GD.Print($"POIManager initialized with seed: {worldSeed}");
        }

        /// <summary>
        /// Get all POIs that affect a specific chunk
        /// </summary>
        public List<PointOfInterest> GetPOIsAffectingChunk(Vector2I chunkPos, int chunkSize)
        {
            // Check if we have cached results for this chunk
            if (_chunkPOICache.TryGetValue(chunkPos, out List<PointOfInterest> cachedPOIs))
            {
                return cachedPOIs;
            }

            // Get POIs from the generator
            List<PointOfInterest> pois = POIGenerator.Instance.GetPOIsAffectingChunk(chunkPos, chunkSize, MAX_POI_INFLUENCE_RADIUS);

            // Cache POIs by their position to ensure consistency
            foreach (var poi in pois)
            {
                _poiPositionCache[poi.Position] = poi;
            }

            // Check neighboring chunks for POIs that might affect this chunk but weren't detected
            // This helps ensure consistent POI detection across chunk boundaries
            for (int xOffset = -1; xOffset <= 1; xOffset++)
            {
                for (int zOffset = -1; zOffset <= 1; zOffset++)
                {
                    if (xOffset == 0 && zOffset == 0) continue; // Skip the current chunk

                    Vector2I neighborPos = new Vector2I(chunkPos.X + xOffset, chunkPos.Y + zOffset);

                    // If the neighbor has cached POIs, check if any should affect this chunk
                    if (_chunkPOICache.TryGetValue(neighborPos, out List<PointOfInterest> neighborPOIs))
                    {
                        foreach (var poi in neighborPOIs)
                        {
                            // Calculate if this POI should affect the current chunk
                            if (ShouldPOIAffectChunk(poi, chunkPos, chunkSize) && !pois.Any(p => p.Position == poi.Position))
                            {
                                pois.Add(poi);
                                GD.Print($"Added neighbor POI at {poi.Position} to chunk {chunkPos}");
                            }
                        }
                    }
                }
            }

            // Cache the results
            _chunkPOICache[chunkPos] = pois;

            if (pois.Count > 0)
            {
                GD.Print($"Chunk {chunkPos} has {pois.Count} POIs affecting it");
            }

            return pois;
        }

        /// <summary>
        /// Determines if a POI should affect a specific chunk
        /// </summary>
        private bool ShouldPOIAffectChunk(PointOfInterest poi, Vector2I chunkPos, int chunkSize)
        {
            // Calculate chunk boundaries
            int chunkMinX = chunkPos.X * chunkSize;
            int chunkMaxX = chunkMinX + chunkSize;
            int chunkMinZ = chunkPos.Y * chunkSize;
            int chunkMaxZ = chunkMinZ + chunkSize;

            // Calculate the maximum distance from the POI to the chunk
            int maxDistanceX = Math.Max(Math.Abs(chunkMinX - poi.Position.X), Math.Abs(chunkMaxX - poi.Position.X));
            int maxDistanceZ = Math.Max(Math.Abs(chunkMinZ - poi.Position.Y), Math.Abs(chunkMaxZ - poi.Position.Y));

            // If the POI is inside the chunk, it definitely affects it
            if (poi.Position.X >= chunkMinX && poi.Position.X < chunkMaxX &&
                poi.Position.Y >= chunkMinZ && poi.Position.Y < chunkMaxZ)
            {
                return true;
            }

            // Check if the POI's influence radius overlaps with the chunk
            return Math.Max(maxDistanceX, maxDistanceZ) <= poi.InfluenceRadius;
        }

        /// <summary>
        /// Get all POIs within a certain radius of a position
        /// </summary>
        public List<PointOfInterest> GetPOIsInRadius(Vector2I position, int radius)
        {
            List<PointOfInterest> result = new List<PointOfInterest>();

            // Use a grid-based approach to sample potential POI locations
            int gridStep = 8; // Significantly reduced for much better coverage

            // Calculate search area
            int minX = position.X - radius;
            int maxX = position.X + radius;
            int minZ = position.Y - radius;
            int maxZ = position.Y + radius;

            for (int x = minX; x <= maxX; x += gridStep)
            {
                for (int z = minZ; z <= maxZ; z += gridStep)
                {
                    // Check if there's a POI at this grid point
                    PointOfInterest poi = POIGenerator.Instance.GetPOIAt(x, z);

                    if (poi != null)
                    {
                        // Check if this POI is within the radius
                        int dx = poi.Position.X - position.X;
                        int dz = poi.Position.Y - position.Y;
                        float distanceSquared = dx * dx + dz * dz;

                        if (distanceSquared <= radius * radius)
                        {
                            result.Add(poi);
                        }
                    }
                }
            }

            return result;
        }

    }
}
