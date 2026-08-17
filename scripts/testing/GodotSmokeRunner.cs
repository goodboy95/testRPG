using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Presentation;

namespace TownRpg.Testing;

public partial class GodotSmokeRunner : Node
{
    private static readonly string[] ScenePaths =
    [
        "res://scenes/app/Main.tscn",
        "res://scenes/world/OldHouse.tscn",
        "res://scenes/world/StationQuarter.tscn",
        "res://scenes/dream/DreamEscape.tscn",
        "res://scenes/dream/DreamCorridor.tscn",
        "res://scenes/dream/DreamClassroom.tscn",
        "res://scenes/dream/DreamMusicRoom.tscn",
        "res://scenes/dream/DreamArchiveRoom.tscn",
        "res://scenes/dream/DreamAuditorium.tscn"
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
                session.Content.Events.Events.Count == 0 ||
                session.Content.Items.Items.Count == 0)
            {
                throw new InvalidOperationException("Content catalogs were loaded but are empty.");
            }

            session.NewGame("冒烟测试", "同学");

            var captureDirectory = OS.GetCmdlineUserArgs()
                .FirstOrDefault(argument => argument.StartsWith("--capture-dream-art=", StringComparison.Ordinal))?
                ["--capture-dream-art=".Length..];
            if (!string.IsNullOrWhiteSpace(captureDirectory))
            {
                await CaptureDreamClassroomArt(session, Path.GetFullPath(captureDirectory));
                GD.Print("DREAM_ART_CAPTURE_PASS files=5");
                GetTree().Quit(0);
                return;
            }

            foreach (var item in session.Content.Items.Items)
            {
                if (ResourceLoader.Load<Texture2D>(item.IconPath) is null)
                {
                    throw new InvalidOperationException($"Could not load item icon '{item.IconPath}'.");
                }
            }
            if (ResourceLoader.Load<Texture2D>(
                    "res://assets/generated/dream_escape/lin-xiaoyu-neutral.png") is null)
            {
                throw new InvalidOperationException("Could not load Lin Xiaoyu dream portrait.");
            }
            var dreamVisuals = new[]
            {
                "res://assets/generated/dream_escape/lin-xiaoyu-walk-4x3.png",
                "res://assets/generated/dream_escape/class-photo-filter-closeup.png",
                "res://assets/generated/dream_escape/recorder-closeup.png",
                "res://assets/generated/dream_escape/auditorium-control-board.png",
                "res://assets/generated/dream_escape/pixel/school-interior-tiles-v1.png",
                "res://assets/generated/dream_escape/pixel/classroom-props-v1.png",
                "res://assets/generated/dream_escape/pixel/dream-ambient-v1.png",
                "res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png",
                "res://assets/generated/dream_escape/pixel/lin-xiaoyu-walk-4x4-v2.png",
                "res://assets/generated/dream_escape/experimental/classroom-original-source-v1.png",
                "res://assets/generated/dream_escape/experimental/classroom-original-background-v1.png",
                "res://assets/generated/dream_escape/experimental/classroom-original-occluders-v1.png"
            };
            foreach (var visual in dreamVisuals)
            {
                if (ResourceLoader.Load<Texture2D>(visual) is null)
                {
                    throw new InvalidOperationException($"Could not load dream visual '{visual}'.");
                }
            }
            if (ResourceLoader.Load<Shader>("res://assets/shaders/pixel_outline.gdshader") is null)
            {
                throw new InvalidOperationException("Could not load dream interaction outline shader.");
            }

            var protagonistSheet = ResourceLoader.Load<Texture2D>(
                "res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png")!;
            var frames = DreamCharacterFrames.Create(protagonistSheet);
            if (frames.GetFrameCount("down") != 4 || frames.GetFrameCount("up") != 4)
            {
                throw new InvalidOperationException("Dream character SpriteFrames layout is invalid.");
            }

