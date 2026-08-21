using Godot;

namespace TownRpg.Presentation;

public sealed class AreaCoordinateMapper3D(
    Rect2 storedBounds,
    Rect2 worldBounds,
    float worldY = 0f)
{
    public Rect2 StoredBounds { get; } = storedBounds;
    public Rect2 WorldBounds { get; } = worldBounds;
    public float WorldY { get; } = worldY;

    public Vector3 ToWorld(Vector2 storedPosition)
    {
        var normalized = new Vector2(
            Mathf.InverseLerp(StoredBounds.Position.X, StoredBounds.End.X, storedPosition.X),
            Mathf.InverseLerp(StoredBounds.Position.Y, StoredBounds.End.Y, storedPosition.Y));
        return new Vector3(
            Mathf.Lerp(WorldBounds.Position.X, WorldBounds.End.X, normalized.X),
            WorldY,
            Mathf.Lerp(WorldBounds.Position.Y, WorldBounds.End.Y, normalized.Y));
    }

    public Vector2 ToStored(Vector3 worldPosition)
    {
        var normalized = new Vector2(
            Mathf.InverseLerp(WorldBounds.Position.X, WorldBounds.End.X, worldPosition.X),
            Mathf.InverseLerp(WorldBounds.Position.Y, WorldBounds.End.Y, worldPosition.Z));
        return new Vector2(
            Mathf.Lerp(StoredBounds.Position.X, StoredBounds.End.X, normalized.X),
            Mathf.Lerp(StoredBounds.Position.Y, StoredBounds.End.Y, normalized.Y));
    }

    public Vector3 ClampWorld(Vector3 worldPosition) => new(
        Mathf.Clamp(worldPosition.X, WorldBounds.Position.X, WorldBounds.End.X),
        WorldY,
        Mathf.Clamp(worldPosition.Z, WorldBounds.Position.Y, WorldBounds.End.Y));
}
