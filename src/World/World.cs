using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;
using CubeGen.World.Generation;

public partial class World : Node3D
{
	[Export] public PackedScene PlayerScene { get; set; }
	[Export] public int ViewDistance { get; set; } = 5;
	[Export] public int Seed { get; set; } = 0;
	[Export] public float MapHeight { get; set; } = 500.0f;
	[Export] public float MapMoveSpeed { get; set; } = 200.0f; // Increased from 50.0f for faster panning
	[Export] public float MapMoveSpeedFast { get; set; } = 500.0f; // Even faster speed when holding Shift
	[Export] public int MapSize { get; set; } = 200;
	[Export] public float MapTileSize { get; set; } = 5.0f;
	[Export] public float ZoomSpeed { get; set; } = 20.0f; // Base zoom speed
	[Export] public float ZoomSpeedFast { get; set; } = 50.0f; // Fast zoom speed when holding Shift
	[Export] public float MinZoom { get; set; } = 20.0f; // Minimum zoom level (more zoomed in)
	[Export] public float MaxZoom { get; set; } = 1000.0f; // Maximum zoom level (more zoomed out)

	private WorldGenerator _worldGenerator;
	private ChunkManager _chunkManager;
	private CloudGenerator _cloudGenerator;
	private BiomeRegionGenerator _biomeRegionGenerator;
	private Player _player;
	private Godot.Timer _chunkUpdateTimer;
	private Camera3D _mapCamera;
	private Node3D _mapVisualizer;
	private Control _mapUI;
	private Label _biomeLabel;
	private Label _controlsLabel;
	private bool _isMapMode = false;

	// Store camera rotation to preserve it during movement and zooming
	private Basis _mapCameraRotation;

	// Track player movement for direction-based chunk loading
	private Vector3 _lastPlayerPosition = Vector3.Zero;
	private Vector3 _playerMovementDirection = Vector3.Zero;

	public override void _Ready()
	{
		// Initialize BiomeMaterials first to ensure materials are ready before any chunks are generated
		BiomeMaterials.Initialize();

		_worldGenerator = GetNode<WorldGenerator>("WorldGenerator");
		_chunkManager = GetNode<ChunkManager>("WorldGenerator/ChunkManager");

		// Get cloud generator
		_cloudGenerator = GetNode<CloudGenerator>("CloudGenerator");

		// Get the biome region generator singleton
		_biomeRegionGenerator = BiomeRegionGenerator.Instance;

		// Set seed
		if (Seed == 0)
		{
			// Random seed if not specified
			Random random = new Random();
			Seed = random.Next();
		}
		_worldGenerator.Seed = Seed;
		_worldGenerator.ViewDistance = ViewDistance;

		// Set the same seed for cloud generator
		if (_cloudGenerator != null)
		{
			_cloudGenerator.Seed = Seed;
		}

		// Connect chunk requested signal
		_chunkManager.ChunkRequested += OnChunkRequested;

		// Create player
		SpawnPlayer();

		// Create timer for chunk updates
		_chunkUpdateTimer = new Godot.Timer();
		_chunkUpdateTimer.WaitTime = 0.3f; // Reduced from 1.0f for much faster chunk updates
		_chunkUpdateTimer.Timeout += OnChunkUpdateTimerTimeout;
		AddChild(_chunkUpdateTimer);
		_chunkUpdateTimer.Start();
	}

	public override void _Input(InputEvent @event)
	{
		// Toggle map mode when M is pressed
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && Input.IsActionJustPressed("toggle_map"))
		{
			ToggleMapMode();
		}

