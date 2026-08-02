using Godot;
using System.Collections.Generic;
using Mindani.World;

namespace Mindani.UI;

public partial class SpawnSelector : CanvasLayer
{
    CharacterBody3D? _player;

    readonly List<(string Name, Vector3 Pos)> _locs = new();

    Control _panel  = null!;
    Label   _toast  = null!;
    Button  _toggle = null!;
    bool    _open;
    double  _toastTimer;

    public override void _Ready()
    {
        Layer = 5;

        _player = GetNodeOrNull<CharacterBody3D>("/root/Main/Lindani");

        // First entry is always home
        _locs.Add(("Home Base", new Vector3(32, 24, 32)));
        _locs.AddRange(VoxelWorld.SpawnPoints);

        BuildUI();
    }

    void BuildUI()
    {
        // Toggle button — top-left corner
        _toggle = new Button { Text = "[ Locations ]" };
        _toggle.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _toggle.SetOffset(Side.Left,   10f);
        _toggle.SetOffset(Side.Top,    10f);
        _toggle.SetOffset(Side.Right,  140f);
        _toggle.SetOffset(Side.Bottom, 36f);
        _toggle.AddThemeFontSizeOverride("font_size", 13);
        _toggle.Pressed += TogglePanel;
        AddChild(_toggle);

        // Hint label just below the button
        var hint = new Label { Text = "F1–F5 = quick travel" };
        hint.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        hint.SetOffset(Side.Left,   10f);
        hint.SetOffset(Side.Top,    38f);
        hint.SetOffset(Side.Right,  200f);
        hint.SetOffset(Side.Bottom, 54f);
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.75f));
        AddChild(hint);

        // Dropdown panel
        float panelH = _locs.Count * 40f + 8f;
        _panel = new Control();
        _panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _panel.SetOffset(Side.Left,   10f);
        _panel.SetOffset(Side.Top,    56f);
        _panel.SetOffset(Side.Right,  260f);
        _panel.SetOffset(Side.Bottom, 56f + panelH);
        _panel.Visible = false;
        AddChild(_panel);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0f, 0f, 0f, 0.78f);
        _panel.AddChild(bg);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 4);
        _panel.AddChild(vbox);

        for (int i = 0; i < _locs.Count; i++)
        {
            int captured = i;
            var name = _locs[i].Name;

            var btn = new Button { Text = $"  [F{i + 1}]  {name}" };
            btn.CustomMinimumSize = new Vector2(0, 36);
            btn.AddThemeFontSizeOverride("font_size", 14);
            btn.AddThemeColorOverride("font_color", Colors.White);
            btn.Pressed += () => Teleport(captured);
            vbox.AddChild(btn);
        }

        // Toast — centred, appears briefly on teleport
        _toast = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _toast.SetAnchorsPreset(Control.LayoutPreset.Center);
        _toast.GrowHorizontal = Control.GrowDirection.Both;
        _toast.GrowVertical   = Control.GrowDirection.Both;
        _toast.SetOffset(Side.Left,   -180f);
        _toast.SetOffset(Side.Right,   180f);
        _toast.SetOffset(Side.Top,    -60f);
        _toast.SetOffset(Side.Bottom, -28f);
        _toast.AddThemeFontSizeOverride("font_size", 22);
        _toast.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.4f));
        _toast.Visible = false;
        AddChild(_toast);
    }

    void TogglePanel()
    {
        _open = !_open;
        _panel.Visible = _open;
        Input.MouseMode = _open
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured;
    }

    void Teleport(int idx)
    {
        if (_player == null || (uint)idx >= (uint)_locs.Count) return;

        _player.Velocity      = Vector3.Zero;
        _player.GlobalPosition = _locs[idx].Pos;

        if (_open)
        {
            _open          = false;
            _panel.Visible = false;
        }
        Input.MouseMode = Input.MouseModeEnum.Captured;

        _toast.Text    = $"Arrived: {_locs[idx].Name}";
        _toast.Visible = true;
        _toastTimer    = 2.5;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey key || !key.Pressed || key.Echo) return;

        int idx = key.PhysicalKeycode switch
        {
            Key.F1 => 0,
            Key.F2 => 1,
            Key.F3 => 2,
            Key.F4 => 3,
            Key.F5 => 4,
            _      => -1,
        };

        if (idx >= 0)
        {
            Teleport(idx);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_toastTimer > 0)
        {
            _toastTimer -= delta;
            if (_toastTimer <= 0)
                _toast.Visible = false;
        }
    }
}
