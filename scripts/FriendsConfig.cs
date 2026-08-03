using Godot;

namespace Mindani;

public static class FriendsConfig
{
    public class Def
    {
        public string Name          = "";
        public Color  BodyColor     = new(0.72f, 0.50f, 0.30f); // skin tone
        public Color  EyeColor      = new(0.12f, 0.08f, 0.04f); // dark brown eyes
        public Color  HairColor     = new(0.12f, 0.08f, 0.04f);
        public Color  ShirtColor    = Colors.White;
        public Color  PantsColor    = new(0.18f, 0.18f, 0.32f); // dark navy
        public Color  ShoeColor     = new(0.18f, 0.12f, 0.08f); // dark brown
        public Color  AccessoryColor = new(0.85f, 0.20f, 0.20f); // red
        public string HairStyle     = "none";   // none|short|afro|spiky|long|braids|mohawk
        public string HeadShape     = "round";  // round|square
        public string Accessory     = "none";   // none|cap|glasses|bow

        public string[] Dialogue    = [];
        public bool     FemaleVoice = false;
        public float    VoicePitch  = 1.5f;
        public float    VoiceRate   = 1.0f;
        public Vector3  StartPos    = Vector3.Zero;
        public bool     Enabled     = true;
    }

    static readonly Color _navy   = new(0.10f, 0.10f, 0.28f);
    static readonly Color _brown  = new(0.18f, 0.12f, 0.08f);

    public static readonly Def[] All =
    [
        new() {
            Name = "Caleb",
            BodyColor = new Color(0.62f, 0.42f, 0.25f),
            ShirtColor = new Color(0.10f, 0.35f, 0.85f),
            PantsColor = _navy, ShoeColor = _brown,
            HairColor = new Color(0.12f, 0.08f, 0.04f), HairStyle = "spiky",
            Dialogue = ["Look how far we can see!", "I bet there is treasure over that hill!", "Race you to that big tree!", "This is so awesome!", "I have never been this far before!"],
            VoicePitch = 1.5f, VoiceRate = 1.1f, StartPos = new Vector3(310, 85, 290),
        },
        new() {
            Name = "Conner",
            BodyColor = new Color(0.55f, 0.38f, 0.22f),
            ShirtColor = new Color(0.15f, 0.60f, 0.25f),
            PantsColor = new Color(0.15f, 0.25f, 0.12f), ShoeColor = _brown,
            HairColor = new Color(0.50f, 0.30f, 0.05f), HairStyle = "afro",
            Dialogue = ["I am going to build the biggest castle ever!", "We need more rocks!", "Can we dig a hole here?", "My castle is going to have a moat!", "I found a flat spot, let us build!"],
            VoicePitch = 1.4f, VoiceRate = 1.0f, StartPos = new Vector3(290, 85, 315),
        },
        new() {
            Name = "Milani",
            BodyColor = new Color(0.52f, 0.35f, 0.18f),
            ShirtColor = new Color(0.55f, 0.15f, 0.80f),
            PantsColor = new Color(0.20f, 0.10f, 0.28f), ShoeColor = _brown,
            HairColor = new Color(0.12f, 0.08f, 0.04f), HairStyle = "spiky",
            Dialogue = ["Look, a flower!", "Shh, there is an animal over there!", "The trees are so tall!", "Can we keep one of the chickens?", "I like all the colours!"],
            VoicePitch = 1.45f, VoiceRate = 1.05f, StartPos = new Vector3(330, 85, 278),
        },
        new() {
            Name = "Xyan",
            BodyColor = new Color(0.68f, 0.48f, 0.28f),
            ShirtColor = new Color(0.95f, 0.45f, 0.10f),
            PantsColor = new Color(0.10f, 0.22f, 0.28f), ShoeColor = _brown,
            HairColor = new Color(0.12f, 0.08f, 0.04f), HairStyle = "spiky",
            Dialogue = ["Let us go!", "I can run really fast, watch!", "Last one there is a rotten egg!", "I am not even tired yet!", "Come on, let us go faster!"],
            VoicePitch = 1.5f, VoiceRate = 1.3f, StartPos = new Vector3(278, 85, 330),
        },
        new() {
            Name = "Amarra",
            BodyColor = new Color(0.45f, 0.30f, 0.15f),
            ShirtColor = new Color(0.85f, 0.20f, 0.55f),
            PantsColor = new Color(0.25f, 0.10f, 0.22f), ShoeColor = new Color(0.85f, 0.85f, 0.85f),
            HairColor = new Color(0.12f, 0.08f, 0.04f), HairStyle = "long",
            Dialogue = ["Everything is so pretty here!", "Look at the colours in the sky!", "It looks like a painting!", "This is the most beautiful place ever!", "I want to remember this forever!"],
            FemaleVoice = true, VoicePitch = 1.55f, VoiceRate = 1.1f, StartPos = new Vector3(350, 85, 340),
        },
        new() {
            Name = "Azalia",
            BodyColor = new Color(0.38f, 0.25f, 0.12f),
            ShirtColor = new Color(0.10f, 0.70f, 0.65f),
            PantsColor = new Color(0.08f, 0.28f, 0.28f), ShoeColor = new Color(0.85f, 0.85f, 0.85f),
            HairColor = new Color(0.12f, 0.08f, 0.04f), HairStyle = "long",
            Dialogue = ["Why is the grass that colour?", "I wonder what is underground!", "How do the trees grow so tall?", "Look, the dirt changes colour here!", "I want to know everything about this place!"],
            FemaleVoice = true, VoicePitch = 1.5f, VoiceRate = 1.0f, StartPos = new Vector3(215, 85, 270),
        },
    ];

