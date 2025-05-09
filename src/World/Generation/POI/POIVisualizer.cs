using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.World.Generation.POI
{
    /// <summary>
    /// Class for visualizing POIs in the world for debugging purposes
    /// </summary>
    public partial class POIVisualizer : Node3D
    {
        [Export] public float VoxelScale { get; set; } = 0.5f;

        private Dictionary<int, Node3D> _poiMarkers = new Dictionary<int, Node3D>();

        public override void _Ready()
        {
            // Initialize the visualizer
        }

        /// <summary>
        /// Update the POI visualization
        /// </summary>
        public void UpdateVisualization(Vector3 playerPosition)
        {
            // Clear existing markers
            ClearMarkers();

            // Get POIs within visualization range
            int visualizationRadius = 500; // Adjust based on desired visualization distance
            Vector2I playerPos2D = new Vector2I(
                Mathf.FloorToInt(playerPosition.X / VoxelScale),
                Mathf.FloorToInt(playerPosition.Z / VoxelScale)
            );

            List<PointOfInterest> nearbyPOIs = POIManager.Instance.GetPOIsInRadius(playerPos2D, visualizationRadius);

            // Create markers for each POI
            foreach (var poi in nearbyPOIs)
            {
                CreateMarker(poi);
            }
        }

        /// <summary>
        /// Create a visual marker for a POI
        /// </summary>
        private void CreateMarker(PointOfInterest poi)
        {
            // Create a marker mesh
            MeshInstance3D marker = new MeshInstance3D();

            // Create different shapes based on POI type
            switch (poi.Type)
            {
                case POIType.Village:
                    marker.Mesh = new BoxMesh
                    {
                        Size = new Vector3(5, 10, 5)
                    };
                    break;

                case POIType.Tower:
                    marker.Mesh = new CylinderMesh
                    {
                        Height = 15,
                        BottomRadius = 2,
                        TopRadius = 1
                    };
                    break;

                case POIType.Ruin:
                    marker.Mesh = new PrismMesh
                    {
                        Size = new Vector3(6, 8, 6)
                    };
                    break;

                case POIType.Obelisk:
                    marker.Mesh = new CylinderMesh
                    {
                        Height = 20,
                        BottomRadius = 1.5f,
                        TopRadius = 0.5f
                    };
                    break;

                default:
                    marker.Mesh = new SphereMesh
                    {
                        Radius = 3,
                        Height = 6
                    };
                    break;
            }

            // Set material based on POI type
            StandardMaterial3D material = new StandardMaterial3D();

            switch (poi.Type)
            {
                case POIType.Village:
                    material.AlbedoColor = new Color(0, 1, 0, 0.7f); // Green
                    break;

                case POIType.Tower:
                    material.AlbedoColor = new Color(1, 0, 0, 0.7f); // Red
                    break;

                case POIType.Ruin:
                    material.AlbedoColor = new Color(0.5f, 0.5f, 0.5f, 0.7f); // Gray
                    break;

                case POIType.Obelisk:
                    material.AlbedoColor = new Color(0, 0, 1, 0.7f); // Blue
                    break;

                default:
                    material.AlbedoColor = new Color(1, 1, 0, 0.7f); // Yellow
                    break;
            }

            material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            marker.MaterialOverride = material;

            // Position the marker
            marker.Position = new Vector3(
                poi.Position.X * VoxelScale,
                100, // Fixed height for visibility
                poi.Position.Y * VoxelScale
            );

            // Add the marker to the scene
            AddChild(marker);

            // Store the marker using position as a key since there's no ID
            int markerKey = poi.Position.X * 10000 + poi.Position.Y;
            _poiMarkers[markerKey] = marker;

            // Add a label with POI info
            Label3D label = new Label3D();
            label.Text = $"{poi.Type} ({poi.Name})";
            label.FontSize = 12;
            label.Position = new Vector3(0, 10, 0); // Position above the marker

            marker.AddChild(label);
        }

        /// <summary>
        /// Clear all POI markers
        /// </summary>
        private void ClearMarkers()
        {
            foreach (var marker in _poiMarkers.Values)
            {
                marker.QueueFree();
            }

            _poiMarkers.Clear();
        }
    }
}
