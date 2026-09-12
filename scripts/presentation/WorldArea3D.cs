using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class WorldArea3D : Node3D
{
    public event Action<string>? InteractionRequested;
    public event Action<string>? PromptChanged;

    public CameraControlMode CameraControlMode => CameraControlMode.Orbit;

    protected PlayerController3D Player { get; private set; } = null!;
    protected ThirdPersonCameraRig CameraRig { get; private set; } = null!;
    protected bool UseGamepadPrompts { get; private set; }
    public int InteractionMarkerCount => _markers.Count;
    public int ActiveInteractionMarkerCount => _markers.Count(IsMarkerActive);
    public IReadOnlyList<string> ActiveInteractionIds => _markers
        .Where(IsMarkerActive)
        .Select(marker => marker.TargetId)
        .ToArray();
    public string? CurrentInteractionTargetId => _focused?.TargetId;
    public Vector2? InteractionScreenPoint
    {
        get
        {
            if (_focused is null || CameraRig.Camera.IsPositionBehind(_focused.GlobalPosition)) return null;
            var viewport = GetViewport().GetVisibleRect().Size;
            return CameraRig.Camera.UnprojectPosition(_focused.GlobalPosition + Vector3.Up * .45f) / viewport * new Vector2(640, 360);
        }
    }

    private readonly List<InteractionMarker3D> _markers = [];
    private InteractionMarker3D? _focused;
    private string _lastPrompt = "\0";
    private bool _highContrast;
    private bool _runtimeInputLocked;

    protected virtual bool InputBlocked => false;
    protected virtual bool IsMarkerActive(InteractionMarker3D marker) => true;

    public override void _Ready()
    {
        Player = GetNode<PlayerController3D>("Player");
        CameraRig = GetNode<ThirdPersonCameraRig>("ThirdPersonCameraRig");
        Player.MovementCamera = CameraRig.Camera;
        CameraRig.SetFollowTarget(Player, Player.GetRid());
        var markerHost = GetNode<Node3D>("InteractionMarkers");
        foreach (var marker in markerHost.GetChildren().OfType<InteractionMarker3D>())
        {
            if (string.IsNullOrWhiteSpace(marker.TargetId))
                throw new InvalidOperationException($"3D interaction marker '{marker.Name}' has no target ID.");
            if (_markers.Any(existing => existing.TargetId == marker.TargetId))
                throw new InvalidOperationException($"Duplicate 3D interaction marker '{marker.TargetId}'.");
            _markers.Add(marker);
        }
    }

    public override void _ExitTree()
    {
        CameraRig?.SetInputActive(false);
    }

    public override void _Process(double delta)
    {
        _ = delta;
        var blocked = InputBlocked || _runtimeInputLocked;
        Player.InputEnabled = !blocked;
        CameraRig.SetInputActive(!blocked);
        if (blocked)
        {
            Focus(null);
            SetPrompt(string.Empty);
            return;
        }
        var next = FindInteractionTarget();
        Focus(next);
        SetPrompt(next is null ? string.Empty : BuildPrompt(next));
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (InputBlocked || _runtimeInputLocked) return;
        if (_focused is not null && inputEvent.IsActionPressed("interact"))
        {
            GetViewport().SetInputAsHandled();
            var target = _focused;
            var pose = target.TargetId.StartsWith("pickup:", StringComparison.Ordinal) ? "pickup" :
                target.TargetId.StartsWith("observe:", StringComparison.Ordinal) ? "observe" :
                target.TargetId == "hint:xiaoyu" ? "talk" : "operate";
            Player.PlayInteractionPose(pose, target.GlobalPosition);
            if (pose == "talk") GetNodeOrNull<DreamCompanion3D>("XiaoyuCompanion")?.PlayConversationPose(Player.GlobalPosition);
            InteractionRequested?.Invoke(target.TargetId);
        }
    }

    public virtual void SetGamepadPrompts(bool value)
    {
        UseGamepadPrompts = value;
        _lastPrompt = "\0";
    }

    public virtual void ApplySettings(PlayerSettings settings)
    {
        _highContrast = settings.HighContrastPrompts;
        CameraRig?.SetLookSensitivity(settings.CameraLookSensitivity);
        if (_focused is not null) _focused.SetFocused(true, _highContrast);
    }

    protected void SetRuntimeInputLocked(bool locked)
    {
        _runtimeInputLocked = locked;
        if (locked)
        {
            Player.InputEnabled = false;
            CameraRig.SetInputActive(false);
            Focus(null);
            SetPrompt(string.Empty);
        }
    }

    protected virtual string BuildPrompt(InteractionMarker3D marker) =>
        $"[{(UseGamepadPrompts ? "A" : "F")}] {(marker.Prompt.StartsWith("DREAM_V6_", StringComparison.Ordinal) ? DreamPrologueText.Get(marker.Prompt) : marker.Prompt)}";

    private InteractionMarker3D? FindInteractionTarget()
    {
        return _markers
            .Where(IsMarkerActive)
            .Select(marker => new
            {
                Marker = marker,
                Distance = FlatDistance(Player.GlobalPosition, marker.GlobalPosition),
                Facing = Math.Max(0f, Player.FacingWorld.Dot(
                    FlatDirection(Player.GlobalPosition, marker.GlobalPosition))) * 0.25f
            })
            .Where(item => item.Distance <= item.Marker.InteractionRange)
            .OrderBy(item => item.Distance - item.Facing)
            .ThenBy(item => item.Marker.TargetId, StringComparer.Ordinal)
            .Select(item => item.Marker)
            .FirstOrDefault();
    }

    private void Focus(InteractionMarker3D? marker)
    {
        if (_focused == marker) return;
        _focused?.SetFocused(false, _highContrast);
        _focused = marker;
        _focused?.SetFocused(true, _highContrast);
    }

    private void SetPrompt(string prompt)
    {
        if (_lastPrompt == prompt) return;
        _lastPrompt = prompt;
        PromptChanged?.Invoke(prompt);
    }

    private static float FlatDistance(Vector3 from, Vector3 to) =>
        new Vector2(from.X, from.Z).DistanceTo(new Vector2(to.X, to.Z));

    private static Vector3 FlatDirection(Vector3 from, Vector3 to)
    {
        var direction = new Vector3(to.X - from.X, 0f, to.Z - from.Z);
        return direction.LengthSquared() <= 0.0001f ? Vector3.Zero : direction.Normalized();
    }

}