            foreach (var scenePath in ScenePaths)
            {
                var packedScene = ResourceLoader.Load<PackedScene>(scenePath)
                    ?? throw new InvalidOperationException($"Could not load scene '{scenePath}'.");
                var instance = packedScene.Instantiate();
                AddChild(instance);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (scenePath == "res://scenes/dream/DreamClassroom.tscn")
                {
                    var visual = instance.GetNode<DreamOriginalClassroomVisual>("ClassroomVisual");
                    var markerCount = instance.GetNode("InteractionMarkers").GetChildren()
                        .OfType<DreamInteractionMarker>().Count();
                    var blockedMarkers = instance.GetNode("InteractionMarkers").GetChildren()
                        .OfType<DreamInteractionMarker>()
                        .Where(marker => visual.IsBlocked(marker.Position))
                        .Select(marker => marker.TargetId)
                        .ToArray();
                    var collisionNodes = visual.GetNode("CollisionBodies").GetChildren()
                        .OfType<StaticBody2D>().Count();
                    var player = instance.GetNode<PlayerController>("Player");
                    var playerShape = player.GetNode<CollisionShape2D>("CollisionShape2D").Shape;
                    if (markerCount != 9 || collisionNodes != 17 ||
                        visual.CollisionBodyCount != 17 || visual.FurnitureCollisionCount != 12 ||
                        playerShape is null || blockedMarkers.Length > 0)
                    {
                        throw new InvalidOperationException(
                            $"Original classroom composition invalid: collisions={collisionNodes} " +
                            $"furniture={visual.FurnitureCollisionCount} markers={markerCount} " +
                            $"playerShape={playerShape is not null} blockedMarkers={string.Join(',', blockedMarkers)}.");
                    }
                    await ProbeClassroomPhysics(player, visual);
                }
                instance.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            GD.Print($"GODOT_SMOKE_PASS scenes={ScenePaths.Length} " +
                     $"characters={session.Content.Characters.Characters.Count} " +
                     $"schedules={session.Content.Schedules.Entries.Count} " +
                     $"events={session.Content.Events.Events.Count} " +
                     $"items={session.Content.Items.Items.Count}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"GODOT_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task CaptureDreamClassroomArt(GameSession session, string outputDirectory)
    {
        DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamClassroom.tscn")
            ?? throw new InvalidOperationException("Could not load dream classroom for art capture.");
        var classroom = packed.Instantiate<DreamAreaController>();
        AddChild(classroom);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var visual = classroom.GetNode<DreamOriginalClassroomVisual>("ClassroomVisual");
        var dream = session.State.DreamEscape
            ?? throw new InvalidOperationException("Dream state missing during art capture.");

        visual.SetFocusedTarget("pickup:dream.chalk", false);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        visual.SetFocusedTarget(null, false);
        await SaveCapture(outputDirectory, "01-classroom-initial.png");
        dream.SolvedPuzzles.Add("dream.pickup.chalk");
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleBlackboard);
        visual.ApplyState(dream);
        await SaveCapture(outputDirectory, "02-classroom-blackboard.png");
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleTeacherDesk);
        visual.ApplyState(dream);
        await SaveCapture(outputDirectory, "03-classroom-teacher-desk.png");
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleMapCabinet);
        visual.ApplyState(dream);
        await SaveCapture(outputDirectory, "04-classroom-map-cabinet.png");
        visual.SetCollisionDebug(true);
        await SaveCapture(outputDirectory, "05-classroom-collisions.png");
        visual.SetCollisionDebug(false);
        classroom.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task ProbeClassroomPhysics(
        PlayerController player,
        DreamOriginalClassroomVisual visual)
    {
        if (!visual.IsBlocked(new Vector2(100, 300)) ||
            visual.IsBlocked(new Vector2(178, 260)) ||
            visual.IsBlocked(visual.ResolveSpawn(new Vector2(320, 280))))
        {
            throw new InvalidOperationException("Original classroom collision layout rejected its deterministic probes.");
        }

        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        player.Position = new Vector2(100, 250);
        if (!player.TestMove(player.Transform, new Vector2(0, 60)))
            throw new InvalidOperationException("Physics probe crossed a student desk.");
        player.Position = new Vector2(178, 250);
        if (player.TestMove(player.Transform, new Vector2(0, 60)))
            throw new InvalidOperationException("Physics probe blocked the central aisle.");
        player.Position = new Vector2(335, 190);
        if (!player.TestMove(player.Transform, new Vector2(0, 45)))
            throw new InvalidOperationException("Physics probe crossed the teacher desk.");
        player.Position = new Vector2(590, 220);
        if (player.TestMove(player.Transform, new Vector2(25, 0)))
            throw new InvalidOperationException("Physics probe blocked the right-side doorway.");
    }

    private async Task SaveCapture(string outputDirectory, string fileName)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(Path.Combine(outputDirectory, fileName));
        if (error != Error.Ok)
        {
            throw new IOException($"Could not save dream art capture '{fileName}': {error}");
        }
    }
}
