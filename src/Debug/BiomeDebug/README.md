# Biome Debug Scenes

This directory contains scenes for debugging individual biomes in isolation.

## Available Scenes

1. **BiomeDebugSelector.tscn** - Main selector scene that allows you to choose which biome to debug
2. **BiomeDebugScene.tscn** - Interactive scene that allows switching between biomes at runtime
3. Individual biome debug scenes:
   - **ForestLandsDebug.tscn** - Debug scene for ForestLands biome (combined Forest, Plains, and Mountains)
   - **DesertDebug.tscn** - Debug scene for Desert biome
   - **TundraDebug.tscn** - Debug scene for Tundra biome
   - **IslandsDebug.tscn** - Debug scene for Islands biome

## How to Use

1. Open the Godot editor
2. Navigate to `src/Debug/BiomeDebug` in the FileSystem panel
3. Double-click on `BiomeDebugSelector.tscn` to open it
4. Run the scene (F5 or Play button)
5. Select the biome you want to debug from the menu

Alternatively, you can directly open and run any of the individual biome debug scenes.

## Controls

- **WASD** - Move the player/camera
- **Space** - Jump (in player mode)
- **Mouse** - Look around (in player mode)
- **Q/E** - Move up/down (in camera-only mode)
- **Mouse Wheel** - Zoom in/out (in camera-only mode)

## How It Works

These debug scenes use the `SingleBiomeRegionGenerator` class to override the normal biome generation and force the world generator to create only a single biome type. This allows for easier testing and debugging of individual biome characteristics without interference from other biomes.

The `BiomeDebugScene.tscn` provides an interactive UI that allows switching between different biomes at runtime without having to restart the scene.

## Adding New Biomes

If you add a new biome type to the `BiomeType` enum, you should:

1. Create a new debug script (e.g., `NewBiomeDebug.cs`)
2. Create a new debug scene (e.g., `NewBiomeDebug.tscn`)
3. Update the `BiomeDebugSelector.cs` script to include the new biome
