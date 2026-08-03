using Godot;

namespace Mindani.Companions;

public partial class CompanionController : CharacterBody3D
{
    [Export] public string CompanionName  = "Companion";
    [Export] public Color  BodyColor      = new(0.62f, 0.42f, 0.25f);
    [Export] public Color  EyeColor       = new(0.12f, 0.08f, 0.04f);
    [Export] public Color  HairColor      = new(0.12f, 0.08f, 0.04f);
    [Export] public Color  ShirtColor     = new(0.10f, 0.35f, 0.85f);
    [Export] public Color  PantsColor     = new(0.10f, 0.10f, 0.28f);
    [Export] public Color  ShoeColor      = new(0.18f, 0.12f, 0.08f);
    [Export] public Color  AccessoryColor = new(0.85f, 0.20f, 0.20f);
    [Export] public string HairStyle      = "none";   // none|short|afro|spiky|long|braids|mohawk
    [Export] public string HeadShape      = "round";  // round|square
    [Export] public string Accessory      = "none";   // none|cap|glasses|bow
    [Export] public string[] Dialogue     = [];
    [Export] public bool   FemaleVoice    = false;
    [Export] public float  VoicePitch     = 1.0f;
    [Export] public float  VoiceRate      = 1.0f;

    const float WalkSpeed   = 2.2f;
    const float FollowSpeed = 3.5f;
    const float Gravity     = 20f;
    const float DetectRange = 10f;
    const float StopRange   = 2.5f;
    const float LostRange   = 22f;

    enum AiState { Wander, Idle, Follow, Grouped }

    AiState _state = AiState.Wander;
    Node3D? _target;

    Vector2 _wanderDir;
    double  _stateTimer;
    double  _sayTimer;
    Vector3 _lastPlayerMoveDir = Vector3.Forward;

    string _voiceId     = "";
    int    _utteranceId = 0;

    Label3D          _bubble = null!;
    CharacterBody3D? _player;

    static readonly System.Random _rng = new();

    // Shared material for all batched companion meshes — one draw call per companion
    // instead of one per box part.
    static StandardMaterial3D? _batchMat;
    static StandardMaterial3D BatchMat => _batchMat ??= new StandardMaterial3D { VertexColorUseAsAlbedo = true };

    public override void _Ready()
    {
        BuildVisuals();
        ResolveVoice();

        _player     = GetNodeOrNull<CharacterBody3D>("/root/Main/Lindani");
        _wanderDir  = RandDir();
        _stateTimer = _rng.NextDouble() * 5 + 2;
        _sayTimer   = _rng.NextDouble() * 8 + 5;

        FloorSnapLength = 0.4f;
        FloorMaxAngle   = Mathf.DegToRad(52f);

        AddToGroup("companion");
    }

    void ResolveVoice()
    {
        var voices = DisplayServer.TtsGetVoices();
        if (voices.Count == 0) return;

        string[] prefs = FemaleVoice
            ? ["Zira", "female", "woman"]
            : ["David", "Mark", "male", "man"];

        foreach (var target in prefs)
            foreach (var v in voices)
            {
                var name = v.TryGetValue("name", out var n) ? n.AsString() : "";
                var id   = v.TryGetValue("id",   out var i) ? i.AsString() : "";
                if (name.Contains(target, System.StringComparison.OrdinalIgnoreCase)
                 || id.Contains(target,   System.StringComparison.OrdinalIgnoreCase))
                { _voiceId = id; return; }
            }

        _voiceId = voices[0].TryGetValue("id", out var fid) ? fid.AsString() : "";
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
            LookAt(GlobalPosition + new Vector3(vx, 0, vz), Vector3.Up);
    }

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
        if (_player != null && HorizDist(_player.GlobalPosition) < DetectRange)
        { Engage(_player, "Lindani! I found you! Come on, let's play!"); return (0f, 0f); }

