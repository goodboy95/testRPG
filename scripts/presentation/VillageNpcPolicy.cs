using System.Text.Json;
using System.Text.RegularExpressions;

namespace TownRpg.Presentation;

public sealed record VillageNpcDefinition(string Id, string Building, float[][] Points, float Yaw,
    string Model, string Dialogue, string StartTitle, string Prompt, string VisualProfile = "pilot");

/// <summary>Engine-independent validation and distance rules for the preview content.</summary>
public static class VillageNpcPolicy
{
    public const string CatalogPath = "res://data/previews/village-npcs.json";
    public static VillageNpcDefinition[] Parse(string json)
    {
        var entries = JsonSerializer.Deserialize<VillageNpcDefinition[]>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        if (entries.Length != 2 || entries.Select(n => n.Id).Distinct().Count() != entries.Length)
            throw new InvalidDataException("The NPC pilot requires two unique residents.");
        foreach (var n in entries)
        {
            if (n.Id is not ("aunt_qiao" or "shen_yan") || n.Building is not ("shop" or "community") ||
                n.Points.Length == 0 || n.Points.Any(p => p.Length != 2 || p.Any(v => !float.IsFinite(v))) ||
                !float.IsFinite(n.Yaw) || string.IsNullOrWhiteSpace(n.Prompt) ||
                !Regex.IsMatch(n.StartTitle, "^[a-z_]+$") || n.VisualProfile != "pilot" ||
                !n.Model.StartsWith("res://assets/generated/village/npc-pilot/", StringComparison.Ordinal) ||
                !n.Dialogue.StartsWith("res://dialogue/previews/village_npcs/", StringComparison.Ordinal) ||
                n.Model.Contains("..", StringComparison.Ordinal) || n.Dialogue.Contains("..", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid village NPC: " + n.Id);
        }
        return entries;
    }

    public static int Lod(float distance, int previous, bool talking) => talking ? 0 :
        distance > 55 || previous == 2 && distance >= 50 ? 2 : distance > 25 ? 1 : 0;

    public static bool WithinReach(float distance, float height) => distance <= 2 && Math.Abs(height) <= .85f;

    // Deliberately supports only literal speech, choices, labels and jumps. No expression evaluator.
    public static IReadOnlyList<string> ValidateDialogue(string source)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var labels = new HashSet<string>(StringComparer.Ordinal);
        var jumps = new List<string>();
        foreach (var raw in source.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (Regex.IsMatch(line, "^~ [a-z_]+$"))
            { if (!labels.Add(line[2..])) throw new InvalidDataException("Duplicate dialogue label."); continue; }
            if (Regex.IsMatch(line, "^=> ([a-z_]+|END)$")) { jumps.Add(line[3..]); continue; }
            var match = Regex.Match(line, @"^(?:主角: |乔姨: |沈砚: |- )[^\[\]{}$]+ \[ID:(NPC_PILOT_[A-Z0-9_]+)\]$");
            if (!match.Success || !ids.Add(match.Groups[1].Value))
                throw new InvalidDataException("Unsupported or duplicate NPC dialogue line: " + line);
        }
        if (!labels.Contains("start") || jumps.Any(j => j != "END" && !labels.Contains(j)) || ids.Count == 0)
            throw new InvalidDataException("Missing NPC dialogue entry or jump destination.");
        return ids.ToArray();
    }
}
