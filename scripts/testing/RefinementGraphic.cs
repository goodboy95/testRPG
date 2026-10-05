using Godot;
using TownRpg.Presentation;

namespace TownRpg.Scripts.Testing;

/// <summary>Original, reproducible world graphics; exact typography, no generated puzzle content.</summary>
public partial class RefinementGraphic : Node2D
{
    public string Kind { get; init; } = "clock";
    public static readonly string[] Kinds = ["clock", "shop", "notebook", "maintenance", "schedule", "duty"];
    public static readonly string[] ClueKeys = ["DREAM_V6_WORLD_WINDOW", "DREAM_V6_WORLD_SCHEDULE", "DREAM_V6_WORLD_DUTY"];
    public static Vector2I SizeFor(string kind) => kind=="clock" ? new(1024,1024) : kind=="shop" ? new(1024,320) : new(1024,650);
    private Font _font = null!;
    public override void _Ready() => _font = ResourceLoader.Load<Font>("res://assets/fonts/source-han-sans/SourceHanSansSC-Medium.otf");
    private void Text(string value, Vector2 center, int size, Color color)
    {
        var width = _font.GetStringSize(value, fontSize: size).X;
        DrawString(_font, center - new Vector2(width / 2, 0), value, fontSize: size, modulate: color);
    }
    public override void _Draw()
    {
        if (_font is null) return;
        var ink = new Color("354b4c");
        if (Kind == "clock")
        {
            var c = new Vector2(512, 512);
            DrawCircle(c, 505, new Color("b4b2a5"));
            DrawCircle(c, 493, new Color("ede8d7"));
            DrawArc(c, 463, 0, Mathf.Tau, 240, new Color("999e93"), 2, true);
            DrawArc(c, 340, 0, Mathf.Tau, 240, new Color("c8c8b9"), 1, true);
            for (var i = 0; i < 60; i++)
            {
                var angle = i * Mathf.Tau / 60 - Mathf.Pi / 2;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                DrawLine(c + direction * (i % 5 == 0 ? 420 : 440), c + direction * 456, ink, i % 5 == 0 ? 6 : 2.5f, true);
            }
            for (var hour = 1; hour <= 12; hour++)
            {
                var angle = hour * Mathf.Tau / 12 - Mathf.Pi / 2;
                Text(hour.ToString(), c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 372 + new Vector2(0, 27), 76, ink);
            }
            DrawArc(c, 37, 0, Mathf.Tau, 64, new Color("a3a799"), 2, true);
            // Deliberately no hands: five existing 3D states remain authoritative.
            return;
        }
        if (Kind == "shop")
        {
            DrawRect(new Rect2(0, 0, 1024, 320), new Color("546b5d"));
            DrawRect(new Rect2(18, 18, 988, 284), new Color("c5c2a0"), false, 3);
            Text("杂货铺", new Vector2(512, 228), 192, new Color("f2e7c8"));
            return;
        }
        DrawRect(new Rect2(0, 0, 1024, 1024), new Color("e9e0c8"));
        // Gentle paper fibres, kept much lighter than text; no distress obscures clues.
        for (var i = 0; i < 140; i++)
        {
            var x = (i * 139) % 1000; var y = (i * 227) % 1024;
            DrawLine(new Vector2(x,y), new Vector2(x+17,y+1), new Color("dcd5c040"), 1);
        }
        DrawRect(new Rect2(42, 42, 940, 566), new Color("92a399"), false, 3);
        if (Kind == "notebook")
        {
            DrawRect(new Rect2(0, 0, 42, 1024), new Color("617f7f"));
            Text("练习簿", new Vector2(512, 225), 100, ink);
            foreach (var y in new[] { 350, 440, 530 }) DrawLine(new Vector2(210,y), new Vector2(814,y), new Color("889b93"), 3,true);
            return;
        }
        var index = Array.IndexOf(new[] { "maintenance", "schedule", "duty" }, Kind);
        if (index < 0) throw new InvalidOperationException("Unknown graphic " + Kind);
        var lines = DreamPrologueText.Get(ClueKeys[index]).Split('\n');
        Text(lines[0], new Vector2(512, 220), 90, ink);
        DrawLine(new Vector2(125,270), new Vector2(899,270), new Color("81958c"), 3,true);
        for (var i = 1; i < lines.Length; i++) Text(lines[i], new Vector2(512, 355 + i*70), 80, ink);
    }
}
