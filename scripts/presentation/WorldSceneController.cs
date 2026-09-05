using Godot;
using TownRpg.Application;
using TownRpg.Core.Content;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Presentation;

public partial class WorldSceneController : Node2D
{
    [Export] public string LocationId { get; set; } = "old_house";

    public event Action<string>? InteractionRequested;
    public event Action<string>? PromptChanged;

    private readonly Dictionary<string, NpcActor> _actors = new(StringComparer.Ordinal);
    private GameSession _session = null!;
    private PlayerController _player = null!;
    private ScheduleService _schedules = null!;
    private string _currentTargetId = string.Empty;
    private string _lastPrompt = string.Empty;
    private bool _useGamepadPrompts;

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        _player = GetNode<PlayerController>("Player");
        _schedules = new ScheduleService(_session.Content.Schedules.Entries);

        var painter = GetNode<WorldPainter>("Painter");
        painter.LocationId = LocationId;
        painter.QueueRedraw();

        _player.Position = new Vector2(
            _session.State.Player.PositionX,
            _session.State.Player.PositionY);
        _player.MovementBounds = LocationId == "old_house"
            ? new Rect2(42, 58, 556, 246)
            : new Rect2(18, 42, 604, 290);

        SpawnActors();
        RefreshActors();
        _session.StateChanged += RefreshActors;
    }

    public override void _ExitTree()
    {
        if (_session is not null)
        {
            _session.StateChanged -= RefreshActors;
        }
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_session.Modal.IsBlocked)
        {
            SetPrompt(string.Empty);
            return;
        }

        var target = FindInteractionTarget();
        _currentTargetId = target?.Id ?? string.Empty;
        SetPrompt(target is null
            ? string.Empty
            : $"[{(_useGamepadPrompts ? "A" : "F")}] {target.Prompt}");

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

    public void MovePlayerTo(Vector2 position)
    {
        _player.Position = position;
        _session.State.Player.PositionX = position.X;
        _session.State.Player.PositionY = position.Y;
    }

    public void MovePlayerToSafePoint()
    {
        MovePlayerTo(LocationId == "old_house" ? new Vector2(320, 250) : new Vector2(310, 190));
    }

    public void SetGamepadPrompts(bool useGamepad)
    {
        _useGamepadPrompts = useGamepad;
        _lastPrompt = string.Empty;
    }

    private void SpawnActors()
    {
        foreach (var character in _session.Content.Characters.Characters)
        {
            if (character.Id == CharacterIds.Protagonist)
            {
                continue;
            }

            var actor = new NpcActor { Visible = false };
            actor.Configure(
                character.Id,
                TranslateOrFallback(character.NameKey, CharacterName(character.Id)),
                new Color(character.ColorHex));
            AddChild(actor);
            _actors[character.Id] = actor;
        }
    }

    private void RefreshActors()
    {
        if (!IsInsideTree())
        {
            return;
        }

        foreach (var pair in _actors)
        {
            var entry = _schedules.Current(pair.Key, _session.State.World);
            var visible = entry is not null && MapForScheduleLocation(entry.LocationId) == LocationId;
            pair.Value.Visible = visible;
            if (visible)
            {
                pair.Value.Position = PositionFor(entry!.LocationId, pair.Key);
            }
        }
    }

    private InteractionTarget? FindInteractionTarget()
    {
        var candidates = new List<InteractionTarget>();
        foreach (var actor in _actors.Values.Where(actor => actor.Visible))
        {
            candidates.Add(new InteractionTarget(
                $"npc:{actor.NpcId}",
                actor.Position,
                $"与{actor.DisplayName}交谈"));
        }

        candidates.AddRange(StaticInteractions());
        return candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Distance = _player.Position.DistanceTo(candidate.Position),
                Facing = FacingScore(candidate.Position)
            })
            .Where(item => item.Distance <= 44f)
            .OrderBy(item => item.Distance - item.Facing)
            .ThenBy(item => item.Candidate.Id, StringComparer.Ordinal)
            .Select(item => item.Candidate)
            .FirstOrDefault();
    }

    private float FacingScore(Vector2 targetPosition)
    {
        var direction = _player.Position.DirectionTo(targetPosition);
        return Mathf.Max(0, _player.Facing.Dot(direction)) * 10f;
    }

    private IEnumerable<InteractionTarget> StaticInteractions()
    {
        if (LocationId == "old_house")
        {
            yield return new InteractionTarget("bed", new Vector2(150, 140), "休息到第二天");
            yield return new InteractionTarget("exit_house", new Vector2(320, 296), "前往镇上");
            yield return new InteractionTarget("desk", new Vector2(515, 135), "查看旧书桌");
            yield break;
        }

        yield return new InteractionTarget("enter_house", new Vector2(80, 264), "回到旧宅");
        yield return new InteractionTarget("railway_notice", new Vector2(438, 137), "阅读工程告示");
        yield return new InteractionTarget("old_rail", new Vector2(585, 84), "靠近废弃隧道");
    }

    private static string MapForScheduleLocation(string scheduleLocation) =>
        scheduleLocation == "old_house" ? "old_house" : "station_quarter";

    private static Vector2 PositionFor(string scheduleLocation, string npcId)
    {
        var basePosition = scheduleLocation switch
        {
            "station_square" => new Vector2(245, 158),
            "station_platform" => new Vector2(260, 170),
            "station_market" => new Vector2(442, 194),
            "shop" => new Vector2(472, 204),
            "riverbank" => new Vector2(462, 318),
            "school_archive" => new Vector2(132, 104),
            "school_gate" => new Vector2(132, 104),
            "residential_lane" => new Vector2(150, 188),
            "abandoned_railway" => new Vector2(510, 112),
            "old_rail" => new Vector2(520, 116),
            _ => new Vector2(350, 184)
        };
        var offset = npcId switch
        {
            CharacterIds.AbilityHolder => new Vector2(0, 0),
            CharacterIds.KnowledgeFriend => new Vector2(42, 2),
            CharacterIds.TokenHolder => new Vector2(84, -2),
            CharacterIds.Shopkeeper => new Vector2(12, 0),
            _ => new Vector2(0, 22)
        };
        return basePosition + offset;
    }

    private void SetPrompt(string prompt)
    {
        if (_lastPrompt == prompt)
        {
            return;
        }

        _lastPrompt = prompt;
        PromptChanged?.Invoke(prompt);
    }

    private string TranslateOrFallback(string key, string fallback)
    {
        var translated = Tr(key);
        return translated == key ? fallback : translated;
    }

    public static string CharacterName(string id) => id switch
    {
        CharacterIds.AbilityHolder => "林晓雨",
        CharacterIds.KnowledgeFriend => "周明",
        CharacterIds.TokenHolder => "沈砚",
        CharacterIds.Shopkeeper => "乔姨",
        CharacterIds.Doctor => "何医生",
        CharacterIds.Teacher => "温老师",
        CharacterIds.Reporter => "罗记者",
        CharacterIds.SiteWorker => "彭师傅",
        _ => id
    };

    private sealed record InteractionTarget(string Id, Vector2 Position, string Prompt);
}
