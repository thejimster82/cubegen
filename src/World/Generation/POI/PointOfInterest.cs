using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation.POI
{
    /// <summary>
    /// Represents a Point of Interest in the world
    /// </summary>
    public class PointOfInterest
    {
        // Basic properties
        public Vector2I Position { get; private set; }  // Center position of the POI in world coordinates
        public POIType Type { get; private set; }       // Type of POI
        public POICategory Category { get; private set; } // Category of POI
        public POISize Size { get; private set; }       // Size/scale of the POI
        public BiomeType Biome { get; private set; }    // Biome where this POI is located
        public int Seed { get; private set; }           // Seed for this specific POI's generation
        public string Name { get; private set; }        // Unique name for this POI

        // Influence properties
        public int InfluenceRadius { get; private set; } // How far this POI affects the terrain
        public float TerrainInfluence { get; private set; } // How strongly this POI affects terrain height (0-1)

        // Additional data for specific POI types
        public Dictionary<string, object> CustomData { get; private set; } = new Dictionary<string, object>();

        /// <summary>
        /// Create a new Point of Interest
        /// </summary>
        public PointOfInterest(Vector2I position, POIType type, POISize size, BiomeType biome, int worldSeed)
        {
            Position = position;
            Type = type;
            Size = size;
            Biome = biome;

            // Determine category based on type
            Category = GetCategoryFromType(type);

            // Generate a unique seed for this POI based on position and world seed
            Seed = worldSeed + position.X * 73856093 + position.Y * 19349663;

            // Generate a name for this POI
            Name = GenerateName();

            // Set influence radius based on size
            InfluenceRadius = GetInfluenceRadiusFromSize(size);

            // Set terrain influence based on type
            TerrainInfluence = GetTerrainInfluenceFromType(type);

            // Initialize any custom data based on POI type
            InitializeCustomData();
        }

        /// <summary>
        /// Determine the POI category from its type
        /// </summary>
        private POICategory GetCategoryFromType(POIType type)
        {
            switch (type)
            {
                case POIType.Village:
                case POIType.Town:
                case POIType.Camp:
                    return POICategory.Settlement;

                case POIType.Cave:
                case POIType.Ruin:
                case POIType.Mine:
                    return POICategory.Dungeon;

                case POIType.Lake:
                case POIType.Pond:
                case POIType.SpecialTree:
                case POIType.RockFormation:
                case POIType.Waterfall:
                    return POICategory.NaturalFeature;

                case POIType.Tower:
                case POIType.Statue:
                case POIType.Obelisk:
                    return POICategory.Landmark;

                case POIType.Quarry:
                case POIType.OreDeposit:
                case POIType.MagicSpring:
                    return POICategory.Resource;

                default:
                    return POICategory.NaturalFeature;
            }
        }

        /// <summary>
        /// Generate a name for this POI
        /// </summary>
        private string GenerateName()
        {
            // Create a random generator with the POI's seed
            Random random = new Random(Seed);

            // Simple name generation based on type and a random number
            string typeName = Type.ToString();
            int nameNumber = random.Next(1, 1000);

            return $"{typeName} {nameNumber}";
        }

        /// <summary>
        /// Get influence radius based on POI size
        /// </summary>
        private int GetInfluenceRadiusFromSize(POISize size)
        {
            switch (size)
            {
                case POISize.Tiny: return 15;    // Increased from 5
                case POISize.Small: return 30;   // Increased from 10
                case POISize.Medium: return 60;  // Increased from 20
                case POISize.Large: return 100;  // Increased from 40
                case POISize.Huge: return 150;   // Increased from 80
                default: return 60;              // Increased from 20
            }
        }

        /// <summary>
        /// Get terrain influence factor based on POI type
        /// </summary>
        private float GetTerrainInfluenceFromType(POIType type)
        {
            switch (type)
            {
                // Settlements tend to flatten terrain
                case POIType.Village:
                case POIType.Town:
                    return 0.8f;

                // Dungeons can create depressions or elevations
                case POIType.Cave:
                    return 0.6f;
                case POIType.Ruin:
                    return 0.4f;

                // Natural features have varying effects
                case POIType.Lake:
                case POIType.Pond:
                    return 0.9f; // Strong depression for water
                case POIType.RockFormation:
                    return 0.7f; // Moderate elevation

                // Default moderate influence
                default:
                    return 0.5f;
            }
        }

        /// <summary>
        /// Initialize custom data based on POI type
        /// </summary>
        private void InitializeCustomData()
        {
            Random random = new Random(Seed);

            switch (Type)
            {
                case POIType.Village:
                case POIType.Town:
                    // Number of buildings
                    int buildingCount = Type == POIType.Village ?
                        random.Next(3, 8) : random.Next(8, 15);
                    CustomData["BuildingCount"] = buildingCount;
                    break;

                case POIType.Lake:
                case POIType.Pond:
                    // Depth of the water body
                    float depth = Type == POIType.Lake ?
                        0.2f + (float)random.NextDouble() * 0.3f :
                        0.1f + (float)random.NextDouble() * 0.2f;
                    CustomData["Depth"] = depth;
                    break;

                // Add more custom data for other POI types as needed
            }
        }
    }
}
