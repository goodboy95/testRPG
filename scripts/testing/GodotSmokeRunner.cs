using Godot;
using TownRpg.Application;

namespace TownRpg.Testing;

public partial class GodotSmokeRunner : Node
{
    private static readonly string[] ScenePaths =
    [
        "res://scenes/app/Main.tscn",
        "res://scenes/world/OldHouse.tscn",
        "res://scenes/world/StationQuarter.tscn"
    ];

    public override void _Ready() => CallDeferred(MethodName.RunSmokeTest);

    private async void RunSmokeTest()
    {
        try
        {
            var session = GetNode<GameSession>("/root/GameSession");
            _ = GetNode<NarrativeBridge>("/root/Narrative");
            if (!string.IsNullOrEmpty(session.InitializationError))
            {
                throw new InvalidOperationException(
                    $"Content initialization failed: {session.InitializationError}");
            }

            if (session.Content.Characters.Characters.Count == 0 ||
                session.Content.Schedules.Entries.Count == 0 ||
                session.Content.Events.Events.Count == 0)
            {
                throw new InvalidOperationException("Content catalogs were loaded but are empty.");
            }

            foreach (var scenePath in ScenePaths)
            {
                var packedScene = ResourceLoader.Load<PackedScene>(scenePath)
                    ?? throw new InvalidOperationException($"Could not load scene '{scenePath}'.");
                var instance = packedScene.Instantiate();
                AddChild(instance);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                instance.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            GD.Print($"GODOT_SMOKE_PASS scenes={ScenePaths.Length} " +
                     $"characters={session.Content.Characters.Characters.Count} " +
                     $"schedules={session.Content.Schedules.Entries.Count} " +
                     $"events={session.Content.Events.Events.Count}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"GODOT_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }
}
