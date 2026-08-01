using Godot;

namespace Mindani.Player;

public partial class BlockInteraction : Node
{
    [Export] public float Reach = 5f;
    [Export] public int SelectedBlockId = 1;

    Camera3D     _camera  = null!;
    VoxelTerrain _terrain = null!;

    public override void _Ready()
    {
        _camera  = GetNode<Camera3D>("%Camera3D");
        _terrain = GetNode<VoxelTerrain>("/root/Main/VoxelTerrain");
    }

    public override void _UnhandledInput(InputEvent e)
    {
        var result = Raycast();
        if (result is null) return;

        Vector3 hitPos    = (Vector3)result["position"];
        Vector3 hitNormal = (Vector3)result["normal"];

        if (e.IsActionPressed("break_block"))
        {
            var tool = _terrain.GetVoxelTool();
            tool.SetVoxel((Vector3I)(hitPos - hitNormal * 0.5f).Floor(), 0);
        }

        if (e.IsActionPressed("place_block"))
        {
            var tool = _terrain.GetVoxelTool();
            tool.SetVoxel((Vector3I)(hitPos + hitNormal * 0.5f).Floor(), SelectedBlockId);
        }
    }

    Godot.Collections.Dictionary? Raycast()
    {
        var space = GetViewport().GetCamera3D().GetWorld3D().DirectSpaceState;
        Vector3 from = _camera.GlobalPosition;
        Vector3 to   = from + (-_camera.GlobalTransform.Basis.Z) * Reach;
        var query    = PhysicsRayQueryParameters3D.Create(from, to);
        var result   = space.IntersectRay(query);
        return result.Count > 0 ? result : null;
    }
}
