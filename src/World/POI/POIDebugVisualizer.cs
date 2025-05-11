using Godot;
using System;
using System.Collections.Generic;

namespace CubeGen.World.POI
{
    /// <summary>
    /// Handles visualization of POIs for debugging purposes
    /// </summary>
    public partial class POIDebugVisualizer : Node3D
    {
        [Export] public bool ShowPOIMarkers { get; set; } = true;
        [Export] public float MarkerHeight { get; set; } = 100.0f;
        [Export] public float UpdateInterval { get; set; } = 1.0f;
        
        private float _timeSinceLastUpdate = 0.0f;
        private Dictionary<string, Node3D> _poiMarkers = new Dictionary<string, Node3D>();
        
        // Materials for different POI types
        private Dictionary<POIType, StandardMaterial3D> _poiMaterials = new Dictionary<POIType, StandardMaterial3D>();
        
        public override void _Ready()
        {
            // Initialize materials for different POI types
            InitializeMaterials();
        }
        
        public override void _Process(double delta)
        {
            if (!ShowPOIMarkers)
            {
                // If markers are disabled, hide all markers
                foreach (var marker in _poiMarkers.Values)
                {
                    marker.Visible = false;
                }
                return;
            }
            
            // Update timer
            _timeSinceLastUpdate += (float)delta;
            
            // Update markers at the specified interval
            if (_timeSinceLastUpdate >= UpdateInterval)
            {
                UpdateMarkers();
                _timeSinceLastUpdate = 0.0f;
            }
        }
        
        /// <summary>
        /// Initialize materials for different POI types
        /// </summary>
        private void InitializeMaterials()
        {
            // Create materials with different colors for each POI type
            
            // Test sphere - bright red
            var testSphereMaterial = new StandardMaterial3D();
            testSphereMaterial.AlbedoColor = new Color(1.0f, 0.0f, 0.0f); // Red
            testSphereMaterial.EmissionEnabled = true;
            testSphereMaterial.Emission = new Color(1.0f, 0.0f, 0.0f, 0.5f); // Red glow
            _poiMaterials[POIType.TestSphere] = testSphereMaterial;
            
            // Large rock - gray
            var rockMaterial = new StandardMaterial3D();
            rockMaterial.AlbedoColor = new Color(0.5f, 0.5f, 0.5f); // Gray
            _poiMaterials[POIType.LargeRock] = rockMaterial;
            
            // Lake - blue
            var lakeMaterial = new StandardMaterial3D();
            lakeMaterial.AlbedoColor = new Color(0.0f, 0.0f, 1.0f); // Blue
            lakeMaterial.EmissionEnabled = true;
            lakeMaterial.Emission = new Color(0.0f, 0.0f, 1.0f, 0.3f); // Blue glow
            _poiMaterials[POIType.Lake] = lakeMaterial;
            
            // Volcano - orange
            var volcanoMaterial = new StandardMaterial3D();
            volcanoMaterial.AlbedoColor = new Color(1.0f, 0.5f, 0.0f); // Orange
            volcanoMaterial.EmissionEnabled = true;
            volcanoMaterial.Emission = new Color(1.0f, 0.3f, 0.0f, 0.5f); // Orange glow
            _poiMaterials[POIType.Volcano] = volcanoMaterial;
            
            // Town - green
            var townMaterial = new StandardMaterial3D();
            townMaterial.AlbedoColor = new Color(0.0f, 1.0f, 0.0f); // Green
            _poiMaterials[POIType.Town] = townMaterial;
            
            // Farm - yellow
            var farmMaterial = new StandardMaterial3D();
            farmMaterial.AlbedoColor = new Color(1.0f, 1.0f, 0.0f); // Yellow
            _poiMaterials[POIType.Farm] = farmMaterial;
            
            // Ruins - purple
            var ruinsMaterial = new StandardMaterial3D();
            ruinsMaterial.AlbedoColor = new Color(0.5f, 0.0f, 0.5f); // Purple
            _poiMaterials[POIType.Ruins] = ruinsMaterial;
        }
        
        /// <summary>
        /// Update POI markers based on current POIs
        /// </summary>
        private void UpdateMarkers()
        {
            // Get all POIs
            var pois = POIGenerator.Instance.GetAllPOIs();
            
            // Track which POIs we've updated
            HashSet<string> updatedPOIs = new HashSet<string>();
            
            // Update or create markers for each POI
            foreach (var poi in pois)
            {
                string poiKey = poi.Name;
                updatedPOIs.Add(poiKey);
                
                if (_poiMarkers.ContainsKey(poiKey))
                {
                    // Update existing marker
                    UpdateMarker(_poiMarkers[poiKey], poi);
                }
                else
                {
                    // Create new marker
                    Node3D marker = CreateMarker(poi);
                    AddChild(marker);
                    _poiMarkers[poiKey] = marker;
                }
            }
            
            // Remove markers for POIs that no longer exist
            List<string> markersToRemove = new List<string>();
            foreach (var key in _poiMarkers.Keys)
            {
                if (!updatedPOIs.Contains(key))
                {
                    markersToRemove.Add(key);
                }
            }
            
            foreach (var key in markersToRemove)
            {
                _poiMarkers[key].QueueFree();
                _poiMarkers.Remove(key);
            }
        }
        
        /// <summary>
        /// Create a new marker for a POI
        /// </summary>
        private Node3D CreateMarker(PointOfInterest poi)
        {
            // Create a new marker node
            Node3D marker = new Node3D();
            marker.Name = $"POIMarker_{poi.Name}";
            
            // Create a mesh instance for the marker
            MeshInstance3D meshInstance = new MeshInstance3D();
            
            // Create a cylinder mesh for the marker
            CylinderMesh cylinderMesh = new CylinderMesh();
            cylinderMesh.TopRadius = poi.Radius * 0.1f; // Smaller top for a cone-like shape
            cylinderMesh.BottomRadius = poi.Radius * 0.5f;
            cylinderMesh.Height = MarkerHeight;
            
            // Set the mesh
            meshInstance.Mesh = cylinderMesh;
            
            // Set the material based on POI type
            if (_poiMaterials.TryGetValue(poi.Type, out StandardMaterial3D material))
            {
                meshInstance.MaterialOverride = material;
            }
            
            // Add the mesh instance to the marker
            marker.AddChild(meshInstance);
            
            // Position the marker
            UpdateMarker(marker, poi);
            
            // Add a label with the POI name
            Label3D label = new Label3D();
            label.Text = $"{poi.Type} ({poi.Size})";
            label.FontSize = 12;
            label.Position = new Vector3(0, MarkerHeight + 5, 0);
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            marker.AddChild(label);
            
            return marker;
        }
        
        /// <summary>
        /// Update an existing marker for a POI
        /// </summary>
        private void UpdateMarker(Node3D marker, PointOfInterest poi)
        {
            // Update marker position
            marker.Position = new Vector3(poi.Center.X, 0, poi.Center.Y);
            
            // Make sure the marker is visible
            marker.Visible = ShowPOIMarkers;
            
            // Update the label text
            Label3D label = marker.GetNodeOrNull<Label3D>("Label3D");
            if (label != null)
            {
                label.Text = $"{poi.Type} ({poi.Size})";
            }
        }
        
        /// <summary>
        /// Toggle visibility of POI markers
        /// </summary>
        public void ToggleMarkers()
        {
            ShowPOIMarkers = !ShowPOIMarkers;
            GD.Print($"POI markers {(ShowPOIMarkers ? "enabled" : "disabled")}");
        }
    }
}
