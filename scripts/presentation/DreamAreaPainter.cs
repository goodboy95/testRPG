using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamAreaPainter : Node2D
{
    public string AreaId { get; set; } = DreamEscapeIds.AreaCorridor;
    private Font _font = null!;

    public override void _Ready()
    {
        _font = UiStyles.CreateFont();
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 640, 360), new Color("101a2a"));
        DrawRect(new Rect2(24, 48, 592, 278), new Color("1d3340"));
        for (var y = 64; y < 326; y += 32)
            DrawLine(new Vector2(24, y), new Vector2(616, y), new Color("284a51"), 1);

        if (AreaId == DreamEscapeIds.AreaCorridor) DrawCorridor();
        else if (AreaId == DreamEscapeIds.AreaClassroom) DrawClassroom();
        else if (AreaId == DreamEscapeIds.AreaMusic) DrawMusic();
        else if (AreaId == DreamEscapeIds.AreaArchive) DrawArchive();
        else DrawAuditorium();
    }

    private void DrawCorridor()
    {
        DrawRect(new Rect2(24, 84, 592, 210), new Color("314852"));
        DrawRect(new Rect2(24, 250, 592, 76), new Color("172737"));
        DrawDoor(62, "一", new Color("729a91"));
        DrawDoor(205, "二", new Color("8b7658"));
        DrawDoor(360, "三", new Color("596f79"));
        DrawDoor(518, "四", new Color("735d79"));
        DrawString(_font, new Vector2(234, 70), "旧校舍走廊", HorizontalAlignment.Left, -1, 18, new Color("e8d9ad"));
    }

    private void DrawClassroom()
    {
        DrawRect(new Rect2(214, 58, 212, 58), new Color("203b38"));
        DrawString(_font, new Vector2(260, 92), "被擦过的黑板", HorizontalAlignment.Left, -1, 14, new Color("d7e3d8"));

        DrawRect(new Rect2(65, 72, 82, 84), new Color("425765"));
        DrawTriangle(new Vector2(106, 114), new Color("c89a54"));
        DrawString(_font, new Vector2(60, 65), "旧地图柜", HorizontalAlignment.Left, -1, 12, new Color("e8d9ad"));

        DrawRect(new Rect2(62, 184, 86, 70), new Color("6f8f98"));
        for (var pane = 1; pane < 3; pane++)
            DrawLine(new Vector2(62 + pane * 28, 184), new Vector2(62 + pane * 28, 254), new Color("c8d9d5"), 2);
        DrawLine(new Vector2(76, 198), new Vector2(88, 212), new Color("e8d9ad"), 2);
        DrawLine(new Vector2(101, 206), new Vector2(111, 220), new Color("e8d9ad"), 2);
        DrawLine(new Vector2(125, 196), new Vector2(136, 211), new Color("e8d9ad"), 2);
        DrawString(_font, new Vector2(72, 178), "旧窗格", HorizontalAlignment.Left, -1, 12, new Color("e8d9ad"));

        DrawCircle(new Vector2(468, 92), 24, new Color("d4c89f"));
        DrawCircle(new Vector2(468, 92), 20, new Color("263947"));
        DrawLine(new Vector2(468, 92), new Vector2(456, 102), new Color("f0c86f"), 2);
        DrawLine(new Vector2(468, 92), new Vector2(468, 76), new Color("f0c86f"), 2);
        DrawString(_font, new Vector2(444, 63), "时钟", HorizontalAlignment.Left, -1, 12, new Color("e8d9ad"));

        DrawRect(new Rect2(520, 68, 72, 92), new Color("d7d0b8"));
        for (var row = 0; row < 4; row++)
            DrawLine(new Vector2(528, 88 + row * 15), new Vector2(583, 88 + row * 15), new Color("65727a"), 1);
        DrawCircle(new Vector2(536, 103), 5, new Color("b85c5c"));
        DrawString(_font, new Vector2(530, 63), "值日表", HorizontalAlignment.Left, -1, 12, new Color("e8d9ad"));

        DrawRect(new Rect2(190, 126, 32, 10), new Color("e8dfc5"));
        DrawRect(new Rect2(474, 184, 92, 60), new Color("76543c"));
        DrawString(_font, new Vector2(486, 178), "教师桌", HorizontalAlignment.Left, -1, 12, new Color("e8d9ad"));
        DrawDesks();
    }

    private void DrawMusic()
    {
        DrawRect(new Rect2(82, 70, 105, 70), new Color("655568"));
        DrawString(_font, new Vector2(86, 64), "蒙尘乐谱", HorizontalAlignment.Left, -1, 12, new Color("e8d9ad"));
        DrawRect(new Rect2(478, 64, 92, 105), new Color("374d58"));
        DrawString(_font, new Vector2(484, 58), "器材间", HorizontalAlignment.Left, -1, 12, new Color("e8d9ad"));
        for (var index = 0; index < 4; index++)
            DrawRect(new Rect2(490 + index * 17, 95, 10, 38 - index * 5), new Color("d0a75d"));
        DrawRect(new Rect2(488, 205, 84, 66), new Color("455763"));
        DrawString(_font, new Vector2(500, 198), "点名柜", HorizontalAlignment.Left, -1, 12, new Color("e8d9ad"));
        DrawCircle(new Vector2(160, 205), 13, new Color("7094a0"));
        DrawCircle(new Vector2(320, 120), 13, new Color("7094a0"));
        DrawCircle(new Vector2(475, 205), 13, new Color("7094a0"));
    }

    private void DrawArchive()
    {
        DrawRect(new Rect2(60, 62, 118, 72), new Color("6c624f"));
        DrawString(_font, new Vector2(70, 57), "归档规则", HorizontalAlignment.Left, -1, 12, new Color("e8d9ad"));
        var colors = new[] { "8d6c7e", "507d88", "b18355", "637b55" };
        for (var index = 0; index < 4; index++)
            DrawRect(new Rect2(170 + index * 29, 155, 22, 56), new Color(colors[index]));
        DrawRect(new Rect2(410, 112, 112, 92), new Color("4a555c"));
        DrawCircle(new Vector2(442, 151), 22, new Color("1c2730"));
        DrawCircle(new Vector2(490, 151), 22, new Color("1c2730"));
        DrawString(_font, new Vector2(425, 106), "旧录音机", HorizontalAlignment.Left, -1, 12, new Color("e8d9ad"));
    }

    private void DrawAuditorium()
    {
        DrawRect(new Rect2(60, 60, 520, 66), new Color("583f50"));
        DrawRect(new Rect2(78, 126, 484, 118), new Color("283846"));
        var board = ResourceLoader.Load<Texture2D>(
            "res://assets/generated/dream_escape/auditorium-control-board.png");
        if (board is not null)
        {
            DrawTextureRect(board, new Rect2(48, 62, 544, 232), false);
            return;
        }
        DrawString(_font, new Vector2(276, 92), "旧礼堂舞台", HorizontalAlignment.Left, -1, 16, new Color("e8d9ad"));
        DrawPanel(95, "372");
        DrawPanel(245, "♪");
        DrawPanel(395, "▣");
        DrawRect(new Rect2(490, 220, 24, 64), new Color("ba8a4e"));
        DrawRect(new Rect2(550, 220, 24, 64), new Color("ba8a4e"));
    }

    private void DrawDoor(float x, string label, Color color)
    {
        DrawRect(new Rect2(x, 104, 62, 115), color);
        DrawRect(new Rect2(x + 8, 116, 46, 103), new Color(color, 0.72f));
        DrawString(_font, new Vector2(x + 23, 144), label, HorizontalAlignment.Left, -1, 18, new Color("f4e4bd"));
    }

    private void DrawDesks()
    {
        for (var row = 0; row < 2; row++)
        for (var column = 0; column < 4; column++)
            DrawRect(new Rect2(205 + column * 65, 155 + row * 62, 46, 26), new Color("8a6948"));
    }

    private void DrawPanel(float x, string label)
    {
        DrawRect(new Rect2(x, 145, 112, 65), new Color("3e5860"));
        DrawString(_font, new Vector2(x + 40, 184), label, HorizontalAlignment.Left, -1, 18, new Color("f0c86f"));
    }

    private void DrawTriangle(Vector2 center, Color color) => DrawColoredPolygon(
        [center + new Vector2(0, -8), center + new Vector2(-8, 7), center + new Vector2(8, 7)], color);
}
