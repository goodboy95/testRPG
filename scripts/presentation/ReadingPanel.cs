using Godot;
using TownRpg.Application;

namespace TownRpg.Presentation;

public partial class ReadingPanel : Control
{
    public bool PlainText { get; set; }
    public bool ControllerScroll { get; init; }
    public string? HeadingOverride { get; init; }
    public int ScrollPosition => _document.Scroll.ScrollVertical;
    private float _scrollRemainder;
    public ModalGate? Modal { get; init; }
    private UiModalSurface _surface = null!;
    private ModalNavigation _navigation = null!;
    private Button _back = null!;
    private PaperDocument _document = null!;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color("101514cc") };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        _surface = new UiModalSurface { BaseSize = PlainText ? new Vector2(432, 278) : new Vector2(568, 326), ScaleHeight = false }; AddChild(_surface);
        _surface.Scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _surface.Body.SizeFlagsVertical = SizeFlags.ExpandFill;
        _document = new PaperDocument { UsePaper = !PlainText, SizeFlagsVertical = SizeFlags.ExpandFill }; _surface.Body.AddChild(_document);
        _back = UiStyles.Button(UiText.Get("UI_BACK")); _back.Pressed += ClosePanel; _surface.Footer.AddChild(_back);
        _navigation = new ModalNavigation(this, Modal ?? GetNode<GameSession>("/root/GameSession").Modal, "reading");
        Visible = false;
    }
    public void Open(string title, string text, string? imagePath = null, Action? returnTo = null)
    {
        _surface.Heading.Text = HeadingOverride ?? UiText.Get("UI_READING");
        _document.ShowDocument(title, text, imagePath: imagePath);
        _surface.Scroll.ScrollVertical = 0;
        _navigation.Open(returnTo); _back.GrabFocus();
    }
    public void ClosePanel() => _navigation.Close();
    public override void _Process(double delta)
    {
        if (!Visible || !ControllerScroll || !GetWindow().HasFocus()) return;
        _scrollRemainder += Input.GetAxis("move_up","move_down") * (float)delta * 180;
        var pixels = (int)_scrollRemainder;
        _scrollRemainder -= pixels;
        _document.Scroll.ScrollVertical += pixels;
    }
    public override void _Input(InputEvent e)
    {
        if (Visible && e.IsActionPressed("ui_cancel")) { GetViewport().SetInputAsHandled(); ClosePanel(); }
    }
    public override void _ExitTree() => _navigation.Dispose();
}
