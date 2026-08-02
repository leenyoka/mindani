using Godot;
using Mindani.Player;

namespace Mindani.UI;

public partial class Hotbar : HBoxContainer
{
    [Export] public BlockInteraction? Interaction;

    static readonly (int id, string name, Color colour)[] Slots =
    [
        (1, "Grass",  new Color(0.3f, 0.7f, 0.2f)),
        (2, "Dirt",   new Color(0.5f, 0.35f, 0.1f)),
        (3, "Stone",  new Color(0.55f, 0.55f, 0.55f)),
        (4, "Sand",   new Color(0.9f, 0.85f, 0.5f)),
    ];

    int _selected = 0;

    public override void _Ready()
    {
        foreach (var (_, name, colour) in Slots)
        {
            var panel = new PanelContainer();
            var label = new Label { Text = name, HorizontalAlignment = HorizontalAlignment.Center };
            panel.AddChild(label);
            AddChild(panel);
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = colour });
        }

        if (Interaction == null)
        {
            foreach (var node in GetTree().GetNodesInGroup("block_interaction"))
            {
                if (node is BlockInteraction bi) { Interaction = bi; break; }
            }
        }

        UpdateSelection();
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true } key)
        {
            int slot = (int)key.Keycode - (int)Key.Key1;
            if (slot >= 0 && slot < Slots.Length) { _selected = slot; UpdateSelection(); }
        }

        if (e is InputEventMouseButton { Pressed: true } mouse)
        {
            if (mouse.ButtonIndex == MouseButton.WheelUp)
                { _selected = (_selected - 1 + Slots.Length) % Slots.Length; UpdateSelection(); }
            if (mouse.ButtonIndex == MouseButton.WheelDown)
                { _selected = (_selected + 1) % Slots.Length; UpdateSelection(); }
        }
    }

    void UpdateSelection()
    {
        if (Interaction != null)
            Interaction.SelectedBlockId = Slots[_selected].id;

        for (int i = 0; i < GetChildCount(); i++)
        {
            var style = (StyleBoxFlat)GetChild<PanelContainer>(i).GetThemeStylebox("panel");
            style.SetBorderWidthAll(i == _selected ? 3 : 0);
            style.BorderColor = Colors.White;
        }
    }
}
