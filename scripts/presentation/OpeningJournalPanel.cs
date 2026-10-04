using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Presentation;

public partial class OpeningJournalPanel : Control
{
    public VillageOpeningService Service { get; init; } = null!;
    public Action? TrackingChanged { get; init; }
    public event Action? Closed;
    public OpeningJob SelectedJob { get; private set; }
    public int ScrollPosition => _surface.Scroll.ScrollVertical;
    private UiModalSurface _surface = null!;
    private ModalNavigation _navigation = null!;
    private HBoxContainer _jobs = null!;
    private Label _detail = null!;
    private VBoxContainer _targets = null!;
    private Button _track = null!;
    private readonly Dictionary<OpeningJob, Button> _buttons = [];

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        UiStyles.UseCinematic(this);
        var shade = new ColorRect { Color = new("101514b3") };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        _surface = new UiModalSurface { BaseSize = new(512, 300), ScaleHeight = false }; AddChild(_surface);
        _surface.Heading.Text = "周六事项";
        // Horizontal task tabs keep the details wide enough at 200% text scale.
        var tabs = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        _surface.Body.AddChild(tabs);
        _jobs = new HBoxContainer(); tabs.AddChild(_jobs);
        _detail = UiStyles.Wrapped(""); _surface.Body.AddChild(_detail);
        _targets = new VBoxContainer(); _surface.Body.AddChild(_targets);
        _track = UiStyles.Button("跟踪这件事"); _surface.Body.AddChild(_track);
        _track.Pressed += () =>
        {
            if (Service.State.Tracking?.Job == SelectedJob) Service.StopTracking(); else Service.Track(SelectedJob);
            TrackingChanged?.Invoke(); Select(SelectedJob); _track.GrabFocus();
        };
        var back = UiStyles.Button("返回"); back.Pressed += ClosePanel; _surface.Footer.AddChild(back);
        _navigation = new(this, GetNode<GameSession>("/root/GameSession").Modal, "opening-journal");
        Visible = false;
    }

    public void Open()
    {
        UiStyles.ApplyScale(this, GetNode<GameSession>("/root/GameSession").State.Settings.FontScale);
        UiStyles.Clear(_jobs); _buttons.Clear();
        foreach (var objective in Service.Objectives())
        {
            var job = objective.Job;
            var button = UiStyles.Button(objective.Title); button.Name = "Job" + job; button.ToggleMode = true;
            button.Pressed += () => Select(job); _jobs.AddChild(button); _buttons.Add(job, button);
        }
        _navigation.Open();
        Select(Service.State.Tracking?.Job ?? OpeningJob.Stool); _buttons[SelectedJob].GrabFocus();
    }

    public void Select(OpeningJob job)
    {
        if (!_buttons.ContainsKey(job)) job = OpeningJob.Stool;
        SelectedJob = job;
        var objective = Service.Objective(job);
        var tracking = Service.State.Tracking;
        foreach (var (id, button) in _buttons)
        { button.Text = (tracking?.Job == id ? "◆ " : "") + VillageOpeningService.Title(id); button.SetPressedNoSignal(id == job); }
        var deliveries = job == OpeningJob.Orders ? Service.State.Delivered : Service.State.Unloaded;
        var householdText = job == OpeningJob.Stool ? "" : "\n\n" + string.Join("\n", VillageOpeningIds.Houses.Select(h =>
            VillageOpeningService.HouseName(h) + "：" + (deliveries.Contains(h) ? "已经收货" : job == OpeningJob.Orders
                ? h switch { "house02" => "洗衣皂两块，住宅巷门廊", "house13" => "棉绳一卷，溪西岸晾晒院", _ => "毛巾两条，溪东岸菜畦外" }
                : "米一袋、面一袋")));
        _detail.Text = objective.Stage + $"\n约定酬金：{VillageOpeningIds.Rewards[job]} 元" + (job == OpeningJob.Cart ? "（沈砚另领 40 元）" : "") + householdText
            + $"\n\n村长代管的待领酬金：{Service.Payable} 元。当前余额：{Service.Balance} 元。";
        UiStyles.Clear(_targets);
        foreach (var target in objective.Targets)
        {
            var button = UiStyles.Button((tracking?.Job == job && tracking.Target == target.Id ? "◆ " : "") + target.Name);
            button.Name = "Target_" + target.Id.Replace(':', '_');
            button.Pressed += () => { Service.Track(job, target.Id); TrackingChanged?.Invoke(); Select(job); _track.GrabFocus(); };
            _targets.AddChild(button);
        }
        _track.Disabled = !objective.Trackable;
        _track.Text = tracking?.Job == job ? "取消跟踪" : "跟踪这件事";
        _surface.Scroll.ScrollVertical = 0;
    }
    public void ClosePanel() { _navigation.Close(); Closed?.Invoke(); }
    public override void _Input(InputEvent e)
    {
        if (!Visible) return;
        if (e.IsActionPressed("ui_cancel")) { GetViewport().SetInputAsHandled(); ClosePanel(); }
        var direction = UiStyles.PageDirection(e, this);
        if (direction != 0)
        {
            var jobs = _buttons.Keys.ToArray(); var index = Array.IndexOf(jobs, SelectedJob);
            Select(jobs[(index + direction + jobs.Length) % jobs.Length]); _buttons[SelectedJob].GrabFocus(); GetViewport().SetInputAsHandled();
        }
    }
    public override void _ExitTree() => _navigation.Dispose();
}
