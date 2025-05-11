using Godot;
using System;
using System.Collections.Generic;
using CubeGen.Player.CharacterParts;
using CubeGen.World.Common;

namespace CubeGen.Player
{
	/// <summary>
	/// Voxel-based character with customizable body parts
	/// </summary>
	public partial class VoxelCharacter : Node3D
	{
		// Body part references
		private VoxelHead _head;
		private VoxelBody _body;
		private VoxelLimb _leftArm;
		private VoxelLimb _rightArm;
		private VoxelLimb _leftLeg;
		private VoxelLimb _rightLeg;

		// Animation properties
		private float _walkCycle = 0.0f;
		private float _walkSpeed = 5.0f;
		private bool _isWalking = false;
		private bool _isJumping = false;

		// Customization properties
		[Export] public Color SkinColor { get; set; } = new Color(1.0f, 0.8f, 0.6f); // Skin color for hands
		[Export] public Color FaceColor { get; set; } = new Color(1.0f, 1.0f, 0.8f); // White/cream face color
		[Export] public Color HairColor { get; set; } = new Color(1.0f, 0.5f, 0.0f); // Orange hair like in the image
		[Export] public Color ShirtColor { get; set; } = new Color(1.0f, 0.5f, 0.0f); // Orange shirt like in the image
		[Export] public Color PantsColor { get; set; } = new Color(0.4f, 0.3f, 0.6f); // Purple pants like in the image
		[Export] public Color ShoeColor { get; set; } = new Color(0.0f, 0.0f, 0.0f); // Black shoes like in the image
		[Export] public HairStyle HairStyle { get; set; } = HairStyle.PixelStyle; // Use the pixel character style

		// Collision shape for the character
		private CollisionShape3D _collisionShape;

		public override void _Ready()
		{
			// Create body parts
			CreateBodyParts();

			// Position body parts
			PositionBodyParts();

			// Create collision shape
			CreateCollisionShape();
		}

		/// <summary>
		/// Create all body parts
		/// </summary>
		private void CreateBodyParts()
		{
			// Create head
			_head = new VoxelHead
			{
				BaseColor = FaceColor, // Use face color for the base
				FaceColor = FaceColor, // Set the face color
				HairColor = HairColor,
				HairStyle = HairStyle
			};
			AddChild(_head);

			// Create body
			_body = new VoxelBody
			{
				ShirtColor = ShirtColor,
				PantsColor = PantsColor,
				HasBelt = false // No belt for pixel character
			};
			AddChild(_body);

			// Create arms
			_leftArm = new VoxelLimb
			{
				Type = VoxelLimb.LimbType.Arm,
				IsLeft = true,
				SkinColor = SkinColor,
				ClothingColor = ShirtColor
			};
			AddChild(_leftArm);

			_rightArm = new VoxelLimb
			{
				Type = VoxelLimb.LimbType.Arm,
				IsLeft = false,
				SkinColor = SkinColor,
				ClothingColor = ShirtColor
			};
			AddChild(_rightArm);

			// Create legs
			_leftLeg = new VoxelLimb
			{
				Type = VoxelLimb.LimbType.Leg,
				IsLeft = true,
				SkinColor = SkinColor,
				ClothingColor = PantsColor,
				ShoeColor = ShoeColor
			};
			AddChild(_leftLeg);

			_rightLeg = new VoxelLimb
			{
				Type = VoxelLimb.LimbType.Leg,
				IsLeft = false,
				SkinColor = SkinColor,
				ClothingColor = PantsColor,
				ShoeColor = ShoeColor
			};
			AddChild(_rightLeg);
		}

		/// <summary>
		/// Position all body parts for the pixel character
		/// </summary>
		private void PositionBodyParts()
		{
			// Position head on top of body with no gap for the blocky pixel look
			_head.Position = new Vector3(0, 0.35f, 0); // Adjusted for smaller head

			// Body is at the center
			_body.Position = new Vector3(0, 0, 0);

			// Position arms on sides of body
			// Move arms slightly higher and further out for the pixel character look
			_leftArm.Position = new Vector3(-0.32f, 0.1f, 0);
			_rightArm.Position = new Vector3(0.32f, 0.1f, 0);

			// Position legs below body
			// Keep legs closer together for the pixel character look
			_leftLeg.Position = new Vector3(-0.12f, -0.4f, 0);
			_rightLeg.Position = new Vector3(0.12f, -0.4f, 0);
		}

		/// <summary>
		/// Create collision shape for the character
		/// </summary>
		private void CreateCollisionShape()
		{
			// Create collision shape
			_collisionShape = new CollisionShape3D();
			_collisionShape.Name = "CharacterCollision";

			// Create capsule shape that encompasses the character
			CapsuleShape3D capsuleShape = new CapsuleShape3D();
			capsuleShape.Radius = 0.4f;
			capsuleShape.Height = 1.8f;

			// Set shape
			_collisionShape.Shape = capsuleShape;

			// Position collision shape
			_collisionShape.Position = new Vector3(0, 0, 0);

			// Add to character
			AddChild(_collisionShape);
		}

		/// <summary>
		/// Get the collision shape for the character
		/// </summary>
		public CollisionShape3D GetCollisionShape()
		{
			return _collisionShape;
		}

