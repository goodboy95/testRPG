using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public enum CameraControlMode
{
    None,
    Orbit
}

public interface IDreamAreaRuntime
{
    event Action<string>? InteractionRequested;
    event Action<string>? PromptChanged;

    CameraControlMode CameraControlMode { get; }
    void SetGamepadPrompts(bool value);
    void SetSelectedItem(string itemId);
    void ApplySettings(PlayerSettings settings);
    void RefreshState();
}

public interface IDreamAreaExitTransition
{
    Task PlayExitTransitionAsync(string targetId, CancellationToken cancellationToken);
}

public interface IDreamAreaVisual
{
    void ApplyState(DreamEscapeState state);
    void ApplySettings(PlayerSettings settings);
    void SetFocusedTarget(string? targetId, bool highContrast);
}

public interface IDreamCollisionLayout
{
    Rect2 MovementBounds { get; }
    int CollisionBodyCount { get; }
    int FurnitureCollisionCount { get; }
    bool IsBlocked(Vector2 playerPosition);
    Vector2 ResolveSpawn(Vector2 requestedPosition);
}
