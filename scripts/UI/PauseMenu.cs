using Godot;
using Mindani;

namespace Mindani.UI;

public partial class PauseMenu : CanvasLayer
{
    public static PauseMenu? Instance { get; private set; }

    public static bool AnyOverlayOpen =>
        Instance != null && (Instance._overlay.Visible || Instance._chestOverlay.Visible);

    Control _overlay      = null!;
    Control _chestOverlay = null!;
    Button? _daytimeBtn;
    Button? _soundBtn;

    public override void _Ready()
    {
        Instance = this;
        GameSettings.Load();
        GameSettings.ApplyAudio();
        BuildPausePanel();
        BuildChestOverlay();
    }

    // ── Pause panel ───────────────────────────────────────────────────────────

    void BuildPausePanel()
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
        panel.SetOffset(Side.Top,    -285f);
        panel.SetOffset(Side.Bottom,  285f);
        _overlay.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        vbox.Alignment = BoxContainer.AlignmentMode.Center;
        panel.AddChild(vbox);

        var title = new Label { Text = "PAUSED", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 38);
        vbox.AddChild(title);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        AddBtn(vbox, "Resume",     new Color(0.12f, 0.44f, 0.12f), OnResume);

        vbox.AddChild(new HSeparator());

        AddBtn(vbox, "Pick Block", new Color(0.38f, 0.22f, 0.08f), OnPickBlock);

        vbox.AddChild(new HSeparator());

        _daytimeBtn = new Button();
        _daytimeBtn.CustomMinimumSize = new Vector2(270, 50);
        _daytimeBtn.AddThemeFontSizeOverride("font_size", 17);
        _daytimeBtn.AddThemeColorOverride("font_color", Colors.White);
        _daytimeBtn.Pressed += OnToggleDaytime;
        vbox.AddChild(_daytimeBtn);
        StyleDaytimeBtn();

        vbox.AddChild(new HSeparator());

        _soundBtn = new Button();
        _soundBtn.CustomMinimumSize = new Vector2(270, 50);
        _soundBtn.AddThemeFontSizeOverride("font_size", 17);
        _soundBtn.AddThemeColorOverride("font_color", Colors.White);
        void ToggleSound() { GameSettings.SoundMuted = !GameSettings.SoundMuted; GameSettings.Save(); GameSettings.ApplyAudio(); StyleSoundBtn(); }
        _soundBtn.Pressed += ToggleSound;
        vbox.AddChild(_soundBtn);
        StyleSoundBtn();

        vbox.AddChild(new HSeparator());

