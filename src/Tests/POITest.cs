using Godot;
using System;
using CubeGen.World.Generation;
using CubeGen.World.Generation.POI;
using System.Linq;

namespace CubeGen.Tests
{
	public partial class POITest : Node3D
	{
		[Export] public int Seed { get; set; } = 0;
		[Export] public int ViewDistance { get; set; } = 5;
		
		private WorldGenerator _worldGenerator;
		
		public override void _Ready()
		{
			// Initialize the POI registry
			POIRegistry.Instance.ClearInstances();
			
			// Initialize the world generator
			_worldGenerator = GetNode<WorldGenerator>("WorldGenerator");
			if (_worldGenerator != null)
			{
				_worldGenerator.Initialize(Seed, ViewDistance);
				GD.Print("POI Test initialized");
			}
			else
			{
				GD.PrintErr("WorldGenerator not found!");
			}
		}
		
		public override void _Process(double delta)
		{
			// Update the world generator
			if (_worldGenerator != null)
			{
				// Get the player position
				Node3D player = GetNode<Node3D>("Player");
				if (player != null)
				{
					Vector3 playerPosition = player.GlobalPosition;
					
					// Convert to chunk coordinates
					int chunkSize = WorldGenerator.CHUNK_SIZE;
					Vector3 playerChunkPos = new Vector3(
						Mathf.FloorToInt(playerPosition.X / chunkSize),
						Mathf.FloorToInt(playerPosition.Y / chunkSize),
						Mathf.FloorToInt(playerPosition.Z / chunkSize)
					);
					
					// Update chunks around player
					_worldGenerator.GetNode<ChunkManager>("ChunkManager").UpdateChunksAroundPlayer(playerChunkPos, ViewDistance);
				}
			}
		}
		
		// Handle input for debugging
		public override void _Input(InputEvent @event)
		{
			if (@event is InputEventKey keyEvent && keyEvent.Pressed)
			{
				// Regenerate world with a new seed when R is pressed
				if (keyEvent.Keycode == Key.R)
				{
					Seed = new Random().Next();
					GD.Print($"Regenerating world with seed: {Seed}");
					
					// Clear existing POIs
					POIRegistry.Instance.ClearInstances();
					
					// Reinitialize the world generator
					_worldGenerator.Initialize(Seed, ViewDistance);
				}
				
				// Print POI information when P is pressed
				if (keyEvent.Keycode == Key.P)
				{
					PrintPOIInfo();
				}
			}
		}
		
		// Print information about all POIs in the world
		private void PrintPOIInfo()
		{
			GD.Print($"POI Information (Seed: {Seed}):");
			GD.Print($"Total POIs: {POIRegistry.Instance.Instances.Count}");
			
			// Group POIs by type
			var poiGroups = POIRegistry.Instance.Instances.GroupBy(poi => poi.Definition.Id);
			
			foreach (var group in poiGroups)
			{
				GD.Print($"  {group.First().Definition.Name}: {group.Count()} instances");
				
				// Print the first 5 POIs of each type
				int count = 0;
				foreach (var poi in group.Take(5))
				{
					GD.Print($"    - Position: {poi.Position}, Rotation: {poi.Rotation}°");
					count++;
				}
				
				if (group.Count() > 5)
				{
					GD.Print($"    ... and {group.Count() - 5} more");
				}
			}
		}
	}
}
