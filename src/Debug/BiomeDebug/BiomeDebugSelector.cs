using Godot;
using System;
using System.Collections.Generic;
using CubeGen.World.Common;

namespace CubeGen.Debug.BiomeDebug
{
	public partial class BiomeDebugSelector : Control
	{
		private Dictionary<BiomeType, string> _biomeScenes = new Dictionary<BiomeType, string>();

		public override void _Ready()
		{
			// Initialize biome scene paths
			InitializeBiomeScenes();

			// Create UI
			CreateUI();
		}

		private void InitializeBiomeScenes()
		{
			_biomeScenes[BiomeType.ForestLands] = "res://src/Debug/BiomeDebug/ForestLandsDebug.tscn";
			_biomeScenes[BiomeType.Desert] = "res://src/Debug/BiomeDebug/DesertDebug.tscn";
			_biomeScenes[BiomeType.Tundra] = "res://src/Debug/BiomeDebug/TundraDebug.tscn";
			_biomeScenes[BiomeType.Islands] = "res://src/Debug/BiomeDebug/IslandsDebug.tscn";
		}

		private void CreateUI()
		{
			// Create a panel
			Panel panel = new Panel();
			panel.AnchorRight = 1.0f;
			panel.AnchorBottom = 1.0f;
			AddChild(panel);

			// Create a VBoxContainer for layout
			VBoxContainer container = new VBoxContainer();
			container.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			container.Position = new Vector2(20, 20);
			container.Size = new Vector2(panel.Size.X - 40, panel.Size.Y - 40);
			panel.AddChild(container);

			// Add a title label
			Label titleLabel = new Label();
			titleLabel.Text = "Biome Debug Selector";
			titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
			titleLabel.CustomMinimumSize = new Vector2(0, 50);
			container.AddChild(titleLabel);

			// Add instructions
			Label instructionsLabel = new Label();
			instructionsLabel.Text = "Select a biome to debug:";
			container.AddChild(instructionsLabel);

			// Add some spacing
			container.AddChild(new Control { CustomMinimumSize = new Vector2(0, 20) });

			// Create buttons for each biome
			foreach (BiomeType biomeType in Enum.GetValues(typeof(BiomeType)))
			{
				Button biomeButton = new Button();
				biomeButton.Text = biomeType.ToString();
				biomeButton.CustomMinimumSize = new Vector2(0, 50);

				// Connect button press to load scene
				biomeButton.Pressed += () => LoadBiomeScene(biomeType);

				container.AddChild(biomeButton);

				// Add a small spacing between buttons
				container.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
			}

			// Add a button to load the main scene with all biomes
			Button allBiomesButton = new Button();
			allBiomesButton.Text = "All Biomes (Main Scene)";
			allBiomesButton.CustomMinimumSize = new Vector2(0, 50);
			allBiomesButton.Pressed += LoadMainScene;
			container.AddChild(allBiomesButton);

			// Add a button to load the biome debug scene
			Button biomeDebugButton = new Button();
			biomeDebugButton.Text = "Interactive Biome Debug";
			biomeDebugButton.CustomMinimumSize = new Vector2(0, 50);
			biomeDebugButton.Pressed += LoadBiomeDebugScene;
			container.AddChild(biomeDebugButton);
		}

		private void LoadBiomeScene(BiomeType biomeType)
		{
			if (_biomeScenes.TryGetValue(biomeType, out string scenePath))
			{
				GetTree().ChangeSceneToFile(scenePath);
			}
			else
			{
				GD.PrintErr($"No scene found for biome type: {biomeType}");
			}
		}

		private void LoadMainScene()
		{
			GetTree().ChangeSceneToFile("res://src/World/World.tscn");
		}

		private void LoadBiomeDebugScene()
		{
			GetTree().ChangeSceneToFile("res://src/Debug/BiomeDebug/BiomeDebugScene.tscn");
		}
	}
}
