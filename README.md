# CubeGen - Procedurally Generated Voxel RPG

This is a procedurally generated 3D voxel RPG inspired by Cubeworld, built using C# inside of Godot 4.4. The game features a fully procedurally generated world including:

<img width="1728" height="1054" alt="image" src="https://github.com/user-attachments/assets/63d6299b-ef7c-436b-9765-375bf00ee3a3" />
<img width="1728" height="1053" alt="image" src="https://github.com/user-attachments/assets/c160ec29-1257-421b-b4bd-c4d21dc2fc4c" />


- Terrain generation using noise algorithms
- Different biome types (Forest, Plains, Desert, Mountains, Tundra)
- Placement of objects in the world such as trees and foliage
- Points of interest and towns (planned)
- Chunk-based world loading system for infinite worlds

## Project Structure

- `src/World/Generation`: Contains world generation logic
- `src/World/Chunks`: Contains chunk management and mesh generation
- `src/Player`: Contains player controller
- `src/Utils`: Utility classes
- `src/UI`: UI elements

## Getting Started

1. Open the project in Godot 4.4
2. Run the main scene
3. Use WASD to move, Space to jump, and mouse to look around
4. Press Escape to toggle mouse capture

## Development Status

This project is in early development. Current features:
- Basic terrain generation with different biomes
- Chunk-based world loading
- Simple player controller

## Planned Features

- More detailed biomes
- Improved object placement (trees, rocks, etc.)
- Towns and points of interest
- RPG elements (inventory, quests, etc.)
- Improved visuals with custom shaders
