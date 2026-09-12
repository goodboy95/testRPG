using Godot;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Presentation;

public partial class BattlePanel : Control
{
    public event Action<BattleAction>? ActionSelected;
    public event Action? RetrySelected;

    private Label _status = null!;
    private RichTextLabel _log = null!;
    private HBoxContainer _actions = null!;
    private Button _retry = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = UiStyles.Background };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        var frame = new VBoxContainer { Position = new Vector2(24, 14), Size = new Vector2(592, 332) };
        frame.AddThemeConstantOverride("separation", 8); AddChild(frame);
        var title = new Label
        {
            Text = "旧隧道 · 异常共鸣",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        UiStyles.Heading(title, 20);
        frame.AddChild(title);

        _status = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        UiStyles.Font(_status, 12);
        frame.AddChild(_status);
        var paper = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; UiStyles.PaperSurface(paper); frame.AddChild(paper);
        _log = new RichTextLabel
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            BbcodeEnabled = true,
            Text = "铁轨深处传来不属于机械的低鸣。你们不需要消灭它，只需找到让共鸣平静的方法。"
        };
        paper.AddChild(_log);

        _actions = new HBoxContainer
        {
            Position = new Vector2(24, 294),
            Size = new Vector2(592, 46),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        frame.AddChild(_actions);
        AddAction("观察节拍", BattleAction.Observe);
        AddAction("保护同伴", BattleAction.Guard);
        AddAction("请求安抚", BattleAction.Soothe);
        AddAction("打断共鸣", BattleAction.Disrupt);
        AddAction("撤退", BattleAction.Retreat);
        _retry = UiStyles.Button("立即重试");
        _retry.Pressed += () => RetrySelected?.Invoke();
        _retry.Visible = false;
        _actions.AddChild(_retry);
        Visible = false;
    }

    public void Open(ResonanceBattle battle)
    {
        Visible = true;
        _log.Text = "铁轨深处传来不属于机械的低鸣。你们不需要消灭它，只需找到让共鸣平静的方法。";
        _retry.Visible = false;
        SetActionsEnabled(true);
        UpdateStatus(battle);
        if (_actions.GetChildCount() > 0 && _actions.GetChild(0) is Button first)
        {
            first.GrabFocus();
        }
    }

    public void ApplyResult(BattleTurnResult result)
    {
        _log.AppendText($"\n\n[color=#754d32]第 {result.Turn} 回合[/color]\n{result.Message}");
        _status.Text = $"共鸣强度：{result.Resonance}/6　　意志：{result.Resolve}/6";
        if (result.Won || result.Retreated)
        {
            SetActionsEnabled(false);
        }

        if (result.Defeated)
        {
            ShowRetry();
        }
    }

    public void ClosePanel() => Visible = false;

    private void AddAction(string text, BattleAction action)
    {
        var button = UiStyles.Button(text);
        button.Pressed += () => ActionSelected?.Invoke(action);
        _actions.AddChild(button);
    }

    private void ShowRetry()
    {
        SetActionsEnabled(false);
        foreach (var child in _actions.GetChildren().OfType<Button>()) child.Visible = child == _retry;
        _retry.Visible = true;
        _retry.Disabled = false;
        _retry.GrabFocus();
    }

    private void SetActionsEnabled(bool enabled)
    {
        foreach (var child in _actions.GetChildren())
        {
            if (child is Button button)
            {
                button.Disabled = !enabled;
                if (enabled && button != _retry) button.Visible = true;
            }
        }

        if (_retry is not null)
        {
            _retry.Disabled = false;
        }
    }

    private void UpdateStatus(ResonanceBattle battle) =>
        _status.Text = $"共鸣强度：{battle.Resonance}/6　　意志：{battle.Resolve}/6";
}
