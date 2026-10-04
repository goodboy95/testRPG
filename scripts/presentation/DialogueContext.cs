using TownRpg.Core.Domain;
using TownRpg.Application;

namespace TownRpg.Presentation;

/// <summary>Optional instance-only context for previews. Default dialogue keeps the live session context.</summary>
public sealed class DialogueContext
{
    public string PlayerName { get; init; } = "小岚";
    public PlayerSettings Settings { get; init; } = new();
    public HashSet<string> ReadLines { get; } = new(StringComparer.Ordinal);
    public ModalGate Modal { get; } = new();
}
