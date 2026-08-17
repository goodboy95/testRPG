using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class DreamAreaController : Node2D
{
    [Export] public string AreaId { get; set; } = DreamEscapeIds.AreaCorridor;

    public event Action<string>? InteractionRequested;
    public event Action<string>? PromptChanged;

    private GameSession _session = null!;
    private PlayerController _player = null!;
    private string _currentTargetId = string.Empty;
    private string _lastPrompt = "\0";
    private string _selectedItemId = string.Empty;
    private bool _useGamepadPrompts;
    private DreamClassroomVisual? _classroomVisual;
    private readonly Dictionary<string, Vector2> _markerPositions = new(StringComparer.Ordinal);

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        _player = GetNode<PlayerController>("Player");
        var dream = _session.State.DreamEscape
            ?? throw new InvalidOperationException("Dream area loaded without dream state.");
        _player.Position = new Vector2(dream.PositionX, dream.PositionY);
        _player.MovementBounds = new Rect2(24, 54, 592, 270);
        var protagonistTexture = ResourceLoader.Load<Texture2D>(
            "res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png");
        if (protagonistTexture is not null)
        {
            _player.ConfigureCharacterFrames(DreamCharacterFrames.Create(protagonistTexture));
        }
        if (GetNodeOrNull<DreamAreaPainter>("Painter") is { } painter)
        {
            painter.AreaId = AreaId;
            painter.QueueRedraw();
        }
        _classroomVisual = GetNodeOrNull<DreamClassroomVisual>("ClassroomVisual");
        _classroomVisual?.ApplySettings(_session.State.Settings);
        LoadInteractionMarkers();
        SpawnXiaoyuIfNeeded(dream);
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_session.Modal.IsBlocked)
        {
            SetPrompt(string.Empty);
            _classroomVisual?.SetFocusedTarget(null, _session.State.Settings.HighContrastPrompts);
            return;
        }

        var target = FindInteractionTarget();
        _currentTargetId = target?.Id ?? string.Empty;
        _classroomVisual?.SetFocusedTarget(target?.Id, _session.State.Settings.HighContrastPrompts);
        SetPrompt(target is null ? string.Empty : InteractionPrompt(target));
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_session.Modal.IsBlocked || string.IsNullOrEmpty(_currentTargetId))
        {
            return;
        }
        if (inputEvent.IsActionPressed("interact"))
        {
            GetViewport().SetInputAsHandled();
            InteractionRequested?.Invoke(_currentTargetId);
        }
    }

    public void SetGamepadPrompts(bool value)
    {
        _useGamepadPrompts = value;
        _lastPrompt = string.Empty;
    }

    public void SetSelectedItem(string itemId)
    {
        _selectedItemId = itemId;
        _lastPrompt = "\0";
    }

    public void ApplySettings(PlayerSettings settings)
    {
        _classroomVisual?.ApplySettings(settings);
    }

    private InteractionTarget? FindInteractionTarget()
    {
        return StaticInteractions()
            .Select(target => new
            {
                Target = target,
                Distance = _player.Position.DistanceTo(target.Position),
                Facing = Math.Max(0, _player.Facing.Dot(_player.Position.DirectionTo(target.Position))) * 10f
            })
            .Where(item => item.Distance <= 48f)
            .OrderBy(item => item.Distance - item.Facing)
            .ThenBy(item => item.Target.Id, StringComparer.Ordinal)
            .Select(item => item.Target)
            .FirstOrDefault();
    }

    private IEnumerable<InteractionTarget> StaticInteractions()
    {
        var dream = _session.State.DreamEscape!;
        if (AreaId == DreamEscapeIds.AreaCorridor)
        {
            yield return Target("area:dream.classroom", 92, 142, "进入旧教室");
            yield return dream.UnlockedAreas.Contains(DreamEscapeIds.AreaMusic)
                ? Target("area:dream.music", 235, 142, "进入音乐教室")
                : Target("target.corridor.room_two_door", 235, 142, "检查第二扇门");
            yield return dream.UnlockedAreas.Contains(DreamEscapeIds.AreaArchive)
                ? Target("area:dream.archive", 390, 142, "进入档案兼广播室")
                : Target("locked:room_three", 390, 142, "检查第三扇门");
            yield return dream.UnlockedAreas.Contains(DreamEscapeIds.AreaAuditorium)
                ? Target("area:dream.auditorium", 548, 142, "进入旧礼堂")
                : Target("puzzle:room_four_door", 548, 142, "检查第四扇门");
            yield break;
        }

        yield return Target("area:dream.corridor", 320, 310, "返回走廊");
        if (AreaId == DreamEscapeIds.AreaClassroom)
        {
            if (!dream.SolvedPuzzles.Contains("dream.pickup.chalk"))
                yield return Target("pickup:dream.chalk", 205, 135, "拿起窗台边的粉笔");
            yield return Target("target.classroom.blackboard", 320, 86, "查看黑板擦痕");
            if (dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleBlackboard))
            {
                yield return Target("observe:target.classroom.window", 105, 222, "观察旧窗格");
                yield return Target("observe:target.classroom.clock", 468, 92, "观察停住的时钟");
                yield return Target("observe:target.classroom.duty_roster", 555, 112, "查看值日表");
            }
            yield return Target("puzzle:teacher_desk", 520, 215, "检查教师桌密码锁");
            yield return dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleMapCabinet)
                ? Target("target.classroom.class_photo", 105, 112, "查看地图柜中的合影")
                : Target("target.classroom.map_cabinet", 105, 112, "检查三角螺丝地图柜");
        }
        else if (AreaId == DreamEscapeIds.AreaMusic)
        {
            yield return Target("target.music.score", 135, 105, "查看蒙尘乐谱");
            yield return Target("puzzle:music_cage", 520, 112, "操作器材间音板锁");
            if (dream.XiaoyuJoined)
            {
                yield return Target("observe:target.music.listen_window", 160, 205, "请晓雨在窗边辨听");
                yield return Target("observe:target.music.listen_lectern", 320, 120, "请晓雨在讲台旁辨听");
                yield return Target("observe:target.music.listen_door", 475, 205, "请晓雨在门边辨听");
                yield return Target("puzzle:listening", 320, 72, "排列四种乐器");
                yield return Target("hint:xiaoyu", 375, 250, "和林晓雨一起想想");
            }
            yield return Target("puzzle:attendance", 530, 235, "检查点名柜");
        }
        else if (AreaId == DreamEscapeIds.AreaArchive)
        {
            yield return Target("observe:target.archive.rules", 115, 92, "阅读社团归档规则");
            yield return Target("puzzle:archive_order", 205, 190, "排列四份社团档案");
            yield return Target("target.archive.recorder", 465, 172, "检查旧录音机");
            if (dream.XiaoyuJoined)
                yield return Target("hint:xiaoyu", 370, 250, "和林晓雨一起想想");
        }
        else if (AreaId == DreamEscapeIds.AreaAuditorium)
        {
            yield return Target("puzzle:final_desk", 135, 175, "操作数字控制板");
            yield return Target("puzzle:final_instruments", 285, 175, "操作乐器控制板");
            yield return Target("puzzle:final_broadcast", 435, 175, "操作广播控制板");
            yield return Target("action:left_lever", 505, 250, "请晓雨守住左侧机关");
            yield return Target("action:open_exit", 565, 250, "拉下右侧总闸");
            if (dream.XiaoyuJoined)
                yield return Target("hint:xiaoyu", 370, 270, "和林晓雨一起想想");
        }
    }

    private void SpawnXiaoyuIfNeeded(DreamEscapeState dream)
    {
        if (!dream.XiaoyuJoined || AreaId is DreamEscapeIds.AreaCorridor or DreamEscapeIds.AreaClassroom)
        {
            return;
        }
        var texture = ResourceLoader.Load<Texture2D>(
            "res://assets/generated/dream_escape/pixel/lin-xiaoyu-walk-4x4-v2.png");
        if (texture is not null)
        {
            AddChild(new AnimatedSprite2D
            {
                SpriteFrames = DreamCharacterFrames.Create(texture),
                Animation = "down",
                Frame = 0,
                Position = new Vector2(375, 250),
                ZIndex = 4
            });
            return;
        }

        var fallback = new NpcActor { Position = new Vector2(375, 250), ZIndex = 4 };
        fallback.Configure(CharacterIds.AbilityHolder, "林晓雨", new Color("3b8791"));
        AddChild(fallback);
    }

    private InteractionTarget Target(string id, float x, float y, string prompt) =>
        new(id, _markerPositions.GetValueOrDefault(id, new Vector2(x, y)), prompt);

    private void LoadInteractionMarkers()
    {
        _markerPositions.Clear();
        var host = GetNodeOrNull<Node>("InteractionMarkers");
        if (host is null) return;
        foreach (var marker in host.GetChildren().OfType<DreamInteractionMarker>())
        {
            if (string.IsNullOrWhiteSpace(marker.TargetId))
            {
                throw new InvalidOperationException($"Dream interaction marker '{marker.Name}' has no target ID.");
            }
            if (!_markerPositions.TryAdd(marker.TargetId, marker.Position))
            {
                throw new InvalidOperationException($"Duplicate dream interaction marker '{marker.TargetId}'.");
            }
        }
    }

    private string InteractionPrompt(InteractionTarget target)
    {
        var button = _useGamepadPrompts ? "A" : "E";
        if (!string.IsNullOrEmpty(_selectedItemId) && target.Id.StartsWith("target.", StringComparison.Ordinal))
        {
            return $"[{button}] 对目标使用：{DreamInventoryPanel.ItemName(_selectedItemId)}";
        }
        return $"[{button}] {target.Prompt}";
    }

    private void SetPrompt(string value)
    {
        if (_lastPrompt == value) return;
        _lastPrompt = value;
        PromptChanged?.Invoke(value);
    }

    private sealed record InteractionTarget(string Id, Vector2 Position, string Prompt);
}
