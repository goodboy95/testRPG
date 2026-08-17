using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

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
