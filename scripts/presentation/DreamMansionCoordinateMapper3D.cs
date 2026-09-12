using Godot;

namespace TownRpg.Presentation;

public sealed class DreamMansionCoordinateMapper3D
{
    public Rect2 StoredBounds { get; } = new(0f, 0f, 640f, 360f);
    public Vector2 WorldXBounds { get; } = new(-MansionLayout3D.FoyerWidth / 2, MansionLayout3D.FoyerWidth / 2);
    public Vector2 WorldZBounds { get; } = new(-MansionLayout3D.FoyerDepth, MansionLayout3D.HallDepth);

    public Vector3 ToWorld(Vector2 storedPosition)
    {
        var normalizedLongitudinal = Mathf.InverseLerp(
            StoredBounds.Position.X, StoredBounds.End.X, storedPosition.X);
        var normalizedLateral = Mathf.InverseLerp(
            StoredBounds.Position.Y, StoredBounds.End.Y, storedPosition.Y);
        return new Vector3(
            Mathf.Lerp(WorldXBounds.X, WorldXBounds.Y, normalizedLateral),
            0f,
            Mathf.Lerp(WorldZBounds.X, WorldZBounds.Y, normalizedLongitudinal));
    }

    public Vector2 ToStored(Vector3 worldPosition)
    {
        var normalizedLongitudinal = Mathf.InverseLerp(
            WorldZBounds.X, WorldZBounds.Y, worldPosition.Z);
        var normalizedLateral = Mathf.InverseLerp(
            WorldXBounds.X, WorldXBounds.Y, worldPosition.X);
        return new Vector2(
            Mathf.Lerp(StoredBounds.Position.X, StoredBounds.End.X, normalizedLongitudinal),
            Mathf.Lerp(StoredBounds.Position.Y, StoredBounds.End.Y, normalizedLateral));
    }

    public Vector3 ClampWorld(Vector3 worldPosition) => new(
        Mathf.Clamp(worldPosition.X, WorldXBounds.X, WorldXBounds.Y),
        0f,
        Mathf.Clamp(worldPosition.Z, WorldZBounds.X, WorldZBounds.Y));
}
