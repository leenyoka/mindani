using Godot;
using Mindani;
using System;
using System.Collections.Generic;

namespace Mindani.UI;

public partial class MainMenu : Control
{
    readonly Dictionary<string, Button> _friendBtns = new();

    Control?           _editOverlay;
    VBoxContainer?     _editContent;
    CompanionPortrait? _portrait;

    // ── Colour palettes ───────────────────────────────────────────────────────

    static readonly Color[] SkinColors =
    [
        new(0.95f, 0.80f, 0.60f), // light
        new(0.82f, 0.64f, 0.42f), // medium-light
        new(0.72f, 0.50f, 0.30f), // medium
        new(0.60f, 0.40f, 0.22f), // medium-dark
        new(0.45f, 0.28f, 0.13f), // dark
        new(0.30f, 0.18f, 0.08f), // very dark
        new(0.78f, 0.56f, 0.42f), // warm
        new(0.65f, 0.46f, 0.30f), // olive
    ];

    static readonly Color[] EyeColors =
    [
        new(0.12f, 0.08f, 0.04f), // very dark brown
        new(0.42f, 0.26f, 0.08f), // medium brown
        new(0.22f, 0.48f, 0.22f), // green
        new(0.20f, 0.40f, 0.80f), // blue
        new(0.55f, 0.40f, 0.15f), // hazel
        new(0.60f, 0.60f, 0.60f), // gray
        new(0.80f, 0.55f, 0.10f), // amber
        new(0.48f, 0.10f, 0.72f), // purple
    ];

    static readonly Color[] HairColors =
    [
        new(0.06f, 0.04f, 0.02f), // black
        new(0.35f, 0.20f, 0.08f), // dark brown
        new(0.60f, 0.35f, 0.10f), // brown
        new(0.85f, 0.70f, 0.20f), // blonde
        new(0.80f, 0.20f, 0.15f), // red
        new(0.55f, 0.15f, 0.75f), // purple
        new(0.15f, 0.40f, 0.85f), // blue
        new(0.85f, 0.35f, 0.60f), // pink
    ];

    static readonly Color[] ShirtColors =
    [
        new(0.10f, 0.35f, 0.85f), // blue
        new(0.15f, 0.60f, 0.25f), // green
        new(0.55f, 0.15f, 0.80f), // purple
        new(0.95f, 0.45f, 0.10f), // orange
        new(0.85f, 0.20f, 0.55f), // pink
        new(0.85f, 0.15f, 0.15f), // red
        new(0.85f, 0.75f, 0.10f), // yellow
        new(0.90f, 0.90f, 0.90f), // white
    ];

    static readonly Color[] PantsColors =
    [
        new(0.10f, 0.10f, 0.28f), // navy
        new(0.05f, 0.05f, 0.08f), // black
        new(0.22f, 0.22f, 0.22f), // dark gray
        new(0.15f, 0.35f, 0.15f), // dark green
        new(0.35f, 0.20f, 0.08f), // brown
        new(0.55f, 0.45f, 0.25f), // tan/khaki
        new(0.60f, 0.15f, 0.15f), // dark red
        new(0.75f, 0.75f, 0.75f), // light gray
    ];

    static readonly Color[] ShoeColors =
    [
        new(0.10f, 0.07f, 0.04f), // dark brown
        new(0.05f, 0.05f, 0.05f), // black
        new(0.88f, 0.88f, 0.88f), // white
        new(0.62f, 0.33f, 0.08f), // tan
        new(0.68f, 0.15f, 0.15f), // red
        new(0.15f, 0.40f, 0.85f), // blue
    ];

    static readonly Color[] AccessoryColors =
    [
        new(0.85f, 0.15f, 0.15f), // red
        new(0.15f, 0.40f, 0.85f), // blue
        new(0.20f, 0.65f, 0.20f), // green
        new(0.85f, 0.75f, 0.10f), // yellow
        new(0.55f, 0.15f, 0.80f), // purple
        new(0.05f, 0.05f, 0.05f), // black
        new(0.90f, 0.90f, 0.90f), // white
        new(0.85f, 0.45f, 0.10f), // orange
    ];

