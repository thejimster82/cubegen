using Godot;
using System;
using CubeGen.World.Common;
using CubeGen.World.Generation;

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
	[Export] public float MaxStepHeight { get; set; } = 0.5f; // Increased from 0.3125f for better stepping with larger character

	// Water physics properties
	[Export] public float WaterDragFactor { get; set; } = 0.7f; // Slows movement in water
	[Export] public float WaterBuoyancy { get; set; } = 0.8f; // How much the player floats in water
	[Export] public float SwimSpeed { get; set; } = 2.0f; // Swimming speed when pressing jump in water
	[Export] public float WaterSurfaceOffset { get; set; } = 0.5f; // How far to float above water surface

	private Node3D _head;
	private Node3D _cameraMount;
	private Camera3D _camera;
	private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
	private float _cameraRotation = 0.0f;
	private bool _isInWater = false; // Tracks if player is in water
	private Label _waterIndicator; // UI indicator for when player is in water
	private ColorRect _waterDebugIndicator; // Visual debug indicator for water detection

	public override void _Ready()
	{
		_head = GetNode<Node3D>("Head");
		_cameraMount = GetNode<Node3D>("CameraMount");
		_camera = GetNode<Camera3D>("CameraMount/Camera3D");

		// Initialize camera position
		UpdateCameraPosition();

		// Capture mouse
		Input.MouseMode = Input.MouseModeEnum.Captured;

		// Set up physics properties for better movement
		FloorStopOnSlope = true;
		FloorMaxAngle = 1.0f; // About 60 degrees - even steeper angle for better climbing
		FloorSnapLength = 1.0f; // Increased for larger character (was 0.5f)

		// Set up step climbing properties
		UpDirection = Vector3.Up;
		FloorConstantSpeed = true; // Maintain constant speed on slopes
		FloorBlockOnWall = false; // Allow sliding along walls

		// Additional physics properties
		WallMinSlideAngle = 0.1f; // Allow sliding on very slight walls
		MaxSlides = 10; // Increase maximum slides for better movement around obstacles

		// Create water indicator UI
		CreateWaterIndicator();
	}

	// Create a UI indicator for when the player is in water
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

		// Create a visual indicator for debugging water detection
		_waterDebugIndicator = new ColorRect();
		_waterDebugIndicator.Name = "WaterDebugIndicator";
		_waterDebugIndicator.Size = new Vector2(20, 20);
		_waterDebugIndicator.Position = new Vector2(20, 20);
		_waterDebugIndicator.Color = new Color(1, 0, 0); // Red when not in water

		// Add the debug indicator to the UI
		uiControl.AddChild(_waterDebugIndicator);
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
	}

	private void UpdateCameraPosition()
	{
		// Calculate camera position based on rotation and distance
		float height = CameraHeight + _cameraRotation * 2.0f; // Adjust height based on rotation (reduced multiplier)
		float distance = CameraDistance - _cameraRotation * 1.0f; // Adjust distance based on rotation (reduced multiplier)

		// Set camera transform - position only, keep the rotation from camera mount
		_camera.Position = new Vector3(0, height, distance);

		// Make camera look at player's head
		_camera.LookAt(_head.GlobalPosition);
	}

	// Check if the player is in water
	private bool CheckIfInWater()
	{
		// Get the chunk manager from the scene
		var chunkManager = GetParent().GetNode<ChunkManager>("WorldGenerator/ChunkManager");
		if (chunkManager == null)
		{
			// Try alternative path
			chunkManager = GetTree().Root.GetNode<ChunkManager>("World/WorldGenerator/ChunkManager");
			if (chunkManager == null)
			{
				GD.PrintErr("ChunkManager not found in scene");
				return false;
			}
		}

		// Get player's position in world coordinates
		Vector3 playerPos = GlobalPosition;

		// Check multiple points around the player's body to determine if in water
		// We need at least 3 points in water to consider the player "in water"
		int waterPoints = 0;
		int totalPoints = 0;

		// Get the player's collision shape to determine appropriate check points
		var collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
		float playerHeight = 1.5f; // Default height
		float playerRadius = 0.5f; // Default radius

		if (collisionShape != null && collisionShape.Shape is CapsuleShape3D capsule)
		{
			playerHeight = capsule.Height;
			playerRadius = capsule.Radius;
			GD.Print($"Player collision shape: height={playerHeight}, radius={playerRadius}");
		}

		// Check at center
		bool isInWater = false;
		if (IsPositionInWater(chunkManager, new Vector3(playerPos.X, playerPos.Y+playerHeight/2, playerPos.Z)))
			isInWater = true;

		return isInWater;
	}

	// Helper method to check if a specific position is in water
	private bool IsPositionInWater(ChunkManager chunkManager, Vector3 position)
	{
		try
		{
			// Get the world generator to access voxel scale
			var worldGenerator = GetTree().Root.GetNode<WorldGenerator>("World/WorldGenerator");
			if (worldGenerator == null)
			{
				worldGenerator = GetParent().GetNode<WorldGenerator>("WorldGenerator");
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
			int worldX = Mathf.FloorToInt(position.X * voxelScale / playerScale);
			int worldY = Mathf.FloorToInt(position.Y * voxelScale / playerScale);
			int worldZ = Mathf.FloorToInt(position.Z * voxelScale / playerScale);

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
		var chunkManager = GetParent().GetNode<ChunkManager>("WorldGenerator/ChunkManager");
		if (chunkManager == null)
		{
			// Try alternative path
			chunkManager = GetTree().Root.GetNode<ChunkManager>("World/WorldGenerator/ChunkManager");
			if (chunkManager == null)
			{
				GD.PrintErr("ChunkManager not found in scene");
				return GlobalPosition.Y; // Return current position if can't find chunk manager
			}
		}

		// Get player's position in world coordinates
		Vector3 playerPos = GlobalPosition;

		// Get the world generator to access voxel scale
		var worldGenerator = GetTree().Root.GetNode<WorldGenerator>("World/WorldGenerator");
		if (worldGenerator == null)
		{
			worldGenerator = GetParent().GetNode<WorldGenerator>("WorldGenerator");
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

		// Search upward from the player's position to find the water surface
		float maxSearchHeight = 10.0f; // Maximum search distance
		float step = 0.25f; // Step size for search

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

		// Update water indicator visibility
		if (_waterIndicator != null)
		{
			_waterIndicator.Visible = _isInWater;
		}

		// Update debug indicator color
		if (_waterDebugIndicator != null)
		{
			_waterDebugIndicator.Color = _isInWater ? new Color(0, 0.7f, 1.0f) : new Color(1, 0, 0);
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

		// Apply different physics based on water state
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

			// Set the target Y position to the water surface level
			float targetY = waterSurfaceY;

			// Add a small offset to fine-tune the floating height
			float floatHeightOffset = 0.1f; // Positive value to float higher
			targetY += floatHeightOffset;

			// Calculate buoyancy force based on distance from target position
			float distanceFromTarget = targetY - GlobalPosition.Y;

			// Apply buoyancy force based on distance from target position
			float buoyancyForce;

			// Increase buoyancy slightly to counteract sinking
			float buoyancyMultiplier = 1.2f; // Increased from 1.0

			if (distanceFromTarget > 0)
			{
				// Player is below target - apply upward buoyancy (stronger)
				buoyancyForce = buoyancyMultiplier * WaterBuoyancy * distanceFromTarget * (float)delta;
			}
			else
			{
				// Player is above target - apply downward force
				// Use a slightly weaker downward force to prevent sinking
				buoyancyForce = 0.8f * WaterBuoyancy * distanceFromTarget * (float)delta;
			}

			// Apply buoyancy force
			velocity.Y += buoyancyForce;

			// Add a moderate damping effect to prevent oscillation
			velocity.Y *= 0.95f; // Less damping (was 0.9f)

			// Apply a very small constant force to fine-tune the equilibrium position
			// Positive value for upward force, negative for downward
			velocity.Y += 0.01f; // Small upward force to counteract sinking

			// Handle swimming up when jump is pressed in water
			if (Input.IsActionPressed("ui_accept"))
			{
				velocity.Y = SwimSpeed;
			}

			// Debug output
			if (Engine.GetProcessFrames() % 60 == 0)
			{
				GD.Print($"Water physics: surface={waterSurfaceY:F2}, player={GlobalPosition.Y:F2}, " +
					$"target={targetY:F2}, distance={distanceFromTarget:F2}, " +
					$"force={buoyancyForce:F2}, velocity={velocity.Y:F2}");
			}
		}
		else
		{
			// Regular gravity when not in water
			if (!IsOnFloor())
				velocity.Y -= _gravity * (float)delta;

			// Regular jump when on floor and not in water
			if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
				velocity.Y = JumpVelocity;
		}

		// Store the original Y velocity for stair stepping
		float originalY = velocity.Y;

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

		// Enhanced stair stepping implementation
		if (IsOnFloor() && inputDir.Length() > 0.1f)
		{
			// Get the horizontal velocity (movement direction)
			Vector3 horizontalVelocity = new Vector3(Velocity.X, 0, Velocity.Z);

			if (horizontalVelocity.Length() > 0.1f)
			{
				// Normalize and scale to check ahead (slightly longer distance)
				Vector3 stepCheckDirection = horizontalVelocity.Normalized() * 0.25f; // Increased for larger character (was 0.125f)

				// Store current position for potential rollback
				Vector3 originalPosition = Position;

				// First, check if there's an obstacle in front of us
				PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
					Position + new Vector3(0, 0.05f, 0), // Increased for larger character (was 0.025f)
					Position + new Vector3(0, 0.05f, 0) + stepCheckDirection, // Check in movement direction
					1, // Collision mask (adjust as needed)
					new Godot.Collections.Array<Godot.Rid>() // Exclude nothing
				);

				// Cast the ray
				var result = GetWorld3D().DirectSpaceState.IntersectRay(query);

				// If we hit something, it might be a step
				if (result.Count > 0)
				{
					// Get the collision point
					Vector3 collisionPoint = (Vector3)result["position"];

					// Calculate how far we need to check above the collision
					float checkHeight = MaxStepHeight + 0.1f; // Increased for larger character (was 0.05f)

					// Check if there's space above the obstacle (for the player's height)
					for (float h = 0.125f; h <= checkHeight; h += 0.125f) // Increased for larger character (was 0.0625f)
					{
						// Cast a ray from above the obstacle
						query = PhysicsRayQueryParameters3D.Create(
							new Vector3(collisionPoint.X, Position.Y + h, collisionPoint.Z), // Start above the collision
							new Vector3(collisionPoint.X, Position.Y + h, collisionPoint.Z) + stepCheckDirection * 0.25f, // Increased for larger character (was 0.125f)
							1, // Collision mask
							new Godot.Collections.Array<Godot.Rid>() // Exclude nothing
						);

						var upperResult = GetWorld3D().DirectSpaceState.IntersectRay(query);

						// If there's nothing blocking at this height, we can try to step up
						if (upperResult.Count == 0)
						{
							// Try to move the player up to this height
							Position = new Vector3(Position.X, Position.Y + h - 0.025f, Position.Z); // Increased for larger character (was 0.0125f)

							// Apply a small forward impulse to help get over the step
							velocity = horizontalVelocity.Normalized() * Speed * 1.1f; // Keep the same impulse multiplier
							velocity.Y = 0; // Don't apply gravity during the step

							Velocity = velocity;
							MoveAndSlide();

							// Check if we successfully moved forward
							Vector3 newHorizontalPos = new Vector3(Position.X, 0, Position.Z);
							Vector3 oldHorizontalPos = new Vector3(originalPosition.X, 0, originalPosition.Z);

							if ((newHorizontalPos - oldHorizontalPos).Length() < 0.1f)
							{
								// We didn't move forward enough, try a higher step
								continue;
							}

							// We successfully stepped up
							GD.Print("Successfully stepped up by " + h + " units");
							return;
						}
					}

					// If we get here, we couldn't step up, so restore original position
					Position = originalPosition;
					velocity.Y = originalY; // Restore original Y velocity
					Velocity = velocity;
				}
			}
		}
	}
}
