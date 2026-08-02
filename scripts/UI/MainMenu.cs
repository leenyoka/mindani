using Godot;

namespace Mindani.UI;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        SetAnchorsPreset(LayoutPreset.FullRect);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.Color = new Color(0.05f, 0.13f, 0.05f);
        AddChild(bg);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.Center);
        vbox.GrowHorizontal = GrowDirection.Both;
        vbox.GrowVertical   = GrowDirection.Both;
        vbox.SetOffset(Side.Left,   -210f);
        vbox.SetOffset(Side.Right,   210f);
        vbox.SetOffset(Side.Top,    -200f);
        vbox.SetOffset(Side.Bottom,  200f);
        vbox.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(vbox);

        var title = new Label { Text = "MINDANI", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 72);
        title.AddThemeColorOverride("font_color", new Color(0.85f, 1.0f, 0.35f));
        vbox.AddChild(title);

        var sub = new Label { Text = "An adventure with Lindani", HorizontalAlignment = HorizontalAlignment.Center };
        sub.AddThemeFontSizeOverride("font_size", 18);
        sub.AddThemeColorOverride("font_color", new Color(0.70f, 0.88f, 0.70f));
        vbox.AddChild(sub);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 44) });

        var play = MakeBtn("PLAY", new Color(0.12f, 0.50f, 0.12f));
        play.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/main.tscn");
        vbox.AddChild(play);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 14) });

        var exit = MakeBtn("EXIT", new Color(0.48f, 0.10f, 0.10f));
        exit.Pressed += () => GetTree().Quit();
        vbox.AddChild(exit);
    }

    static Button MakeBtn(string text, Color bg)
    {
        var btn = new Button { Text = text };
        btn.CustomMinimumSize = new Vector2(290, 58);
        btn.AddThemeFontSizeOverride("font_size", 24);
        btn.AddThemeColorOverride("font_color", Colors.White);

        var s = MakeStyle(bg);
        btn.AddThemeStyleboxOverride("normal", s);

        var hover = (StyleBoxFlat)s.Duplicate();
        hover.BgColor = bg.Lightened(0.2f);
        btn.AddThemeStyleboxOverride("hover", hover);

        var pressed = (StyleBoxFlat)s.Duplicate();
        pressed.BgColor = bg.Darkened(0.15f);
        btn.AddThemeStyleboxOverride("pressed", pressed);

        return btn;
    }

    static StyleBoxFlat MakeStyle(Color bg)
    {
        var s = new StyleBoxFlat { BgColor = bg };
        s.CornerRadiusTopLeft     = 8;
        s.CornerRadiusTopRight    = 8;
        s.CornerRadiusBottomLeft  = 8;
        s.CornerRadiusBottomRight = 8;
        s.ContentMarginLeft       = 20;
        s.ContentMarginRight      = 20;
        s.ContentMarginTop        = 10;
        s.ContentMarginBottom     = 10;
        return s;
    }
}
