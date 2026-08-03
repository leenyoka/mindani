using Godot;

namespace Mindani.UI;

// Full-screen touch overlay: fixed D-pad + dynamic joystick (left), swipe-to-look (right), action buttons.
// Only activates on touchscreen devices; invisible and inactive on desktop.
public partial class TouchControls : Control
{
    // Tick this in the Godot Inspector to preview controls on desktop
    [Export] public bool ForceShow = false;

    // True once _Ready confirms we're on a touchscreen or ForceShow is on.
    // PlayerController reads this to decide whether to capture the mouse.
    public static bool IsActive { get; private set; } = false;

    // Read by PlayerController for analog movement and camera look
    public static Vector2 JoystickDir { get; private set; } = Vector2.Zero;

    static Vector2 _lookAccum = Vector2.Zero;
    public static Vector2 ConsumeLookDelta()
    {
        var v = _lookAccum;
        _lookAccum = Vector2.Zero;
        return v;
    }

    // Layout
    const float JoyRadius   = 110f;
    const float ThumbRadius = 48f;
    const float BtnRadius   = 65f;
    const float ChestR      = 55f;
    const float DpadArm     = 88f;   // arm length from centre to tip
    const float DpadHalfW   = 22f;   // half-width of each arm

    // One tracked finger per control zone
    int     _joyFinger    = -1;
    int     _lookFinger   = -1;
    int     _dpadFinger   = -1;
    int     _jumpFinger   = -1;
    int     _breakFinger  = -1;
    int     _placeFinger  = -1;
    int     _chestFinger  = -1;
    int     _pauseFinger  = -1;
    Vector2 _joyCenter    = Vector2.Zero;
    DpadDir _dpadActive   = DpadDir.None;

    Vector2 _dpadCenter;
    Vector2 _jumpPos, _breakPos, _placePos, _chestPos, _pausePos;

    const float PauseR = 38f;

    static readonly Color ColDpad       = new(1, 1, 1, 0.28f);
    static readonly Color ColDpadActive = new(1, 1, 1, 0.65f);
    static readonly Color ColJoyBase    = new(1, 1, 1, 0.14f);
    static readonly Color ColJoyRing    = new(1, 1, 1, 0.38f);
    static readonly Color ColJoyThumb   = new(1, 1, 1, 0.52f);
    static readonly Color ColJump       = new(0.20f, 0.85f, 0.20f, 0.60f);
    static readonly Color ColBreak      = new(0.90f, 0.35f, 0.10f, 0.60f);
    static readonly Color ColPlace      = new(0.20f, 0.50f, 0.90f, 0.60f);
    static readonly Color ColChest      = new(0.55f, 0.32f, 0.08f, 0.70f);
    static readonly Color ColBtnRing    = new(1, 1, 1, 0.70f);
    static readonly Color ColCrosshair  = new(1, 1, 1, 0.80f);
    static readonly Color ColPause      = new(0.20f, 0.20f, 0.20f, 0.70f);

    enum DpadDir { None, Up, Down, Left, Right }

    bool _layoutReady = false;

    public override void _Ready()
    {
        bool touch = DisplayServer.IsTouchscreenAvailable() || ForceShow;
        IsActive = touch;
        if (!touch)
        {
            Visible = false;
            SetProcess(false);
            SetProcessInput(false);
            return;
        }
        // Remove the mouse-button bindings from break_block / place_block so that
        // tapping anywhere on screen doesn't accidentally dig or place blocks.
        // TouchControls fires these actions via explicit InputEventAction only.
        // We leave EmulateMouseFromTouch ON so that normal UI Buttons still work.
        foreach (var action in new[] { "break_block", "place_block" })
        {
            var toRemove = new System.Collections.Generic.List<InputEvent>();
            foreach (var ev in InputMap.ActionGetEvents(action))
                if (ev is InputEventMouseButton)
                    toRemove.Add(ev);
            foreach (var ev in toRemove)
                InputMap.ActionEraseEvent(action, ev);
        }

        // Stay active when the game is paused so the pause button can un-pause.
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        // Do NOT call LayoutButtons() here — the OS window size is not settled yet
        // and GetVisibleRect() returns the project base resolution (1280×720), not
        // the device's actual pixel size. Positions are calculated on the first frame.
    }

