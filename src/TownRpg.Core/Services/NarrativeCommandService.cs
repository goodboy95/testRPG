using System.Globalization;
using TownRpg.Core.Domain;

namespace TownRpg.Core.Services;

public sealed class NarrativeCommandService(GameState state)
{
    private readonly RelationshipService _relationships = new(state);
    private readonly PromiseService _promises = new(state);

    public static readonly IReadOnlySet<string> AllowedCommands = new HashSet<string>(StringComparer.Ordinal)
    {
        "add_fact",
        "add_evidence",
        "add_lead",
        "timeline",
        "change_affinity",
        "change_trust",
        "remember",
        "create_promise",
        "resolve_promise",
        "raise_exposure",
        "complete_event",
        "grant_xp"
    };

    public static readonly IReadOnlyDictionary<string, int> AllowedArgumentCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["add_fact"] = 1,
            ["add_evidence"] = 1,
            ["add_lead"] = 1,
            ["timeline"] = 1,
            ["change_affinity"] = 3,
            ["change_trust"] = 3,
            ["remember"] = 3,
            ["create_promise"] = 4,
            ["resolve_promise"] = 2,
            ["raise_exposure"] = 1,
            ["complete_event"] = 1,
            ["grant_xp"] = 1
        };

    public void Execute(string command, params string[] arguments)
    {
        ValidateMutation(command, arguments);

        switch (command)
        {
            case "add_fact":
                RequireCount(command, arguments, 1);
                state.World.AddFact(arguments[0]);
                break;
            case "add_evidence":
                RequireCount(command, arguments, 1);
                state.World.AddEvidence(arguments[0]);
                break;
            case "add_lead":
                RequireCount(command, arguments, 1);
                state.Investigation.AddLead(arguments[0]);
                break;
            case "timeline":
                RequireCount(command, arguments, 1);
                state.Investigation.AddTimeline(state.World.Day, arguments[0]);
                break;
            case "change_affinity":
                RequireCount(command, arguments, 3);
                _relationships.GetOrCreate(arguments[0], arguments[1])
                    .ChangeAffinity(ParseInt(arguments[2]));
                break;
            case "change_trust":
                RequireCount(command, arguments, 3);
                _relationships.GetOrCreate(arguments[0], arguments[1])
                    .ChangeTrust(ParseInt(arguments[2]));
                break;
            case "remember":
                RequireCount(command, arguments, 3);
                _relationships.GetOrCreate(arguments[0], arguments[1]).Memories.Add(arguments[2]);
                break;
            case "create_promise":
                RequireCount(command, arguments, 4);
                _promises.Create(arguments[0], arguments[1], arguments[2], arguments[3]);
                break;
            case "resolve_promise":
                RequireCount(command, arguments, 2);
                _promises.Resolve(arguments[0], Enum.Parse<PromiseStatus>(arguments[1], true));
                break;
            case "raise_exposure":
                RequireCount(command, arguments, 1);
                state.World.RaiseExposure(ParseInt(arguments[0]));
                break;
            case "complete_event":
                RequireCount(command, arguments, 1);
                state.CompletedEvents.Add(arguments[0]);
                break;
            case "grant_xp":
                RequireCount(command, arguments, 1);
                state.Progression.GrantExperience(ParseInt(arguments[0]));
                break;
        }
    }

    public static void ValidateMutation(string command, IReadOnlyList<string> arguments)
    {
        if (!AllowedCommands.Contains(command))
        {
            throw new InvalidOperationException($"Narrative command '{command}' is not allowed.");
        }

        var required = AllowedArgumentCounts[command];
        if (arguments.Count != required)
        {
            throw new ArgumentException(
                $"Command '{command}' expects {required} arguments, received {arguments.Count}.");
        }

        if (arguments.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException($"Command '{command}' contains an empty argument.");
        }

        if (command is "change_affinity" or "change_trust")
        {
            _ = ParseInt(arguments[2]);
        }
        else if (command is "raise_exposure" or "grant_xp")
        {
            _ = ParseInt(arguments[0]);
        }
        else if (command == "resolve_promise")
        {
            var status = Enum.Parse<PromiseStatus>(arguments[1], true);
            if (status == PromiseStatus.Pending)
            {
                throw new ArgumentException("A promise cannot be resolved back to pending.");
            }
        }
    }

    public bool Evaluate(string condition, params string[] arguments)
    {
        return condition switch
        {
            "has_fact" when arguments.Length == 1 => state.World.HasFact(arguments[0]),
            "has_lead" when arguments.Length == 1 => state.Investigation.Leads.Contains(arguments[0]),
            "event_complete" when arguments.Length == 1 => state.CompletedEvents.Contains(arguments[0]),
            "trust_at_least" when arguments.Length == 3 =>
                (FindRelationship(arguments[0], arguments[1])?.Trust ?? 0) >= ParseInt(arguments[2]),
            "affinity_at_least" when arguments.Length == 3 =>
                (FindRelationship(arguments[0], arguments[1])?.Affinity ?? 0) >= ParseInt(arguments[2]),
            "day_at_least" when arguments.Length == 1 => state.World.Day >= ParseInt(arguments[0]),
            _ => throw new InvalidOperationException($"Unknown or malformed condition '{condition}'.")
        };
    }

    private static int ParseInt(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    private RelationshipState? FindRelationship(string sourceId, string targetId) =>
        state.Relationships.FirstOrDefault(relationship =>
            relationship.SourceId == sourceId && relationship.TargetId == targetId);

    private static void RequireCount(string command, string[] arguments, int required)
    {
        if (arguments.Length != required)
        {
            throw new ArgumentException(
                $"Command '{command}' expects {required} arguments, received {arguments.Length}.");
        }
    }
}
