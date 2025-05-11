using Godot;
using System;
using System.Reflection;
using CubeGen.World.Common;
using CubeGen.World.Generation;
using CubeGen.Player;
using CubeGen.Player.CharacterParts;

public partial class Player : CharacterBody3D
{
	[Export] public float Speed { get; set; } = 4.0f; // Increased from 2.7f for better gameplay feel
	[Export] public float JumpVelocity { get; set; } = 3.5f; // Kept the same
	[Export] public float MouseSensitivity { get; set; } = 0.002f;
	[Export] public float CameraRotationSpeed { get; set; } = 3.0f;
	[Export] public float CameraDistance { get; set; } = 3.0f; // Increased from 1.5f to zoom out the camera
	[Export] public float CameraHeight { get; set; } = 0.75f; // Increased from 0.375f for better view with larger character
	[Export] public float Friction { get; set; } = 0.1f; // Ground friction
	[Export] public float Acceleration { get; set; } = 0.25f; // Movement acceleration
	[Export] public float MaxStepHeight { get; set; } = 1.2f; // Set higher than voxel height to allow climbing walls

	// Climbing properties
	[Export] public float ClimbingSpeed { get; set; } = 3.0f; // Speed when climbing walls (increased from 2.0f)
	[Export] public float ClimbingAcceleration { get; set; } = 0.2f; // Acceleration when climbing (increased from 0.15f)
	[Export] public float WallDetectionDistance { get; set; } = 1.0f; // Distance to detect walls for climbing (increased from 0.6f)

	// Water physics properties
	[Export] public float WaterDragFactor { get; set; } = 0.7f; // Slows movement in water
	[Export] public float WaterBuoyancy { get; set; } = 0.8f; // How much the player floats in water
	[Export] public float SwimSpeed { get; set; } = 2.0f; // Swimming speed when pressing jump in water
	[Export] public float WaterSurfaceOffset { get; set; } = 0.5f; // How far to float above water surface

	private Node3D _head;
	private Node3D _cameraMount;
	private Camera3D _camera;
	private SpringArm3D _springArm;
	private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
	private float _cameraRotation = 0.0f;
	private bool _isInWater = false; // Tracks if player is in water
	private bool _isClimbing = false; // Tracks if player is climbing
	private Vector3 _climbNormal = Vector3.Zero; // Normal of the wall being climbed
	private Label _waterIndicator; // UI indicator for when player is in water
	private Label _climbingIndicator; // UI indicator for when player is climbing
	private ColorRect _waterDebugIndicator; // Visual debug indicator for water detection
	private ColorRect _climbingDebugIndicator; // Visual debug indicator for climbing
	private CanvasLayer _underwaterOverlayCanvas; // Canvas layer for underwater effect
	private ColorRect _underwaterOverlay; // Translucent overlay for underwater effect
	private float _currentCameraDistance; // For smoothing camera movement

	// Voxel character
	private VoxelCharacter _voxelCharacter;

	public override void _Ready()
	{
		_head = GetNode<Node3D>("Head");
		_cameraMount = GetNode<Node3D>("CameraMount");
		_camera = GetNode<Camera3D>("CameraMount/Camera3D");

		// Create voxel character
		CreateVoxelCharacter();

		// Let's take a completely different approach - instead of reparenting,
		// we'll create a SpringArm3D but keep the original camera setup

		// Create the SpringArm3D node
		_springArm = new SpringArm3D
		{
			Name = "SpringArm3D",
			SpringLength = CameraDistance,
			Margin = 0.01f, // Small margin to prevent camera from getting too close to surfaces
			CollisionMask = 1 // Use the default collision layer
		};

		// Add the SpringArm3D to the camera mount
		_cameraMount.AddChild(_springArm);

		// Position the SpringArm3D at the origin of the camera mount
		_springArm.Position = Vector3.Zero;

		// Initialize the current camera distance for smooth transitions
		_currentCameraDistance = CameraDistance;

		// We'll use the SpringArm3D for collision detection only
		// The camera will remain as a direct child of the camera mount

		// Initialize camera position
		UpdateCameraPosition();

		// Capture mouse
		Input.MouseMode = Input.MouseModeEnum.Captured;

		// Set up physics properties for better movement and wall climbing
		FloorStopOnSlope = true;
		FloorMaxAngle = 1.4f; // About 80 degrees - much steeper angle for wall climbing
		FloorSnapLength = 1.2f; // Increased for better snapping to steep surfaces

		// Set up step climbing and wall climbing properties
		UpDirection = Vector3.Up;
		FloorConstantSpeed = true; // Maintain constant speed on slopes
		FloorBlockOnWall = false; // Allow sliding along walls for better wall climbing

		// Configure built-in stair-stepping and wall climbing (Godot 4.4 feature)
		// Set max_step_height higher to allow climbing walls
		MaxStepHeight = 1.2f; // Higher than voxel height to allow climbing walls

		// Additional physics properties for wall climbing
		WallMinSlideAngle = 0.05f; // Reduced to allow climbing on even slighter walls
		MaxSlides = 15; // Increased for better wall climbing and movement around obstacles
		SlideOnCeiling = true; // Allow sliding on ceilings for more advanced climbing

		// Create water indicator UI
		CreateWaterIndicator();
	}

	/// <summary>
	/// Create the voxel character and replace the capsule mesh
	/// </summary>
	private void CreateVoxelCharacter()
	{
		// Remove the existing capsule mesh
		var existingMesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
		if (existingMesh != null)
		{
			existingMesh.QueueFree();
		}

		// Create the voxel character
		_voxelCharacter = new VoxelCharacter();
		_voxelCharacter.Name = "VoxelCharacter";

		// Add the character to the player
		AddChild(_voxelCharacter);

		// Position the character to match the collision shape
		var collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
		if (collisionShape != null)
		{
			_voxelCharacter.Position = collisionShape.Position;
		}
		else
		{
			_voxelCharacter.Position = new Vector3(0, 0.75f, 0);
		}
	}