		// Handle map input when in map mode
		if (_isMapMode && _mapCamera != null)
		{
			// Handle camera rotation with Q and E keys
			if (@event is InputEventKey rotateKeyEvent && rotateKeyEvent.Pressed)
			{
				// Rotate left with Q key
				if (rotateKeyEvent.Keycode == Key.Q)
				{
					// Rotate the camera basis around the Y axis (15 degrees counterclockwise)
					_mapCameraRotation = _mapCameraRotation.Rotated(Vector3.Up, Mathf.DegToRad(15));

					// Apply the rotation to the camera
					_mapCamera.GlobalTransform = new Transform3D(_mapCameraRotation, _mapCamera.Position);

					// Update the controls label
					if (_controlsLabel != null)
					{
						_controlsLabel.Text = "WASD: Move | Q/E: Rotate | Shift: Fast Move | +/-: Zoom | M: Exit Map";
					}
				}
				// Rotate right with E key
				else if (rotateKeyEvent.Keycode == Key.E)
				{
					// Rotate the camera basis around the Y axis (15 degrees clockwise)
					_mapCameraRotation = _mapCameraRotation.Rotated(Vector3.Up, Mathf.DegToRad(-15));

					// Apply the rotation to the camera
					_mapCamera.GlobalTransform = new Transform3D(_mapCameraRotation, _mapCamera.Position);

					// Update the controls label
					if (_controlsLabel != null)
					{
						_controlsLabel.Text = "WASD: Move | Q/E: Rotate | Shift: Fast Move | +/-: Zoom | M: Exit Map";
					}
				}
			}

			// Handle zooming with keys (+ and - keys)
			if (@event is InputEventKey zoomKeyEvent && zoomKeyEvent.Pressed)
			{
				// Get current distance from camera to target
				Vector3 lookTarget = new Vector3(_mapCamera.Position.X, 0, _mapCamera.Position.Z);
				float currentDistance = _mapCamera.Position.DistanceTo(lookTarget);

				// Determine zoom speed based on whether Shift is held
				float currentZoomSpeed = Input.IsKeyPressed(Key.Shift) ? ZoomSpeedFast : ZoomSpeed;

				// Zoom in with + key or = key (same key on most keyboards)
				if (zoomKeyEvent.Keycode == Key.Equal || zoomKeyEvent.Keycode == Key.Plus || zoomKeyEvent.Keycode == Key.KpAdd)
				{
					// Zoom in - move camera closer to target
					float zoomFactor = 0.9f; // Zoom in by 10%

					// Calculate new position (move closer to target)
					Vector3 direction = (_mapCamera.Position - lookTarget).Normalized();
					float newDistance = Mathf.Max(currentDistance * zoomFactor, MinZoom);
					Vector3 newPosition = lookTarget + direction * newDistance;

					// Maintain height ratio
					float heightRatio = _mapCamera.Position.Y / currentDistance;
					newPosition.Y = lookTarget.Y + newDistance * heightRatio;

					// Update camera position while preserving rotation
					_mapCamera.Position = newPosition;

					// Apply the stored rotation instead of using LookAt
					_mapCamera.GlobalTransform = new Transform3D(_mapCameraRotation, newPosition);

					// Update the controls label
					if (_controlsLabel != null)
					{
						_controlsLabel.Text = $"WASD: Move | Shift: Fast Move | +/-: Zoom | M: Exit Map | Distance: {newDistance:F0}";
					}
				}
				// Zoom out with - key
				else if (zoomKeyEvent.Keycode == Key.Minus || zoomKeyEvent.Keycode == Key.KpSubtract)
				{
					// Zoom out - move camera away from target
					float zoomFactor = 1.1f; // Zoom out by 10%

					// Calculate new position (move away from target)
					Vector3 direction = (_mapCamera.Position - lookTarget).Normalized();
					float newDistance = Mathf.Min(currentDistance * zoomFactor, MaxZoom);
					Vector3 newPosition = lookTarget + direction * newDistance;

					// Maintain height ratio
					float heightRatio = _mapCamera.Position.Y / currentDistance;
					newPosition.Y = lookTarget.Y + newDistance * heightRatio;

					// Update camera position while preserving rotation
					_mapCamera.Position = newPosition;

					// Apply the stored rotation instead of using LookAt
					_mapCamera.GlobalTransform = new Transform3D(_mapCameraRotation, newPosition);

					// Update the controls label
					if (_controlsLabel != null)
					{
						_controlsLabel.Text = $"WASD: Move | Shift: Fast Move | +/-: Zoom | M: Exit Map | Distance: {newDistance:F0}";
					}
				}
			}
		}
	}

	public override void _Process(double delta)
	{
		// Handle map movement when in map mode
		if (_isMapMode && _mapCamera != null)
		{
			// Handle movement
			Vector3 moveDirection = Vector3.Zero;

			// Get camera's forward and right vectors for movement relative to camera view
			Vector3 forward = -_mapCamera.GlobalTransform.Basis.Z;
			forward.Y = 0; // Keep movement on the XZ plane
			forward = forward.Normalized();

			Vector3 right = _mapCamera.GlobalTransform.Basis.X;
			right.Y = 0; // Keep movement on the XZ plane
			right = right.Normalized();

			// Calculate movement direction based on input
			if (Input.IsActionPressed("ui_up"))
				moveDirection += forward;
			if (Input.IsActionPressed("ui_down"))
				moveDirection -= forward;
			if (Input.IsActionPressed("ui_left"))
				moveDirection -= right;
			if (Input.IsActionPressed("ui_right"))
				moveDirection += right;

			if (moveDirection != Vector3.Zero)
			{
				moveDirection = moveDirection.Normalized();

				// Use faster speed when Shift is held
				float currentSpeed = Input.IsKeyPressed(Key.Shift) ? MapMoveSpeedFast : MapMoveSpeed;

				// For perspective camera, we don't need to adjust speed based on zoom
				// Instead, move at a consistent speed

				// Move the camera
				Vector3 newPosition = _mapCamera.Position + moveDirection * currentSpeed * (float)delta;

				// Update camera position while preserving rotation
				_mapCamera.Position = newPosition;

				// Apply the stored rotation instead of using LookAt
				_mapCamera.GlobalTransform = new Transform3D(_mapCameraRotation, newPosition);
			}

			// Update biome label
			if (_biomeLabel != null)
			{
				// Use the point directly below the camera for biome determination
				int worldX = (int)_mapCamera.Position.X;
				int worldZ = (int)_mapCamera.Position.Z;
				BiomeType biomeType = _biomeRegionGenerator.GetBiomeType(worldX, worldZ);

				// Check if we're near a boundary
				bool nearBoundary = _biomeRegionGenerator.IsNearBoundary(worldX, worldZ);
				string boundaryText = nearBoundary ? " (Near Boundary)" : "";

				_biomeLabel.Text = $"Biome: {biomeType}{boundaryText} | Position: ({worldX}, {worldZ})";
			}
		}
	}

	private void ToggleMapMode()
	{
		_isMapMode = !_isMapMode;

		if (_isMapMode)
		{
			// Switch to map mode
			EnableMapMode();
		}
		else
		{
			// Switch back to normal mode
			DisableMapMode();
		}
	}

	private void EnableMapMode()
	{
		// Create map camera if it doesn't exist
		if (_mapCamera == null)
		{
			CreateMapCamera();
		}

		// Create map visualizer if it doesn't exist
		if (_mapVisualizer == null)
		{
			CreateMapVisualizer();
		}

		// Create map UI if it doesn't exist
		if (_mapUI == null)
		{
			CreateMapUI();
		}

		// Show map components
		if (_mapCamera != null)
		{
			_mapCamera.Visible = true;
			_mapCamera.Current = true;
		}

		if (_mapVisualizer != null)
		{
			_mapVisualizer.Visible = true;
		}

		if (_mapUI != null)
		{
			_mapUI.Visible = true;
		}

		// Disable player input
		if (_player != null)
		{
			_player.SetProcessInput(false);
			_player.SetPhysicsProcess(false);
		}
	}

	private void DisableMapMode()
	{
		// Hide map components
		if (_mapCamera != null)
		{
			_mapCamera.Visible = false;
		}

		if (_mapVisualizer != null)
		{
			_mapVisualizer.Visible = false;
		}

		if (_mapUI != null)
		{
			_mapUI.Visible = false;
		}

		// Re-enable player camera
		if (_player != null)
		{
			Camera3D playerCamera = _player.GetNode<Camera3D>("CameraMount/Camera3D");
			if (playerCamera != null)
			{
				playerCamera.Current = true;
			}

			// Re-enable player input
			_player.SetProcessInput(true);
			_player.SetPhysicsProcess(true);
		}
	}

	private void CreateMapCamera()
	{
		// Create a new camera for the map view
		_mapCamera = new Camera3D();
		_mapCamera.Name = "MapCamera";

		// Set up camera properties for a steeper 45-degree angled view
		// Position the camera to achieve approximately 45-degree viewing angle
		_mapCamera.Position = new Vector3(-MapHeight * 0.7f, MapHeight * 0.7f, MapHeight * 0.7f);

		// Use perspective projection for better 3D view
		_mapCamera.Projection = Camera3D.ProjectionType.Perspective;
		_mapCamera.Fov = 40.0f; // Narrower FOV for less distortion
		_mapCamera.Far = 2000.0f; // Increased far clipping plane for better visibility
		_mapCamera.Current = false;
		_mapCamera.Visible = false;

		// Add to scene
		AddChild(_mapCamera);

		// We need to wait until the camera is added to the scene tree before calling LookAt
		// Use CallDeferred to ensure the node is properly added to the tree first
		CallDeferred(nameof(SetupMapCameraLookAt));
	}

	private void SetupMapCameraLookAt()
	{
		// This method is called after the camera is added to the scene tree
		if (_mapCamera != null && _mapCamera.IsInsideTree())
		{
			// Look at the center of the map
			// Check if camera position would cause colinear vectors
			Vector3 cameraToTarget = Vector3.Zero - _mapCamera.Position;
			cameraToTarget = cameraToTarget.Normalized();
			float dotProduct = cameraToTarget.Dot(Vector3.Up);

			// If the camera is looking almost straight down or up, use a different up vector
			if (Math.Abs(dotProduct) > 0.99f)
			{
				// Use a different up vector
				_mapCamera.LookAt(Vector3.Zero, Vector3.Forward);
			}
			else
			{
				// Normal case
				_mapCamera.LookAt(Vector3.Zero, Vector3.Up);
			}

			// Store the initial camera rotation to preserve it during movement and zooming
			_mapCameraRotation = _mapCamera.GlobalTransform.Basis;
		}
	}

	private void CreateMapVisualizer()
	{
		// Create a new node for the map visualizer
		_mapVisualizer = new Node3D();
		_mapVisualizer.Name = "MapVisualizer";
		_mapVisualizer.Visible = false;

		// Create a mesh for the map
		CreateMapMesh();

		// Add a directional light for better terrain visualization
		DirectionalLight3D light = new DirectionalLight3D();
		light.LightEnergy = 0.8f;
		light.LightColor = new Color(1.0f, 0.98f, 0.9f); // Slightly warm light
		light.ShadowEnabled = true;
		light.ShadowBlur = 6.0f;
		light.ShadowNormalBias = 3.0f;
		light.DirectionalShadowBlendSplits = true;
		light.DirectionalShadowFadeStart = 0.8f;
		light.DirectionalShadowMaxDistance = 1000.0f;
		light.DirectionalShadowPancakeSize = 40.0f;
		light.LightAngularDistance = 0.5f;

		// Position the light to cast shadows that highlight terrain features
		light.RotationDegrees = new Vector3(50, -30, 0);

		_mapVisualizer.AddChild(light);

		// Add to scene
		AddChild(_mapVisualizer);
	}

	private void CreateMapMesh()
	{
		// Create a new ArrayMesh
		ArrayMesh mesh = new ArrayMesh();

		// Create surface arrays
		Godot.Collections.Array arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);

		// Create vertices, colors, indices, and normals
		List<Vector3> vertices = new List<Vector3>();
		List<Color> colors = new List<Color>();
		List<int> indices = new List<int>();
		List<Vector3> normals = new List<Vector3>();

		// Initialize biome colors
		Dictionary<BiomeType, Color> biomeColors = new Dictionary<BiomeType, Color>
		{
			{ BiomeType.Plains, new Color(0.4f, 0.83f, 0.3f) },
			{ BiomeType.Forest, new Color(0.2f, 0.6f, 0.2f) },
			{ BiomeType.Desert, new Color(0.95f, 0.85f, 0.5f) },
			{ BiomeType.Mountains, new Color(0.5f, 0.5f, 0.6f) },
			{ BiomeType.Tundra, new Color(0.95f, 0.97f, 1.0f) }
		};

		// Calculate half size for centering
		float halfSize = MapSize * MapTileSize / 2.0f;

		// Height scale factor for the map (to make terrain features visible but not too extreme)
		float heightScale = 0.5f;

		// Create a grid of quads
		for (int x = 0; x < MapSize; x++)
		{
			for (int z = 0; z < MapSize; z++)
			{
				// Calculate world position
				float worldX = x * MapTileSize - halfSize;
				float worldZ = z * MapTileSize - halfSize;

				// Get biome type for this position
				int sampleX = (int)(worldX);
				int sampleZ = (int)(worldZ);
				BiomeType biomeType = _biomeRegionGenerator.GetBiomeType(sampleX, sampleZ);

				// Get terrain height for each corner of the quad
				float heightNW = GetTerrainHeight(sampleX, sampleZ, biomeType) * heightScale;
				float heightNE = GetTerrainHeight(sampleX + (int)MapTileSize, sampleZ, biomeType) * heightScale;
				float heightSE = GetTerrainHeight(sampleX + (int)MapTileSize, sampleZ + (int)MapTileSize, biomeType) * heightScale;
				float heightSW = GetTerrainHeight(sampleX, sampleZ + (int)MapTileSize, biomeType) * heightScale;

				// Get color for this biome
				Color biomeColor = biomeColors[biomeType];

				// Add vertices for a quad with height information
				int baseIndex = vertices.Count;

				vertices.Add(new Vector3(worldX, heightNW, worldZ));                           // NW
				vertices.Add(new Vector3(worldX + MapTileSize, heightNE, worldZ));             // NE
				vertices.Add(new Vector3(worldX + MapTileSize, heightSE, worldZ + MapTileSize)); // SE
				vertices.Add(new Vector3(worldX, heightSW, worldZ + MapTileSize));             // SW

				// Calculate normal for this quad (for proper lighting)
				Vector3 edge1 = vertices[baseIndex + 1] - vertices[baseIndex];     // NE - NW
				Vector3 edge2 = vertices[baseIndex + 3] - vertices[baseIndex];     // SW - NW
				Vector3 normal = edge1.Cross(edge2).Normalized();

				// Add normals for each vertex
				normals.Add(normal);
				normals.Add(normal);
				normals.Add(normal);
				normals.Add(normal);

				// Add colors for each vertex
				colors.Add(biomeColor);
				colors.Add(biomeColor);
				colors.Add(biomeColor);
				colors.Add(biomeColor);

				// Add indices for two triangles to form a quad
				indices.Add(baseIndex);
				indices.Add(baseIndex + 1);
				indices.Add(baseIndex + 2);

				indices.Add(baseIndex);
				indices.Add(baseIndex + 2);
				indices.Add(baseIndex + 3);
			}
		}

		// Set arrays
		arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
		arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
		arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
		arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

		// Create surface
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

		// Create mesh instance
		MeshInstance3D mapMesh = new MeshInstance3D();
		mapMesh.Mesh = mesh;

		// Create material
		StandardMaterial3D material = new StandardMaterial3D();
		material.VertexColorUseAsAlbedo = true;
		material.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel; // Use per-pixel shading for better terrain features
		material.CullMode = BaseMaterial3D.CullModeEnum.Back; // Cull back faces

		// Set material
		mapMesh.MaterialOverride = material;

		// Add to visualizer
		_mapVisualizer.AddChild(mapMesh);
	}

	// Helper method to get terrain height for map visualization
	private float GetTerrainHeight(int worldX, int worldZ, BiomeType biomeType)
	{
		// Use the same noise and settings as the world generator
		FastNoiseLite terrainNoise = new FastNoiseLite();
		terrainNoise.Seed = Seed;
		terrainNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		terrainNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		terrainNoise.Frequency = 0.01f; // Doubled frequency for higher resolution (was 0.005f)
		terrainNoise.FractalOctaves = 2;

		// Base terrain height from noise
		float heightNoise = terrainNoise.GetNoise2D(worldX, worldZ);

		// Convert noise from [-1, 1] to [0, 1]
		heightNoise = (heightNoise + 1f) * 0.5f;

		// Check if we're near a biome boundary and adjust height for visualization
		if (_biomeRegionGenerator.IsNearBoundary(worldX, worldZ, 0.05f))
		{
			// Slightly lower the terrain at biome boundaries to create visible "borders"
			heightNoise *= 0.9f;
		}

		// Apply biome-specific height modifications
		float baseHeight = 0.3f; // Consistent base height for all terrain

		switch (biomeType)
		{
			case BiomeType.Desert:
				heightNoise = heightNoise * 0.1f + baseHeight; // Very flat, low
				break;
			case BiomeType.Plains:
				heightNoise = heightNoise * 0.15f + baseHeight; // Very flat
				break;
			case BiomeType.Forest:
				heightNoise = heightNoise * 0.2f + baseHeight; // Slightly more varied but still flat
				break;
			case BiomeType.Mountains:
				heightNoise = heightNoise * 0.3f + baseHeight; // Less mountainous, more like hills
				break;
			case BiomeType.Tundra:
				heightNoise = heightNoise * 0.15f + baseHeight; // Very flat
				break;
		}

		// Return height value (0-1 range)
		return heightNoise * 100.0f; // Scale to a reasonable height for visualization
	}

	private void CreateMapUI()
	{
		// Create a Control node for UI
		_mapUI = new Control();
		_mapUI.AnchorRight = 1.0f;
		_mapUI.AnchorBottom = 1.0f;
		_mapUI.Visible = false;

		// Create biome label
		_biomeLabel = new Label();
		_biomeLabel.Position = new Vector2(20, 20);
		_biomeLabel.Text = "Biome: Unknown";
		_mapUI.AddChild(_biomeLabel);

		// Create controls label
		_controlsLabel = new Label();
		_controlsLabel.Position = new Vector2(20, 50);
		_controlsLabel.Text = "WASD: Move | Q/E: Rotate | Shift: Fast Move | +/-: Zoom | M: Exit Map";
		_mapUI.AddChild(_controlsLabel);

		// Add to scene
		AddChild(_mapUI);
	}

	private void SpawnPlayer()
	{
		_player = PlayerScene.Instantiate<Player>();
		AddChild(_player);

		// Position player above the terrain at spawn point
		Vector3 spawnPosition = new Vector3(0, 60, 0); // Halved for higher resolution voxels (was 100)
		_player.Position = spawnPosition;

		GD.Print("Player spawned at position: " + spawnPosition);
	}

	private void OnChunkRequested(Vector2I chunkPosition)
	{
		// Generate the requested chunk
		_worldGenerator.GenerateChunk(chunkPosition);

		// Debug output to confirm chunk generation
		// GD.Print($"Generated chunk at position: {chunkPosition}");
	}

	private void OnChunkUpdateTimerTimeout()
	{
		if (_player != null && _chunkManager != null)
		{
			// Capture player position on the main thread
			Vector3 playerPosition = _player.Position;

			// Update last position
			_lastPlayerPosition = playerPosition;

			// Call UpdateChunksAroundPlayer directly on the main thread
			// This is now thread-safe with our ConcurrentDictionary implementation
			_chunkManager.UpdateChunksAroundPlayer(playerPosition, ViewDistance);
		}
	}
}
