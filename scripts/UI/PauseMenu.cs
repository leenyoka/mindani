using Godot;

namespace Mindani.UI;

public partial class PauseMenu : CanvasLayer
{
    Control _overlay = null!;

    public override void _Ready()
    {
        _overlay = new Control();
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.Visible = false;
        AddChild(_overlay);

        var dim = new ColorRect();
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dim.Color = new Color(0f, 0f, 0f, 0.62f);
        _overlay.AddChild(dim);

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.GrowVertical   = Control.GrowDirection.Both;
        panel.SetOffset(Side.Left,   -160f);
        panel.SetOffset(Side.Right,   160f);
        panel.SetOffset(Side.Top,    -150f);
        panel.SetOffset(Side.Bottom,  150f);
        _overlay.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        vbox.Alignment = BoxContainer.AlignmentMode.Center;
        panel.AddChild(vbox);

        var title = new Label { Text = "PAUSED", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 38);
        vbox.AddChild(title);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });

        AddBtn(vbox, "Resume",       new Color(0.12f, 0.44f, 0.12f), OnResume);
        AddBtn(vbox, "Quit to Menu", new Color(0.38f, 0.34f, 0.06f), OnMenu);
        AddBtn(vbox, "Exit Game",    new Color(0.44f, 0.09f, 0.09f), OnExit);
    }

    void AddBtn(VBoxContainer parent, string text, Color bg, System.Action cb)
    {
        var btn = new Button { Text = text };
        btn.CustomMinimumSize = new Vector2(270, 50);
        btn.AddThemeFontSizeOverride("font_size", 19);
        btn.AddThemeColorOverride("font_color", Colors.White);

        var s = new StyleBoxFlat { BgColor = bg };
        s.CornerRadiusTopLeft     = 6;
        s.CornerRadiusTopRight    = 6;
        s.CornerRadiusBottomLeft  = 6;
        s.CornerRadiusBottomRight = 6;
        s.ContentMarginLeft       = 14;
        s.ContentMarginRight      = 14;
        s.ContentMarginTop        = 8;
        s.ContentMarginBottom     = 8;
        btn.AddThemeStyleboxOverride("normal", s);

        var hover = (StyleBoxFlat)s.Duplicate();
        hover.BgColor = bg.Lightened(0.2f);
        btn.AddThemeStyleboxOverride("hover", hover);

        btn.Pressed += cb;
        parent.AddChild(btn);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel"))
            Toggle();
    }

    void Toggle()
    {
        bool pausing = !GetTree().Paused;
        GetTree().Paused  = pausing;
        _overlay.Visible  = pausing;
        Input.MouseMode   = pausing
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured;
        GetViewport().SetInputAsHandled();
    }

    void OnResume() => Toggle();

    void OnMenu()
    {
        GetTree().Paused = false;
        Input.MouseMode  = Input.MouseModeEnum.Visible;
        GetTree().ChangeSceneToFile("res://scenes/menu.tscn");
    }

    void OnExit() => GetTree().Quit();
}