        AddBtn(vbox, "Quit to Menu", new Color(0.38f, 0.34f, 0.06f), OnMenu);
        AddBtn(vbox, "Exit Game",    new Color(0.44f, 0.09f, 0.09f), OnExit);
    }

    void OnToggleDaytime()
    {
        GameSettings.AlwaysDaytime = !GameSettings.AlwaysDaytime;
        GameSettings.Save();
        StyleDaytimeBtn();
    }

    void StyleSoundBtn()
    {
        if (_soundBtn == null) return;
        bool muted = GameSettings.SoundMuted;
        _soundBtn.Text = muted ? "Sound: OFF" : "Sound: ON";
        var bg = muted ? new Color(0.44f, 0.09f, 0.09f) : new Color(0.10f, 0.38f, 0.10f);
        var s  = MakeStyle(bg);
        _soundBtn.AddThemeStyleboxOverride("normal", s);
        var h = (StyleBoxFlat)s.Duplicate(); h.BgColor = bg.Lightened(0.2f);
        _soundBtn.AddThemeStyleboxOverride("hover", h);
    }

    void StyleDaytimeBtn()
    {
        if (_daytimeBtn == null) return;
        bool on = GameSettings.AlwaysDaytime;
        _daytimeBtn.Text = on ? "Always Daytime: ON" : "Always Daytime: OFF";
        var bg = on ? new Color(0.52f, 0.42f, 0.04f) : new Color(0.22f, 0.22f, 0.22f);
        var s  = MakeStyle(bg);
        _daytimeBtn.AddThemeStyleboxOverride("normal", s);
        var h = (StyleBoxFlat)s.Duplicate(); h.BgColor = bg.Lightened(0.2f);
        _daytimeBtn.AddThemeStyleboxOverride("hover", h);
    }

    void AddBtn(VBoxContainer parent, string text, Color bg, System.Action cb)
    {
        var btn = new Button { Text = text };
        btn.CustomMinimumSize = new Vector2(270, 50);
        btn.AddThemeFontSizeOverride("font_size", 19);
        btn.AddThemeColorOverride("font_color", Colors.White);
        var s = MakeStyle(bg);
        btn.AddThemeStyleboxOverride("normal", s);
        var h = (StyleBoxFlat)s.Duplicate(); h.BgColor = bg.Lightened(0.2f);
        btn.AddThemeStyleboxOverride("hover", h);
        btn.Pressed += cb;
        parent.AddChild(btn);
    }

    static StyleBoxFlat MakeStyle(Color bg)
    {
        var s = new StyleBoxFlat { BgColor = bg };
        s.CornerRadiusTopLeft = s.CornerRadiusTopRight =
        s.CornerRadiusBottomLeft = s.CornerRadiusBottomRight = 6;
        s.ContentMarginLeft = s.ContentMarginRight = 14;
        s.ContentMarginTop  = s.ContentMarginBottom = 8;
        return s;
    }

    // ── Chest / block-picker overlay ──────────────────────────────────────────

    void BuildChestOverlay()
    {
        _chestOverlay = new Control();
        _chestOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _chestOverlay.Visible = false;
        AddChild(_chestOverlay);

        var dim = new ColorRect();
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dim.Color     = new Color(0f, 0f, 0f, 0.65f);
        dim.GuiInput += e =>
        {
            // On touch devices the emulated MouseButton fires right after OpenChest() and
            // would instantly close the overlay — only react to the real ScreenTouch there.
            bool close = e is InputEventScreenTouch { Pressed: true }
                      || (!TouchControls.IsActive && e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true });
            if (close) CloseChest();
        };
        _chestOverlay.AddChild(dim);

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.GrowVertical   = Control.GrowDirection.Both;
        panel.SetOffset(Side.Left,   -265f);
        panel.SetOffset(Side.Right,   265f);
        panel.SetOffset(Side.Top,    -260f);
        panel.SetOffset(Side.Bottom,  260f);
        _chestOverlay.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vbox);

        var title = new Label { Text = "Pick a Block", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        vbox.AddChild(title);

        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        vbox.AddChild(grid);

        for (int i = 0; i < Hotbar.AllBlocks.Length; i++)
        {
            var (_, name, colour) = Hotbar.AllBlocks[i];
            int capture = i;
            var s = new StyleBoxFlat { BgColor = colour };
            s.CornerRadiusTopLeft = s.CornerRadiusTopRight =
            s.CornerRadiusBottomLeft = s.CornerRadiusBottomRight = 8;
            s.ContentMarginLeft = s.ContentMarginRight = 8;
            s.ContentMarginTop  = s.ContentMarginBottom = 5;
            var btn = new Button { Text = name };
            btn.CustomMinimumSize = new Vector2(90, 75);
            btn.AddThemeFontSizeOverride("font_size", 14);
            btn.AddThemeColorOverride("font_color", Colors.White);
            btn.AddThemeStyleboxOverride("normal", s);
            var h = (StyleBoxFlat)s.Duplicate(); h.BgColor = colour.Lightened(0.25f);
            btn.AddThemeStyleboxOverride("hover", h);

            void Pick() { Hotbar.Instance?.SelectBlock(capture); CloseChest(); }
            btn.Pressed += Pick;
            grid.AddChild(btn);
        }

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });

        var closeStyle = new StyleBoxFlat { BgColor = new Color(0.28f, 0.28f, 0.28f) };
        closeStyle.CornerRadiusTopLeft = closeStyle.CornerRadiusTopRight =
        closeStyle.CornerRadiusBottomLeft = closeStyle.CornerRadiusBottomRight = 6;
        closeStyle.ContentMarginLeft = closeStyle.ContentMarginRight = 14;
        closeStyle.ContentMarginTop  = closeStyle.ContentMarginBottom = 8;
        var closeBtn = new Button { Text = "Close" };
        closeBtn.CustomMinimumSize = new Vector2(150, 50);
        closeBtn.AddThemeFontSizeOverride("font_size", 17);
        closeBtn.AddThemeColorOverride("font_color", Colors.White);
        closeBtn.AddThemeStyleboxOverride("normal", closeStyle);
        var ch = (StyleBoxFlat)closeStyle.Duplicate(); ch.BgColor = new Color(0.45f, 0.45f, 0.45f);
        closeBtn.AddThemeStyleboxOverride("hover", ch);
        closeBtn.Pressed += CloseChest;

        var hcenter = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        hcenter.AddChild(closeBtn);
        vbox.AddChild(hcenter);
    }

    // Call from TouchControls or pause-menu button to show the block picker.
    public void OpenChest()
    {
        _overlay.Visible      = false;
        _chestOverlay.Visible = true;
        if (!GetTree().Paused)
        {
            GetTree().Paused = true;
            Input.MouseMode  = Input.MouseModeEnum.Visible;
        }
    }

    void CloseChest()
    {
        _chestOverlay.Visible = false;
        GetTree().Paused      = false;
        Input.MouseMode       = Input.MouseModeEnum.Captured;
    }

    // ── Input / toggle ────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel"))
            Toggle();
    }

    void Toggle()
    {
        if (_chestOverlay.Visible) { CloseChest(); return; }
        bool pausing = !GetTree().Paused;
        GetTree().Paused  = pausing;
        _overlay.Visible  = pausing;
        Input.MouseMode   = pausing
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured;
        GetViewport().SetInputAsHandled();
    }

    void OnResume()    => Toggle();
    void OnPickBlock() => OpenChest();

    void OnMenu()
    {
        GetTree().Paused = false;
        Input.MouseMode  = Input.MouseModeEnum.Visible;
        GetTree().ChangeSceneToFile("res://scenes/menu.tscn");
    }

    void OnExit() => GetTree().Quit();
}
