using Godot;

namespace TownRpg.Presentation;

public sealed class StatefulObjectVisual3D(IReadOnlyDictionary<string, Node3D> variants)
{
    public string CurrentVariant { get; private set; } = string.Empty;

    public void SetVariant(string variant)
    {
        if (!variants.ContainsKey(variant))
            throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown 3D visual variant.");
        foreach (var entry in variants)
            entry.Value.Visible = entry.Key == variant;
        CurrentVariant = variant;
    }
}
