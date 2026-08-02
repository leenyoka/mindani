using Godot;

namespace Mindani.Animals;

public partial class AnimalController : CharacterBody3D
{
    [Export] public string AnimalKind = "Chicken";

    const float Gravity   = 20f;
    const float WalkSpeed = 1.6f;
    const float FleeSpeed = 4.5f;
    const float FleeRange = 4.5f;

    CharacterBody3D? _player;
    Vector2          _wanderDir;
    double           _wanderTime;

    static readonly System.Random _rng = new();

    public override void _Ready()
    {
        BuildVisuals();
        _player     = GetNodeOrNull<CharacterBody3D>("/root/Main/Lindani");
        _wanderDir  = RandDir();
        _wanderTime = _rng.NextDouble() * 3 + 1;
        AddToGroup("animal");
    }

    void BuildVisuals()
    {
        switch (AnimalKind)
        {
            case "Pig":   BuildPig();   break;
            case "Sheep": BuildSheep(); break;
            default:      BuildChicken(); break;
        }
    }

    void BuildChicken()
    {
        var white = new Color(0.96f, 0.96f, 0.92f);
        var yell  = new Color(1.00f, 0.65f, 0.00f);
        var red   = new Color(0.85f, 0.10f, 0.10f);

        B(new(0,      0.45f,  0),    new(0.35f, 0.30f, 0.45f), white); // body
        B(new(0,      0.72f, -0.17f), new(0.22f, 0.22f, 0.22f), white); // head
        B(new(0,      0.69f, -0.31f), new(0.08f, 0.06f, 0.06f), yell);  // beak
        B(new(0,      0.86f, -0.17f), new(0.06f, 0.09f, 0.06f), red);   // comb
        B(new(-0.08f, 0.13f,  0),    new(0.06f, 0.25f, 0.06f), yell);   // leg L
        B(new( 0.08f, 0.13f,  0),    new(0.06f, 0.25f, 0.06f), yell);   // leg R
    }

    void BuildPig()
    {
        var pink = new Color(0.96f, 0.62f, 0.67f);
        var dp   = pink.Darkened(0.12f);

        B(new(0,      0.38f,  0),    new(0.55f, 0.38f, 0.70f), pink); // body
        B(new(0,      0.55f, -0.34f), new(0.40f, 0.35f, 0.35f), pink); // head
        B(new(0,      0.50f, -0.53f), new(0.22f, 0.14f, 0.10f), dp);   // snout
        B(new(-0.20f, 0.73f, -0.30f), new(0.12f, 0.12f, 0.06f), dp);   // ear L
        B(new( 0.20f, 0.73f, -0.30f), new(0.12f, 0.12f, 0.06f), dp);   // ear R
        B(new(-0.16f, 0.13f, -0.22f), new(0.12f, 0.26f, 0.12f), dp);   // legs
        B(new( 0.16f, 0.13f, -0.22f), new(0.12f, 0.26f, 0.12f), dp);
        B(new(-0.16f, 0.13f,  0.22f), new(0.12f, 0.26f, 0.12f), dp);
        B(new( 0.16f, 0.13f,  0.22f), new(0.12f, 0.26f, 0.12f), dp);
    }

    void BuildSheep()
    {
        var wool = new Color(0.92f, 0.92f, 0.92f);
        var skin = new Color(0.58f, 0.52f, 0.46f);

        B(new(0,      0.48f,  0),    new(0.62f, 0.46f, 0.76f), wool); // wool body
        B(new(0,      0.68f, -0.38f), new(0.30f, 0.30f, 0.30f), skin); // head
        B(new(-0.16f, 0.13f, -0.22f), new(0.10f, 0.27f, 0.10f), skin); // legs
        B(new( 0.16f, 0.13f, -0.22f), new(0.10f, 0.27f, 0.10f), skin);
        B(new(-0.16f, 0.13f,  0.22f), new(0.10f, 0.27f, 0.10f), skin);
        B(new( 0.16f, 0.13f,  0.22f), new(0.10f, 0.27f, 0.10f), skin);
    }

    void B(Vector3 pos, Vector3 size, Color color) =>
        AddChild(new MeshInstance3D
        {
            Mesh     = new BoxMesh { Size = size, Material = new StandardMaterial3D { AlbedoColor = color } },
            Position = pos,
        });

    public override void _PhysicsProcess(double delta)
    {
        var vel = Velocity;
        if (!IsOnFloor()) vel.Y -= Gravity * (float)delta;
        else              vel.Y  = 0f;

        float vx = 0f, vz = 0f;
        bool fleeing = false;

        if (_player != null)
        {
            var toP = _player.GlobalPosition - GlobalPosition;
            toP.Y = 0f;
            float dist = toP.Length();
            if (dist < FleeRange && dist > 0.05f)
            {
                var away = -toP.Normalized();
                vx = away.X * FleeSpeed;
                vz = away.Z * FleeSpeed;
                fleeing = true;
            }
        }

        if (!fleeing)
        {
            _wanderTime -= delta;
            if (_wanderTime <= 0)
            {
                _wanderDir  = _rng.NextDouble() < 0.25 ? Vector2.Zero : RandDir();
                _wanderTime = _rng.NextDouble() * 3 + 1.5;
            }
            vx = _wanderDir.X * WalkSpeed;
            vz = _wanderDir.Y * WalkSpeed;
        }

        Velocity = new Vector3(vx, vel.Y, vz);
        MoveAndSlide();

        if (new Vector2(vx, vz).LengthSquared() > 0.01f)
        {
            var flat = GlobalPosition + new Vector3(vx, 0, vz);
            LookAt(flat, Vector3.Up);
        }
    }

    static Vector2 RandDir()
    {
        float a = (float)(_rng.NextDouble() * Mathf.Tau);
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }
}
