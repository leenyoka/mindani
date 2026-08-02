using Godot;

namespace Mindani.Companions;

public partial class CompanionController : CharacterBody3D
{
    [Export] public string   CompanionName = "Companion";
    [Export] public Color    BodyColor     = Colors.Orange;
    [Export] public string[] Dialogue      = [];

    // ── Tuning ────────────────────────────────────────────────────────────
    const float WalkSpeed   = 2.2f;
    const float FollowSpeed = 3.5f;
    const float Gravity     = 20f;
    const float DetectRange = 10f;   // notice player / companion within this
    const float StopRange   = 2.5f;  // stop walking when this close
    const float LostRange   = 22f;   // give up following beyond this distance

    // ── State ─────────────────────────────────────────────────────────────
    enum AiState { Wander, Idle, Follow, Grouped }

    AiState _state = AiState.Wander;
    Node3D? _target;

    Vector2 _wanderDir;
    double  _stateTimer;
    double  _sayTimer;

    // ── References ────────────────────────────────────────────────────────
    Label3D          _bubble = null!;
    CharacterBody3D? _player;

    static readonly System.Random _rng = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public override void _Ready()
    {
        BuildVisuals();

        _player     = GetNodeOrNull<CharacterBody3D>("/root/Main/Lindani");
        _wanderDir  = RandDir();
        _stateTimer = _rng.NextDouble() * 5 + 2;
        _sayTimer   = _rng.NextDouble() * 8 + 5;

        FloorSnapLength = 0.4f;
        FloorMaxAngle   = Mathf.DegToRad(52f);

        AddToGroup("companion");
    }

    public override void _PhysicsProcess(double delta)
    {
        var vel = Velocity;

        if (!IsOnFloor()) vel.Y -= Gravity * (float)delta;
        else              vel.Y  = 0f;

        var (vx, vz) = Tick(delta);
        vel.X = vx;
        vel.Z = vz;
        Velocity = vel;
        MoveAndSlide();

        if (new Vector2(vx, vz).LengthSquared() > 0.01f)
        {
            var look = GlobalPosition + new Vector3(vx, 0, vz);
            LookAt(look, Vector3.Up);
        }
    }

    // ── State machine ─────────────────────────────────────────────────────

    (float vx, float vz) Tick(double delta) => _state switch
    {
        AiState.Wander  => TickWander(delta),
        AiState.Idle    => TickIdle(delta),
        AiState.Follow  => TickFollow(delta),
        AiState.Grouped => TickGrouped(delta),
        _               => (0f, 0f),
    };

    (float vx, float vz) TickWander(double delta)
    {
        // ── Detect player ──────────────────────────────────────────────
        if (_player != null)
        {
            var d = HorizDist(_player.GlobalPosition);
            if (d < DetectRange)
            {
                Engage(_player, "Hey Lindani! I found you!");
                return (0f, 0f);
            }
        }

        // ── Detect companions ──────────────────────────────────────────
        foreach (var node in GetTree().GetNodesInGroup("companion"))
        {
            if (node == this) continue;
            var other = (CompanionController)node;
            if (other._target == this) continue; // prevent A↔B mutual lock
            if (HorizDist(other.GlobalPosition) < DetectRange * 0.8f)
            {
                Engage(other, $"{other.CompanionName}! There you are!");
                return (0f, 0f);
            }
        }

        // ── Change direction or go idle ────────────────────────────────
        _stateTimer -= delta;
        if (_stateTimer <= 0)
        {
            if (_rng.NextDouble() < 0.28)
            {
                _state      = AiState.Idle;
                _stateTimer = _rng.NextDouble() * 3 + 1.5;
            }
            else
            {
                _wanderDir  = RandDir();
                _stateTimer = _rng.NextDouble() * 5 + 3;
            }
        }

        return (_wanderDir.X * WalkSpeed, _wanderDir.Y * WalkSpeed);
    }

    (float vx, float vz) TickIdle(double delta)
    {
        _stateTimer -= delta;
        if (_stateTimer <= 0)
        {
            _state      = AiState.Wander;
            _wanderDir  = RandDir();
            _stateTimer = _rng.NextDouble() * 5 + 3;
        }

        _sayTimer -= delta;
        if (_sayTimer <= 0 && Dialogue.Length > 0)
        {
            Say(Dialogue[_rng.Next(Dialogue.Length)]);
            _sayTimer = _rng.NextDouble() * 9 + 6;
        }

        return (0f, 0f);
    }

    (float vx, float vz) TickFollow(double delta)
    {
        if (_target == null || !IsInstanceValid(_target))
        {
            ResumeWander(null);
            return (0f, 0f);
        }

        float dist = HorizDist(_target.GlobalPosition);

        if (dist > LostRange)
        {
            ResumeWander("Wait up...");
            return (0f, 0f);
        }

        if (dist < StopRange)
        {
            _state      = AiState.Grouped;
            _stateTimer = _rng.NextDouble() * 8 + 5;
            _sayTimer   = _rng.NextDouble() * 3 + 2;
            return (0f, 0f);
        }

        // Move toward the flank position so we approach from the side, not directly behind
        var flank = FlankPosition();
        var dir   = new Vector2(flank.X - GlobalPosition.X, flank.Z - GlobalPosition.Z).Normalized();
        return (dir.X * FollowSpeed, dir.Y * FollowSpeed);
    }

