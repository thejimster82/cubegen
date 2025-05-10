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
        private const int MAX_POI_INFLUENCE_RADIUS = 100; // Increased from 80 to ensure we don't miss any POIs

        // Cache for POIs affecting chunks to avoid recalculation
        // Key is chunk position, value is list of POIs affecting that chunk
        private ConcurrentDictionary<Vector2I, List<PointOfInterest>> _chunkPOICache = new ConcurrentDictionary<Vector2I, List<PointOfInterest>>();

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

            // Cache the results
            _chunkPOICache[chunkPos] = pois;

            return pois;
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

        /// <summary>
        /// Get the nearest POI to a position
        /// </summary>
        public PointOfInterest GetNearestPOI(Vector2I position, int maxSearchRadius = 500)
        {
            PointOfInterest nearest = null;
            float nearestDistanceSquared = float.MaxValue;

            // Start with a small radius and expand until we find something
            for (int radius = 100; radius <= maxSearchRadius; radius += 100)
            {
                List<PointOfInterest> pois = GetPOIsInRadius(position, radius);

                foreach (PointOfInterest poi in pois)
                {
                    int dx = poi.Position.X - position.X;
                    int dz = poi.Position.Y - position.Y;
                    float distanceSquared = dx * dx + dz * dz;

                    if (distanceSquared < nearestDistanceSquared)
                    {
                        nearest = poi;
                        nearestDistanceSquared = distanceSquared;
                    }
                }

                // If we found at least one POI, return the nearest
                if (nearest != null)
                {
                    return nearest;
                }
            }

            return null; // No POI found within max search radius
        }

        /// <summary>
        /// Clear the POI cache for a specific chunk
        /// </summary>
        public void ClearChunkPOICache(Vector2I chunkPos)
        {
            _chunkPOICache.TryRemove(chunkPos, out _);
        }

        /// <summary>
        /// Clear the entire POI cache
        /// </summary>
        public void ClearAllPOICache()
        {
            _chunkPOICache.Clear();
        }
    }
}
