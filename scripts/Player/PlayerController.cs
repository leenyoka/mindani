using Godot;

namespace Mindani.Player;

public partial class PlayerController : CharacterBody3D
{
    [Export] public float MoveSpeed    = 5f;
    [Export] public float SprintSpeed  = 9f;
    [Export] public float JumpStrength = 7f;
    [Export] public float Sensitivity  = 0.002f;

    const float Gravity = -20f;

    Camera3D _camera = null!;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("CameraArm/Camera3D");
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-motion.Relative.X * Sensitivity);
            var arm = _camera.GetParent<Node3D>();
            arm.RotateX(-motion.Relative.Y * Sensitivity);
            arm.Rotation = new Vector3(
                Mathf.Clamp(arm.Rotation.X, -1.4f, 1.4f),
                arm.Rotation.Y,
                0f
            );
        }

        if (e is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 vel = Velocity;

        if (!IsOnFloor())
            vel.Y += Gravity * (float)delta;

        if (Input.IsActionJustPressed("jump") && IsOnFloor())
            vel.Y = JumpStrength;

        float speed = Input.IsActionPressed("sprint") ? SprintSpeed : MoveSpeed;
        Vector2 dir2D = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        Vector3 dir = (Transform.Basis * new Vector3(dir2D.X, 0, dir2D.Y)).Normalized();
        vel.X = dir.X * speed;
        vel.Z = dir.Z * speed;

        Velocity = vel;
        MoveAndSlide();
    }
}
