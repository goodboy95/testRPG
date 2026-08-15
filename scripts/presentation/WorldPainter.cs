using Godot;

namespace TownRpg.Presentation;

public partial class WorldPainter : Node2D
{
    public string LocationId { get; set; } = "old_house";
    private Font _font = null!;

    public override void _Ready()
    {
        _font = UiStyles.CreateFont();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (LocationId == "old_house")
        {
            DrawOldHouse();
            return;
        }

        DrawStationQuarter();
    }

    private void DrawOldHouse()
    {
        DrawRect(new Rect2(0, 0, 640, 360), new Color("172337"));
        DrawRect(new Rect2(30, 42, 580, 280), new Color("c19a6b"));
        for (var x = 30; x < 610; x += 32)
        {
            DrawLine(new Vector2(x, 42), new Vector2(x, 322), new Color(0.2f, 0.12f, 0.08f, 0.12f), 1);
        }
        DrawRect(new Rect2(95, 105, 108, 64), new Color("ece0c4"));
        DrawRect(new Rect2(95, 105, 108, 16), new Color("7f6674"));
        DrawRect(new Rect2(470, 105, 92, 55), new Color("6b4937"));
        DrawRect(new Rect2(482, 112, 68, 40), new Color("d1b487"));
        DrawRect(new Rect2(286, 294, 68, 28), new Color("463b4a"));
        DrawString(_font, new Vector2(92, 94), "床铺", HorizontalAlignment.Left, -1, 12, new Color("463b4a"));
        DrawString(_font, new Vector2(468, 94), "旧书桌", HorizontalAlignment.Left, -1, 12, new Color("463b4a"));
    }

    private void DrawStationQuarter()
    {
        DrawRect(new Rect2(0, 0, 640, 360), new Color("7eaa72"));
        DrawRect(new Rect2(0, 70, 640, 76), new Color("c1aa83"));
        DrawRect(new Rect2(0, 225, 640, 72), new Color("708ea0"));
        DrawLine(new Vector2(0, 250), new Vector2(640, 250), new Color("d8d2b5"), 3);
        DrawLine(new Vector2(0, 272), new Vector2(640, 272), new Color("d8d2b5"), 3);
        for (var x = 0; x < 640; x += 18)
        {
            DrawLine(new Vector2(x, 246), new Vector2(x + 4, 277), new Color("5a4b43"), 3);
        }

        DrawBuilding(new Rect2(25, 170, 110, 118), new Color("d0b07a"), "旧宅");
        DrawBuilding(new Rect2(405, 155, 120, 72), new Color("c87858"), "乔姨杂货铺");
        DrawBuilding(new Rect2(170, 42, 170, 90), new Color("d8c89b"), "雾山站");
        DrawRect(new Rect2(421, 116, 34, 42), new Color("6e5746"));
        DrawRect(new Rect2(427, 122, 22, 26), new Color("ead8a0"));
        DrawString(_font, new Vector2(409, 108), "工程告示", HorizontalAlignment.Left, -1, 11, new Color("263447"));

        DrawRect(new Rect2(535, 28, 100, 75), new Color("1f3140"));
        DrawCircle(new Vector2(585, 100), 42, new Color("263947"));
        DrawCircle(new Vector2(585, 100), 28, new Color("0c1521"));
        DrawString(_font, new Vector2(536, 26), "废弃隧道", HorizontalAlignment.Left, -1, 11, Colors.White);
    }

    private void DrawBuilding(Rect2 rect, Color color, string name)
    {
        DrawRect(rect, color);
        DrawColoredPolygon(
            new Vector2[]
            {
                new Vector2(rect.Position.X - 8, rect.Position.Y),
                new Vector2(rect.End.X + 8, rect.Position.Y),
                new Vector2(rect.GetCenter().X, rect.Position.Y - 28)
            },
            new Color("34435a"));
        DrawRect(new Rect2(rect.GetCenter().X - 12, rect.End.Y - 34, 24, 34), new Color("554437"));
        DrawString(_font, rect.Position + new Vector2(8, 20), name, HorizontalAlignment.Left, -1, 12, new Color("273142"));
    }
}