	/// <summary>
	/// Regenerate all character meshes to fix face culling issues
	/// </summary>
	public void RegenerateCharacterMeshes()
	{
		if (_voxelCharacter != null)
		{
			_voxelCharacter.RegenerateAllMeshes();
		}
	}

	/// <summary>
	/// Recreate the character with the updated voxel size
	/// </summary>
	public void RecreateCharacterWithNewVoxelSize()
	{
		// Remove the existing voxel character
		if (_voxelCharacter != null)
		{
			_voxelCharacter.QueueFree();
			_voxelCharacter = null;
		}

		// Create a new voxel character with the updated voxel size
		CreateVoxelCharacter();
	}

	// Create UI indicators for player states
	private void CreateWaterIndicator()
	{
		// Create a new Control node for UI
		var uiControl = new Control();
		uiControl.Name = "UI";
		uiControl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(uiControl);

		// Create a label for water indicator
		_waterIndicator = new Label();
		_waterIndicator.Name = "WaterIndicator";
		_waterIndicator.Text = "SWIMMING";
		_waterIndicator.HorizontalAlignment = HorizontalAlignment.Center;
		_waterIndicator.VerticalAlignment = VerticalAlignment.Top;
		_waterIndicator.Position = new Vector2(0, 50);
		_waterIndicator.Size = new Vector2(200, 50);
		_waterIndicator.Visible = false; // Hide initially

		// Make the text larger
		_waterIndicator.AddThemeFontSizeOverride("font_size", 24);

		// Style the label
		_waterIndicator.AddThemeColorOverride("font_color", new Color(0, 0.7f, 1.0f));
		_waterIndicator.AddThemeConstantOverride("outline_size", 2);
		_waterIndicator.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));

		// Add the label to the UI
		uiControl.AddChild(_waterIndicator);

		// Create a climbing indicator
		_climbingIndicator = new Label();
		_climbingIndicator.Name = "ClimbingIndicator";
		_climbingIndicator.Text = "CLIMBING";
		_climbingIndicator.HorizontalAlignment = HorizontalAlignment.Center;
		_climbingIndicator.VerticalAlignment = VerticalAlignment.Top;
		_climbingIndicator.Position = new Vector2(0, 100);
		_climbingIndicator.Size = new Vector2(200, 50);
		_climbingIndicator.Visible = false; // Hide initially

		// Make the text larger
		_climbingIndicator.AddThemeFontSizeOverride("font_size", 24);

		// Style the label
		_climbingIndicator.AddThemeColorOverride("font_color", new Color(1.0f, 0.5f, 0.0f)); // Orange for climbing
		_climbingIndicator.AddThemeConstantOverride("outline_size", 2);
		_climbingIndicator.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));

		// Add the climbing indicator to the UI
		uiControl.AddChild(_climbingIndicator);

		// Create a visual indicator for debugging water detection
		_waterDebugIndicator = new ColorRect();
		_waterDebugIndicator.Name = "WaterDebugIndicator";
		_waterDebugIndicator.Size = new Vector2(20, 20);
		_waterDebugIndicator.Position = new Vector2(20, 20);
		_waterDebugIndicator.Color = new Color(1, 0, 0); // Red when not in water

		// Add the debug indicator to the UI
		uiControl.AddChild(_waterDebugIndicator);

		// Create a visual indicator for climbing (triangle shape)
		_climbingDebugIndicator = new ColorRect();
		_climbingDebugIndicator.Name = "ClimbingDebugIndicator";
		_climbingDebugIndicator.Size = new Vector2(20, 20);
		_climbingDebugIndicator.Position = new Vector2(50, 20); // Position it next to the water indicator
		_climbingDebugIndicator.Color = new Color(0.5f, 0.5f, 0.5f); // Gray when not climbing

		// Add a stylized mountain icon to represent climbing
		var stylizedIcon = new Polygon2D();
		stylizedIcon.Name = "ClimbingIcon";
		stylizedIcon.Color = new Color(0, 0, 0, 0.7f); // Semi-transparent black

		// Create a triangle shape for the mountain icon
		var points = new Vector2[] {
			new Vector2(10, 2),   // Top of mountain
			new Vector2(2, 18),   // Bottom left
			new Vector2(18, 18)   // Bottom right
		};
		stylizedIcon.Polygon = points;

		// Add the icon to the climbing indicator
		_climbingDebugIndicator.AddChild(stylizedIcon);

		// Add the climbing indicator to the UI
		uiControl.AddChild(_climbingDebugIndicator);

		// Create a CanvasLayer for the underwater overlay
		// This ensures it's attached to the camera's view rather than the player
		_underwaterOverlayCanvas = new CanvasLayer();
		_underwaterOverlayCanvas.Name = "UnderwaterOverlayCanvas";
		_underwaterOverlayCanvas.Layer = 10; // Use a higher layer to ensure it's visible

		// Create a full-screen underwater overlay
		_underwaterOverlay = new ColorRect();
		_underwaterOverlay.Name = "UnderwaterOverlay";
		_underwaterOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect); // Make it cover the entire screen
		_underwaterOverlay.Size = new Vector2(1920, 1080); // Ensure it's large enough

		// Set a default water color with transparency (will be updated dynamically)
		// This is just a fallback in case we can't determine the actual water color
		Color defaultWaterColor = new Color(0.0f, 0.5f, 0.8f, 0.5f);
		_underwaterOverlay.Color = defaultWaterColor;
		_underwaterOverlay.Visible = false; // Hide initially
		_underwaterOverlay.MouseFilter = Control.MouseFilterEnum.Ignore; // Make sure it doesn't block input

		// Add the overlay to the canvas layer
		_underwaterOverlayCanvas.AddChild(_underwaterOverlay);

		// Add the canvas layer directly to the scene root instead of the player
		// This ensures it's not affected by player transformations and is always visible
		// Use call_deferred to avoid adding a child while the parent is still setting up
		GetTree().Root.CallDeferred("add_child", _underwaterOverlayCanvas);
		_underwaterOverlay.Visible = false;
	}

	public override void _Input(InputEvent @event)
	{
		// Mouse look
		if (@event is InputEventMouseMotion mouseMotion)
		{
			// Rotate camera mount (left/right)
			_cameraMount.RotateY(-mouseMotion.Relative.X * MouseSensitivity);

			// Adjust camera height (up/down) with limits
			_cameraRotation -= mouseMotion.Relative.Y * MouseSensitivity * CameraRotationSpeed;
			_cameraRotation = Mathf.Clamp(_cameraRotation, -0.5f, 0.5f);

			// Update camera position
			UpdateCameraPosition();
		}

		// Toggle mouse capture
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
		{
			if (Input.MouseMode == Input.MouseModeEnum.Captured)
				Input.MouseMode = Input.MouseModeEnum.Visible;
			else
				Input.MouseMode = Input.MouseModeEnum.Captured;
		}

		// Toggle underwater overlay with F4 (for testing)
		if (@event is InputEventKey keyEvent2 && keyEvent2.Pressed && keyEvent2.Keycode == Key.F4 && _underwaterOverlay != null)
		{
			_underwaterOverlay.Visible = !_underwaterOverlay.Visible;
			GD.Print($"Underwater overlay toggled: {_underwaterOverlay.Visible}");
		}
	}

	// Helper method to start the test timer (called via CallDeferred)
	private void StartTestTimer(Timer timer)
	{
		if (timer != null && IsInstanceValid(timer))
		{
			timer.Start();
			GD.Print("Test timer started");
		}
	}

	// Helper method to get the color of the liquid at a specific position
	private Color GetLiquidColorAtPosition(ChunkManager chunkManager, Vector3 position)
	{
		try
		{
			// Get the voxel type at this position
			// First convert world position to voxel coordinates
			WorldGenerator worldGenerator = null;

			// Try to find WorldGenerator to get voxel scale
			try
			{
				worldGenerator = GetTree().Root.GetNodeOrNull<WorldGenerator>("World/WorldGenerator");

				if (worldGenerator == null)
					worldGenerator = GetTree().Root.GetNodeOrNull<WorldGenerator>("WorldGenerator");

				if (worldGenerator == null)
					worldGenerator = GetParent().GetNodeOrNull<WorldGenerator>("WorldGenerator");

				if (worldGenerator == null)
					worldGenerator = GetParent().GetParent()?.GetNodeOrNull<WorldGenerator>("WorldGenerator");
			}
			catch (Exception ex)
			{
				GD.PrintErr($"Error finding WorldGenerator: {ex.Message}");
			}

			float voxelScale = 1.0f;
			if (worldGenerator != null)
			{
				voxelScale = worldGenerator.VoxelScale;

				// If voxel scale is 0, use default of 1.0
				if (voxelScale <= 0)
				{
					voxelScale = 1.0f;
				}
				else
				{
					// Convert scale to divisor (e.g., scale of 0.5 means 2x resolution)
					voxelScale = 1.0f / voxelScale;
				}
			}

			// Convert world position to voxel coordinates
			float playerScale = 1.0f;
			if (Scale != Vector3.One)
			{
				playerScale = Scale.X; // Get player scale
			}

			// Apply voxel scale to convert world position to voxel coordinates
			// We don't divide by playerScale here to be consistent with IsPositionInWater
			int worldX = Mathf.FloorToInt(position.X * voxelScale);
			int worldY = Mathf.FloorToInt(position.Y * voxelScale);
			int worldZ = Mathf.FloorToInt(position.Z * voxelScale);

			// Get the voxel type at this position
			VoxelType voxelType = chunkManager.GetVoxelType(worldX, worldY, worldZ);

			// If it's water, return a blue color
			if (voxelType == VoxelType.Water)
			{
				// Try to get the water material from the scene
				try
				{
					// Look for water materials in the scene
					var meshes = GetTree().Root.FindChildren("*", "MeshInstance3D", true, false);
					foreach (var node in meshes)
					{
						if (node is MeshInstance3D mesh)
						{
							// Check if the mesh has a material
							if (mesh.MaterialOverride is StandardMaterial3D stdMat)
							{
								// Check if this might be a water material based on its color
								Color color = stdMat.AlbedoColor;
								if (color.B > 0.5f && color.B > color.R && color.B > color.G)
								{
									// This looks like a blue water material
									return color;
								}
							}
							// Also check mesh surface materials
							else if (mesh.Mesh != null)
							{
								for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
								{
									var material = mesh.Mesh.SurfaceGetMaterial(i);
									if (material is StandardMaterial3D surfaceMat)
									{
										Color color = surfaceMat.AlbedoColor;
										if (color.B > 0.5f && color.B > color.R && color.B > color.G)
										{
											// This looks like a blue water material
											return color;
										}
									}
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr($"Error finding water material: {ex.Message}");
				}

				// Default water color if we couldn't find a material
				return new Color(0.0f, 0.5f, 0.8f); // Default blue water
			}
			else
			{
				// For any other voxel type, return a default blue water color
				// This is a fallback in case we're in a liquid that's not specifically water
				return new Color(0.0f, 0.5f, 0.8f);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Error in GetLiquidColorAtPosition: {ex.Message}");
			return new Color(0.0f, 0.5f, 0.8f); // Default blue water as fallback
		}
	}

	private void UpdateCameraPosition()
	{
		// Calculate camera position based on rotation and distance
		float height = CameraHeight + _cameraRotation * 2.0f; // Adjust height based on rotation (reduced multiplier)
		float targetDistance = CameraDistance - _cameraRotation * 1.0f; // Adjust distance based on rotation (reduced multiplier)

		// Update the spring arm for collision detection
		_springArm.SpringLength = targetDistance;

		// Use the SpringArm3D to detect collisions
		float hitLength = _springArm.GetHitLength();

		// Determine the target distance based on collision detection
		float targetCameraDistance;
		if (hitLength < targetDistance)
		{
			// Apply a small margin to prevent clipping
			targetCameraDistance = hitLength - 0.1f;

			// Ensure we don't go into negative distance
			targetCameraDistance = Mathf.Max(targetCameraDistance, 0.1f);
		}
		else
		{
			// No collision, use the calculated distance
			targetCameraDistance = targetDistance;
		}

		// Smooth the camera distance transition
		// Use a smaller smoothing factor when moving camera closer (to avoid clipping)
		float smoothFactor = targetCameraDistance < _currentCameraDistance ? 0.5f : 0.1f;
		_currentCameraDistance = Mathf.Lerp(_currentCameraDistance, targetCameraDistance, smoothFactor);

		// Position the camera with the smoothed distance
		_camera.Position = new Vector3(0, height, _currentCameraDistance);

		// Make the camera look at the player's head
		_camera.LookAt(_head.GlobalPosition);
	}

	// Check if the player is in water
	private bool CheckIfInWater()
	{
		// Get the chunk manager from the scene
		ChunkManager chunkManager = null;

		// Try different paths to find the ChunkManager
		// This makes the code more robust to different scene structures
		try
		{
			// Try to find ChunkManager in various possible locations
			chunkManager = GetParent().GetNodeOrNull<ChunkManager>("WorldGenerator/ChunkManager");

			if (chunkManager == null)
				chunkManager = GetTree().Root.GetNodeOrNull<ChunkManager>("World/WorldGenerator/ChunkManager");

			if (chunkManager == null)
				chunkManager = GetTree().Root.GetNodeOrNull<ChunkManager>("WorldGenerator/ChunkManager");

			if (chunkManager == null)
				chunkManager = GetParent().GetParent()?.GetNodeOrNull<ChunkManager>("WorldGenerator/ChunkManager");

			if (chunkManager == null)
			{
				GD.PrintErr("ChunkManager not found in scene");
				return false;
			}
		}
		catch (Exception ex)
		{
			// Log the error but continue with default values
			GD.PrintErr($"Error finding ChunkManager: {ex.Message}");
			return false;
		}

		// Get player's position in world coordinates
		Vector3 playerPos = GlobalPosition;

		// Get the player's collision shape to determine appropriate check points
		var collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
		float playerHeight = 1.5f; // Default height
		float playerRadius = 0.5f; // Default radius

		if (collisionShape != null && collisionShape.Shape is CapsuleShape3D capsule)
		{
			playerHeight = capsule.Height;
			playerRadius = capsule.Radius;

			// Only print this occasionally to avoid spam
			if (Engine.GetProcessFrames() % 300 == 0)
			{
				GD.Print($"Player collision shape: height={playerHeight}, radius={playerRadius}");
			}
		}

		// Check at center of player
		bool isInWater = false;

		// Get player scale to adjust check position
		float playerScale = 1.0f;
		if (Scale != Vector3.One)
		{
			playerScale = Scale.X;
		}

		// Adjust the check position based on player scale
		// For a 2x scaled player, we need to check at the actual center of the scaled model
		Vector3 checkPosition = new Vector3(
			playerPos.X,
			playerPos.Y + (playerHeight * playerScale) / 2,
			playerPos.Z
		);

		// Debug output
		if (Engine.GetProcessFrames() % 120 == 0)
		{
			GD.Print($"Water check: Player scale={playerScale}, Check position={checkPosition}, Player position={playerPos}");
		}

		if (IsPositionInWater(chunkManager, checkPosition))
			isInWater = true;

		return isInWater;
	}

	// Helper method to check if a specific position is in water
	private bool IsPositionInWater(ChunkManager chunkManager, Vector3 position)
	{
		try
		{
			// Get the world generator to access voxel scale
			WorldGenerator worldGenerator = null;

			// Try different paths to find the WorldGenerator
			// This makes the code more robust to different scene structures
			try
			{
				// Try to find WorldGenerator in various possible locations
				worldGenerator = GetTree().Root.GetNodeOrNull<WorldGenerator>("World/WorldGenerator");

				if (worldGenerator == null)
					worldGenerator = GetTree().Root.GetNodeOrNull<WorldGenerator>("WorldGenerator");

				if (worldGenerator == null)
					worldGenerator = GetParent().GetNodeOrNull<WorldGenerator>("WorldGenerator");

				if (worldGenerator == null)
					worldGenerator = GetParent().GetParent()?.GetNodeOrNull<WorldGenerator>("WorldGenerator");
			}
			catch (Exception ex)
			{
				// Log the error but continue with default values
				GD.PrintErr($"Error finding WorldGenerator: {ex.Message}");
			}

			float voxelScale = 1.0f;
			if (worldGenerator != null)
			{
				// Get the voxel scale from the world generator
				voxelScale = worldGenerator.VoxelScale;

				// If voxel scale is 0, use default of 1.0
				if (voxelScale <= 0)
				{
					voxelScale = 1.0f;
				}
				else
				{
					// Convert scale to divisor (e.g., scale of 0.5 means 2x resolution)
					voxelScale = 1.0f / voxelScale;
				}
			}

			// Convert world position to voxel coordinates
			// Account for player scale and voxel scale
			float playerScale = 1.0f;
			if (Scale != Vector3.One)
			{
				playerScale = Scale.X; // Get player scale
			}

			// Apply both scales to convert world position to voxel coordinates
			// When converting world position to voxel coordinates, we should multiply by playerScale, not divide
			// This is because a larger player means the world is effectively smaller relative to the player
			int worldX = Mathf.FloorToInt(position.X * voxelScale);
			int worldY = Mathf.FloorToInt(position.Y * voxelScale);
			int worldZ = Mathf.FloorToInt(position.Z * voxelScale);

			// Use the ChunkManager's GetVoxelType method to get the voxel type at this position
			VoxelType voxelType = chunkManager.GetVoxelType(worldX, worldY, worldZ);

			// Debug output for specific points (limit to avoid spam)
			if (Engine.GetProcessFrames() % 120 == 0)
			{
				GD.Print($"Checking position: {position}, voxel coords: ({worldX}, {worldY}, {worldZ}), voxel type: {voxelType}");
			}

			// Check if it's water
			return voxelType == VoxelType.Water;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Error checking water: {ex.Message}");
			return false;
		}
	}

	// Find the Y coordinate of the water surface
	private float FindWaterSurfaceLevel()
	{
		// Get the chunk manager from the scene
		ChunkManager chunkManager = null;

		// Try different paths to find the ChunkManager
		// This makes the code more robust to different scene structures
		try
		{
			// Try to find ChunkManager in various possible locations
			chunkManager = GetParent().GetNodeOrNull<ChunkManager>("WorldGenerator/ChunkManager");

			if (chunkManager == null)
				chunkManager = GetTree().Root.GetNodeOrNull<ChunkManager>("World/WorldGenerator/ChunkManager");

			if (chunkManager == null)
				chunkManager = GetTree().Root.GetNodeOrNull<ChunkManager>("WorldGenerator/ChunkManager");

			if (chunkManager == null)
				chunkManager = GetParent().GetParent()?.GetNodeOrNull<ChunkManager>("WorldGenerator/ChunkManager");

			if (chunkManager == null)
			{
				GD.PrintErr("ChunkManager not found in scene");
				return GlobalPosition.Y; // Return current position if can't find chunk manager
			}
		}
		catch (Exception ex)
		{
			// Log the error but continue with default values
			GD.PrintErr($"Error finding ChunkManager: {ex.Message}");
			return GlobalPosition.Y;
		}

		// Get player's position in world coordinates
		Vector3 playerPos = GlobalPosition;

		// Get the world generator to access voxel scale
		WorldGenerator worldGenerator = null;

		// Try different paths to find the WorldGenerator
		try
		{
			// Try to find WorldGenerator in various possible locations
			worldGenerator = GetTree().Root.GetNodeOrNull<WorldGenerator>("World/WorldGenerator");

			if (worldGenerator == null)
				worldGenerator = GetTree().Root.GetNodeOrNull<WorldGenerator>("WorldGenerator");

			if (worldGenerator == null)
				worldGenerator = GetParent().GetNodeOrNull<WorldGenerator>("WorldGenerator");

			if (worldGenerator == null)
				worldGenerator = GetParent().GetParent()?.GetNodeOrNull<WorldGenerator>("WorldGenerator");
		}
		catch (Exception ex)
		{
			// Log the error but continue with default values
			GD.PrintErr($"Error finding WorldGenerator: {ex.Message}");
		}

		float voxelScale = 1.0f;
		if (worldGenerator != null)
		{
			// Get the voxel scale from the world generator
			voxelScale = worldGenerator.VoxelScale;

			// If voxel scale is 0, use default of 1.0
			if (voxelScale <= 0)
			{
				voxelScale = 1.0f;
			}
		}

		// Get player scale to adjust check position
		float playerScale = 1.0f;
		if (Scale != Vector3.One)
		{
			playerScale = Scale.X;
		}

		// Search upward from the player's position to find the water surface
		float maxSearchHeight = 10.0f * playerScale; // Maximum search distance adjusted for player scale
		float step = 0.25f * playerScale; // Step size for search adjusted for player scale

		// Debug output
		if (Engine.GetProcessFrames() % 120 == 0)
		{
			GD.Print($"Water surface search: Player scale={playerScale}, Max height={maxSearchHeight}, Step={step}");
		}

		for (float yOffset = 0; yOffset <= maxSearchHeight; yOffset += step)
		{
			Vector3 checkPos = new Vector3(playerPos.X, playerPos.Y + yOffset, playerPos.Z);

			// If this position is not water, we've found the surface
			if (!IsPositionInWater(chunkManager, checkPos))
			{
				// Return the Y coordinate of the water surface (just below this point)
				return playerPos.Y + yOffset - (step / 2.0f);
			}
		}

		// If we didn't find a surface, return a position slightly above the player
		return playerPos.Y + 1.0f;
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		// Check if player is in water
		bool wasInWater = _isInWater;
		_isInWater = CheckIfInWater();

		// Check if player is climbing
		bool wasClimbing = _isClimbing;
		_isClimbing = CheckForClimbableWall(out _climbNormal);

		// Update water indicator visibility
		if (_waterIndicator != null)
		{
			_waterIndicator.Visible = _isInWater;
		}

		// Update climbing indicator visibility
		if (_climbingIndicator != null)
		{
			_climbingIndicator.Visible = _isClimbing;
		}

		// Update water debug indicator color
		if (_waterDebugIndicator != null)
		{
			_waterDebugIndicator.Color = _isInWater ? new Color(0, 0.7f, 1.0f) : new Color(1, 0, 0);
		}

		// Update climbing debug indicator color
		if (_climbingDebugIndicator != null)
		{
			_climbingDebugIndicator.Color = _isClimbing ? new Color(1.0f, 0.5f, 0.0f) : new Color(0.5f, 0.5f, 0.5f);
		}

		// Update underwater overlay visibility
		if (_underwaterOverlay != null)
		{
			// Check if the camera is underwater
			bool cameraInWater = false;

			// Get the world position of the camera
			Vector3 cameraPosition = _camera.GlobalPosition;

			// Get the chunk manager
			ChunkManager chunkManager = null;

			// Try different paths to find the ChunkManager
			// This makes the code more robust to different scene structures
			try
			{
				// Try to find ChunkManager in various possible locations
				chunkManager = GetParent().GetNodeOrNull<ChunkManager>("WorldGenerator/ChunkManager");

				if (chunkManager == null)
					chunkManager = GetTree().Root.GetNodeOrNull<ChunkManager>("World/WorldGenerator/ChunkManager");

				if (chunkManager == null)
					chunkManager = GetTree().Root.GetNodeOrNull<ChunkManager>("WorldGenerator/ChunkManager");

				if (chunkManager == null)
					chunkManager = GetParent().GetParent()?.GetNodeOrNull<ChunkManager>("WorldGenerator/ChunkManager");
			}
			catch (Exception ex)
			{
				// Log the error but continue with default values
				if (Engine.GetProcessFrames() % 300 == 0)
				{
					GD.PrintErr($"Error finding ChunkManager: {ex.Message}");
				}
			}

			// Check if the camera position is in water
			if (chunkManager != null)
			{
				cameraInWater = IsPositionInWater(chunkManager, cameraPosition);

				// If camera is in water, update the overlay color based on the water color
				if (cameraInWater)
				{
					// Get the water color from the voxel at the camera position
					Color waterColor = GetLiquidColorAtPosition(chunkManager, cameraPosition);

					// Apply the water color to the overlay with transparency
					waterColor.A = 0.5f; // Set transparency
					_underwaterOverlay.Color = waterColor;
				}
			}

			// Store current state for debug output
			bool currentVisibility = _underwaterOverlay.Visible;

			// Update visibility - only show the overlay when the camera is underwater
			_underwaterOverlay.Visible = cameraInWater;

			// Always print debug info every few seconds
			if (Engine.GetProcessFrames() % 120 == 0)
			{
				GD.Print($"Underwater debug - Camera in water: {cameraInWater}, Overlay visible: {_underwaterOverlay.Visible}, Canvas layer: {_underwaterOverlayCanvas.Layer}");
			}

			// Debug output when underwater state changes
			if (currentVisibility != cameraInWater)
			{
				GD.Print($"Camera underwater state changed: {cameraInWater}");
			}
		}

		// Print debug message when water state changes
		if (_isInWater != wasInWater)
		{
			if (_isInWater)
			{
				GD.Print("Player entered water");
			}
			else
			{
				GD.Print("Player exited water");
			}
		}

		// Print debug message when climbing state changes
		if (_isClimbing != wasClimbing)
		{
			if (_isClimbing)
			{
				GD.Print("Player started climbing");
			}
			else
			{
				GD.Print("Player stopped climbing");
			}
		}

		// Apply different physics based on player state
		if (_isInWater)
		{
			// Find the water surface level
			float waterSurfaceY = FindWaterSurfaceLevel();

			// Get the player's collision shape to determine height
			var collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
			float playerHeight = 1.5f; // Default height
			float playerRadius = 0.5f; // Default radius

			if (collisionShape != null && collisionShape.Shape is CapsuleShape3D capsule)
			{
				playerHeight = capsule.Height;
				playerRadius = capsule.Radius;
			}

			// Calculate the target position - we want the player to float with half their body in water
			// This means the player's center should be at the water surface

			// Get player scale to adjust floating position
			float playerScale = 1.0f;
			if (Scale != Vector3.One)
			{
				playerScale = Scale.X;
			}

			// Set the target Y position to the water surface level
			float targetY = waterSurfaceY;

			// Add a small offset to fine-tune the floating height
			// Scale the offset based on player scale
			float floatHeightOffset = 0.1f * playerScale; // Positive value to float higher
			targetY += floatHeightOffset;

			// Calculate buoyancy force based on distance from target position
			float distanceFromTarget = targetY - GlobalPosition.Y;

			// Apply buoyancy force based on distance from target position
			float buoyancyForce;

			// Increase buoyancy slightly to counteract sinking
			// Scale the buoyancy multiplier based on player scale
			float buoyancyMultiplier = 1.2f * playerScale; // Increased from 1.0 and scaled with player

			// Debug output
			if (Engine.GetProcessFrames() % 120 == 0)
			{
				GD.Print($"Water physics: Player scale={playerScale}, Buoyancy multiplier={buoyancyMultiplier}");
			}

			if (distanceFromTarget > 0)
			{
				// Player is below target - apply upward buoyancy (stronger)
				buoyancyForce = buoyancyMultiplier * WaterBuoyancy * distanceFromTarget * (float)delta;
			}
			else
			{
				// Player is above target - apply downward force
				// Use a slightly weaker downward force to prevent sinking
				buoyancyForce = 0.8f * playerScale * WaterBuoyancy * distanceFromTarget * (float)delta;
			}

			// Apply buoyancy force
			velocity.Y += buoyancyForce;

			// Add a moderate damping effect to prevent oscillation
			velocity.Y *= 0.95f; // Less damping (was 0.9f)

			// Apply a very small constant force to fine-tune the equilibrium position
			// Positive value for upward force, negative for downward
			// Scale this force with player scale
			velocity.Y += 0.01f * playerScale; // Small upward force to counteract sinking

			// Handle swimming up when jump is pressed in water
			if (Input.IsActionPressed("ui_accept"))
			{
				// Scale swim speed with player scale
				velocity.Y = SwimSpeed * playerScale;
			}

			// Debug output
			if (Engine.GetProcessFrames() % 60 == 0)
			{
				GD.Print($"Water physics: surface={waterSurfaceY:F2}, player={GlobalPosition.Y:F2}, " +
					$"target={targetY:F2}, distance={distanceFromTarget:F2}, " +
					$"force={buoyancyForce:F2}, velocity={velocity.Y:F2}");
			}
		}
		else if (_isClimbing)
		{
			// Climbing physics - override gravity

			// Get player scale to adjust climbing
			float playerScale = 1.0f;
			if (Scale != Vector3.One)
			{
				playerScale = Scale.X;
			}

			// Immediately stop any downward momentum when starting to climb
			if (velocity.Y < 0)
			{
				velocity.Y = 0;
			}

			// Get vertical input for climbing
			float verticalInput = Input.GetAxis("ui_down", "ui_up");

			// Get horizontal input for sideways climbing
			float horizontalInput = Input.GetAxis("ui_left", "ui_right");

			// Apply climbing movement
			if (Mathf.Abs(verticalInput) > 0.1f)
			{
				// Calculate target vertical velocity for climbing
				float targetVelocityY = verticalInput * ClimbingSpeed * playerScale;

				// Apply climbing acceleration
				velocity.Y = Mathf.Lerp(velocity.Y, targetVelocityY, ClimbingAcceleration);

				// Debug output
				if (Engine.GetProcessFrames() % 60 == 0)
				{
					GD.Print($"Climbing: input={verticalInput:F2}, velocity={velocity.Y:F2}");
				}
			}
			else
			{
				// When not actively climbing, maintain position with slight damping
				velocity.Y *= 0.9f;
			}

			// Apply horizontal movement along the wall
			if (Mathf.Abs(horizontalInput) > 0.1f)
			{
				// Get camera's right direction for horizontal wall movement
				Vector3 wallCameraRight = _camera.GlobalTransform.Basis.X;
				wallCameraRight.Y = 0;
				wallCameraRight = wallCameraRight.Normalized();

				// Project the right vector onto the wall plane
				Vector3 wallRight = wallCameraRight - _climbNormal * wallCameraRight.Dot(_climbNormal);
				wallRight = wallRight.Normalized();

				// Apply horizontal movement along the wall
				Vector3 horizontalVelocity = wallRight * horizontalInput * ClimbingSpeed * 0.7f * playerScale;
				velocity.X = Mathf.Lerp(velocity.X, horizontalVelocity.X, ClimbingAcceleration);
				velocity.Z = Mathf.Lerp(velocity.Z, horizontalVelocity.Z, ClimbingAcceleration);
			}
			else
			{
				// Dampen horizontal movement when not actively moving
				velocity.X *= 0.9f;
				velocity.Z *= 0.9f;
			}

			// Allow jumping off the wall
			if (Input.IsActionJustPressed("ui_accept"))
			{
				// Jump away from the wall
				velocity.Y = JumpVelocity;
				velocity += _climbNormal * JumpVelocity * 0.8f; // Stronger push away from wall

				// Stop climbing
				_isClimbing = false;

				// Debug output
				GD.Print("Jumped off wall");
			}

			// Stop climbing if the climb key is released and the key is required
			if (!Input.IsActionPressed("climb") && IsOnFloor())
			{
				_isClimbing = false;
				GD.Print("Stopped climbing - key released");
			}
		}
		else
		{
			// Regular gravity when not in water or climbing
			if (!IsOnFloor())
				velocity.Y -= _gravity * (float)delta;

			// Regular jump when on floor and not in water or climbing
			if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
			{
				velocity.Y = JumpVelocity;

				// Check if we can start climbing immediately after jumping
				Vector3 wallNormal;
				if (CheckForClimbableWall(out wallNormal))
				{
					// We're jumping near a wall, prepare for wall climbing
					_climbNormal = wallNormal;

					// Don't start climbing immediately, but set a flag to check on the next frame
					// This allows the jump to start before attaching to the wall
					GD.Print("Jump detected near wall - preparing for climb");
				}
			}
		}

		// Get movement input
		Vector2 inputDir = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");

		// Store the input magnitude for use in rotation logic
		float inputMagnitude = inputDir.Length();

		// Get camera's forward direction (ignoring Y component)
		Vector3 cameraForward = -_camera.GlobalTransform.Basis.Z;
		cameraForward.Y = 0;
		cameraForward = cameraForward.Normalized();

		// Get camera's right direction (ignoring Y component)
		Vector3 cameraRight = _camera.GlobalTransform.Basis.X;
		cameraRight.Y = 0;
		cameraRight = cameraRight.Normalized();

		// Calculate movement direction relative to camera
		// Invert the Y input to fix the backwards movement (W/S keys)
		Vector3 direction = Vector3.Zero;
		if (inputMagnitude > 0.1f)
		{
			direction = (cameraForward * -inputDir.Y + cameraRight * inputDir.X).Normalized();
		}

		// Handle movement with acceleration and friction
		if (direction != Vector3.Zero)
		{
			// Calculate target velocity - slower in water
			float currentSpeed = _isInWater ? Speed * (1.0f - WaterDragFactor) : Speed;
			Vector3 targetVelocity = direction * currentSpeed;

			// Apply acceleration
			if (IsOnFloor() && !_isInWater)
			{
				// Smoother acceleration on ground
				velocity.X = Mathf.Lerp(velocity.X, targetVelocity.X, Acceleration);
				velocity.Z = Mathf.Lerp(velocity.Z, targetVelocity.Z, Acceleration);
			}
			else
			{
				// Less control in air or water
				float waterAcceleration = _isInWater ? Acceleration * 0.7f : Acceleration * 0.5f;
				velocity.X = Mathf.Lerp(velocity.X, targetVelocity.X, waterAcceleration);
				velocity.Z = Mathf.Lerp(velocity.Z, targetVelocity.Z, waterAcceleration);
			}

			// No player rotation - movement is purely relative to camera direction
		}
		else if (IsOnFloor() || _isInWater)
		{
			// Apply friction when on ground or in water and not actively moving
			// More friction in water
			float frictionFactor = _isInWater ? Friction * 1.5f : Friction;
			velocity.X = Mathf.Lerp(velocity.X, 0, frictionFactor);
			velocity.Z = Mathf.Lerp(velocity.Z, 0, frictionFactor);
		}

		Velocity = velocity;
		MoveAndSlide();

		// Animate the voxel character if it exists
		if (_voxelCharacter != null)
		{
			// Determine if the player is walking based on horizontal velocity
			bool isWalking = new Vector2(velocity.X, velocity.Z).Length() > 0.1f;

			// Determine if the player is jumping based on vertical velocity and floor state
			bool isJumping = !IsOnFloor() && velocity.Y > 0;

			// Update character animation with climbing state
			_voxelCharacter.UpdateAnimation(delta, isWalking, isJumping, velocity, _isClimbing);

			// Rotate character to face movement direction
			if (direction != Vector3.Zero)
			{
				// Calculate target rotation based on movement direction
				float targetRotation = Mathf.Atan2(direction.X, direction.Z);

				// Smoothly rotate the character towards the target rotation
				float currentRotation = _voxelCharacter.Rotation.Y;

				// Calculate the shortest angle difference (handles wrapping around)
				float rotationDifference = CalculateShortestAngleDifference(currentRotation, targetRotation);

				// Apply smooth rotation with speed based on delta time
				float rotationSpeed = 10.0f * (float)delta;
				float newRotation = currentRotation + Mathf.Clamp(rotationDifference, -rotationSpeed, rotationSpeed);

				// Apply rotation to character
				_voxelCharacter.Rotation = new Vector3(0, newRotation, 0);
			}
		}
	}

	/// <summary>
	/// Calculate the shortest angle difference between two angles in radians
	/// </summary>
	private float CalculateShortestAngleDifference(float current, float target)
	{
		// Normalize angles to be between -PI and PI
		float difference = target - current;

		// Wrap around to find the shortest path
		while (difference > Mathf.Pi)
			difference -= Mathf.Pi * 2;

		while (difference < -Mathf.Pi)
			difference += Mathf.Pi * 2;

		return difference;
	}

	/// <summary>
	/// Check if the player can climb a wall
	/// </summary>
	private bool CheckForClimbableWall(out Vector3 wallNormal)
	{
		wallNormal = Vector3.Zero;

		// Don't allow climbing while in water
		if (_isInWater)
			return false;

		// Check for walls in multiple directions around the player
		bool foundWall = false;

		// Get the player's forward direction based on camera
		Vector3 forwardDir = -_camera.GlobalTransform.Basis.Z;
		forwardDir.Y = 0;
		forwardDir = forwardDir.Normalized();

		// Create a ray to check for walls in front of the player
		var spaceState = GetWorld3D().DirectSpaceState;
		var rayOrigin = GlobalPosition + Vector3.Up * 0.5f; // Start ray from middle of player

		// Check in multiple directions (forward, left, right, and slight diagonals)
		Vector3[] directions = {
			forwardDir,
			forwardDir.Rotated(Vector3.Up, Mathf.Pi * 0.25f),
			forwardDir.Rotated(Vector3.Up, -Mathf.Pi * 0.25f),
			forwardDir.Rotated(Vector3.Up, Mathf.Pi * 0.5f),
			forwardDir.Rotated(Vector3.Up, -Mathf.Pi * 0.5f)
		};

		foreach (var dir in directions)
		{
			var rayEnd = rayOrigin + dir * WallDetectionDistance;

			// Create the ray query parameters
			var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd);
			query.CollideWithAreas = false;
			query.CollideWithBodies = true;

			// Cast the ray
			var result = spaceState.IntersectRay(query);

			// Check if we hit something
			if (result.Count > 0 && result.ContainsKey("normal"))
			{
				// Get the normal of the wall
				Vector3 normal = (Vector3)result["normal"];

				// Only climb if the wall is steep enough (close to vertical)
				float wallAngle = Mathf.Abs(normal.Dot(Vector3.Up));
				if (wallAngle < 0.3f) // Wall is close to vertical
				{
					wallNormal = normal;
					foundWall = true;
					break;
				}
			}
		}

		// If we found a wall, check if we should start climbing
		if (foundWall)
		{
			// Start climbing if:
			// 1. The climb key is pressed, OR
			// 2. We're in the air (jumping or falling) and not on the floor
			bool shouldClimb = Input.IsActionPressed("climb") ||
							   (!IsOnFloor() && Velocity.Y != 0);

			return shouldClimb;
		}

		return false;
	}
}