    (float vx, float vz) TickGrouped(double delta)
    {
        if (_target == null || !IsInstanceValid(_target))
        {
            ResumeWander(null);
            return (0f, 0f);
        }

        float dist = HorizDist(_target.GlobalPosition);

        if (dist >= LostRange)
        {
            ResumeWander("I lost them...");
            return (0f, 0f);
        }

        // Re-enter follow if target drifted away
        if (dist > StopRange * 2.5f)
        {
            _state = AiState.Follow;
            return (0f, 0f);
        }

        // Say things while hanging out
        _sayTimer -= delta;
        if (_sayTimer <= 0 && Dialogue.Length > 0)
        {
            Say(Dialogue[_rng.Next(Dialogue.Length)]);
            _sayTimer = _rng.NextDouble() * 8 + 5;
        }

        // Occasionally break off and wander independently again
        _stateTimer -= delta;
        if (_stateTimer <= 0)
        {
            if (_rng.NextDouble() < 0.3)
                ResumeWander("See you later!");
            else
                _stateTimer = _rng.NextDouble() * 10 + 6;
        }

        // Keep pace when the target moves: walk toward flank if we've drifted from it
        var flank      = FlankPosition();
        float flankDist = new Vector2(flank.X - GlobalPosition.X, flank.Z - GlobalPosition.Z).Length();
        if (flankDist > 1.5f)
        {
            var dir = new Vector2(flank.X - GlobalPosition.X, flank.Z - GlobalPosition.Z).Normalized();
            return (dir.X * WalkSpeed, dir.Y * WalkSpeed);
        }

        return (0f, 0f);
    }

    // Returns a position alongside the target so companions walk beside, not behind.
    // Name length determines left-side vs right-side consistently per companion.
    Vector3 FlankPosition()
    {
        if (_target == null) return GlobalPosition;
        float side = (CompanionName.Length & 1) == 0 ? 2.0f : -2.0f;
        return _target.GlobalPosition + _target.GlobalTransform.Basis.X * side;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    void Engage(Node3D target, string greeting)
    {
        _target     = target;
        _state      = AiState.Follow;
        _stateTimer = 0;
        Say(greeting);
    }

    void ResumeWander(string? farewell)
    {
        if (farewell != null) Say(farewell);
        _target     = null;
        _state      = AiState.Wander;
        _wanderDir  = RandDir();
        _stateTimer = _rng.NextDouble() * 5 + 3;
    }

    float   HorizDist(Vector3 pos) => new Vector2(pos.X - GlobalPosition.X, pos.Z - GlobalPosition.Z).Length();
    Vector2 HorizDir(Vector3 pos)  => new Vector2(pos.X - GlobalPosition.X, pos.Z - GlobalPosition.Z).Normalized();

    static Vector2 RandDir()
    {
        float a = (float)(_rng.NextDouble() * Mathf.Tau);
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    void Say(string line)
    {
        _bubble.Text = $"\"{line}\"";
        GetTree().CreateTimer(4.5).Timeout += () =>
        {
            if (IsInstanceValid(this)) _bubble.Text = "";
        };
    }

    // ── Visuals (block-figure construction) ──────────────────────────────

    void BuildVisuals()
    {
        var skin = BodyColor.Lightened(0.45f);
        var dark = BodyColor.Darkened(0.25f);
        var face = Colors.Black.Lightened(0.2f);

        Box(new Vector3(0,      1.55f,  0),    new Vector3(0.50f, 0.50f, 0.50f), skin);
        Box(new Vector3(-0.12f, 1.62f, -0.26f), new Vector3(0.10f, 0.10f, 0.02f), face);
        Box(new Vector3( 0.12f, 1.62f, -0.26f), new Vector3(0.10f, 0.10f, 0.02f), face);
        Box(new Vector3(0,      1.44f, -0.26f), new Vector3(0.20f, 0.05f, 0.02f), face);
        Box(new Vector3(0,      0.93f,  0),    new Vector3(0.52f, 0.60f, 0.28f), BodyColor);
        Box(new Vector3(-0.37f, 0.90f,  0),    new Vector3(0.18f, 0.58f, 0.18f), skin);
        Box(new Vector3( 0.37f, 0.90f,  0),    new Vector3(0.18f, 0.58f, 0.18f), skin);
        Box(new Vector3(-0.13f, 0.32f,  0),    new Vector3(0.22f, 0.62f, 0.22f), dark);
        Box(new Vector3( 0.13f, 0.32f,  0),    new Vector3(0.22f, 0.62f, 0.22f), dark);

        AddChild(new Label3D
        {
            Text        = CompanionName,
            FontSize    = 22,
            Position    = new Vector3(0, 2.25f, 0),
            NoDepthTest = true,
            Billboard   = BaseMaterial3D.BillboardModeEnum.Enabled,
        });

        _bubble = new Label3D
        {
            Text        = "",
            FontSize    = 18,
            Position    = new Vector3(0, 2.75f, 0),
            NoDepthTest = true,
            Billboard   = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate    = new Color(1f, 0.95f, 0.4f),
        };
        AddChild(_bubble);
    }

    void Box(Vector3 pos, Vector3 size, Color color) =>
        AddChild(new MeshInstance3D
        {
            Mesh     = new BoxMesh { Size = size, Material = new StandardMaterial3D { AlbedoColor = color } },
            Position = pos,
        });
}
