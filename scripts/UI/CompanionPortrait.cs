using Godot;
using Mindani;

namespace Mindani.UI;

// 2D block-art portrait of a companion, redrawn live when appearance changes.
// Design space: 100 wide x 130 tall (scaled to fit the control's actual size).
public partial class CompanionPortrait : Control
{
    public Color  BodyColor      = new(0.62f, 0.42f, 0.25f);
    public Color  EyeColor       = new(0.12f, 0.08f, 0.04f);
    public Color  HairColor      = new(0.12f, 0.08f, 0.04f);
    public Color  ShirtColor     = new(0.10f, 0.35f, 0.85f);
    public Color  PantsColor     = new(0.10f, 0.10f, 0.28f);
    public Color  ShoeColor      = new(0.18f, 0.12f, 0.08f);
    public Color  AccessoryColor = new(0.85f, 0.20f, 0.20f);
    public string HairStyle      = "none";   // none|short|afro|spiky|long|braids|mohawk
    public string HeadShape      = "round";  // round|square
    public string Accessory      = "none";   // none|cap|glasses|bow

    const float DW = 100f, DH = 130f;

    public void Refresh(FriendsConfig.Def def)
    {
        BodyColor      = def.BodyColor;
        EyeColor       = def.EyeColor;
        HairColor      = def.HairColor;
        ShirtColor     = def.ShirtColor;
        PantsColor     = def.PantsColor;
        ShoeColor      = def.ShoeColor;
        AccessoryColor = def.AccessoryColor;
        HairStyle      = def.HairStyle;
        HeadShape      = def.HeadShape;
        Accessory      = def.Accessory;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float s  = Mathf.Min(Size.X / DW, Size.Y / DH);
        float ox = (Size.X - DW * s) * 0.5f;
        float oy = (Size.Y - DH * s) * 0.5f;

        DrawBackground(s, ox, oy);
        DrawHairBack(s, ox, oy);
        DrawBody(s, ox, oy);
        DrawHairFront(s, ox, oy);
        DrawFace(s, ox, oy);
        DrawAccessoryLayer(s, ox, oy);
    }

    void DrawBackground(float s, float ox, float oy)
    {
        float w       = DW * s;
        float h       = DH * s;
        float groundY = oy + DH * s * 0.965f; // align ground with shoe bottom

        DrawRect(new Rect2(ox, oy, w, groundY - oy), new Color(0.40f, 0.65f, 0.90f));
        DrawRect(new Rect2(ox, groundY, w, oy + h - groundY), new Color(0.35f, 0.60f, 0.28f));
    }

    void DrawBody(float s, float ox, float oy)
    {
        // Head — shape driven by HeadShape
        if (HeadShape == "round")
            DrawCircle(P(ox, oy, s, 50, 33), 25 * s, BodyColor);
        else
            Fill(ox, oy, s, 25, 8, 50, 50, BodyColor);

        // Torso / shirt
        Fill(ox, oy, s, 28, 58, 44, 42, ShirtColor);

        // Arms: sleeve (shirt colour) + hand (skin)
        var sleeve = ShirtColor.Darkened(0.12f);
        Fill(ox, oy, s, 12, 58, 16, 38, sleeve); // left sleeve
        Fill(ox, oy, s, 13, 96, 14,  8, BodyColor); // left hand
        Fill(ox, oy, s, 72, 58, 16, 38, sleeve); // right sleeve
        Fill(ox, oy, s, 73, 96, 14,  8, BodyColor); // right hand

        // Legs / pants
        Fill(ox, oy, s, 28, 100, 20, 16, PantsColor);
        Fill(ox, oy, s, 52, 100, 20, 16, PantsColor);

        // Shoes
        Fill(ox, oy, s, 27, 116, 22, 9, ShoeColor);
        Fill(ox, oy, s, 51, 116, 22, 9, ShoeColor);
    }

    void DrawHairBack(float s, float ox, float oy)
    {
        switch (HairStyle)
        {
            case "afro":
                DrawCircle(P(ox, oy, s, 50, 28), 30 * s, HairColor);
                DrawCircle(P(ox, oy, s, 22, 30), 14 * s, HairColor);
                DrawCircle(P(ox, oy, s, 78, 30), 14 * s, HairColor);
                break;

            case "long":
                Fill(ox, oy, s, 12, 8, 16, 100, HairColor); // left curtain
                Fill(ox, oy, s, 72, 8, 16, 100, HairColor); // right curtain
                break;

            case "braids":
                // Side curtains behind head and body
                Fill(ox, oy, s, 10, 8, 14, 95, HairColor); // left curtain
                Fill(ox, oy, s, 76, 8, 14, 95, HairColor); // right curtain
                break;
        }
    }

