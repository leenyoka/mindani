using Godot;

namespace Mindani.World;

public partial class DayNightCycle : Node
{
    /// <summary>Seconds for one full in-game day (default 6 minutes).</summary>
    [Export] public float DayDuration = 360f;

    // 0 = midnight · 0.25 = sunrise · 0.5 = noon · 0.75 = sunset
    float _time = 0.28f; // start just after sunrise so first boot looks bright

    DirectionalLight3D?    _sun;
    Environment?           _env;
    ProceduralSkyMaterial? _sky;

    // ── Sky colour palette ────────────────────────────────────────────────
    static readonly Color NightTop     = new(0.02f, 0.03f, 0.12f);
    static readonly Color NightHorizon = new(0.05f, 0.07f, 0.20f);
    static readonly Color DawnHorizon  = new(0.92f, 0.46f, 0.13f); // warm orange glow
    static readonly Color DayTop       = new(0.18f, 0.50f, 0.92f);
    static readonly Color DayHorizon   = new(0.68f, 0.85f, 1.00f);
    static readonly Color GroundNight  = new(0.04f, 0.06f, 0.10f);
    static readonly Color GroundDay    = new(0.40f, 0.60f, 0.30f);

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public override void _Ready()
    {
        _sun = GetNodeOrNull<DirectionalLight3D>("/root/Main/DirectionalLight3D");

        var worldEnv = GetNodeOrNull<WorldEnvironment>("/root/Main/WorldEnvironment");
        if (worldEnv?.Environment is { } env)
        {
            _env = env;
            _sky = env.Sky?.SkyMaterial as ProceduralSkyMaterial;
        }
    }

    public override void _Process(double delta)
    {
        _time = (_time + (float)delta / DayDuration) % 1.0f;

        // sunHeight: +1 at noon, 0 at sunrise/sunset, -1 at midnight
        float sunHeight = Mathf.Sin((_time - 0.25f) * Mathf.Tau);

        UpdateLight(sunHeight);
        UpdateSky(sunHeight);
        UpdateAmbient(sunHeight);
    }

    // ── Sun / moon light ──────────────────────────────────────────────────

    void UpdateLight(float sunHeight)
    {
        if (_sun == null) return;

        // Arc the sun across the sky (rotates around X with slight Y tilt for realism)
        _sun.RotationDegrees = new Vector3(-(_time - 0.25f) * 360f, -30f, 0f);

        float daylight  = Mathf.Max(0f, sunHeight);
        float moonlight = Mathf.Max(0f, -sunHeight) * 0.15f;

        if (daylight > 0.01f)
        {
            // Warm orange near the horizon, white-yellow at noon
            float warmth  = Mathf.Clamp((1f - daylight) * daylight * 4f, 0f, 1f);
            _sun.LightColor  = new Color(1f,
                Mathf.Lerp(1f, 0.55f, warmth),
                Mathf.Lerp(1f, 0.05f, warmth));
            _sun.LightEnergy = daylight * 1.3f;
        }
        else
        {
            // Cool blue-white moonlight at night
            _sun.LightColor  = new Color(0.70f, 0.80f, 1.00f);
            _sun.LightEnergy = moonlight;
        }
    }

    // ── Sky colours ───────────────────────────────────────────────────────

    void UpdateSky(float sunHeight)
    {
        if (_sky == null) return;

        // Day fraction: 0 = full night, 1 = full daylight
        float t      = Mathf.Clamp((sunHeight + 0.20f) / 0.50f, 0f, 1f);
        float smooth = t * t * (3f - 2f * t); // smoothstep — prevents harsh transitions

        // Dawn/dusk orange factor: peaks when sun is right at the horizon
        float dusk = Mathf.Max(0f, 1f - Mathf.Abs(sunHeight) / 0.25f);

        _sky.SkyTopColor = NightTop.Lerp(DayTop, smooth);

        // Horizon: base lerps night→day, then tinted orange near sunrise/sunset
        var baseHorizon = NightHorizon.Lerp(DayHorizon, smooth);
        _sky.SkyHorizonColor    = baseHorizon.Lerp(DawnHorizon, dusk * 0.85f);

        _sky.GroundHorizonColor = GroundNight.Lerp(GroundDay, smooth);
        _sky.GroundBottomColor  = GroundNight.Lerp(GroundDay, smooth * 0.5f);
    }

    // ── Ambient light ─────────────────────────────────────────────────────

    void UpdateAmbient(float sunHeight)
    {
        if (_env == null) return;
        float t = Mathf.Clamp((sunHeight + 0.20f) / 0.60f, 0f, 1f);
        // Quadratic so the world dims quickly as the sun dips
        _env.AmbientLightEnergy = Mathf.Lerp(0.04f, 0.50f, t * t);
    }
}