        foreach (var node in GetTree().GetNodesInGroup("companion"))
        {
            if (node == this) continue;
            var other = (CompanionController)node;
            if (other._target == this) continue;
            if (HorizDist(other.GlobalPosition) < DetectRange * 0.8f)
            { Engage(other, $"{other.CompanionName}! Wait for me!"); return (0f, 0f); }
        }

        _stateTimer -= delta;
        if (_stateTimer <= 0)
        {
            if (_rng.NextDouble() < 0.28) { _state = AiState.Idle; _stateTimer = _rng.NextDouble() * 3 + 1.5; }
            else                          { _wanderDir = RandDir();  _stateTimer = _rng.NextDouble() * 5 + 3; }
        }
        return (_wanderDir.X * WalkSpeed, _wanderDir.Y * WalkSpeed);
    }

    (float vx, float vz) TickIdle(double delta)
    {
        _stateTimer -= delta;
        if (_stateTimer <= 0) { _state = AiState.Wander; _wanderDir = RandDir(); _stateTimer = _rng.NextDouble() * 5 + 3; }

        _sayTimer -= delta;
        if (_sayTimer <= 0 && Dialogue.Length > 0)
        { Say(Dialogue[_rng.Next(Dialogue.Length)]); _sayTimer = _rng.NextDouble() * 9 + 6; }
        return (0f, 0f);
    }

    (float vx, float vz) TickFollow(double delta)
    {
        if (_target == null || !IsInstanceValid(_target)) { ResumeWander(null); return (0f, 0f); }
        if (HorizDist(_target.GlobalPosition) > LostRange) { ResumeWander("Hey, wait for me!"); return (0f, 0f); }

        var toFlank = Flank2D();
        if (toFlank.Length() < 1.5f) { _state = AiState.Grouped; _stateTimer = _rng.NextDouble() * 8 + 5; _sayTimer = _rng.NextDouble() * 3 + 2; return (0f, 0f); }
        var dir = toFlank.Normalized();
        return (dir.X * FollowSpeed, dir.Y * FollowSpeed);
    }

    (float vx, float vz) TickGrouped(double delta)
    {
        if (_target == null || !IsInstanceValid(_target)) { ResumeWander(null); return (0f, 0f); }
        float dist = HorizDist(_target.GlobalPosition);
        if (dist >= LostRange)                  { ResumeWander("Where did you go?"); return (0f, 0f); }
        if (dist > StopRange * 2.5f)            { _state = AiState.Follow; return (0f, 0f); }

        _sayTimer -= delta;
        if (_sayTimer <= 0 && Dialogue.Length > 0) { Say(Dialogue[_rng.Next(Dialogue.Length)]); _sayTimer = _rng.NextDouble() * 8 + 5; }

        _stateTimer -= delta;
        if (_stateTimer <= 0)
        {
            if (_rng.NextDouble() < 0.3) ResumeWander("I'm gonna go play over there!");
            else                         _stateTimer = _rng.NextDouble() * 10 + 6;
        }

        var toFlank = Flank2D();
        var vel     = (_target as CharacterBody3D)?.Velocity ?? Vector3.Zero;
        bool moving = new Vector2(vel.X, vel.Z).LengthSquared() > 0.25f;
        if (toFlank.Length() > 1.5f && (moving || toFlank.Length() > 4f))
        { var dir = toFlank.Normalized(); return (dir.X * WalkSpeed, dir.Y * WalkSpeed); }
        return (0f, 0f);
    }

    Vector2 Flank2D()
    {
        if (_target == null) return Vector2.Zero;
        var vel = (_target as CharacterBody3D)?.Velocity ?? Vector3.Zero;
        var flat = new Vector3(vel.X, 0, vel.Z);
        if (flat.LengthSquared() > 0.25f) _lastPlayerMoveDir = flat.Normalized();
        var right = new Vector3(-_lastPlayerMoveDir.Z, 0, _lastPlayerMoveDir.X);
        float side = (CompanionName.Length & 1) == 0 ? 3f : -3f;
        var target3D = _target.GlobalPosition + right * side;
        return new Vector2(target3D.X - GlobalPosition.X, target3D.Z - GlobalPosition.Z);
    }

    void Engage(Node3D target, string greeting)
    { _target = target; _state = AiState.Follow; _stateTimer = 0; Say(greeting); }

    void ResumeWander(string? farewell)
    { if (farewell != null) Say(farewell); _target = null; _state = AiState.Wander; _wanderDir = RandDir(); _stateTimer = _rng.NextDouble() * 5 + 3; }

    float   HorizDist(Vector3 pos) => new Vector2(pos.X - GlobalPosition.X, pos.Z - GlobalPosition.Z).Length();

    static Vector2 RandDir() { float a = (float)(_rng.NextDouble() * Mathf.Tau); return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }

    void Say(string line)
    {
        _bubble.Text = $"\"{line}\"";
        if (_voiceId.Length > 0 && !GameSettings.SoundMuted)
            DisplayServer.TtsSpeak(line, _voiceId, 80, VoicePitch, VoiceRate, ++_utteranceId, false);
        GetTree().CreateTimer(4.5).Timeout += () => { if (IsInstanceValid(this)) _bubble.Text = ""; };
    }

    // ── Visuals (batched into one mesh per companion) ─────────────────────────

    void BuildVisuals()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        // Eyes
        B(st, new Vector3(-0.12f, 1.62f, -0.260f), new Vector3(0.10f, 0.10f, 0.020f), EyeColor);
        B(st, new Vector3( 0.12f, 1.62f, -0.260f), new Vector3(0.10f, 0.10f, 0.020f), EyeColor);
        B(st, new Vector3(-0.12f, 1.62f, -0.275f), new Vector3(0.04f, 0.04f, 0.010f), Colors.Black);
        B(st, new Vector3( 0.12f, 1.62f, -0.275f), new Vector3(0.04f, 0.04f, 0.010f), Colors.Black);
        B(st, new Vector3(0,      1.44f, -0.260f), new Vector3(0.20f, 0.05f, 0.020f), Colors.Black.Lightened(0.15f));

        // Torso / shirt
        B(st, new Vector3(0,      0.93f, 0),    new Vector3(0.52f, 0.60f, 0.28f), ShirtColor);
        // Arms: sleeve + hand
        B(st, new Vector3(-0.37f, 0.96f, 0),    new Vector3(0.18f, 0.52f, 0.18f), ShirtColor.Darkened(0.10f));
        B(st, new Vector3(-0.37f, 0.64f, 0),    new Vector3(0.16f, 0.12f, 0.16f), BodyColor);
        B(st, new Vector3( 0.37f, 0.96f, 0),    new Vector3(0.18f, 0.52f, 0.18f), ShirtColor.Darkened(0.10f));
        B(st, new Vector3( 0.37f, 0.64f, 0),    new Vector3(0.16f, 0.12f, 0.16f), BodyColor);
        // Legs + shoes
        B(st, new Vector3(-0.13f, 0.38f, 0),    new Vector3(0.22f, 0.54f, 0.22f), PantsColor);
        B(st, new Vector3( 0.13f, 0.38f, 0),    new Vector3(0.22f, 0.54f, 0.22f), PantsColor);
        B(st, new Vector3(-0.13f, 0.05f, 0.02f), new Vector3(0.24f, 0.10f, 0.26f), ShoeColor);
        B(st, new Vector3( 0.13f, 0.05f, 0.02f), new Vector3(0.24f, 0.10f, 0.26f), ShoeColor);

        // Square head goes in the batch too
        if (HeadShape != "round")
            B(st, new Vector3(0, 1.55f, 0), new Vector3(0.50f, 0.50f, 0.50f), BodyColor);

        BuildHair(st);
        BuildAccessory(st);

        st.SetMaterial(BatchMat);
        AddChild(new MeshInstance3D { Mesh = st.Commit() });

        // Sphere head is a separate mesh (can't be easily expressed as box vertices)
        if (HeadShape == "round")
            AddChild(new MeshInstance3D
            {
                Mesh     = new SphereMesh { Radius = 0.27f, Height = 0.54f,
                               Material  = new StandardMaterial3D { AlbedoColor = BodyColor } },
                Position = new Vector3(0, 1.55f, 0),
            });

        AddChild(new Label3D
        {
            Text        = CompanionName,
            FontSize    = 22,
            Position    = new Vector3(0, 2.35f, 0),
            NoDepthTest = true,
            Billboard   = BaseMaterial3D.BillboardModeEnum.Enabled,
        });

        _bubble = new Label3D
        {
            Text        = "",
            FontSize    = 18,
            Position    = new Vector3(0, 2.80f, 0),
            NoDepthTest = true,
            Billboard   = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate    = new Color(1f, 0.95f, 0.4f),
        };
        AddChild(_bubble);
    }

    void BuildHair(SurfaceTool st)
    {
        switch (HairStyle)
        {
            case "spiky":
                B(st, new Vector3(0,      1.83f,  0),    new Vector3(0.50f, 0.06f, 0.50f), HairColor);
                B(st, new Vector3(-0.15f, 2.12f,  0),    new Vector3(0.10f, 0.40f, 0.10f), HairColor);
                B(st, new Vector3( 0.00f, 2.18f,  0),    new Vector3(0.10f, 0.40f, 0.10f), HairColor);
                B(st, new Vector3( 0.15f, 2.12f,  0),    new Vector3(0.10f, 0.40f, 0.10f), HairColor);
                break;
            case "afro":
                B(st, new Vector3(0,      2.05f,  0),     new Vector3(0.62f, 0.45f, 0.60f), HairColor);
                B(st, new Vector3(-0.32f, 1.90f,  0),     new Vector3(0.14f, 0.30f, 0.46f), HairColor);
                B(st, new Vector3( 0.32f, 1.90f,  0),     new Vector3(0.14f, 0.30f, 0.46f), HairColor);
                B(st, new Vector3(0,      1.88f,  0.30f), new Vector3(0.54f, 0.26f, 0.12f), HairColor);
                B(st, new Vector3(0,      1.88f, -0.30f), new Vector3(0.54f, 0.26f, 0.12f), HairColor);
                break;
            case "long":
                B(st, new Vector3(0,      1.83f,  0),     new Vector3(0.52f, 0.06f, 0.52f), HairColor);
                B(st, new Vector3(-0.27f, 1.42f,  0),     new Vector3(0.08f, 0.76f, 0.30f), HairColor);
                B(st, new Vector3( 0.27f, 1.42f,  0),     new Vector3(0.08f, 0.76f, 0.30f), HairColor);
                B(st, new Vector3(0,      1.40f,  0.27f), new Vector3(0.44f, 0.76f, 0.08f), HairColor);
                break;
            case "short":
                B(st, new Vector3(0, 1.83f, 0), new Vector3(0.52f, 0.04f, 0.52f), HairColor);
                B(st, new Vector3(0, 1.87f, 0), new Vector3(0.46f, 0.10f, 0.46f), HairColor);
                break;
            case "braids":
                B(st, new Vector3(0,      1.83f, 0),     new Vector3(0.52f, 0.06f, 0.52f), HairColor);
                B(st, new Vector3(-0.18f, 1.18f, 0.05f), new Vector3(0.08f, 0.80f, 0.08f), HairColor);
                B(st, new Vector3( 0.00f, 1.12f, 0.08f), new Vector3(0.08f, 0.92f, 0.08f), HairColor);
                B(st, new Vector3( 0.18f, 1.18f, 0.05f), new Vector3(0.08f, 0.80f, 0.08f), HairColor);
                break;
            case "mohawk":
                B(st, new Vector3(0, 1.83f, 0), new Vector3(0.52f, 0.06f, 0.52f), HairColor);
                B(st, new Vector3(0, 2.22f, 0), new Vector3(0.12f, 0.62f, 0.48f), HairColor);
                break;
        }
    }

    void BuildAccessory(SurfaceTool st)
    {
        switch (Accessory)
        {
            case "cap":
                B(st, new Vector3(0,     1.88f,  0.00f), new Vector3(0.56f, 0.20f, 0.56f), AccessoryColor);
                B(st, new Vector3(0.06f, 1.79f, -0.30f), new Vector3(0.44f, 0.08f, 0.20f), AccessoryColor.Darkened(0.15f));
                break;
            case "glasses":
                B(st, new Vector3(-0.14f, 1.62f, -0.28f), new Vector3(0.14f, 0.12f, 0.02f), AccessoryColor);
                B(st, new Vector3( 0.14f, 1.62f, -0.28f), new Vector3(0.14f, 0.12f, 0.02f), AccessoryColor);
                B(st, new Vector3(0,      1.62f, -0.28f), new Vector3(0.06f, 0.03f, 0.02f), AccessoryColor);
                B(st, new Vector3(-0.28f, 1.62f, -0.24f), new Vector3(0.04f, 0.03f, 0.08f), AccessoryColor);
                B(st, new Vector3( 0.28f, 1.62f, -0.24f), new Vector3(0.04f, 0.03f, 0.08f), AccessoryColor);
                break;
            case "bow":
                B(st, new Vector3(-0.15f, 1.92f, 0), new Vector3(0.14f, 0.12f, 0.12f), AccessoryColor);
                B(st, new Vector3( 0.15f, 1.92f, 0), new Vector3(0.14f, 0.12f, 0.12f), AccessoryColor);
                B(st, new Vector3(0,      1.92f, 0), new Vector3(0.06f, 0.08f, 0.08f), AccessoryColor.Darkened(0.15f));
                break;
        }
    }

    // ── Mesh helpers ──────────────────────────────────────────────────────────

    // Add a box to a SurfaceTool with per-face normals and vertex colour.
    static void B(SurfaceTool st, Vector3 c, Vector3 size, Color col)
    {
        var h   = size * 0.5f;
        var lbn = c + new Vector3(-h.X, -h.Y, -h.Z);
        var rbn = c + new Vector3( h.X, -h.Y, -h.Z);
        var rtn = c + new Vector3( h.X,  h.Y, -h.Z);
        var ltn = c + new Vector3(-h.X,  h.Y, -h.Z);
        var lbf = c + new Vector3(-h.X, -h.Y,  h.Z);
        var rbf = c + new Vector3( h.X, -h.Y,  h.Z);
        var rtf = c + new Vector3( h.X,  h.Y,  h.Z);
        var ltf = c + new Vector3(-h.X,  h.Y,  h.Z);

        // Quad adds two CCW triangles so the outward normal is correct.
        void Q(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 n)
        {
            st.SetNormal(n); st.SetColor(col); st.AddVertex(a);
            st.SetNormal(n); st.SetColor(col); st.AddVertex(b);
            st.SetNormal(n); st.SetColor(col); st.AddVertex(cc);
            st.SetNormal(n); st.SetColor(col); st.AddVertex(a);
            st.SetNormal(n); st.SetColor(col); st.AddVertex(cc);
            st.SetNormal(n); st.SetColor(col); st.AddVertex(d);
        }

        Q(lbn, ltn, rtn, rbn, Vector3.Forward); // near  (−Z)
        Q(lbf, rbf, rtf, ltf, Vector3.Back);    // far   (+Z)
        Q(lbn, lbf, ltf, ltn, Vector3.Left);    // left  (−X)
        Q(rbn, rtn, rtf, rbf, Vector3.Right);   // right (+X)
        Q(rtn, ltn, ltf, rtf, Vector3.Up);      // top   (+Y)
        Q(lbn, rbn, rbf, lbf, Vector3.Down);    // bottom(−Y)
    }
}