    void DrawHairFront(float s, float ox, float oy)
    {
        switch (HairStyle)
        {
            case "spiky":
                Tri(ox, oy, s, 50, -8,  42,  8,  58,  8, HairColor);
                Tri(ox, oy, s, 30, -3,  21,  8,  40,  8, HairColor);
                Tri(ox, oy, s, 70, -3,  60,  8,  79,  8, HairColor);
                Fill(ox, oy, s, 21, 3, 58, 8, HairColor);
                break;

            case "afro":
                break; // round head naturally clips the puff

            case "long":
                Fill(ox, oy, s, 23, 3, 54, 8, HairColor); // cap
                break;

            case "short":
                Fill(ox, oy, s, 22, 4, 56, 7, HairColor); // close-cropped cap
                break;

            case "braids":
                Fill(ox, oy, s, 23, 4, 54, 8, HairColor);  // crown cap
                // Front braids hang over chest — drawn in front of shirt
                Fill(ox, oy, s, 33, 58, 8, 62, HairColor); // left braid
                Fill(ox, oy, s, 43, 58, 8, 62, HairColor); // center braid
                Fill(ox, oy, s, 53, 58, 8, 62, HairColor); // right braid
                break;

            case "mohawk":
                Fill(ox, oy, s, 38,  0, 24, 8, HairColor);  // fin (fills all space above head)
                Fill(ox, oy, s, 22,  5, 56, 7, HairColor);  // crown base band
                break;
        }
    }

    void DrawFace(float s, float ox, float oy)
    {
        // Eyes with chosen colour + white pupil highlight
        Fill(ox, oy, s, 33, 24,  7, 7, EyeColor);
        Fill(ox, oy, s, 60, 24,  7, 7, EyeColor);
        Fill(ox, oy, s, 35, 26,  3, 3, Colors.White); // left pupil
        Fill(ox, oy, s, 62, 26,  3, 3, Colors.White); // right pupil

        // Smile
        var dark = Colors.Black.Lightened(0.15f);
        Fill(ox, oy, s, 38, 39,  3, 3, dark);
        Fill(ox, oy, s, 41, 41, 18, 3, dark);
        Fill(ox, oy, s, 59, 39,  3, 3, dark);
    }

    void DrawAccessoryLayer(float s, float ox, float oy)
    {
        var ac = AccessoryColor;
        switch (Accessory)
        {
            case "cap":
                Fill(ox, oy, s, 20, 5, 60, 18, ac);
                Fill(ox, oy, s, 10, 20, 28, 5, ac.Darkened(0.15f)); // brim (left/front overhang)
                break;

            case "glasses":
                // Tinted lenses
                DrawRect(new Rect2(ox + 28*s, oy + 22*s, 16*s, 12*s), new Color(ac.R, ac.G, ac.B, 0.35f));
                DrawRect(new Rect2(ox + 56*s, oy + 22*s, 16*s, 12*s), new Color(ac.R, ac.G, ac.B, 0.35f));
                // Frame outlines
                DrawRect(new Rect2(ox + 28*s, oy + 22*s, 16*s, 12*s), ac, false, 2f);
                DrawRect(new Rect2(ox + 56*s, oy + 22*s, 16*s, 12*s), ac, false, 2f);
                // Bridge + arms
                DrawLine(P(ox, oy, s, 44, 28), P(ox, oy, s, 56, 28), ac, 2f);
                DrawLine(P(ox, oy, s, 20, 28), P(ox, oy, s, 28, 28), ac, 2f);
                DrawLine(P(ox, oy, s, 72, 28), P(ox, oy, s, 80, 28), ac, 2f);
                break;

            case "bow":
                Fill(ox, oy, s, 30, -2, 14, 10, ac);                    // left loop
                Fill(ox, oy, s, 56, -2, 14, 10, ac);                    // right loop
                Fill(ox, oy, s, 44,  0,  12,  6, ac.Darkened(0.15f));   // centre knot
                break;
        }
    }

    // ── Drawing helpers ───────────────────────────────────────────────────────

    Vector2 P(float ox, float oy, float s, float dx, float dy) =>
        new(ox + dx * s, oy + dy * s);

    void Fill(float ox, float oy, float s, float dx, float dy, float dw, float dh, Color c) =>
        DrawRect(new Rect2(ox + dx * s, oy + dy * s, dw * s, dh * s), c);

    void Tri(float ox, float oy, float s,
             float ax, float ay, float bx, float by, float cx, float cy, Color c) =>
        DrawPolygon(
            [P(ox, oy, s, ax, ay), P(ox, oy, s, bx, by), P(ox, oy, s, cx, cy)],
            [c, c, c]);
}
