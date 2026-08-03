using Godot;

namespace Mindani;

public static class GameSettings
{
    public static bool AlwaysDaytime = true;   // on by default
    public static bool SoundMuted    = false;

    const string SavePath = "user://settings.cfg";

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("gameplay", "always_daytime", AlwaysDaytime);
        cfg.SetValue("gameplay", "sound_muted",    SoundMuted);
        cfg.Save(SavePath);
    }

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SavePath) != Error.Ok) return;
        if (cfg.HasSectionKey("gameplay", "always_daytime"))
            AlwaysDaytime = cfg.GetValue("gameplay", "always_daytime").AsBool();
        if (cfg.HasSectionKey("gameplay", "sound_muted"))
            SoundMuted = cfg.GetValue("gameplay", "sound_muted").AsBool();
    }

    // Apply audio state to the engine — call after Load() or after toggling SoundMuted.
    public static void ApplyAudio()
    {
        int bus = AudioServer.GetBusIndex("Master");
        AudioServer.SetBusMute(bus, SoundMuted);
        if (SoundMuted)
            DisplayServer.TtsStop();
    }
}
