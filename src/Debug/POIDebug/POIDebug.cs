using Godot;
using System;
using CubeGen.World.Common;
using CubeGen.World.Generation;
using CubeGen.World.POI;

namespace CubeGen.Debug.POIDebug
{
	public partial class POIDebug : Node3D
	{
		[Export] public int ViewDistance { get; set; } = 15;
		[Export] public int Seed { get; set; } = 12345;

		private WorldGenerator _worldGenerator;
		private ChunkManager _chunkManager;
		private POIDebugVisualizer _poiVisualizer;
		private Camera3D _camera;
		private Label _infoLabel;

		public override void _Ready()
		{
			// Initialize BiomeMaterials first to ensure materials are ready before any chunks are generated
			BiomeMaterials.Initialize();

			// Get references to nodes
			_worldGenerator = GetNode<WorldGenerator>("WorldGenerator");
			_chunkManager = GetNode<ChunkManager>("WorldGenerator/ChunkManager");
			_poiVisualizer = GetNode<POIDebugVisualizer>("POIDebugVisualizer");
			_camera = GetNode<Camera3D>("Camera3D");
			_infoLabel = GetNode<Label>("CanvasLayer/InfoLabel");

			// Initialize world generator with seed
			_worldGenerator.Initialize(Seed, ViewDistance);

			// Connect chunk requested signal
			_chunkManager.ChunkRequested += OnChunkRequested;

			// Update info label
			UpdateInfoLabel();
		}

		public override void _Process(double delta)
		{
			// Update info label with current camera position
			UpdateInfoLabel();

			// Handle camera movement
			HandleCameraMovement(delta);
		}

		private void HandleCameraMovement(double delta)
		{
			float speed = 50.0f;
			if (Input.IsKeyPressed(Key.Shift))
			{
				speed = 200.0f;
			}

			Vector3 direction = Vector3.Zero;

			// Forward/backward
			if (Input.IsKeyPressed(Key.W))
				direction -= _camera.GlobalTransform.Basis.Z;
			if (Input.IsKeyPressed(Key.S))
				direction += _camera.GlobalTransform.Basis.Z;

			// Left/right
			if (Input.IsKeyPressed(Key.A))
				direction -= _camera.GlobalTransform.Basis.X;
			if (Input.IsKeyPressed(Key.D))
				direction += _camera.GlobalTransform.Basis.X;

			// Up/down
			if (Input.IsKeyPressed(Key.E))
				direction += Vector3.Up;
			if (Input.IsKeyPressed(Key.Q))
				direction += Vector3.Down;

			if (direction != Vector3.Zero)
			{
				direction = direction.Normalized();
				_camera.Position += direction * speed * (float)delta;
			}

			// Rotation
			if (Input.IsKeyPressed(Key.Left))
				_camera.RotateY(0.02f);
			if (Input.IsKeyPressed(Key.Right))
				_camera.RotateY(-0.02f);
			if (Input.IsKeyPressed(Key.Up))
				_camera.RotateX(0.02f);
			if (Input.IsKeyPressed(Key.Down))
				_camera.RotateX(-0.02f);
		}

		private void UpdateInfoLabel()
		{
			if (_infoLabel != null && _camera != null)
			{
				Vector3 pos = _camera.Position;
				int worldX = (int)pos.X;
				int worldZ = (int)pos.Z;
				BiomeType biome = WorldGenerator.GetBiomeType(worldX, worldZ);
				
				// Check if there's a POI at this position
				PointOfInterest poi = POIGenerator.Instance.GetOrGeneratePOI(worldX, worldZ);
				string poiInfo = poi != null ? $"POI: {poi.Type} ({poi.Size})" : "No POI";
				
				_infoLabel.Text = $"Position: ({worldX}, {worldZ})\nBiome: {biome}\n{poiInfo}\n\nControls:\nWASD: Move\nQ/E: Up/Down\nArrows: Rotate\nShift: Speed up\nP: Toggle POI markers";
			}
		}

		private void OnChunkRequested(Vector2I chunkPosition)
		{
			// Generate the requested chunk
			_worldGenerator.GenerateChunk(chunkPosition);
		}

		public override void _Input(InputEvent @event)
		{
			// Toggle POI markers when P is pressed
			if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.P)
			{
				if (_poiVisualizer != null)
				{
					_poiVisualizer.ToggleMarkers();
					GD.Print("POI markers toggled");
				}
			}
		}
	}
}