    // ── Setup ─────────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        FriendsConfig.Load();
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
        vbox.SetOffset(Side.Left,   -225f);
        vbox.SetOffset(Side.Right,   225f);
        vbox.SetOffset(Side.Top,    -320f);
        vbox.SetOffset(Side.Bottom,  320f);
        vbox.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(vbox);

        var title = new Label { Text = "MINDANI", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 68);
        title.AddThemeColorOverride("font_color", new Color(0.85f, 1.0f, 0.35f));
        vbox.AddChild(title);

        var sub = new Label { Text = "An adventure with Lindani", HorizontalAlignment = HorizontalAlignment.Center };
        sub.AddThemeFontSizeOverride("font_size", 17);
        sub.AddThemeColorOverride("font_color", new Color(0.70f, 0.88f, 0.70f));
        vbox.AddChild(sub);

        vbox.AddChild(Spacer(18));

        var friendsLabel = new Label { Text = "Who is coming?", HorizontalAlignment = HorizontalAlignment.Center };
        friendsLabel.AddThemeFontSizeOverride("font_size", 17);
        friendsLabel.AddThemeColorOverride("font_color", new Color(0.80f, 0.95f, 0.80f));
        vbox.AddChild(friendsLabel);

        vbox.AddChild(Spacer(6));

        var friendsVBox = new VBoxContainer();
        friendsVBox.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(friendsVBox);

        foreach (var def in FriendsConfig.All)
        {
            var captured = def;

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            friendsVBox.AddChild(row);

            var toggleBtn = MakeFriendBtn(captured);
            toggleBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            toggleBtn.Pressed += () =>
            {
                captured.Enabled = !captured.Enabled;
                StyleFriendBtn(toggleBtn, captured);
            };
            _friendBtns[def.Name] = toggleBtn;
            row.AddChild(toggleBtn);

            var editBtn = new Button { Text = "Edit" };
            editBtn.CustomMinimumSize = new Vector2(58, 42);
            editBtn.AddThemeFontSizeOverride("font_size", 13);
            editBtn.AddThemeColorOverride("font_color", Colors.White);
            var es = new StyleBoxFlat { BgColor = new Color(0.22f, 0.22f, 0.38f) };
            es.CornerRadiusTopLeft = es.CornerRadiusTopRight =
                es.CornerRadiusBottomLeft = es.CornerRadiusBottomRight = 6;
            es.ContentMarginLeft = es.ContentMarginRight = 6;
            es.ContentMarginTop  = es.ContentMarginBottom = 4;
            editBtn.AddThemeStyleboxOverride("normal", es);
            var eh = (StyleBoxFlat)es.Duplicate();
            eh.BgColor = new Color(0.34f, 0.34f, 0.55f);
            editBtn.AddThemeStyleboxOverride("hover", eh);
            editBtn.Pressed += () => OpenEditor(captured);
            row.AddChild(editBtn);
        }

        vbox.AddChild(Spacer(18));

        var play = MakeBtn("PLAY", new Color(0.12f, 0.50f, 0.12f));
        play.Pressed += () =>
        {
            FriendsConfig.Save();
            GetTree().ChangeSceneToFile("res://scenes/main.tscn");
        };
        vbox.AddChild(play);

        vbox.AddChild(Spacer(10));

        var exit = MakeBtn("EXIT", new Color(0.48f, 0.10f, 0.10f));
        exit.Pressed += () => GetTree().Quit();
        vbox.AddChild(exit);