    const string SavePath = "user://friends.cfg";

    static Vector3 CV(Color c) => new(c.R, c.G, c.B);
    static Color   VC(Vector3 v) => new(v.X, v.Y, v.Z);

    public static void Save()
    {
        var cfg = new ConfigFile();
        foreach (var d in All)
        {
            cfg.SetValue("enabled",         d.Name, d.Enabled);
            cfg.SetValue("body_color",      d.Name, CV(d.BodyColor));
            cfg.SetValue("eye_color",       d.Name, CV(d.EyeColor));
            cfg.SetValue("hair_style",      d.Name, d.HairStyle);
            cfg.SetValue("hair_color",      d.Name, CV(d.HairColor));
            cfg.SetValue("shirt_color",     d.Name, CV(d.ShirtColor));
            cfg.SetValue("pants_color",     d.Name, CV(d.PantsColor));
            cfg.SetValue("shoe_color",      d.Name, CV(d.ShoeColor));
            cfg.SetValue("accessory_color", d.Name, CV(d.AccessoryColor));
            cfg.SetValue("head_shape",      d.Name, d.HeadShape);
            cfg.SetValue("accessory",       d.Name, d.Accessory);
        }
        cfg.Save(SavePath);
    }

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SavePath) != Error.Ok) return;
        foreach (var d in All)
        {
            if (cfg.HasSectionKey("enabled",         d.Name)) d.Enabled       = cfg.GetValue("enabled",         d.Name).AsBool();
            if (cfg.HasSectionKey("body_color",      d.Name)) d.BodyColor      = VC(cfg.GetValue("body_color",      d.Name).AsVector3());
            if (cfg.HasSectionKey("eye_color",       d.Name)) d.EyeColor       = VC(cfg.GetValue("eye_color",       d.Name).AsVector3());
            if (cfg.HasSectionKey("hair_style",      d.Name)) d.HairStyle      = cfg.GetValue("hair_style",      d.Name).AsString();
            if (cfg.HasSectionKey("hair_color",      d.Name)) d.HairColor      = VC(cfg.GetValue("hair_color",      d.Name).AsVector3());
            if (cfg.HasSectionKey("shirt_color",     d.Name)) d.ShirtColor     = VC(cfg.GetValue("shirt_color",     d.Name).AsVector3());
            if (cfg.HasSectionKey("pants_color",     d.Name)) d.PantsColor     = VC(cfg.GetValue("pants_color",     d.Name).AsVector3());
            if (cfg.HasSectionKey("shoe_color",      d.Name)) d.ShoeColor      = VC(cfg.GetValue("shoe_color",      d.Name).AsVector3());
            if (cfg.HasSectionKey("accessory_color", d.Name)) d.AccessoryColor = VC(cfg.GetValue("accessory_color", d.Name).AsVector3());
            if (cfg.HasSectionKey("head_shape",      d.Name)) d.HeadShape      = cfg.GetValue("head_shape",      d.Name).AsString();
            if (cfg.HasSectionKey("accessory",       d.Name)) d.Accessory      = cfg.GetValue("accessory",       d.Name).AsString();
        }
    }
}
