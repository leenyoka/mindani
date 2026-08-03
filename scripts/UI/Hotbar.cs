using Godot;
using Mindani.Player;

namespace Mindani.UI;

// Hidden block-selection manager — no visual bar.
// PauseMenu's block picker calls SelectBlock(); keyboard/scroll still works on desktop.
public partial class Hotbar : HBoxContainer
{
    public static Hotbar? Instance { get; private set; }

    [Export] public BlockInteraction? Interaction;

    public static readonly (int id, string name, Color colour)[] AllBlocks =
    [
        ( 1, "Grass",  new Color(0.30f, 0.70f, 0.20f)),
        ( 5, "Wood",   new Color(0.38f, 0.24f, 0.10f)),
        ( 6, "Leaves", new Color(0.15f, 0.52f, 0.10f)),
        ( 7, "Flower", new Color(1.00f, 0.25f, 0.40f)),
        ( 8, "Brick",  new Color(0.52f, 0.24f, 0.14f)),
        ( 9, "Plank",  new Color(0.72f, 0.58f, 0.35f)),
        (10, "Glass",  new Color(0.70f, 0.88f, 1.00f)),
    ];

    int _selected = 0;

    public override void _Ready()
    {
        Instance = this;
        Visible  = false;

        if (Interaction == null)
            foreach (var node in GetTree().GetNodesInGroup("block_interaction"))
                if (node is BlockInteraction bi) { Interaction = bi; break; }

        SelectBlock(0);
    }

    public void SelectBlock(int idx)
    {
        _selected = idx;
        if (Interaction != null)
            Interaction.SelectedBlockId = AllBlocks[idx].id;
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true } key)
        {
            int slot = (int)key.Keycode - (int)Key.Key1;
            if (slot >= 0 && slot < AllBlocks.Length) SelectBlock(slot);
        }
        if (e is InputEventMouseButton { Pressed: true } mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp)
                SelectBlock((_selected - 1 + AllBlocks.Length) % AllBlocks.Length);
            if (mb.ButtonIndex == MouseButton.WheelDown)
                SelectBlock((_selected + 1) % AllBlocks.Length);
        }
    }
}
