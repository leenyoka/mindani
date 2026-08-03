using Godot;
using Mindani.UI;

namespace Mindani.Player;

public partial class PlayerController : CharacterBody3D
{
    [Export] public float MoveSpeed      = 5f;
    [Export] public float SprintSpeed    = 9f;
    [Export] public float JumpStrength   = 7f;
    [Export] public float Sensitivity    = 0.002f;
    [Export] public float TouchSensitivity = 0.004f;

    const float Gravity = -20f;

    Camera3D _camera    = null!;
    Node3D   _cameraArm = null!;
    bool _isTouch;
    bool _touchInit;

    public override void _Ready()
    {
        _camera    = GetNode<Camera3D>("CameraArm/Camera3D");
        _cameraArm = GetNode<Node3D>("CameraArm");
        // _isTouch is set on the first physics frame so TouchControls._Ready
        // (which runs later in the scene tree) has time to set IsActive first.
    }

    public override void _Input(InputEvent e)
    {
        // Mouse look — desktop only
        if (!_isTouch && e is InputEventMouseMotion motion
                      && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            ApplyLook(motion.Relative * Sensitivity);
        }

        // Re-capture the mouse on left-click when it was released (e.g. after ESC or
        // losing focus) — but not while the chest inventory is open.
        if (!_isTouch && e is InputEventMouseButton mb
                      && mb.ButtonIndex == MouseButton.Left && mb.Pressed
                      && Input.MouseMode != Input.MouseModeEnum.Captured
                      && !PauseMenu.AnyOverlayOpen)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_touchInit)
        {
            _touchInit = true;
            _isTouch   = TouchControls.IsActive;
            if (!_isTouch)
                Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        // Touch look — apply accumulated drag delta
        if (_isTouch)
        {
            var td = TouchControls.ConsumeLookDelta();
            if (td != Vector2.Zero)
                ApplyLook(td * TouchSensitivity);
        }

        Vector3 vel = Velocity;

        if (!IsOnFloor())
            vel.Y += Gravity * (float)delta;

        if (Input.IsActionJustPressed("jump") && IsOnFloor())
            vel.Y = JumpStrength;

        float speed = Input.IsActionPressed("sprint") ? SprintSpeed : MoveSpeed;

        // Keyboard first; fall back to touch joystick
        Vector2 dir2D = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        if (dir2D == Vector2.Zero && _isTouch)
            dir2D = TouchControls.JoystickDir;

        Vector3 dir = (Transform.Basis * new Vector3(dir2D.X, 0, dir2D.Y)).Normalized();
        vel.X = dir.X * speed;
        vel.Z = dir.Z * speed;

        Velocity = vel;
        MoveAndSlide();
    }

    void ApplyLook(Vector2 delta)
    {
        RotateY(-delta.X);
        _cameraArm.RotateX(-delta.Y);
        _cameraArm.Rotation = new Vector3(
            Mathf.Clamp(_cameraArm.Rotation.X, -1.4f, 1.4f),
            _cameraArm.Rotation.Y,
            0f
        );
    }
}