        BuildEditOverlay();
    }

    // ── Edit overlay ──────────────────────────────────────────────────────────

    void BuildEditOverlay()
    {
        _editOverlay = new Control();
        _editOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        _editOverlay.Visible = false;
        AddChild(_editOverlay);

        var dim = new ColorRect();
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.Color = new Color(0f, 0f, 0f, 0.75f);
        _editOverlay.AddChild(dim);

        var popup = new PanelContainer();
        popup.SetAnchorsPreset(LayoutPreset.Center);
        popup.GrowHorizontal = GrowDirection.Both;
        popup.GrowVertical   = GrowDirection.Both;
        popup.SetOffset(Side.Left,   -230f);
        popup.SetOffset(Side.Right,   230f);
        popup.SetOffset(Side.Top,    -300f);
        popup.SetOffset(Side.Bottom,  300f);
        _editOverlay.AddChild(popup);

        var scroll = new ScrollContainer();
        popup.AddChild(scroll);

        _editContent = new VBoxContainer();
        _editContent.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_editContent);
    }

    void OpenEditor(FriendsConfig.Def def)
    {
        foreach (var child in _editContent!.GetChildren())
            child.QueueFree();

        Action refresh = () => _portrait?.Refresh(def);

        // Title
        var title = new Label { Text = "Customise " + def.Name, HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 21);
        title.AddThemeColorOverride("font_color", new Color(0.85f, 1.0f, 0.35f));
        _editContent.AddChild(title);

        // Portrait
        _portrait = new CompanionPortrait();
        _portrait.CustomMinimumSize   = new Vector2(120, 130);
        _portrait.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _portrait.Refresh(def);
        _editContent.AddChild(_portrait);

        _editContent.AddChild(new HSeparator());

        // Skin Colour
        _editContent.AddChild(SectionLabel("Skin Colour"));
        _editContent.AddChild(MakeColorPicker(SkinColors, () => def.BodyColor,
            c => { def.BodyColor = c; refresh(); }));

        _editContent.AddChild(Spacer(2));

        // Head Shape
        _editContent.AddChild(SectionLabel("Head Shape"));
        _editContent.AddChild(MakeStringPicker(
            ["round",  "square"],
            ["Round",  "Square"],
            () => def.HeadShape, v => { def.HeadShape = v; refresh(); }));

        _editContent.AddChild(Spacer(2));

        // Hair Style — 7 options in a 4-column grid
        _editContent.AddChild(SectionLabel("Hair Style"));
        _editContent.AddChild(MakeStringPicker(
            ["none",  "short", "afro",  "spiky",  "long",  "braids", "mohawk"],
            ["None",  "Short", "Afro",  "Spiky",  "Long",  "Braids", "Mohawk"],
            () => def.HairStyle, v => { def.HairStyle = v; refresh(); },
            cols: 4, btnW: 80f));

        _editContent.AddChild(Spacer(2));

        // Hair Colour
        _editContent.AddChild(SectionLabel("Hair Colour"));
        _editContent.AddChild(MakeColorPicker(HairColors, () => def.HairColor,
            c => { def.HairColor = c; refresh(); }));

        _editContent.AddChild(Spacer(2));

        // Eye Colour
        _editContent.AddChild(SectionLabel("Eye Colour"));
        _editContent.AddChild(MakeColorPicker(EyeColors, () => def.EyeColor,
            c => { def.EyeColor = c; refresh(); }));

        _editContent.AddChild(Spacer(2));

        // Shirt Colour
        _editContent.AddChild(SectionLabel("Shirt Colour"));
        _editContent.AddChild(MakeColorPicker(ShirtColors, () => def.ShirtColor,
            c => { def.ShirtColor = c; refresh(); }));

        _editContent.AddChild(Spacer(2));

        // Pants Colour
        _editContent.AddChild(SectionLabel("Pants Colour"));
        _editContent.AddChild(MakeColorPicker(PantsColors, () => def.PantsColor,
            c => { def.PantsColor = c; refresh(); }));

        _editContent.AddChild(Spacer(2));

        // Shoe Colour
        _editContent.AddChild(SectionLabel("Shoe Colour"));
        _editContent.AddChild(MakeColorPicker(ShoeColors, () => def.ShoeColor,
            c => { def.ShoeColor = c; refresh(); }));

        _editContent.AddChild(Spacer(2));

        // Accessory
        _editContent.AddChild(SectionLabel("Accessory"));
        _editContent.AddChild(MakeStringPicker(
            ["none",  "cap",  "glasses", "bow"],
            ["None",  "Cap",  "Glasses", "Bow"],
            () => def.Accessory, v => { def.Accessory = v; refresh(); }));

        _editContent.AddChild(Spacer(2));

        // Accessory Colour
        _editContent.AddChild(SectionLabel("Accessory Colour"));
        _editContent.AddChild(MakeColorPicker(AccessoryColors, () => def.AccessoryColor,
            c => { def.AccessoryColor = c; refresh(); }));

        _editContent.AddChild(Spacer(4));

        var done = MakeBtn("Done", new Color(0.12f, 0.44f, 0.12f));
        done.Pressed += () =>
        {
            FriendsConfig.Save();
            _editOverlay!.Visible = false;
            if (_friendBtns.TryGetValue(def.Name, out var btn))
                StyleFriendBtn(btn, def);
        };
        _editContent.AddChild(done);

        _editOverlay!.Visible = true;
    }

    // ── Picker widgets ────────────────────────────────────────────────────────

    static Label SectionLabel(string text)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 14);
        lbl.AddThemeColorOverride("font_color", new Color(0.75f, 0.92f, 0.75f));
        return lbl;
    }

    static HBoxContainer MakeColorPicker(Color[] palette, Func<Color> getCurrent, Action<Color> onPick)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 5);
        var swatches = new Button[palette.Length];

        for (int i = 0; i < palette.Length; i++)
        {
            int idx = i;
            var sw = new Button();
            sw.CustomMinimumSize = new Vector2(40, 40);
            sw.Pressed += () =>
            {
                onPick(palette[idx]);
                RefreshSwatches(swatches, palette, getCurrent());
            };
            swatches[i] = sw;
            hbox.AddChild(sw);
        }

        RefreshSwatches(swatches, palette, getCurrent());
        return hbox;
    }

    static void RefreshSwatches(Button[] swatches, Color[] palette, Color current)
    {
        for (int i = 0; i < swatches.Length; i++)
        {
            bool sel = ColorClose(palette[i], current);
            var s = new StyleBoxFlat { BgColor = palette[i] };
            s.CornerRadiusTopLeft = s.CornerRadiusTopRight =
                s.CornerRadiusBottomLeft = s.CornerRadiusBottomRight = 5;
            if (sel)
            {
                s.BorderWidthTop = s.BorderWidthBottom =
                    s.BorderWidthLeft = s.BorderWidthRight = 3;
                s.BorderColor = Colors.White;
            }
            swatches[i].AddThemeStyleboxOverride("normal", s);
            var h = (StyleBoxFlat)s.Duplicate();
            h.BgColor = palette[i].Lightened(0.20f);
            swatches[i].AddThemeStyleboxOverride("hover", h);
        }
    }

    // Generic string-option picker (replaces old MakeStylePicker).
    // cols=0 → single HBoxContainer row; cols>0 → GridContainer.
    static Control MakeStringPicker(
        string[] options, string[] labels,
        Func<string> getCurrent, Action<string> onSet,
        int cols = 0, float btnW = 88f, float btnH = 42f)
    {
        var btns = new Button[options.Length];

        Container container;
        if (cols > 0)
        {
            var grid = new GridContainer { Columns = cols };
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 6);
            container = grid;
        }
        else
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 6);
            container = hbox;
        }

        for (int i = 0; i < options.Length; i++)
        {
            int idx = i;
            var btn = new Button { Text = labels[idx] };
            btn.CustomMinimumSize = new Vector2(btnW, btnH);
            btn.AddThemeFontSizeOverride("font_size", 14);
            btn.AddThemeColorOverride("font_color", Colors.White);
            btn.Pressed += () =>
            {
                onSet(options[idx]);
                RefreshOptionBtns(btns, options, getCurrent());
            };
            btns[i] = btn;
            container.AddChild(btn);
        }

        RefreshOptionBtns(btns, options, getCurrent());
        return container;
    }

    static void RefreshOptionBtns(Button[] btns, string[] options, string current)
    {
        for (int i = 0; i < btns.Length; i++)
        {
            bool sel = options[i] == current;
            var bg   = sel ? new Color(0.22f, 0.52f, 0.22f) : new Color(0.22f, 0.22f, 0.22f);
            var s    = new StyleBoxFlat { BgColor = bg };
            s.CornerRadiusTopLeft = s.CornerRadiusTopRight =
                s.CornerRadiusBottomLeft = s.CornerRadiusBottomRight = 5;
            if (sel)
            {
                s.BorderWidthTop = s.BorderWidthBottom =
                    s.BorderWidthLeft = s.BorderWidthRight = 2;
                s.BorderColor = Colors.White;
            }
            btns[i].AddThemeStyleboxOverride("normal", s);
            var h = (StyleBoxFlat)s.Duplicate();
            h.BgColor = bg.Lightened(0.18f);
            btns[i].AddThemeStyleboxOverride("hover", h);
        }
    }

    // ── Friend card ───────────────────────────────────────────────────────────

    static Button MakeFriendBtn(FriendsConfig.Def def)
    {
        var btn = new Button();
        btn.CustomMinimumSize = new Vector2(0, 42);
        btn.AddThemeFontSizeOverride("font_size", 15);
        StyleFriendBtn(btn, def);
        return btn;
    }

    static void StyleFriendBtn(Button btn, FriendsConfig.Def def)
    {
        // Use ShirtColor for the card so each friend's distinctive colour shows
        var col     = def.Enabled ? def.ShirtColor.Darkened(0.10f) : new Color(0.18f, 0.18f, 0.18f);
        var hovered = def.Enabled ? def.ShirtColor.Lightened(0.10f) : new Color(0.28f, 0.28f, 0.28f);
        btn.Text = (def.Enabled ? "* " : "   ") + def.Name;
        btn.AddThemeColorOverride("font_color", def.Enabled ? Colors.White : new Color(0.50f, 0.50f, 0.50f));

        var s = MakeStyle(col);
        btn.AddThemeStyleboxOverride("normal", s);
        var h = MakeStyle(hovered);
        btn.AddThemeStyleboxOverride("hover", h);
        var p = MakeStyle(col.Darkened(0.15f));
        btn.AddThemeStyleboxOverride("pressed", p);
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    static bool ColorClose(Color a, Color b) =>
        Mathf.Abs(a.R - b.R) < 0.02f &&
        Mathf.Abs(a.G - b.G) < 0.02f &&
        Mathf.Abs(a.B - b.B) < 0.02f;

    static Control Spacer(float h) => new Control { CustomMinimumSize = new Vector2(0, h) };

    static Button MakeBtn(string text, Color bg)
    {
        var btn = new Button { Text = text };
        btn.CustomMinimumSize = new Vector2(290, 54);
        btn.AddThemeFontSizeOverride("font_size", 22);
        btn.AddThemeColorOverride("font_color", Colors.White);

        var s = MakeStyle(bg);
        btn.AddThemeStyleboxOverride("normal", s);
        var hover = MakeStyle(bg.Lightened(0.2f));
        btn.AddThemeStyleboxOverride("hover", hover);
        var pressed = MakeStyle(bg.Darkened(0.15f));
        btn.AddThemeStyleboxOverride("pressed", pressed);

        return btn;
    }

    static StyleBoxFlat MakeStyle(Color bg)
    {
        var s = new StyleBoxFlat { BgColor = bg };
        s.CornerRadiusTopLeft = s.CornerRadiusTopRight =
            s.CornerRadiusBottomLeft = s.CornerRadiusBottomRight = 7;
        s.ContentMarginLeft   = 16;
        s.ContentMarginRight  = 16;
        s.ContentMarginTop    = 8;
        s.ContentMarginBottom = 8;
        return s;
    }
}
