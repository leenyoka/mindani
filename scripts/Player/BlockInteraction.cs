using Godot;
using Mindani.World;

namespace Mindani.Player;

public partial class BlockInteraction : Node
{
    [Export] public float Reach = 5f;
    [Export] public int SelectedBlockId = 1;

    Camera3D   _camera = null!;
    VoxelWorld _world  = null!;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("%Camera3D");
        _world  = GetNode<VoxelWorld>("/root/Main/VoxelWorld");
        AddToGroup("block_interaction");
    }

    public override void _UnhandledInput(InputEvent e)
    {
        var result = Raycast();
        if (result is null) return;

        if (result["collider"].As<Node>() is not { } collider) return;
        if (!collider.IsInGroup("terrain")) return;

        var hitPos    = (Vector3)result["position"];
        var hitNormal = (Vector3)result["normal"];

        if (e.IsActionPressed("break_block"))
        {
            var blockPos = (Vector3I)(hitPos - hitNormal * 0.5f).Floor();
            _world.SetBlock(blockPos, VoxelWorld.Air);
        }

        if (e.IsActionPressed("place_block"))
        {
            var placePos = (Vector3I)(hitPos + hitNormal * 0.5f).Floor();
            _world.SetBlock(placePos, (byte)SelectedBlockId);
        }
    }

    Godot.Collections.Dictionary? Raycast()
    {
        var space = GetViewport().GetCamera3D().GetWorld3D().DirectSpaceState;
        var from  = _camera.GlobalPosition;
        var to    = from + (-_camera.GlobalTransform.Basis.Z) * Reach;
        var hit   = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to));
        return hit.Count > 0 ? hit : null;
    }
}
