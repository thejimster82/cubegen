using Godot;
using System;
using System.Collections.Generic;
using CubeGen.Debug.BiomeDebug;
using CubeGen.World.Common;
using CubeGen.World.Generation;

namespace CubeGen.Debug.BiomeDebug
{
	public partial class BiomeDebugScene : Node3D
	{
		[Export] public int ViewDistance { get; set; } = 10;
		[Export] public int Seed { get; set; } = 12345; // Fixed seed for consistent debugging

		private WorldGenerator _worldGenerator;
		private ChunkManager _chunkManager;
		private Camera3D _camera;
		private BiomeType _currentBiome = BiomeType.ForestLands;
		private SingleBiomeRegionGenerator _biomeGenerator;
		private Label _biomeLabel;
		private OptionButton _biomeSelector;

		// Called when the node enters the scene tree for the first time
		public override void _Ready()
		{
			// Initialize UI
			InitializeUI();

			// Initialize the biome generator
			InitializeBiomeGenerator();

			// Set up the camera
			SetupCamera();

			// Start generating chunks
			GenerateInitialChunks();
		}

		private void InitializeUI()
		{
			// Create UI container
			Control uiContainer = new Control();
			uiContainer.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
			AddChild(uiContainer);

			// Create biome label
			_biomeLabel = new Label();
			_biomeLabel.Position = new Vector2(20, 20);
			_biomeLabel.Text = $"Current Biome: {_currentBiome}";
			uiContainer.AddChild(_biomeLabel);

			// Create biome selector
			_biomeSelector = new OptionButton();
			_biomeSelector.Position = new Vector2(20, 60);
			_biomeSelector.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			_biomeSelector.CustomMinimumSize = new Vector2(200, 40);

			// Add biome options
			foreach (BiomeType biomeType in Enum.GetValues(typeof(BiomeType)))
			{
				_biomeSelector.AddItem(biomeType.ToString(), (int)biomeType);
			}

			// Connect signal
			_biomeSelector.ItemSelected += OnBiomeSelected;
			uiContainer.AddChild(_biomeSelector);

			// Create instructions label
			Label instructionsLabel = new Label();
			instructionsLabel.Position = new Vector2(20, 110);
			instructionsLabel.Text = "Use WASD to move camera\nUse Q/E to move up/down\nUse mouse wheel to zoom";
			uiContainer.AddChild(instructionsLabel);
		}

		private void InitializeBiomeGenerator()
		{
			// Create a new SingleBiomeRegionGenerator with the current biome
			_biomeGenerator = new SingleBiomeRegionGenerator(_currentBiome);

			// Initialize with the seed
			_biomeGenerator.Initialize(Seed);

			// Set as the singleton instance
			SingleBiomeRegionGenerator.SetInstance(_biomeGenerator);

			// Find the WorldGenerator node
			_worldGenerator = GetNode<WorldGenerator>("WorldGenerator");

			// Initialize the WorldGenerator with our seed and view distance
			_worldGenerator.Initialize(Seed, ViewDistance);

			// Find the ChunkManager node
			_chunkManager = GetNode<ChunkManager>("WorldGenerator/ChunkManager");

			// Connect chunk requested signal
			_chunkManager.ChunkRequested += OnChunkRequested;
		}

		private void SetupCamera()
		{
			// Get the camera
			_camera = GetNode<Camera3D>("Camera3D");

			// Position the camera to view the terrain
			_camera.Position = new Vector3(0, 50, 0);
			_camera.RotationDegrees = new Vector3(-45, 0, 0);
		}

		private void GenerateInitialChunks()
		{
			// Generate chunks around the camera
			Vector3 cameraPosition = _camera.GlobalPosition;
			_chunkManager.UpdateChunksAroundPlayer(cameraPosition, ViewDistance);
		}

		private void OnChunkRequested(Vector2I chunkPosition)
		{
			// Generate the requested chunk
			_worldGenerator.GenerateChunk(chunkPosition);
			GD.Print($"Generated chunk at position: {chunkPosition}");
		}

		private void OnBiomeSelected(long index)
		{
			// Get the selected biome type
			_currentBiome = (BiomeType)index;

			// Update the biome label
			_biomeLabel.Text = $"Current Biome: {_currentBiome}";

			// Create a new biome generator with the selected biome
			_biomeGenerator = new SingleBiomeRegionGenerator(_currentBiome);
			_biomeGenerator.Initialize(Seed);
			SingleBiomeRegionGenerator.SetInstance(_biomeGenerator);

			// Clear existing chunks
			_chunkManager.ClearAllChunks();

			// Disconnect and reconnect the chunk requested signal to avoid duplicates
			_chunkManager.ChunkRequested -= OnChunkRequested;
			_chunkManager.ChunkRequested += OnChunkRequested;

			// Reinitialize the WorldGenerator with the new biome settings
			_worldGenerator.Initialize(Seed, ViewDistance);

			// Generate new chunks
			GenerateInitialChunks();
		}

		// Handle camera movement
		public override void _Process(double delta)
		{
			// Move camera based on input
			Vector3 movement = Vector3.Zero;
			float speed = 20.0f;

			if (Input.IsActionPressed("ui_up"))
				movement.Z -= 1;
			if (Input.IsActionPressed("ui_down"))
				movement.Z += 1;
			if (Input.IsActionPressed("ui_left"))
				movement.X -= 1;
			if (Input.IsActionPressed("ui_right"))
				movement.X += 1;

			// Q and E for up and down
			if (Input.IsKeyPressed(Key.Q))
				movement.Y += 1;
			if (Input.IsKeyPressed(Key.E))
				movement.Y -= 1;

			// Apply movement
			if (movement != Vector3.Zero)
			{
				// Normalize and apply speed
				movement = movement.Normalized() * speed * (float)delta;

				// Apply movement relative to camera orientation (except Y)
				Vector3 horizontalMovement = new Vector3(movement.X, 0, movement.Z);
				horizontalMovement = _camera.GlobalTransform.Basis * horizontalMovement;

				// Combine with vertical movement
				movement = new Vector3(horizontalMovement.X, movement.Y, horizontalMovement.Z);

				// Move camera
				_camera.Position += movement;

				// Update chunks around camera
				_chunkManager.UpdateChunksAroundPlayer(_camera.GlobalPosition, ViewDistance);
			}
		}

		// Handle mouse wheel for zoom
		public override void _Input(InputEvent @event)
		{
			if (@event is InputEventMouseButton mouseButton)
			{
				if (mouseButton.ButtonIndex == MouseButton.WheelUp)
				{
					// Zoom in
					_camera.Position += _camera.GlobalTransform.Basis.Z * 2.0f;
				}
				else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
				{
					// Zoom out
					_camera.Position -= _camera.GlobalTransform.Basis.Z * 2.0f;
				}
			}
		}
	}
}
