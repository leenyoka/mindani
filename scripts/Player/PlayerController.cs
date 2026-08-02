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

        // Larger snap keeps the player grounded when stepping off 1-block edges.
        // Without this, IsOnFloor() briefly returns false mid-step and gravity
        // creates a visible bounce/float on every block transition.
        FloorSnapLength = 0.5f;

        // Allow slightly steeper slopes (platforms are ~40°; default 45° is close)
        FloorMaxAngle = Mathf.DegToRad(52f);
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
