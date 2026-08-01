using Godot;

namespace Mindani.World;

/// Runs as a child of VoxelTerrain.
/// Assigns the world generator and block library mesher at startup.
public partial class BlockLibrary : Node
{
    const string LibraryPath = "res://resources/voxel_library.tres";

    public override void _Ready()
    {
        var terrain = GetParent<VoxelTerrain>();

        terrain.Generator = new WorldGenerator();

        var library = GD.Load<VoxelBlockyLibrary>(LibraryPath);
        if (library == null)
        {
            GD.PrintErr($"BlockLibrary: could not load {LibraryPath}");
            return;
        }

        terrain.Mesher = new VoxelMesherBlocky { Library = library };
    }
}
