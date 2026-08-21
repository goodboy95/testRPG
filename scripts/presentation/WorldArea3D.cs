using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class WorldArea3D : Node3D
{
    public event Action<string>? InteractionRequested;
    public event Action<string>? PromptChanged;

    public bool SupportsQuarterTurnCamera => true;

    protected PlayerController3D Player { get; private set; } = null!;
    protected QuarterTurnCameraRig CameraRig { get; private set; } = null!;
    protected bool UseGamepadPrompts { get; private set; }
    protected string SelectedItemId { get; private set; } = string.Empty;

    private readonly List<InteractionMarker3D> _markers = [];
    private InteractionMarker3D? _focused;
    private string _lastPrompt = "\0";
    private bool _highContrast;

    protected virtual bool InputBlocked => false;
    protected virtual bool IsMarkerActive(InteractionMarker3D marker) => true;

    public override void _Ready()
    {
        Player = GetNode<PlayerController3D>("Player");
        CameraRig = GetNode<QuarterTurnCameraRig>("QuarterTurnCameraRig");
        Player.MovementCamera = CameraRig.Camera;
        CameraRig.RotationStarted += OnCameraRotationStarted;
        CameraRig.RotationCompleted += OnCameraRotationCompleted;
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
        if (CameraRig is null) return;
        CameraRig.RotationStarted -= OnCameraRotationStarted;
        CameraRig.RotationCompleted -= OnCameraRotationCompleted;
    }

    public override void _Process(double delta)
    {
        _ = delta;
        var blocked = InputBlocked || CameraRig.IsRotating;
        Player.InputEnabled = !blocked;
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
        if (InputBlocked) return;
        if (inputEvent.IsActionPressed("camera_rotate_left"))
        {
            if (CameraRig.TryRotate(-1)) GetViewport().SetInputAsHandled();
            return;
        }
        if (inputEvent.IsActionPressed("camera_rotate_right"))
        {
            if (CameraRig.TryRotate(1)) GetViewport().SetInputAsHandled();
            return;
        }
        if (!CameraRig.IsRotating && _focused is not null && inputEvent.IsActionPressed("interact"))
        {
            GetViewport().SetInputAsHandled();
            InteractionRequested?.Invoke(_focused.TargetId);
        }
    }

    public virtual void SetGamepadPrompts(bool value)
    {
        UseGamepadPrompts = value;
        _lastPrompt = "\0";
    }

    public virtual void SetSelectedItem(string itemId)
    {
        SelectedItemId = itemId;
        _lastPrompt = "\0";
    }

    public virtual void ApplySettings(PlayerSettings settings)
    {
        _highContrast = settings.HighContrastPrompts;
        if (_focused is not null) _focused.SetFocused(true, _highContrast);
    }

    protected virtual string BuildPrompt(InteractionMarker3D marker) =>
        $"[{(UseGamepadPrompts ? "A" : "F")}] {marker.Prompt}";

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

    private void OnCameraRotationStarted(int from, int to)
    {
        _ = from;
        _ = to;
        Player.InputEnabled = false;
        Focus(null);
        SetPrompt(string.Empty);
    }

    private void OnCameraRotationCompleted(int viewIndex)
    {
        _ = viewIndex;
        Player.InputEnabled = !InputBlocked;
    }
}
