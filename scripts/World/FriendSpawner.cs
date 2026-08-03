using Godot;
using Mindani;
using Mindani.Companions;

public partial class FriendSpawner : Node
{
    [Export] public PackedScene CompanionScene = null!;

    public override void _Ready()
    {
        FriendsConfig.Load();
        foreach (var def in FriendsConfig.All)
        {
            if (!def.Enabled) continue;
            var c = CompanionScene.Instantiate<CompanionController>();
            c.CompanionName  = def.Name;
            c.BodyColor      = def.BodyColor;
            c.EyeColor       = def.EyeColor;
            c.HairStyle      = def.HairStyle;
            c.HairColor      = def.HairColor;
            c.HeadShape      = def.HeadShape;
            c.ShirtColor     = def.ShirtColor;
            c.PantsColor     = def.PantsColor;
            c.ShoeColor      = def.ShoeColor;
            c.Accessory      = def.Accessory;
            c.AccessoryColor = def.AccessoryColor;
            c.Dialogue       = def.Dialogue;
            c.FemaleVoice    = def.FemaleVoice;
            c.VoicePitch     = def.VoicePitch;
            c.VoiceRate      = def.VoiceRate;
            AddChild(c);
            c.GlobalPosition = def.StartPos;
        }
    }
}
