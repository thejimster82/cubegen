using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation
{
    /// <summary>
    /// Represents a large biome object that can span across multiple chunks
    /// </summary>
    public class BiomeObject
    {
        // Unique identifier for this biome object
        public Guid Id { get; private set; }

        // Type of biome object
        public BiomeObjectType Type { get; private set; }

        // World position of the anchor point (the voxel that determines if this object exists in a chunk)
        public Vector3I AnchorPoint { get; private set; }

        // Bounding box of the object in world coordinates
        public BoundingBox3D BoundingBox { get; private set; }

        // Biome type this object belongs to
        public BiomeType BiomeType { get; private set; }

        // Dictionary of voxels that make up this object
        // Key is a Vector3I representing the position relative to the anchor point
        // Value is the voxel type at that position
        private Dictionary<Vector3I, VoxelType> _voxels = new Dictionary<Vector3I, VoxelType>();

        // Dictionary to store decoration placement information
        // Key is a Vector3I representing the position relative to the anchor point
        // Value is the placement data
        private Dictionary<Vector3I, DecorationClusters.DecorationPlacement> _decorationPlacements =
            new Dictionary<Vector3I, DecorationClusters.DecorationPlacement>();

        /// <summary>
        /// Creates a new biome object
        /// </summary>
        /// <param name="type">Type of biome object</param>
        /// <param name="anchorPoint">World position of the anchor point</param>
        /// <param name="biomeType">Biome type this object belongs to</param>
        public BiomeObject(BiomeObjectType type, Vector3I anchorPoint, BiomeType biomeType)
        {
            Id = Guid.NewGuid();
            Type = type;
            AnchorPoint = anchorPoint;
            BiomeType = biomeType;

            // Initialize with an empty bounding box at the anchor point
            BoundingBox = new BoundingBox3D(new Vector3(anchorPoint.X, anchorPoint.Y, anchorPoint.Z), Vector3.Zero);
        }

        /// <summary>
        /// Sets a voxel in this biome object
        /// </summary>
        /// <param name="relativePosition">Position relative to the anchor point</param>
        /// <param name="voxelType">Type of voxel to set</param>
        public void SetVoxel(Vector3I relativePosition, VoxelType voxelType)
        {
            _voxels[relativePosition] = voxelType;

            // Update bounding box
            Vector3I worldPosInt = AnchorPoint + relativePosition;
            Vector3 worldPos = new Vector3(worldPosInt.X, worldPosInt.Y, worldPosInt.Z);
            BoundingBox = BoundingBox.Expand(worldPos);
        }

        /// <summary>
        /// Gets the voxel type at a position relative to the anchor point
        /// </summary>
        /// <param name="relativePosition">Position relative to the anchor point</param>
        /// <returns>Voxel type at the position, or Air if not set</returns>
        public VoxelType GetVoxel(Vector3I relativePosition)
        {
            if (_voxels.TryGetValue(relativePosition, out VoxelType voxelType))
            {
                return voxelType;
            }

            return VoxelType.Air;
        }

        /// <summary>
        /// Sets decoration placement information for a voxel
        /// </summary>
        /// <param name="relativePosition">Position relative to the anchor point</param>
        /// <param name="placement">Decoration placement information</param>
        public void SetDecorationPlacement(Vector3I relativePosition, DecorationClusters.DecorationPlacement placement)
        {
            _decorationPlacements[relativePosition] = placement;
        }

        /// <summary>
        /// Gets decoration placement information for a voxel
        /// </summary>
        /// <param name="relativePosition">Position relative to the anchor point</param>
        /// <param name="placement">Output decoration placement information</param>
        /// <returns>True if decoration placement information exists, false otherwise</returns>
        public bool TryGetDecorationPlacement(Vector3I relativePosition, out DecorationClusters.DecorationPlacement placement)
        {
            return _decorationPlacements.TryGetValue(relativePosition, out placement);
        }

        /// <summary>
        /// Checks if a world position is within this biome object's bounding box
        /// </summary>
        /// <param name="worldPosition">World position to check</param>
        /// <returns>True if the position is within the bounding box, false otherwise</returns>
        public bool ContainsWorldPosition(Vector3I worldPosition)
        {
            // Convert to Vector3 for bounding box check
            Vector3 pos = new Vector3(worldPosition.X, worldPosition.Y, worldPosition.Z);
            return BoundingBox.Contains(pos);
        }

        /// <summary>
        /// Checks if this biome object intersects with a chunk
        /// </summary>
        /// <param name="chunkPos">Position of the chunk</param>
        /// <param name="chunkSize">Size of the chunk</param>
        /// <returns>True if the object intersects with the chunk, false otherwise</returns>
        public bool IntersectsChunk(Vector2I chunkPos, int chunkSize)
        {
            // Calculate chunk bounds
            Vector3 chunkMin = new Vector3(chunkPos.X * chunkSize, 0, chunkPos.Y * chunkSize);
            Vector3 chunkMax = new Vector3((chunkPos.X + 1) * chunkSize, 256, (chunkPos.Y + 1) * chunkSize);
            BoundingBox3D chunkBounds = new BoundingBox3D(chunkMin, chunkMax - chunkMin, true);

            // Check if the bounding boxes intersect
            bool intersects = BoundingBox.Intersects(chunkBounds);

            // Debug output for the first few chunks
            if (chunkPos.X >= -2 && chunkPos.X <= 2 && chunkPos.Y >= -2 && chunkPos.Y <= 2)
            {
                GD.Print($"[BiomeObject] Object {Id} of type {Type} intersection check with chunk {chunkPos}:");
                GD.Print($"[BiomeObject]   Object bounds: Min={BoundingBox.Min}, Max={BoundingBox.Max}");
                GD.Print($"[BiomeObject]   Chunk bounds: Min={chunkBounds.Min}, Max={chunkBounds.Max}");
                GD.Print($"[BiomeObject]   Intersection result: {intersects}");
            }

            return intersects;
        }

        /// <summary>
        /// Gets all voxels in this biome object
        /// </summary>
        /// <returns>Dictionary of voxels</returns>
        public Dictionary<Vector3I, VoxelType> GetAllVoxels()
        {
            return new Dictionary<Vector3I, VoxelType>(_voxels);
        }

        /// <summary>
        /// Converts a world position to a position relative to the anchor point
        /// </summary>
        /// <param name="worldPosition">World position</param>
        /// <returns>Position relative to the anchor point</returns>
        public Vector3I WorldToRelativePosition(Vector3I worldPosition)
        {
            return worldPosition - AnchorPoint;
        }

        /// <summary>
        /// Converts a position relative to the anchor point to a world position
        /// </summary>
        /// <param name="relativePosition">Position relative to the anchor point</param>
        /// <returns>World position</returns>
        public Vector3I RelativeToWorldPosition(Vector3I relativePosition)
        {
            return AnchorPoint + relativePosition;
        }
    }

    /// <summary>
    /// Types of biome objects
    /// </summary>
    public enum BiomeObjectType
    {
        // Large biome objects
        LargeTree,
        RockFormation,
        Ruins,
        Temple,
        Volcano,
        Lake,
        Mountain,

        // Regular biome objects
        Tree,
        Cactus,
        SnowTree,
        PalmTree,
        Bush,
        Boulder,
        RockSpire,
        IceFormation
    }

    /// <summary>
    /// Custom 3D bounding box implementation
    /// </summary>
    public class BoundingBox3D
    {
        // Minimum corner of the box
        public Vector3 Min { get; private set; }

        // Maximum corner of the box
        public Vector3 Max { get; private set; }

        // Position (center) of the box
        public Vector3 Position => (Min + Max) * 0.5f;

        // Size of the box
        public Vector3 Size => Max - Min;

        /// <summary>
        /// Creates a new bounding box from a position and size
        /// </summary>
        /// <param name="position">Position (center) of the box</param>
        /// <param name="size">Size of the box</param>
        public BoundingBox3D(Vector3 position, Vector3 size)
        {
            Vector3 halfSize = size * 0.5f;
            Min = position - halfSize;
            Max = position + halfSize;
        }

        /// <summary>
        /// Creates a new bounding box from minimum and maximum corners or from position and size
        /// </summary>
        /// <param name="min">Minimum corner (if fromMinMax is true) or position (if fromMinMax is false)</param>
        /// <param name="max">Maximum corner (if fromMinMax is true) or size (if fromMinMax is false)</param>
        /// <param name="fromMinMax">If true, min and max are treated as the minimum and maximum corners.
        /// If false, min is treated as the position and max as the size.</param>
        public BoundingBox3D(Vector3 min, Vector3 max, bool fromMinMax = false)
        {
            if (fromMinMax)
            {
                Min = min;
                Max = max;
            }
            else
            {
                // Treat as position and size
                Vector3 halfSize = max * 0.5f;
                Min = min - halfSize;
                Max = min + halfSize;
            }
        }

        /// <summary>
        /// Checks if a point is inside the bounding box
        /// </summary>
        /// <param name="point">Point to check</param>
        /// <returns>True if the point is inside the box, false otherwise</returns>
        public bool Contains(Vector3 point)
        {
            return point.X >= Min.X && point.X <= Max.X &&
                   point.Y >= Min.Y && point.Y <= Max.Y &&
                   point.Z >= Min.Z && point.Z <= Max.Z;
        }

        /// <summary>
        /// Checks if this bounding box intersects with another
        /// </summary>
        /// <param name="other">Other bounding box</param>
        /// <returns>True if the boxes intersect, false otherwise</returns>
        public bool Intersects(BoundingBox3D other)
        {
            return Min.X <= other.Max.X && Max.X >= other.Min.X &&
                   Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
                   Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
        }

        /// <summary>
        /// Expands the bounding box to include a point
        /// </summary>
        /// <param name="point">Point to include</param>
        /// <returns>Expanded bounding box</returns>
        public BoundingBox3D Expand(Vector3 point)
        {
            Vector3 newMin = new Vector3(
                Mathf.Min(Min.X, point.X),
                Mathf.Min(Min.Y, point.Y),
                Mathf.Min(Min.Z, point.Z)
            );

            Vector3 newMax = new Vector3(
                Mathf.Max(Max.X, point.X),
                Mathf.Max(Max.Y, point.Y),
                Mathf.Max(Max.Z, point.Z)
            );

            return new BoundingBox3D(newMin, newMax, true);
        }
    }
}