    // Recalculate when the viewport is resized (e.g. screen rotation).
    public override void _Notification(int what)
    {
        if (what == NotificationResized && _layoutReady)
            LayoutButtons();
    }

    void LayoutButtons()
    {
        // Use the Control's own viewport rect — this is correct after the OS window
        // is set up, and matches the coordinate space that InputEventScreenTouch uses.
        var s = GetViewportRect().Size;
        _dpadCenter = new Vector2(200f, s.Y - 220f);
        _jumpPos    = new Vector2(s.X - 110f, s.Y - 120f);
        _breakPos   = new Vector2(s.X - 255f, s.Y - 120f);
        _placePos   = new Vector2(s.X - 110f, s.Y - 265f);
        _chestPos   = new Vector2(s.X - 255f, s.Y - 265f);
        _pausePos   = new Vector2(s.X - 55f,  55f);
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventScreenTouch t) HandleTouch(t);
        if (e is InputEventScreenDrag  d) HandleDrag(d);
    }

    void HandleTouch(InputEventScreenTouch e)
    {
        var p = e.Position;

        if (e.Pressed)
        {
            // Pause button — top-right corner, works even when paused
            if (IsNear(p, _pausePos, PauseR) && _pauseFinger == -1)
            {
                _pauseFinger = e.Index;
                Fire("ui_cancel", true);
                Fire("ui_cancel", false);
                return;
            }

            // Action buttons take top priority
            if (IsNear(p, _jumpPos,  BtnRadius) && _jumpFinger  == -1) { _jumpFinger  = e.Index; Fire("jump",        true); return; }
            if (IsNear(p, _breakPos, BtnRadius) && _breakFinger == -1) { _breakFinger = e.Index; Fire("break_block", true); return; }
            if (IsNear(p, _placePos, BtnRadius) && _placeFinger == -1) { _placeFinger = e.Index; Fire("place_block", true); return; }
            if (IsNear(p, _chestPos, ChestR)    && _chestFinger == -1) { _chestFinger = e.Index; PauseMenu.Instance?.OpenChest(); GetViewport().SetInputAsHandled(); return; }

            // D-pad
            var dir = HitDpad(p);
            if (dir != DpadDir.None && _dpadFinger == -1)
            {
                _dpadFinger = e.Index;
                SetDpad(dir);
                return;
            }

            // Dynamic joystick: left half, outside D-pad zone
            float mid = GetViewportRect().Size.X * 0.45f;
            bool inDpadArea = p.DistanceTo(_dpadCenter) <= DpadArm + 30f;
            if (p.X < mid && !inDpadArea && _joyFinger == -1)
            {
                _joyFinger = e.Index;
                _joyCenter = p;
                return;
            }

            // Swipe-to-look: right half
            if (p.X >= mid && _lookFinger == -1)
                _lookFinger = e.Index;
        }
        else
        {
            // Release each control only when its own finger lifts
            if (e.Index == _jumpFinger)  { _jumpFinger  = -1; Fire("jump",        false); }
            if (e.Index == _breakFinger) { _breakFinger = -1; Fire("break_block", false); }
            if (e.Index == _placeFinger) { _placeFinger = -1; Fire("place_block", false); }
            if (e.Index == _chestFinger)   _chestFinger = -1;
            if (e.Index == _pauseFinger)   _pauseFinger = -1;
            if (e.Index == _dpadFinger)   { _dpadFinger   = -1; SetDpad(DpadDir.None); }
            if (e.Index == _joyFinger)    { _joyFinger    = -1; JoystickDir = Vector2.Zero; }
            if (e.Index == _lookFinger)     _lookFinger   = -1;
        }

        QueueRedraw();
    }

    void HandleDrag(InputEventScreenDrag e)
    {
        if (e.Index == _dpadFinger)
        {
            SetDpad(HitDpad(e.Position));
            QueueRedraw();
        }
        else if (e.Index == _joyFinger)
        {
            var off = e.Position - _joyCenter;
            if (off.Length() > JoyRadius) off = off.Normalized() * JoyRadius;
            JoystickDir = off / JoyRadius;
            QueueRedraw();
        }
        else if (e.Index == _lookFinger)
        {
            _lookAccum += e.Relative;
        }
    }

    // Returns which D-pad arm contains point p, or None if outside.
    DpadDir HitDpad(Vector2 p)
    {
        var  rel = p - _dpadCenter;
        float ax = Mathf.Abs(rel.X);
        float ay = Mathf.Abs(rel.Y);
        // Finger must land inside the cross shape (with a small slop for usability)
        bool inCross = (ax <= DpadHalfW + 8f && ay <= DpadArm) ||
                       (ay <= DpadHalfW + 8f && ax <= DpadArm);
        if (!inCross) return DpadDir.None;
        return ay >= ax
            ? (rel.Y <= 0 ? DpadDir.Up   : DpadDir.Down)
            : (rel.X <  0 ? DpadDir.Left : DpadDir.Right);
    }

    // Fires move actions and updates the highlighted arm.
    void SetDpad(DpadDir dir)
    {
        if (_dpadActive == dir) return;

        if (_dpadActive == DpadDir.Up)    Fire("move_forward",  false);
        if (_dpadActive == DpadDir.Down)  Fire("move_backward", false);
        if (_dpadActive == DpadDir.Left)  Fire("move_left",     false);
        if (_dpadActive == DpadDir.Right) Fire("move_right",    false);

        _dpadActive = dir;

        if (dir == DpadDir.Up)    Fire("move_forward",  true);
        if (dir == DpadDir.Down)  Fire("move_backward", true);
        if (dir == DpadDir.Left)  Fire("move_left",     true);
        if (dir == DpadDir.Right) Fire("move_right",    true);

        QueueRedraw();
    }

    static void Fire(string action, bool pressed) =>
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = pressed, Strength = pressed ? 1f : 0f });

    static bool IsNear(Vector2 p, Vector2 c, float r) => p.DistanceTo(c) <= r;

    public override void _Process(double _)
    {
        if (!_layoutReady)
        {
            LayoutButtons();
            _layoutReady = true;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var s = GetRect().Size;

        // Crosshair
        const float Ch = 14f;
        DrawLine(new Vector2(s.X / 2 - Ch, s.Y / 2), new Vector2(s.X / 2 + Ch, s.Y / 2), ColCrosshair, 2f);
        DrawLine(new Vector2(s.X / 2, s.Y / 2 - Ch), new Vector2(s.X / 2, s.Y / 2 + Ch), ColCrosshair, 2f);

        // D-pad (always visible, highlights active arm)
        DrawDpad();

        // Dynamic joystick (only when a finger is on it)
        if (_joyFinger != -1)
        {
            DrawCircle(_joyCenter, JoyRadius, ColJoyBase);
            DrawArc(_joyCenter, JoyRadius, 0, Mathf.Tau, 64, ColJoyRing, 3f);
            DrawCircle(_joyCenter + JoystickDir * JoyRadius, ThumbRadius, ColJoyThumb);
        }

        // Action buttons — 2x2 grid, bottom-right
        //   Sprint  Place
        //   Break   Jump
        DrawCircle(_jumpPos, BtnRadius, ColJump);
        DrawArc(_jumpPos, BtnRadius, 0, Mathf.Tau, 48, ColBtnRing, 2.5f);
        DrawChevron(_jumpPos, 24f, Colors.White, Vector2.Up);

        DrawCircle(_breakPos, BtnRadius, ColBreak);
        DrawArc(_breakPos, BtnRadius, 0, Mathf.Tau, 48, ColBtnRing, 2.5f);
        DrawXIcon(_breakPos, 20f, Colors.White);

        DrawCircle(_placePos, BtnRadius, ColPlace);
        DrawArc(_placePos, BtnRadius, 0, Mathf.Tau, 48, ColBtnRing, 2.5f);
        DrawPlusIcon(_placePos, 20f, Colors.White);

        DrawChestButton(_chestPos, ChestR);

        // Pause button — top-right, three horizontal bars
        DrawCircle(_pausePos, PauseR, ColPause);
        DrawArc(_pausePos, PauseR, 0, Mathf.Tau, 48, ColBtnRing, 2f);
        for (int bar = -1; bar <= 1; bar++)
        {
            var lc = _pausePos + new Vector2(0, bar * 10f);
            DrawLine(lc - new Vector2(13f, 0), lc + new Vector2(13f, 0), Colors.White, 3f);
        }
    }

    void DrawDpad()
    {
        var c = _dpadCenter;
        float a  = DpadArm;
        float hw = DpadHalfW;

        Color U = _dpadActive == DpadDir.Up    ? ColDpadActive : ColDpad;
        Color D = _dpadActive == DpadDir.Down  ? ColDpadActive : ColDpad;
        Color L = _dpadActive == DpadDir.Left  ? ColDpadActive : ColDpad;
        Color R = _dpadActive == DpadDir.Right ? ColDpadActive : ColDpad;

        // Cross shape: centre square + four arms
        DrawRect(new Rect2(c.X - hw, c.Y - hw,  hw * 2, hw * 2), ColDpad);  // centre
        DrawRect(new Rect2(c.X - hw, c.Y - a,   hw * 2, a - hw), U);        // up arm
        DrawRect(new Rect2(c.X - hw, c.Y + hw,  hw * 2, a - hw), D);        // down arm
        DrawRect(new Rect2(c.X - a,  c.Y - hw,  a - hw, hw * 2), L);        // left arm
        DrawRect(new Rect2(c.X + hw, c.Y - hw,  a - hw, hw * 2), R);        // right arm

        // Chevron icon near the tip of each arm
        float tip = a * 0.60f;
        DrawChevron(new Vector2(c.X,       c.Y - tip), 11f, Colors.White, Vector2.Up);
        DrawChevron(new Vector2(c.X,       c.Y + tip), 11f, Colors.White, Vector2.Down);
        DrawChevron(new Vector2(c.X - tip, c.Y),       11f, Colors.White, Vector2.Left);
        DrawChevron(new Vector2(c.X + tip, c.Y),       11f, Colors.White, Vector2.Right);
    }

    // Draws a V-shaped chevron pointing in `dir` (unit vector).
    void DrawChevron(Vector2 c, float size, Color col, Vector2 dir)
    {
        var perp = new Vector2(-dir.Y, dir.X) * size;
        var back = -dir * size;
        var tip  =  dir * size * 0.5f;
        DrawLine(c + perp + back, c + tip, col, 2.5f);
        DrawLine(c - perp + back, c + tip, col, 2.5f);
    }

    void DrawXIcon(Vector2 c, float s, Color col)
    {
        DrawLine(c - new Vector2(s, s), c + new Vector2(s, s),  col, 4f);
        DrawLine(c + new Vector2(-s, s), c + new Vector2(s, -s), col, 4f);
    }

    void DrawPlusIcon(Vector2 c, float s, Color col)
    {
        DrawLine(c - new Vector2(s, 0), c + new Vector2(s, 0), col, 4f);
        DrawLine(c - new Vector2(0, s), c + new Vector2(0, s), col, 4f);
    }

    void DrawChestButton(Vector2 c, float r)
    {
        // Background circle
        DrawCircle(c, r, ColChest);
        DrawArc(c, r, 0, Mathf.Tau, 48, ColBtnRing, 2f);
        // Chest body
        float w = r * 0.65f, hb = r * 0.22f, ht = r * 0.18f;
        DrawRect(new Rect2(c.X - w, c.Y,         w * 2, hb),  new Color(0.35f, 0.18f, 0.04f, 1f));
        DrawRect(new Rect2(c.X - w, c.Y - ht,    w * 2, ht),  new Color(0.55f, 0.32f, 0.08f, 1f));
        // Lock
        DrawRect(new Rect2(c.X - r * 0.12f, c.Y - r * 0.06f, r * 0.24f, r * 0.22f), new Color(0.90f, 0.75f, 0.10f, 1f));
    }
}
