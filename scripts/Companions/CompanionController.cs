using Godot;

namespace Mindani.Companions;

public partial class CompanionController : CharacterBody3D
{
    [Export] public string   CompanionName = "Companion";
    [Export] public Color    BodyColor     = Colors.Orange;
    [Export] public string[] Dialogue      = [];

    const float FollowDist = 7f;
    const float StopDist   = 2.5f;
    const float MoveSpeed  = 4.5f;
    const float Gravity    = 20f;

    CharacterBody3D? _player;
    Label3D          _bubble = null!;

    double _nextSay;
    static readonly System.Random _rng = new();

    public override void _Ready()
    {
        BuildVisuals();
        _player  = GetNodeOrNull<CharacterBody3D>("/root/Main/Lindani");
        _nextSay = _rng.NextDouble() * 4 + 2;
        AddToGroup("companion");
    }

    void BuildVisuals()
    {
        var skin = BodyColor.Lightened(0.45f);
        var dark = BodyColor.Darkened(0.25f);
        var face = Colors.Black.Lightened(0.2f);

        // Head
        Box(new Vector3(0,     1.55f,  0),    new Vector3(0.50f, 0.50f, 0.50f), skin);
        // Eyes (on front face = -Z side)
        Box(new Vector3(-0.12f, 1.62f, -0.26f), new Vector3(0.10f, 0.10f, 0.02f), face);
        Box(new Vector3( 0.12f, 1.62f, -0.26f), new Vector3(0.10f, 0.10f, 0.02f), face);
        // Mouth
        Box(new Vector3(0,     1.44f, -0.26f), new Vector3(0.20f, 0.05f, 0.02f), face);

        // Torso
        Box(new Vector3(0,     0.93f,  0),    new Vector3(0.52f, 0.60f, 0.28f), BodyColor);

        // Arms
        Box(new Vector3(-0.37f, 0.90f, 0),    new Vector3(0.18f, 0.58f, 0.18f), skin);
        Box(new Vector3( 0.37f, 0.90f, 0),    new Vector3(0.18f, 0.58f, 0.18f), skin);

        // Legs
        Box(new Vector3(-0.13f, 0.32f, 0),    new Vector3(0.22f, 0.62f, 0.22f), dark);
        Box(new Vector3( 0.13f, 0.32f, 0),    new Vector3(0.22f, 0.62f, 0.22f), dark);

        // Name tag
        AddChild(new Label3D
        {
            Text        = CompanionName,
            FontSize    = 22,
            Position    = new Vector3(0, 2.25f, 0),
            NoDepthTest = true,
            Billboard   = BaseMaterial3D.BillboardModeEnum.Enabled,
        });

        // Speech bubble
        _bubble = new Label3D
        {
            Text        = "",
            FontSize    = 18,
            Position    = new Vector3(0, 2.7f, 0),
            NoDepthTest = true,
            Billboard   = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate    = new Color(1f, 0.95f, 0.4f),
        };
        AddChild(_bubble);
    }

    void Box(Vector3 pos, Vector3 size, Color color)
    {
        var mat = new StandardMaterial3D { AlbedoColor = color };
        var mi  = new MeshInstance3D
        {
            Mesh     = new BoxMesh { Size = size, Material = mat },
            Position = pos,
        };
        AddChild(mi);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null) return;

        var vel = Velocity;

        // Gravity
        if (!IsOnFloor()) vel.Y -= Gravity * (float)delta;
        else              vel.Y  = 0f;

        // Horizontal: follow player if too far away
        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0f;
        float horizDist = toPlayer.Length();

        if (horizDist > FollowDist)
        {
            var dir = toPlayer.Normalized();
            vel.X = dir.X * MoveSpeed;
            vel.Z = dir.Z * MoveSpeed;
        }
        else if (horizDist < StopDist)
        {
            vel.X = 0f;
            vel.Z = 0f;
        }
        else
        {
            var dir = toPlayer.Normalized();
            float t = (horizDist - StopDist) / (FollowDist - StopDist);
            vel.X = dir.X * MoveSpeed * t;
            vel.Z = dir.Z * MoveSpeed * t;
        }

        Velocity = vel;
        MoveAndSlide();

        // Face the player
        if (horizDist > 0.1f)
        {
            var flat = new Vector3(_player.GlobalPosition.X, GlobalPosition.Y, _player.GlobalPosition.Z);
            LookAt(flat, Vector3.Up);
        }

        // Periodic dialogue
        _nextSay -= delta;
        if (_nextSay <= 0 && Dialogue.Length > 0)
        {
            Say(Dialogue[_rng.Next(Dialogue.Length)]);
            _nextSay = _rng.NextDouble() * 8 + 5;
        }
    }

    void Say(string line)
    {
        _bubble.Text = $"\"{line}\"";
        GetTree().CreateTimer(4.5).Timeout += () =>
        {
            if (IsInstanceValid(this)) _bubble.Text = "";
        };
    }
}
