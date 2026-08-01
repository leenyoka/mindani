# Mindani

A Minecraft-inspired voxel adventure game for Lindani, built with Godot 4 + C#.

## Stack

- Godot 4 (.NET / C#)
- [Voxel Tools](https://github.com/Zylann/godot_voxel) GDExtension (not in repo — see setup)
- Target platform: Android

## First-time setup

1. **Download Voxel Tools** — grab `GodotVoxelExtension.zip` from the
   [v1.6x release](https://github.com/Zylann/godot_voxel/releases/tag/v1.6x)
   and extract it so `addons/zylann.voxel/` sits inside this project root.

2. Open the project in **Godot 4 (.NET build)**.

3. Enable the plugin: **Project → Project Settings → Plugins → zylann.voxel → Enable**.

4. Build the C# solution: **Build** button in the top bar (or Ctrl+B).

5. Press **F5** to run.

## Controls

| Key | Action |
|---|---|
| W A S D | Move |
| Shift | Sprint |
| Space | Jump |
| Mouse | Look |
| Left click | Break block |
| Right click | Place block |
| 1 2 3 4 / Scroll | Select block type |
| Esc | Release mouse |

## Project structure

```
mindani/
├── scenes/
│   ├── main.tscn       # World + player + HUD
│   └── player.tscn     # Lindani character
├── scripts/
│   ├── World/
│   │   └── WorldGenerator.cs   # Terrain generation
│   ├── Player/
│   │   ├── PlayerController.cs # Movement + camera
│   │   └── BlockInteraction.cs # Place/break blocks
│   └── UI/
│       └── Hotbar.cs           # Block selector
└── addons/
    └── zylann.voxel/           # Voxel Tools (download separately)
```