		/// <summary>
		/// Update character animation
		/// </summary>
		public void UpdateAnimation(double delta, bool isWalking, bool isJumping, Vector3 velocity)
		{
			// Update animation state
			_isWalking = isWalking;
			_isJumping = isJumping;

			// Update walk cycle if walking
			if (_isWalking)
			{
				_walkCycle += (float)delta * _walkSpeed * velocity.Length() / 5.0f;

				// Keep walk cycle between 0 and 2π
				if (_walkCycle > Mathf.Pi * 2)
				{
					_walkCycle -= Mathf.Pi * 2;
				}
			}
			else
			{
				// Reset walk cycle when not walking
				_walkCycle = 0;
			}

			// Apply animations
			AnimateWalking();
			AnimateJumping();
		}

		/// <summary>
		/// Animate walking motion
		/// </summary>
		private void AnimateWalking()
		{
			if (_isWalking)
			{
				// Animate legs
				float legAngle = Mathf.Sin(_walkCycle) * 0.5f;
				_leftLeg.Rotation = new Vector3(legAngle, 0, 0);
				_rightLeg.Rotation = new Vector3(-legAngle, 0, 0);

				// Animate arms (opposite of legs)
				float armAngle = Mathf.Sin(_walkCycle) * 0.3f;
				_leftArm.Rotation = new Vector3(-armAngle, 0, 0);
				_rightArm.Rotation = new Vector3(armAngle, 0, 0);

				// Slight body bob
				float bodyBob = Mathf.Abs(Mathf.Sin(_walkCycle)) * 0.05f;
				_body.Position = new Vector3(0, bodyBob, 0);

				// Adjust head and other parts to follow body
				_head.Position = new Vector3(0, 0.35f + bodyBob, 0); // Adjusted for smaller head
				_leftArm.Position = new Vector3(-0.32f, 0.1f + bodyBob, 0);
				_rightArm.Position = new Vector3(0.32f, 0.1f + bodyBob, 0);
			}
			else
			{
				// Reset to default positions when not walking
				_leftLeg.Rotation = Vector3.Zero;
				_rightLeg.Rotation = Vector3.Zero;
				_leftArm.Rotation = Vector3.Zero;
				_rightArm.Rotation = Vector3.Zero;
				_body.Position = new Vector3(0, 0, 0);
				_head.Position = new Vector3(0, 0.35f, 0); // Adjusted for smaller head
				_leftArm.Position = new Vector3(-0.32f, 0.1f, 0);
				_rightArm.Position = new Vector3(0.32f, 0.1f, 0);
			}
		}

		/// <summary>
		/// Animate jumping motion
		/// </summary>
		private void AnimateJumping()
		{
			if (_isJumping)
			{
				// Bend legs slightly
				_leftLeg.Rotation = new Vector3(-0.2f, 0, 0);
				_rightLeg.Rotation = new Vector3(-0.2f, 0, 0);

				// Raise arms
				_leftArm.Rotation = new Vector3(-0.4f, 0, 0);
				_rightArm.Rotation = new Vector3(-0.4f, 0, 0);
			}
		}

		/// <summary>
		/// Customize the character appearance
		/// </summary>
		public void Customize(Color skinColor, Color faceColor, Color hairColor, Color shirtColor, Color pantsColor, Color shoeColor, HairStyle hairStyle)
		{
			// Store new values
			SkinColor = skinColor;
			FaceColor = faceColor;
			HairColor = hairColor;
			ShirtColor = shirtColor;
			PantsColor = pantsColor;
			ShoeColor = shoeColor;
			HairStyle = hairStyle;

			// Update head
			_head.BaseColor = faceColor;
			_head.FaceColor = faceColor;
			_head.HairColor = hairColor;
			_head.HairStyle = hairStyle;
			_head.GenerateMesh();

			// Update body
			_body.ShirtColor = shirtColor;
			_body.PantsColor = pantsColor;
			_body.HasBelt = false; // No belt for pixel character
			_body.GenerateMesh();

			// Update arms
			_leftArm.SkinColor = skinColor;
			_leftArm.ClothingColor = shirtColor;
			_leftArm.GenerateMesh();

			_rightArm.SkinColor = skinColor;
			_rightArm.ClothingColor = shirtColor;
			_rightArm.GenerateMesh();

			// Update legs
			_leftLeg.SkinColor = skinColor;
			_leftLeg.ClothingColor = pantsColor;
			_leftLeg.ShoeColor = shoeColor;
			_leftLeg.GenerateMesh();

			_rightLeg.SkinColor = skinColor;
			_rightLeg.ClothingColor = pantsColor;
			_rightLeg.ShoeColor = shoeColor;
			_rightLeg.GenerateMesh();
		}

		/// <summary>
		/// Simplified customize method for backward compatibility
		/// </summary>
		public void Customize(Color skinColor, Color hairColor, Color shirtColor, Color pantsColor, HairStyle hairStyle)
		{
			// Use default face and shoe colors
			Customize(skinColor, FaceColor, hairColor, shirtColor, pantsColor, ShoeColor, hairStyle);
		}

		/// <summary>
		/// Regenerate all character part meshes
		/// </summary>
		public void RegenerateAllMeshes()
		{
			// Regenerate each body part mesh
			_head.RegenerateMesh();
			_body.RegenerateMesh();
			_leftArm.RegenerateMesh();
			_rightArm.RegenerateMesh();
			_leftLeg.RegenerateMesh();
			_rightLeg.RegenerateMesh();
		}
	}
}
