using Godot;

namespace Mindani.World;

/// Loads the VoxelBlockyLibrary resource and wires it to the terrain mesher.
public partial class BlockLibrary : Node
{
    const string LibraryPath = "res://resources/voxel_library.tres";

    public override void _Ready()
    {
        var terrain = GetParent<VoxelTerrain>();
        var library = GD.Load<VoxelBlockyLibrary>(LibraryPath);

        if (library == null)
        {
            GD.PrintErr($"BlockLibrary: could not load {LibraryPath}");
            return;
        }

        var mesher = new VoxelMesherBlocky { Library = library };
        terrain.Mesher = mesher;
    }
}
