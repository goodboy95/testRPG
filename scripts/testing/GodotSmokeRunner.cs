using Godot;
using System.Globalization;
using System.Text;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Presentation;

namespace TownRpg.Testing;

public partial class GodotSmokeRunner : Node
{
    private const float AdoptedCameraFov = 60f;
    private const float AdoptedCameraSpringLength = 3.4f;
    private const float AdoptedCameraPitch = -21f;
    private const float AdoptedCameraFollowHeight = 1.30f;
    private const float AdoptedCameraShoulderOffset = 0.22f;

    private sealed record CameraFramingPreset(
        string Id,
        string Label,
        float FieldOfView,
        float SpringLength,
        float PitchDegrees,
        float FollowTargetHeight,
        float ShoulderOffset);

    private sealed record CharacterProjectionMetrics(
        float BodyHeightPixels,
        float BodyHeightRatio,
        float VisibleHeightPixels,
        float VisibleHeightRatio,
        float HeadRatio,
        float FeetRatio,
        float VisibleFraction);

    private static readonly CameraFramingPreset[] CameraFramingPresets =
    [
        new("previous", "Previous", 55f, 3.0f, -18f, 1.50f, 0.35f),
        new("adopted", "Adopted A", AdoptedCameraFov, AdoptedCameraSpringLength,
            AdoptedCameraPitch, AdoptedCameraFollowHeight, AdoptedCameraShoulderOffset),
        new("distant", "Distant B", 60f, 4.0f, -22f, 1.35f, 0.22f)
    ];

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
            using var persistenceSuppression = session.SuppressPersistence();
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
            if (session.State.DreamEscape is not { } newDream ||
                newDream.CurrentAreaId != DreamEscapeIds.AreaClassroom ||
                Math.Abs(newDream.PositionX - DreamEscapeState.NewGameClassroomPositionX) > 0.001f ||
                Math.Abs(newDream.PositionY - DreamEscapeState.NewGameClassroomPositionY) > 0.001f)
            {
                throw new InvalidOperationException("New game no longer starts at the adopted classroom safe point.");
            }
            ValidateInputBindings();
            ValidateDisplaySettings();

            var userArguments = OS.GetCmdlineUserArgs();
            if (userArguments.Contains("--profile-classroom-3d", StringComparer.Ordinal))
            {
                await ProfileDreamClassroom3D();
                GetTree().Quit(0);
                return;
            }

            var captureDirectory = userArguments
                .FirstOrDefault(argument => argument.StartsWith("--capture-classroom-3d=", StringComparison.Ordinal))?
                ["--capture-classroom-3d=".Length..];
            if (!string.IsNullOrWhiteSpace(captureDirectory))
            {
                await CaptureDreamClassroom3D(session, Path.GetFullPath(captureDirectory));
                GD.Print("CLASSROOM_3D_CAPTURE_PASS files=15 size=1920x1080");
                GetTree().Quit(0);
                return;
            }

            var mansionCaptureDirectory = userArguments
                .FirstOrDefault(argument => argument.StartsWith(
                    "--capture-dream-manor=", StringComparison.Ordinal))?
                ["--capture-dream-manor=".Length..];
            if (!string.IsNullOrWhiteSpace(mansionCaptureDirectory))
            {
                await CaptureDreamMansion3D(
                    session,
                    Path.GetFullPath(mansionCaptureDirectory));
                GD.Print("DREAM_MANOR_GODOT_CAPTURE_PASS files=9 size=1920x1080");
                GetTree().Quit(0);
                return;
            }

            var musicCaptureDirectory = userArguments
                .FirstOrDefault(argument => argument.StartsWith(
                    "--capture-music-room=", StringComparison.Ordinal))?
                ["--capture-music-room=".Length..];
            if (!string.IsNullOrWhiteSpace(musicCaptureDirectory))
            {
                await CaptureDreamMusicRoom3D(
                    session,
                    Path.GetFullPath(musicCaptureDirectory));
                GD.Print("DREAM_MUSIC_ROOM_CAPTURE_PASS files=4 size=1920x1080");
                GetTree().Quit(0);
                return;
            }

            var lateRoomsCaptureDirectory = userArguments
                .FirstOrDefault(argument => argument.StartsWith(
                    "--capture-dream-late-rooms=", StringComparison.Ordinal))?
                ["--capture-dream-late-rooms=".Length..];
            if (!string.IsNullOrWhiteSpace(lateRoomsCaptureDirectory))
            {
                await CaptureDreamLateRooms3D(
                    session,
                    Path.GetFullPath(lateRoomsCaptureDirectory));
                GD.Print("DREAM_LATE_ROOMS_CAPTURE_PASS rooms=2 files=24 size=1920x1080");
                GetTree().Quit(0);
                return;
            }

            var framingCaptureDirectory = userArguments
                .FirstOrDefault(argument => argument.StartsWith(
                    "--capture-classroom-framing=", StringComparison.Ordinal))?
                ["--capture-classroom-framing=".Length..];
            if (!string.IsNullOrWhiteSpace(framingCaptureDirectory))
            {
                await CaptureClassroomFramingComparison(
                    session,
                    Path.GetFullPath(framingCaptureDirectory));
                GD.Print("CLASSROOM_CAMERA_FRAMING_CAPTURE_PASS images=6 reports=2");
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
                "res://assets/generated/dream_escape/experimental/classroom-original-occluders-v1.png",
                "res://assets/generated/dream_escape/3d/textures-v2/classroom-shell-albedo.png",
                "res://assets/generated/dream_escape/3d/textures-v2/classroom-shell-normal.png",
                "res://assets/generated/dream_escape/3d/textures-v2/classroom-shell-orm.png",
                "res://assets/generated/dream_escape/3d/textures-v2/classroom-furniture-albedo.png",
                "res://assets/generated/dream_escape/3d/textures-v2/classroom-furniture-normal.png",
                "res://assets/generated/dream_escape/3d/textures-v2/classroom-furniture-orm.png",
                "res://assets/generated/dream_escape/3d/textures-v2/classroom-details.png",
                "res://assets/generated/dream_escape/3d/textures-v2/window-exterior.png"
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
            if (ResourceLoader.Load<Shader>("res://assets/shaders/wall_cutaway.gdshader") is null ||
                ResourceLoader.Load<PackedScene>(
                    "res://assets/generated/dream_escape/3d/classroom-art-pass-v5.glb") is null)
            {
                throw new InvalidOperationException("Could not load 3D classroom GLB or cutaway shader.");
            }
            if (ResourceLoader.Load<PackedScene>(
                    "res://assets/generated/dream_escape/3d/dream-manor-hall-v2.glb") is null ||
                ResourceLoader.Load<PackedScene>(
                    "res://assets/generated/dream_escape/3d/dream-manor-exterior-v1.glb") is null)
            {
                throw new InvalidOperationException("Could not load dream manor hall or offline exterior GLB.");
            }
            var corridorSceneSource = Godot.FileAccess.GetFileAsString(
                "res://scenes/dream/DreamCorridor.tscn");
            var corridorDependencies = ResourceLoader.GetDependencies(
                "res://scenes/dream/DreamCorridor.tscn");
            if (corridorSceneSource.Contains("dream-manor-exterior-v1.glb", StringComparison.Ordinal) ||
                corridorDependencies.Any(dependency =>
                    dependency.Contains("dream-manor-exterior-v1.glb", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Runtime dream mansion scene loaded the offline exterior GLB.");
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
                if (scenePath == "res://scenes/dream/DreamCorridor.tscn")
                {
                    var route = DreamAreaSpawnCatalog.Resolve(
                        DreamEscapeIds.AreaClassroom,
                        DreamEscapeIds.AreaCorridor);
                    var move = session.MoveDreamToArea(
                        DreamEscapeIds.AreaCorridor,
                        route.StoredPosition.X,
                        route.StoredPosition.Y);
                    if (!move.Changed)
                        throw new InvalidOperationException("Could not stage the mansion doorway spawn.");
                }
                else if (scenePath == "res://scenes/dream/DreamClassroom.tscn")
                {
                    var move = session.MoveDreamToArea(
                        DreamEscapeIds.AreaClassroom,
                        DreamEscapeState.NewGameClassroomPositionX,
                        DreamEscapeState.NewGameClassroomPositionY);
                    if (!move.Changed)
                        throw new InvalidOperationException("Could not restore the adopted classroom start.");
                }
                else if (scenePath == "res://scenes/dream/DreamMusicRoom.tscn")
                {
                    session.State.DreamEscape!.UnlockedAreas.Add(DreamEscapeIds.AreaMusic);
                    var route = DreamAreaSpawnCatalog.Resolve(
                        DreamEscapeIds.AreaCorridor,
                        DreamEscapeIds.AreaMusic);
                    var move = session.MoveDreamToArea(
                        DreamEscapeIds.AreaMusic,
                        route.StoredPosition.X,
                        route.StoredPosition.Y);
                    if (!move.Changed)
                        throw new InvalidOperationException("Could not stage the music-room doorway spawn.");
                }

                var packedScene = ResourceLoader.Load<PackedScene>(scenePath)
                    ?? throw new InvalidOperationException($"Could not load scene '{scenePath}'.");
                var instance = packedScene.Instantiate();
                AddChild(instance);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (scenePath == "res://scenes/app/Main.tscn")
                {
                    await ValidateDreamPauseAndExit(instance as MainController
                        ?? throw new InvalidOperationException("Main scene did not create MainController."));
                }
                else if (scenePath == "res://scenes/dream/DreamEscape.tscn")
                {
                    await ValidateDreamInventoryShortcuts(instance as DreamEscapeController
                        ?? throw new InvalidOperationException("Dream root did not create DreamEscapeController."));
                }
                else if (scenePath == "res://scenes/dream/DreamCorridor.tscn")
                {
                    var mansion = instance as DreamMansionController3D
                        ?? throw new InvalidOperationException("Dream corridor is not the 3D dream mansion.");
                    await ValidateDreamMansion3D(session, mansion);
                }
                else if (scenePath == "res://scenes/dream/DreamClassroom.tscn")
                {
                    var classroom = instance as DreamAreaController3D
                        ?? throw new InvalidOperationException("Dream classroom is not a 3D dream area.");
                    var visual = classroom.ClassroomVisual;
                    var markers = instance.GetNode("InteractionMarkers").GetChildren()
                        .OfType<InteractionMarker3D>().ToArray();
                    var markerCount = markers.Length;
                    var blockedMarkers = markers
                        .Where(marker => visual.IsBlocked(marker.GlobalPosition))
                        .Select(marker => marker.TargetId)
                        .ToArray();
                    var collisionNodes = visual.GetNode("CollisionBodies").GetChildren()
                        .OfType<StaticBody3D>().Count();
                    var reflectionProbes = instance.GetChildren().OfType<ReflectionProbe>().Count();
                    var player = classroom.PlayerActor;
                    ValidateDefaultCharacterVisual(player);
                    var playerShape = player.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
                    var markerById = markers.ToDictionary(marker => marker.TargetId, StringComparer.Ordinal);
                    var latchMarker = markerById["puzzle:window_latches"];
                    var clueMarker = markerById["observe:target.classroom.maintenance_card"];
                    var separatedPairs = new[]
                    {
                        (clueMarker, latchMarker),
                        (markerById["observe:target.classroom.morning_schedule"],
                            markerById["target.classroom.clock"]),
                        (markerById["observe:target.classroom.duty_note"],
                            markerById["target.classroom.duty_roster"])
                    };
                    var separationFailed = separatedPairs.Any(pair =>
                        new Vector2(pair.Item1.Position.X, pair.Item1.Position.Z).DistanceTo(
                            new Vector2(pair.Item2.Position.X, pair.Item2.Position.Z)) < 2f);
                    var windowMarkersOverlap = new Vector2(
                            latchMarker.GlobalPosition.X,
                            latchMarker.GlobalPosition.Z)
                        .DistanceTo(new Vector2(
                            clueMarker.GlobalPosition.X,
                            clueMarker.GlobalPosition.Z)) <=
                        latchMarker.InteractionRange + clueMarker.InteractionRange;
                    if (markerCount != 12 || collisionNodes != 29 || reflectionProbes != 2 ||
                        visual.CollisionBodyCount != 29 || visual.FurnitureCollisionCount != 20 ||
                        playerShape is null || blockedMarkers.Length > 0 || windowMarkersOverlap || separationFailed ||
                        latchMarker.Position.DistanceTo(new Vector3(-5.20f, 0f, -0.90f)) > 0.001f ||
                        clueMarker.Position.DistanceTo(new Vector3(-3.40f, 0f, 2.20f)) > 0.001f ||
                        Math.Abs(latchMarker.InteractionRange - 1.0f) > 0.001f ||
                        Math.Abs(clueMarker.InteractionRange - 1.0f) > 0.001f)
                    {
                        throw new InvalidOperationException(
                            $"3D classroom composition invalid: collisions={collisionNodes} " +
                            $"furniture={visual.FurnitureCollisionCount} markers={markerCount} " +
                            $"reflectionProbes={reflectionProbes} playerShape={playerShape is not null} " +
                            $"blockedMarkers={string.Join(',', blockedMarkers)} " +
                            $"windowMarkersOverlap={windowMarkersOverlap}.");
                    }
                    ValidateMarkerReachability(visual, markers);
                    ValidateCoordinateMapping();
                    ValidateClassroomArtPass(classroom);
                    ValidateClassroomVisualStates(visual);
                    await ValidateThirdPersonCamera(classroom, markers);
                    await ProbeClassroomPhysics(classroom);
                }
                else if (scenePath == "res://scenes/dream/DreamMusicRoom.tscn")
                {
                    var musicRoom = instance as DreamMusicRoomController3D
                        ?? throw new InvalidOperationException("Dream music room is not a 3D dream area.");
                    await ValidateDreamMusicRoom3D(session, musicRoom);
                }
                else if (scenePath == "res://scenes/dream/DreamArchiveRoom.tscn")
                {
                    ValidateDreamArchiveRoom3D(instance as DreamArchiveRoomController3D
                        ?? throw new InvalidOperationException("Dream archive room is not a 3D dream area."));
                }
                else if (scenePath == "res://scenes/dream/DreamAuditorium.tscn")
                {
                    ValidateDreamAuditorium3D(instance as DreamAuditoriumController3D
                        ?? throw new InvalidOperationException("Dream auditorium is not a 3D dream area."));
                }
                else if (instance is IDreamAreaRuntime runtime && runtime.CameraControlMode != CameraControlMode.None)
                {
                    throw new InvalidOperationException($"2D area '{scenePath}' unexpectedly enabled camera control.");
                }
                instance.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            await ValidateDream3DLockedMarkers(session);
            await ValidateAuditoriumTwoSidedExit(session);
            await ValidateWindowInteractionsThroughDreamRoot(session);
            await ValidateAutomaticRecorderUseThroughDreamRoot(session);
            await ValidateClassroomMansionRoundTrip(session);

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

    private async Task CaptureDreamClassroom3D(GameSession session, string outputDirectory)
    {
        var captureSize = new Vector2I(1920, 1080);
        DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetSize(captureSize);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamClassroom.tscn")
            ?? throw new InvalidOperationException("Could not load 3D dream classroom for capture.");
        var classroom = packed.Instantiate<DreamAreaController3D>();
        AddChild(classroom);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var visual = classroom.ClassroomVisual;
        var dream = session.State.DreamEscape
            ?? throw new InvalidOperationException("Dream state missing during 3D capture.");

        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleWindowLatches);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PickupClockHand);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleBlackboard);
        visual.ApplyState(dream);
        var viewNames = new[] { "blackboard", "east-door", "rear-wall", "west-window" };
        var viewYaw = new[] { 0f, -90f, 180f, 90f };
        for (var index = 0; index < viewNames.Length; index++)
        {
            classroom.CameraController.SetYawImmediate(viewYaw[index]);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await SaveCapture(outputDirectory, $"{index + 1:00}-view-{viewNames[index]}.png", captureSize);
        }

        classroom.CameraController.SetYawImmediate(0f);
        dream.SolvedPuzzles.Clear();
        visual.ApplyState(dream);
        await SaveCapture(outputDirectory, "05-state-initial.png", captureSize);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleWindowLatches);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PickupClockHand);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleBlackboard);
        visual.ApplyState(dream);
        await SaveCapture(outputDirectory, "06-state-blackboard.png", captureSize);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleClockRestore);
        visual.ApplyState(dream);
        await SaveCapture(outputDirectory, "07-state-clock-restored.png", captureSize);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleRosterSlider);
        visual.ApplyState(dream);
        await SaveCapture(outputDirectory, "08-state-roster-aligned.png", captureSize);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleTeacherDesk);
        visual.ApplyState(dream);
        await SaveCapture(outputDirectory, "09-state-teacher-desk.png", captureSize);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleMapCabinet);
        visual.ApplyState(dream);
        await SaveCapture(outputDirectory, "10-state-map-cabinet.png", captureSize);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleTransomCache);
        visual.ApplyState(dream);
        await SaveCapture(outputDirectory, "11-state-transom-cache.png", captureSize);
        visual.SetCollisionDebug(true);
        await SaveCapture(outputDirectory, "12-collision-debug.png", captureSize);
        visual.SetCollisionDebug(false);

        classroom.PlayerActor.GlobalPosition = new Vector3(3.0f, 0f, 2.25f);
        classroom.CameraController.SetYawImmediate(-90f);
        classroom.CameraController.SnapToTarget();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await SaveCapture(outputDirectory, "13-closeup-east-door.png", captureSize);

        classroom.PlayerActor.GlobalPosition = new Vector3(2.35f, 0f, -1.45f);
        classroom.CameraController.SetYawImmediate(0f);
        classroom.CameraController.SnapToTarget();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await SaveCapture(outputDirectory, "14-closeup-blackboard.png", captureSize);

        classroom.PlayerActor.GlobalPosition = new Vector3(0.85f, 0f, 0.70f);
        classroom.CameraController.SetYawImmediate(-28f);
        classroom.CameraController.SnapToTarget();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await SaveCapture(outputDirectory, "15-closeup-student-desks.png", captureSize);
        classroom.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task CaptureDreamMusicRoom3D(GameSession session, string outputDirectory)
    {
        var captureSize = new Vector2I(1920, 1080);
        DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetSize(captureSize);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var dream = session.State.DreamEscape
            ?? throw new InvalidOperationException("Dream state missing during music-room capture.");
        dream.UnlockedAreas.Add(DreamEscapeIds.AreaMusic);
        var route = DreamAreaSpawnCatalog.Resolve(
            DreamEscapeIds.AreaCorridor,
            DreamEscapeIds.AreaMusic);
        session.MoveDreamToArea(
            DreamEscapeIds.AreaMusic,
            route.StoredPosition.X,
            route.StoredPosition.Y);
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamMusicRoom.tscn")
            ?? throw new InvalidOperationException("Could not load 3D music room for capture.");
        var musicRoom = packed.Instantiate<DreamMusicRoomController3D>();
        AddChild(musicRoom);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        async Task Capture(string fileName, Vector3 playerPosition, float yawDegrees)
        {
            musicRoom.PlayerActor.GlobalPosition = playerPosition;
            musicRoom.CameraController.SetYawImmediate(yawDegrees);
            musicRoom.CameraController.SnapToTarget();
            musicRoom.Companion.RefreshImmediate();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await SaveCapture(outputDirectory, fileName, captureSize);
        }

        await Capture("01-entry-overview.png", new Vector3(-4.65f, 0f, 2.72f), -40f);
        await Capture("02-north-mechanisms.png", new Vector3(0f, 0f, 0.95f), 0f);
        await Capture("03-south-piano.png", new Vector3(0f, 0f, 0.95f), 180f);

        dream.XiaoyuJoined = true;
        dream.SolvedPuzzles.UnionWith([
            DreamEscapeIds.PuzzleMusicScore,
            DreamEscapeIds.PuzzleMusicCage,
            DreamEscapeIds.PuzzleMusicMetronome,
            DreamEscapeIds.PuzzleMusicTuningForks,
            DreamEscapeIds.PuzzleMusicPianoRoll,
            DreamEscapeIds.PuzzleMusicAcousticWall
        ]);
        dream.InstalledParts.UnionWith([
            DreamEscapeIds.SlotMusicMetronomeWeight,
            DreamEscapeIds.SlotMusicPianoRoll
        ]);
        musicRoom.RefreshState();
        await Capture("04-solved-acoustic-wall.png", new Vector3(1.8f, 0f, 0.1f), -90f);

        musicRoom.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task CaptureDreamLateRooms3D(GameSession session, string outputDirectory)
    {
        var captureSize = new Vector2I(1920, 1080);
        DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
        var archiveDirectory = Path.Combine(outputDirectory, "archive");
        var auditoriumDirectory = Path.Combine(outputDirectory, "auditorium");
        DirAccess.MakeDirRecursiveAbsolute(archiveDirectory);
        DirAccess.MakeDirRecursiveAbsolute(auditoriumDirectory);
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetSize(captureSize);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        session.NewGame("后两室捕获", "同学");
        var dream = session.State.DreamEscape!;
        dream.XiaoyuJoined = true;
        dream.UnlockedAreas.Add(DreamEscapeIds.AreaArchive);
        var archiveRoute = DreamAreaSpawnCatalog.Resolve(
            DreamEscapeIds.AreaCorridor, DreamEscapeIds.AreaArchive);
        session.MoveDreamToArea(DreamEscapeIds.AreaArchive,
            archiveRoute.StoredPosition.X, archiveRoute.StoredPosition.Y);
        var archivePacked = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamArchiveRoom.tscn")
            ?? throw new InvalidOperationException("Could not load archive room for review capture.");
        var archive = archivePacked.Instantiate<DreamArchiveRoomController3D>();
        AddChild(archive);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        archive.PlayerActor.Visible = false;

        async Task CaptureArchive(string fileName, Vector3 playerPosition, float yawDegrees)
        {
            archive.PlayerActor.GlobalPosition = playerPosition;
            archive.CameraController.SetYawImmediate(yawDegrees);
            archive.CameraController.SnapToTarget();
            archive.Companion.RefreshImmediate();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await SaveCapture(archiveDirectory, fileName, captureSize);
        }

        var archiveEntry = archive.ArchiveVisual.ResolveSpawn(new Vector3(0f, 0f, 2.1f));
        await CaptureArchive("01-entry-north.png", archiveEntry, 0f);
        await CaptureArchive("02-entry-east.png", archiveEntry, -90f);
        await CaptureArchive("03-entry-south.png", archiveEntry, 180f);
        await CaptureArchive("04-entry-west.png", archiveEntry, 90f);
        await CaptureArchive("05-registry-initial.png", new Vector3(1.7f, 0f, 1.25f), 0f);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleArchiveSignalRegistry);
        archive.RefreshState();
        await CaptureArchive("06-registry-solved.png", new Vector3(1.7f, 0f, 1.25f), 0f);
        dream.SolvedPuzzles.UnionWith([
            DreamEscapeIds.PuzzleArchiveDrawerRoute,
            DreamEscapeIds.PuzzleArchiveFuseRepair,
            DreamEscapeIds.PuzzleArchiveRecorderRebuild
        ]);
        dream.InstalledParts.UnionWith([
            DreamEscapeIds.SlotArchiveRecorderReel,
            DreamEscapeIds.SlotArchiveRecorderCrank,
            DreamEscapeIds.SlotArchiveRecorderFuse
        ]);
        archive.RefreshState();
        await CaptureArchive("07-recorder-rebuilt.png", new Vector3(1.0f, 0f, 2.2f), -90f);
        dream.SolvedPuzzles.UnionWith([
            DreamEscapeIds.PuzzleArchiveEditingCounter,
            DreamEscapeIds.PuzzleArchiveUpperVault
        ]);
        archive.RefreshState();
        await CaptureArchive("08-counter-vault-open.png", new Vector3(-1.8f, 0f, 2.0f), 180f);
        await CaptureArchive("09-closeup-drawer.png", new Vector3(-1.2f, 0f, 1.0f), 90f);
        await CaptureArchive("10-closeup-repair-bench.png", new Vector3(2.4f, 0f, -2.7f), -90f);
        archive.ArchiveVisual.SetCollisionDebug(true);
        await CaptureArchive("11-collision-debug.png", new Vector3(0f, 0f, 2.1f), 0f);
        archive.ArchiveVisual.SetCollisionDebug(false);
        await archive.PlayExitTransitionAsync($"area:{DreamEscapeIds.AreaCorridor}", CancellationToken.None);
        await CaptureArchive("12-exit-open.png", archiveEntry, -90f);
        archive.ResetExitTransition();
        archive.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        session.NewGame("后两室捕获", "同学");
        dream = session.State.DreamEscape!;
        dream.XiaoyuJoined = true;
        dream.UnlockedAreas.Add(DreamEscapeIds.AreaAuditorium);
        var auditoriumRoute = DreamAreaSpawnCatalog.Resolve(
            DreamEscapeIds.AreaCorridor, DreamEscapeIds.AreaAuditorium);
        session.MoveDreamToArea(DreamEscapeIds.AreaAuditorium,
            auditoriumRoute.StoredPosition.X, auditoriumRoute.StoredPosition.Y);
        var auditoriumPacked = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamAuditorium.tscn")
            ?? throw new InvalidOperationException("Could not load auditorium for review capture.");
        var auditorium = auditoriumPacked.Instantiate<DreamAuditoriumController3D>();
        AddChild(auditorium);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        auditorium.PlayerActor.Visible = false;

        async Task CaptureAuditorium(string fileName, Vector3 playerPosition, float yawDegrees)
        {
            auditorium.PlayerActor.GlobalPosition = playerPosition;
            auditorium.CameraController.SetYawImmediate(yawDegrees);
            auditorium.CameraController.SnapToTarget();
            auditorium.Companion.RefreshImmediate();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await SaveCapture(auditoriumDirectory, fileName, captureSize);
        }

        var auditoriumEntry = auditorium.AuditoriumVisual.ResolveSpawn(new Vector3(0f, 0f, 1.4f));
        await CaptureAuditorium("01-entry-north.png", auditoriumEntry, 0f);
        await CaptureAuditorium("02-entry-east.png", auditoriumEntry, -90f);
        await CaptureAuditorium("03-entry-south.png", auditoriumEntry, 180f);
        await CaptureAuditorium("04-entry-west.png", auditoriumEntry, 90f);
        await CaptureAuditorium("05-line-set-initial.png", new Vector3(5.8f, 0f, -2.0f), 0f);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAuditoriumLineSetBalance);
        auditorium.RefreshState();
        await CaptureAuditorium("06-curtain-raised.png", new Vector3(1.8f, 0f, -2.1f), 0f);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAuditoriumShadowProjector);
        auditorium.RefreshState();
        await CaptureAuditorium("07-projector-aligned.png", new Vector3(-4.8f, 0f, -2.0f), 90f);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAuditoriumMasterTapeSplice);
        dream.InstalledParts.Add(DreamEscapeIds.SlotAuditoriumMasterTape);
        auditorium.RefreshState();
        await CaptureAuditorium("08-master-tape-installed.png", new Vector3(4.5f, 0f, 1.8f), -90f);
        dream.SolvedPuzzles.UnionWith([
            DreamEscapeIds.PuzzleAuditoriumLightConsole,
            DreamEscapeIds.PuzzleAuditoriumSoundConsole,
            DreamEscapeIds.PuzzleAuditoriumBroadcastConsole
        ]);
        auditorium.RefreshState();
        await CaptureAuditorium("09-three-consoles-solved.png", new Vector3(2.2f, 0f, 3.1f), 180f);
        await CaptureAuditorium("10-closeup-stage.png", new Vector3(0f, 0f, -3.8f), 0f);
        auditorium.AuditoriumVisual.SetCollisionDebug(true);
        await CaptureAuditorium("11-collision-debug.png", new Vector3(0f, 0f, 1.4f), 0f);
        auditorium.AuditoriumVisual.SetCollisionDebug(false);
        dream.Status = DreamEscapeStatus.ExitOpened;
        auditorium.RefreshState();
        await CaptureAuditorium("12-exit-open.png", new Vector3(0f, 0f, -3.8f), 0f);
        auditorium.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task CaptureDreamMansion3D(GameSession session, string outputDirectory)
    {
        var captureSize = new Vector2I(1920, 1080);
        DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetSize(captureSize);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var route = DreamAreaSpawnCatalog.Resolve(
            DreamEscapeIds.AreaClassroom,
            DreamEscapeIds.AreaCorridor);
        session.MoveDreamToArea(
            DreamEscapeIds.AreaCorridor,
            route.StoredPosition.X,
            route.StoredPosition.Y);
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamCorridor.tscn")
            ?? throw new InvalidOperationException("Could not load 3D dream mansion for capture.");
        using var modalLease = session.Modal.Acquire("capture.dream_manor");
        var mansion = packed.Instantiate<DreamMansionController3D>();
        AddChild(mansion);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var views = new[]
        {
            ("01-foyer-front-door.png", new Vector3(0f, 0f, -3.55f), 0f),
            ("02-hall-looking-north.png", new Vector3(0f, 0f, -0.15f), 180f),
            ("03-room-one-door.png", new Vector3(-1.25f, 0f, 3.20f), 90f),
            ("04-room-two-door.png", new Vector3(1.25f, 0f, 3.20f), -90f),
            ("05-fireplace-and-foyer.png", new Vector3(-1.35f, 0f, -3.45f), 90f),
            ("06-room-four-double-door.png", new Vector3(0f, 0f, 15.35f), 180f)
        };
        foreach (var (fileName, playerPosition, yawDegrees) in views)
        {
            mansion.PlayerActor.GlobalPosition = playerPosition;
            mansion.CameraController.SetYawImmediate(yawDegrees);
            mansion.CameraController.SnapToTarget();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await SaveCapture(outputDirectory, fileName, captureSize);
        }

        mansion.PlayerActor.GlobalPosition = DreamAreaSpawnCatalog.MansionRoomOneEntryWorld;
        mansion.ApplyEntryPose(Vector3.Right, null);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await SaveCapture(outputDirectory, "07-room-one-entry-spawn.png", captureSize);

        mansion.MansionVisual.SetDoorOpenProgress($"area:{DreamEscapeIds.AreaClassroom}", 1f);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await SaveCapture(outputDirectory, "08-room-one-door-open.png", captureSize);
        mansion.MansionVisual.ResetDoors();

        mansion.MansionVisual.SetCollisionDebug(true);
        mansion.PlayerActor.GlobalPosition = new Vector3(0f, 0f, -0.15f);
        mansion.CameraController.SetYawImmediate(180f);
        mansion.CameraController.SnapToTarget();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await SaveCapture(outputDirectory, "09-collision-debug.png", captureSize);
        mansion.MansionVisual.SetCollisionDebug(false);

        mansion.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task ValidateClassroomMansionRoundTrip(GameSession session)
    {
        session.NewGame("双向门口换场", "同学");
        var classroomRoute = DreamAreaSpawnCatalog.Resolve(
            DreamEscapeIds.AreaCorridor,
            DreamEscapeIds.AreaClassroom);
        session.MoveDreamToArea(
            DreamEscapeIds.AreaClassroom,
            classroomRoute.StoredPosition.X,
            classroomRoute.StoredPosition.Y);
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamEscape.tscn")
            ?? throw new InvalidOperationException("Could not load dream root for doorway round-trip smoke.");
        var dreamRoot = packed.Instantiate<DreamEscapeController>();
        AddChild(dreamRoot);
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var areaHost = dreamRoot.GetNode<Node>("AreaHost");
            var classroom = areaHost.GetNode<DreamAreaController3D>("DreamClassroom3D");
            var sourceCameraOffset = classroom.CaptureCameraDoorOffsetDegrees(Vector3.Right);
            classroom.PlayerActor.GlobalPosition = new Vector3(5.22f, 0f, 3.82f);
            classroom.PlayerActor.SetFacingImmediate(Vector3.Right);
            classroom.CameraController.SetYawImmediate(180f);
            classroom.CameraController.SnapToTarget();
            sourceCameraOffset = classroom.CaptureCameraDoorOffsetDegrees(Vector3.Right);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.ParseInputEvent(new InputEventAction { Action = "interact", Pressed = true });
            Input.ParseInputEvent(new InputEventAction { Action = "interact", Pressed = false });

            DreamMansionController3D? mansion = null;
            for (var frame = 0; frame < 180 && mansion is null; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                mansion = areaHost.GetNodeOrNull<DreamMansionController3D>("DreamMansion3D");
            }
            if (mansion is null)
                throw new InvalidOperationException("Classroom interaction did not enter the 3D mansion.");
            for (var frame = 0; frame < 90 && session.Modal.IsBlocked; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (session.Modal.IsBlocked || !mansion.PlayerActor.InputEnabled)
                throw new InvalidOperationException("Classroom-to-mansion transition did not release its modal lock.");
            var mansionOffset = mansion.CaptureCameraDoorOffsetDegrees(Vector3.Right);
            if (session.State.DreamEscape?.CurrentAreaId != DreamEscapeIds.AreaCorridor ||
                mansion.PlayerActor.GlobalPosition.DistanceTo(
                    DreamAreaSpawnCatalog.MansionRoomOneEntryWorld) > 0.001f ||
                mansion.CameraControlMode != CameraControlMode.Orbit ||
                Math.Abs(Mathf.AngleDifference(
                    Mathf.DegToRad(mansionOffset),
                    Mathf.DegToRad(sourceCameraOffset))) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"Classroom-to-mansion doorway transition failed: " +
                    $"area={session.State.DreamEscape?.CurrentAreaId} " +
                    $"position={mansion.PlayerActor.GlobalPosition} " +
                    $"cameraOffset={mansionOffset}/{sourceCameraOffset}.");
            }

            mansion.PlayerActor.GlobalPosition = DreamAreaSpawnCatalog.MansionRoomOneEntryWorld;
            mansion.PlayerActor.SetFacingImmediate(Vector3.Left);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.ParseInputEvent(new InputEventAction { Action = "interact", Pressed = true });
            Input.ParseInputEvent(new InputEventAction { Action = "interact", Pressed = false });

            DreamAreaController3D? returnedClassroom = null;
            for (var frame = 0; frame < 180 && returnedClassroom is null; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                returnedClassroom = areaHost.GetNodeOrNull<DreamAreaController3D>("DreamClassroom3D");
            }
            if (returnedClassroom is null)
                throw new InvalidOperationException("Mansion interaction did not return to the 3D classroom.");
            var mapper = new AreaCoordinateMapper3D(
                new Rect2(16f, 178f, 608f, 152f),
                new Rect2(-5.7f, -4.2f, 11.4f, 8.4f));
            var expectedDoorWorld = returnedClassroom.ClassroomVisual.ResolveSpawn(
                mapper.ToWorld(DreamAreaSpawnCatalog.ClassroomDoorEntryStored));
            var newGameWorld = mapper.ToWorld(new Vector2(
                DreamEscapeState.NewGameClassroomPositionX,
                DreamEscapeState.NewGameClassroomPositionY));
            if (session.State.DreamEscape?.CurrentAreaId != DreamEscapeIds.AreaClassroom ||
                returnedClassroom.PlayerActor.GlobalPosition.DistanceTo(expectedDoorWorld) > 0.001f ||
                returnedClassroom.PlayerActor.GlobalPosition.DistanceTo(newGameWorld) < 0.5f)
            {
                throw new InvalidOperationException(
                    $"Mansion-to-classroom doorway transition failed: " +
                    $"area={session.State.DreamEscape?.CurrentAreaId} " +
                    $"position={returnedClassroom.PlayerActor.GlobalPosition} " +
                    $"expectedDoor={expectedDoorWorld} newGame={newGameWorld}.");
            }
        }
        finally
        {
            dreamRoot.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task ValidateWindowInteractionsThroughDreamRoot(GameSession session)
    {
        session.NewGame("窗扣线索分离", "同学");
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamEscape.tscn")
            ?? throw new InvalidOperationException("Could not load dream root for window interaction smoke.");
        var dreamRoot = packed.Instantiate<DreamEscapeController>();
        AddChild(dreamRoot);
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var classroom = dreamRoot.GetNode<DreamAreaController3D>("AreaHost/DreamClassroom3D");
            var markerById = classroom.GetNode<Node3D>("InteractionMarkers").GetChildren()
                .OfType<InteractionMarker3D>()
                .ToDictionary(marker => marker.TargetId, StringComparer.Ordinal);
            var latchMarker = markerById["puzzle:window_latches"];
            var clueMarker = markerById["observe:target.classroom.maintenance_card"];
            var mechanism = Descendants<DreamMechanismPanel>(dreamRoot).Single();
            var inventory = Descendants<DreamInventoryPanel>(dreamRoot).Single();
            var dream = session.State.DreamEscape!;

            async Task FocusAndInteract(InteractionMarker3D marker, string expectedPrompt)
            {
                classroom.PlayerActor.GlobalPosition = marker.GlobalPosition;
                classroom.PlayerActor.SetFacingImmediate(Vector3.Left);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!Descendants<Label>(dreamRoot).Any(label =>
                        label.Visible && label.Text == $"[F] {expectedPrompt}"))
                {
                    throw new InvalidOperationException(
                        $"Window interaction prompt was not shown for '{marker.TargetId}'.");
                }
                Input.ParseInputEvent(new InputEventAction { Action = "interact", Pressed = true });
                Input.ParseInputEvent(new InputEventAction { Action = "interact", Pressed = false });
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            if (classroom.ActiveInteractionMarkerCount != 12)
                throw new InvalidOperationException("Initial classroom did not expose both window interactions.");

            await FocusAndInteract(latchMarker, "调整三只旧窗扣");
            if (!mechanism.Visible ||
                dream.DiscoveredClues.Contains(DreamEscapeIds.ClueClassroomWindow) ||
                !Descendants<Label>(mechanism).Any(label =>
                    label.Text.Contains("课桌区域寻找维护资料", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Unobserved latch interaction did not open the non-spoiler mechanism panel.");
            }
            mechanism.ClosePanel();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            await FocusAndInteract(clueMarker, "调查窗扣维护卡");
            if (mechanism.Visible ||
                !dream.DiscoveredClues.Contains(DreamEscapeIds.ClueClassroomWindow) ||
                classroom.ActiveInteractionMarkerCount != 11 ||
                !Descendants<Label>(dreamRoot).Any(label =>
                    label.Visible && label.Text.Contains(
                        "上段、下段、中段。记录页已保存",
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Maintenance-card interaction did not record the clue independently of the latch panel.");
            }

            inventory.Open(DreamInventoryPage.Clues);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!Descendants<Label>(inventory).Any(label => label.Text.Contains(
                    "三幅窗扣磨损示意从左到右是上段、下段、中段",
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Recorded scratch pattern was not available on the clue page.");
            }
            inventory.ClosePanel();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            await FocusAndInteract(latchMarker, "调整三只旧窗扣");
            if (!mechanism.Visible ||
                !Descendants<Label>(mechanism).Any(label =>
                    label.Text.Contains("上段—下段—中段", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Observed latch interaction did not show the recorded scratch pattern.");
            }
            mechanism.ClosePanel();
            var solved = session.SubmitDreamPuzzle(
                DreamEscapeIds.PuzzleWindowLatches,
                "up", "down", "middle");
            classroom.RefreshState();
            if (!solved.Changed || classroom.ActiveInteractionMarkerCount != 10)
            {
                throw new InvalidOperationException(
                    "Solved window latch or recorded scratch marker remained active.");
            }

            await FocusAndInteract(markerById["target.classroom.blackboard"], "查看黑板擦痕");
            if (!dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleBlackboard) ||
                !dream.Items.Contains(DreamEscapeIds.ItemChalk))
            {
                throw new InvalidOperationException(
                    "Blackboard did not automatically use the held chalk without consuming its first use.");
            }

            session.PickupDreamItem(DreamEscapeIds.ItemClockHand);
            await FocusAndInteract(markerById["target.classroom.clock"], "检查缺少部件的时钟");
            if (!mechanism.Visible ||
                !dream.InstalledParts.Contains(DreamEscapeIds.SlotClassroomClockHand) ||
                !dream.InstalledParts.Contains(DreamEscapeIds.SlotClassroomClockKey) ||
                dream.Items.Contains(DreamEscapeIds.ItemClockHand) ||
                dream.Items.Contains(DreamEscapeIds.ItemClockKey))
            {
                throw new InvalidOperationException(
                    "Clock interaction did not install both held parts and continue to the mechanism panel.");
            }
            mechanism.ClosePanel();
        }
        finally
        {
            dreamRoot.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task ValidateAutomaticRecorderUseThroughDreamRoot(GameSession session)
    {
        session.NewGame("录音机自动安装", "同学");
        var dream = session.State.DreamEscape!;
        dream.UnlockedAreas.Add(DreamEscapeIds.AreaArchive);
        dream.CurrentAreaId = DreamEscapeIds.AreaArchive;
        dream.PositionX = 320f;
        dream.PositionY = 265f;
        dream.XiaoyuJoined = true;
        dream.Items.UnionWith([
            DreamEscapeIds.ItemCalibrationReel,
            DreamEscapeIds.ItemRecorderCrank,
            DreamEscapeIds.ItemRepairedFuse
        ]);

        var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamEscape.tscn")
            ?? throw new InvalidOperationException("Could not load dream root for recorder auto-use smoke.");
        var dreamRoot = packed.Instantiate<DreamEscapeController>();
        AddChild(dreamRoot);
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var archive = dreamRoot.GetNode<DreamArchiveRoomController3D>("AreaHost/DreamArchiveRoom3D");
            var recorder = archive.GetNode<Node3D>("InteractionMarkers/Recorder");
            archive.PlayerActor.GlobalPosition = recorder.GlobalPosition;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.ParseInputEvent(new InputEventAction { Action = "interact", Pressed = true });
            Input.ParseInputEvent(new InputEventAction { Action = "interact", Pressed = false });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            if (!dream.SolvedPuzzles.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild) ||
                !dream.InstalledParts.IsSupersetOf([
                    DreamEscapeIds.SlotArchiveRecorderReel,
                    DreamEscapeIds.SlotArchiveRecorderCrank,
                    DreamEscapeIds.SlotArchiveRecorderFuse
                ]) ||
                dream.Items.Contains(DreamEscapeIds.ItemCalibrationReel) ||
                dream.Items.Contains(DreamEscapeIds.ItemRecorderCrank) ||
                dream.Items.Contains(DreamEscapeIds.ItemRepairedFuse))
            {
                throw new InvalidOperationException(
                    "Recorder interaction did not install all three held parts and start reconstruction.");
            }
        }
        finally
        {
            dreamRoot.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task CaptureClassroomFramingComparison(GameSession session, string outputDirectory)
    {
        const string artPassV3Path =
            "res://assets/generated/dream_escape/3d/classroom-art-pass-v3.glb";
        var captureSize = new Vector2I(1920, 1080);
        DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetSize(captureSize);
        DisplayServer.WindowSetPosition(Vector2I.Zero);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamClassroom.tscn")
            ?? throw new InvalidOperationException("Could not load 3D dream classroom for framing capture.");
        var classroom = packed.Instantiate<DreamAreaController3D>();
        ReplaceClassroomModel(classroom, artPassV3Path);
        AddChild(classroom);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        using var modalLease = session.Modal.Acquire("capture.camera_framing");
        _ = session.State.DreamEscape
            ?? throw new InvalidOperationException("Dream state missing during framing capture.");
        var captureState = new DreamEscapeState();
        captureState.SolvedPuzzles.Add(DreamEscapeIds.PuzzleWindowLatches);
        captureState.SolvedPuzzles.Add(DreamEscapeIds.PickupClockHand);
        captureState.SolvedPuzzles.Add(DreamEscapeIds.PuzzleBlackboard);
        classroom.ClassroomVisual.ApplyState(captureState);

        var sprite = classroom.PlayerActor.CharacterSprite
            ?? throw new InvalidOperationException("Framing capture requires the protagonist sprite.");
        sprite.Animation = "down";
        sprite.Stop();
        sprite.Frame = 0;

        var adoptedSpawn = classroom.PlayerActor.GlobalPosition;
        var previousResolvedSpawn = new Vector3(-0.85f, 0f, 0.70f);

        var groups = new[]
        {
            (Id: "previous_spawn", Label: "Previous resolved spawn", Position: previousResolvedSpawn),
            (Id: "adopted_spawn", Label: "Adopted new-game spawn", Position: adoptedSpawn)
        };
        var csv = new StringBuilder();
        csv.AppendLine(
            "group,preset,label,viewport_width,viewport_height,player_x,player_y,player_z,yaw," +
            "fov,pitch,follow_height,shoulder,requested_arm,actual_arm,arm_ratio," +
            "body_height_px,body_height_ratio,visible_height_px,visible_height_ratio," +
            "head_ratio,feet_ratio,visible_fraction");

        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

        var fileIndex = 1;
        foreach (var group in groups)
        {
            foreach (var preset in CameraFramingPresets)
            {
                ApplyCameraFraming(classroom, group.Position, 0f, preset);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                var projectionViewportSize = GetViewport().GetVisibleRect().Size;
                var windowSize = DisplayServer.WindowGetSize();
                if (windowSize != captureSize)
                {
                    throw new InvalidOperationException(
                        $"Framing capture must be 1920x1080, actual window is {windowSize}.");
                }

                var actualArm = classroom.CameraController.SpringArm.GetHitLength();
                var projection = MeasureCharacterProjection(
                    sprite,
                    classroom.CameraController.Camera,
                    projectionViewportSize,
                    captureSize);
                var fileName = $"{fileIndex:00}-{group.Id}-{preset.Id}.png";
                await SaveCapture(outputDirectory, fileName, captureSize);
                AppendFramingMetrics(
                    csv,
                    group.Id,
                    preset,
                    captureSize,
                    group.Position,
                    actualArm,
                    projection);
                GD.Print(FormattableString.Invariant(
                    $"CAMERA_FRAMING group={group.Id} preset={preset.Id} body={projection.BodyHeightRatio:P1} arm={actualArm:F3}/{preset.SpringLength:F3} head={projection.HeadRatio:P1} feet={projection.FeetRatio:P1}"));
                fileIndex++;
            }
        }

        File.WriteAllText(
            Path.Combine(outputDirectory, "camera-framing-metrics.csv"),
            csv.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(
            Path.Combine(outputDirectory, "camera-framing-readme.txt"),
            "The previous_spawn row reproduces the old wall-compressed start.\n" +
            "The adopted_spawn row uses the approved new-game position.\n" +
            "Each preset is a complete framing alternative: FOV, pitch, follow height, shoulder offset and arm length may differ.\n" +
            "All captures use the v3 art candidate without changing the runtime scene reference.\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        classroom.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void ReplaceClassroomModel(DreamAreaController3D classroom, string modelPath)
    {
        var existingModel = classroom.GetNode<Node3D>("ClassroomModel");
        classroom.RemoveChild(existingModel);
        existingModel.Free();
        var packedModel = ResourceLoader.Load<PackedScene>(modelPath)
            ?? throw new InvalidOperationException($"Could not load classroom candidate '{modelPath}'.");
        var candidate = packedModel.Instantiate<Node3D>();
        candidate.Name = "ClassroomModel";
        classroom.AddChild(candidate);
        classroom.MoveChild(candidate, 0);
    }

    private static void ApplyCameraFraming(
        DreamAreaController3D classroom,
        Vector3 playerPosition,
        float yawDegrees,
        CameraFramingPreset preset)
    {
        var rig = classroom.CameraController;
        classroom.PlayerActor.GlobalPosition = playerPosition;
        rig.Camera.Fov = preset.FieldOfView;
        rig.FixedPitchDegrees = preset.PitchDegrees;
        rig.FollowTargetHeight = preset.FollowTargetHeight;
        rig.SpringArm.RotationDegrees = new Vector3(preset.PitchDegrees, 0f, 0f);
        rig.SpringArm.SpringLength = preset.SpringLength;
        rig.Camera.Position = new Vector3(
            preset.ShoulderOffset,
            rig.Camera.Position.Y,
            rig.Camera.Position.Z);
        rig.SetYawImmediate(yawDegrees);
        rig.SnapToTarget();
    }

    private static CharacterProjectionMetrics MeasureCharacterProjection(
        AnimatedSprite3D sprite,
        Camera3D camera,
        Vector2 projectionViewportSize,
        Vector2I captureSize)
    {
        var texture = sprite.SpriteFrames.GetFrameTexture(sprite.Animation, sprite.Frame)
            ?? throw new InvalidOperationException("Could not read the protagonist capture frame.");
        var image = texture.GetImage();
        var bodyBounds = FindAlphaBounds(image, 0.5f);
        var visibleBounds = FindAlphaBounds(image, 0.001f);
        var bodyProjection = ProjectVerticalBounds(
            sprite,
            camera,
            projectionViewportSize,
            image.GetHeight(),
            bodyBounds);
        var visibleProjection = ProjectVerticalBounds(
            sprite,
            camera,
            projectionViewportSize,
            image.GetHeight(),
            visibleBounds);
        var outputScale = captureSize.Y / projectionViewportSize.Y;
        return new CharacterProjectionMetrics(
            bodyProjection.HeightPixels * outputScale,
            bodyProjection.HeightPixels / projectionViewportSize.Y,
            visibleProjection.HeightPixels * outputScale,
            visibleProjection.HeightPixels / projectionViewportSize.Y,
            bodyProjection.HeadY / projectionViewportSize.Y,
            bodyProjection.FeetY / projectionViewportSize.Y,
            visibleProjection.VisibleFraction);
    }

    private static (float HeightPixels, float HeadY, float FeetY, float VisibleFraction) ProjectVerticalBounds(
        AnimatedSprite3D sprite,
        Camera3D camera,
        Vector2 viewportSize,
        int frameHeight,
        Rect2I bounds)
    {
        var scaleY = sprite.GlobalTransform.Basis.Y.Length();
        var center = sprite.GlobalPosition;
        var topOffset = (frameHeight / 2f - bounds.Position.Y) * sprite.PixelSize * scaleY;
        var bottomOffset = (frameHeight / 2f - bounds.End.Y) * sprite.PixelSize * scaleY;
        var top = camera.UnprojectPosition(center + Vector3.Up * topOffset).Y;
        var bottom = camera.UnprojectPosition(center + Vector3.Up * bottomOffset).Y;
        var head = Math.Min(top, bottom);
        var feet = Math.Max(top, bottom);
        var projectedHeight = Math.Max(feet - head, 0.0001f);
        var clippedHeight = Math.Max(
            Math.Min(feet, viewportSize.Y) - Math.Max(head, 0f),
            0f);
        return (projectedHeight, head, feet, clippedHeight / projectedHeight);
    }

    private static Rect2I FindAlphaBounds(Image image, float threshold)
    {
        var minX = image.GetWidth();
        var minY = image.GetHeight();
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < image.GetHeight(); y++)
        {
            for (var x = 0; x < image.GetWidth(); x++)
            {
                if (image.GetPixel(x, y).A < threshold) continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }
        if (maxX < minX || maxY < minY)
            throw new InvalidOperationException("The protagonist capture frame has no visible pixels.");
        return new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static void AppendFramingMetrics(
        StringBuilder csv,
        string groupId,
        CameraFramingPreset preset,
        Vector2 viewportSize,
        Vector3 playerPosition,
        float actualArm,
        CharacterProjectionMetrics projection)
    {
        var culture = CultureInfo.InvariantCulture;
        var values = new object[]
        {
            groupId, preset.Id, preset.Label,
            viewportSize.X, viewportSize.Y,
            playerPosition.X, playerPosition.Y, playerPosition.Z, 0f,
            preset.FieldOfView, preset.PitchDegrees, preset.FollowTargetHeight,
            preset.ShoulderOffset, preset.SpringLength, actualArm,
            actualArm / preset.SpringLength,
            projection.BodyHeightPixels, projection.BodyHeightRatio,
            projection.VisibleHeightPixels, projection.VisibleHeightRatio,
            projection.HeadRatio, projection.FeetRatio, projection.VisibleFraction
        };
        csv.AppendLine(string.Join(',', values.Select(value =>
            value is IFormattable formatted
                ? formatted.ToString("0.######", culture)
                : value.ToString())));
    }

    private async Task ProfileDreamClassroom3D()
    {
        var priorMaxFps = Engine.MaxFps;
        Engine.MaxFps = 60;
        try
        {
            var loadStarted = Time.GetTicksUsec();
            var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamClassroom.tscn")
                ?? throw new InvalidOperationException("Could not load 3D dream classroom for profiling.");
            var classroom = packed.Instantiate<DreamAreaController3D>();
            AddChild(classroom);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var loadMilliseconds = (Time.GetTicksUsec() - loadStarted) / 1000.0;

            for (var index = 0; index < 30; index++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var frameTimes = new List<double>(240);
            var orbitFrameTimes = new List<double>();
            var previous = Time.GetTicksUsec();
            for (var index = 0; index < 240; index++)
            {
                classroom.CameraController.SetYawImmediate(classroom.CameraController.YawDegrees + 0.75f);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var current = Time.GetTicksUsec();
                var milliseconds = (current - previous) / 1000.0;
                previous = current;
                frameTimes.Add(milliseconds);
                orbitFrameTimes.Add(milliseconds);
            }

            var averageFrame = frameTimes.Average();
            var worstFrame = frameTimes.Max();
            var worstRotation = orbitFrameTimes.Count == 0 ? 0.0 : orbitFrameTimes.Max();
            var windowSize = DisplayServer.WindowGetSize();
            GD.Print(
                $"CLASSROOM_3D_PROFILE_PASS resolution={windowSize.X}x{windowSize.Y} " +
                $"averageFps={1000.0 / averageFrame:F1} minimumFps={1000.0 / worstFrame:F1} " +
                $"maxRotationFrameMs={worstRotation:F2} sceneLoadMs={loadMilliseconds:F2}");
            classroom.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        finally
        {
            Engine.MaxFps = priorMaxFps;
        }
    }

    private static void ValidateCoordinateMapping()
    {
        var mapper = new AreaCoordinateMapper3D(
            new Rect2(16f, 178f, 608f, 152f),
            new Rect2(-5.7f, -4.2f, 11.4f, 8.4f));
        foreach (var point in new[]
                 {
                     new Vector2(16, 178),
                     new Vector2(320, 280),
                     new Vector2(
                         DreamEscapeState.NewGameClassroomPositionX,
                         DreamEscapeState.NewGameClassroomPositionY),
                     new Vector2(624, 330)
                 })
        {
            var roundTrip = mapper.ToStored(mapper.ToWorld(point));
            if (roundTrip.DistanceTo(point) > 0.01f)
                throw new InvalidOperationException($"3D coordinate round trip drifted: {point} -> {roundTrip}.");
        }
    }

    private static void ValidateInputBindings()
    {
        static bool HasKey(StringName action, Key key) => InputMap.ActionGetEvents(action)
            .OfType<InputEventKey>()
            .Any(input => input.PhysicalKeycode == key || input.Keycode == key);
        static bool HasLogicalKey(StringName action, Key key) => InputMap.ActionGetEvents(action)
            .OfType<InputEventKey>()
            .Any(input => input.Keycode == key);
        static bool HasButton(StringName action, JoyButton button) => InputMap.ActionGetEvents(action)
            .OfType<InputEventJoypadButton>()
            .Any(input => input.ButtonIndex == button);
        static bool HasAxis(StringName action, JoyAxis axis, float direction) => InputMap.ActionGetEvents(action)
            .OfType<InputEventJoypadMotion>()
            .Any(input => input.Axis == axis && Math.Sign(input.AxisValue) == Math.Sign(direction));

        if (!HasKey("interact", Key.F) || !HasKey("interact", Key.Space) ||
            !HasButton("interact", JoyButton.A) ||
            !HasKey("inventory", Key.I) || !HasKey("inventory", Key.Tab) ||
            HasKey("inventory", Key.Backspace) || !HasButton("inventory", JoyButton.X) ||
            !HasLogicalKey("pause_menu", Key.Escape) ||
            !HasAxis("camera_orbit_left", JoyAxis.RightX, -1f) ||
            !HasAxis("camera_orbit_right", JoyAxis.RightX, 1f))
        {
            throw new InvalidOperationException(
                "Keyboard or Xbox interaction, inventory or orbit bindings are incomplete.");
        }
    }

    private async Task ValidateDreamPauseAndExit(MainController main)
    {
        var newGame = Descendants<Button>(main).FirstOrDefault(button => button.Text == "开始新游戏")
            ?? throw new InvalidOperationException("Title screen new-game button is missing.");
        newGame.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var begin = Descendants<Button>(main).FirstOrDefault(button => button.Text == "开始暑假")
            ?? throw new InvalidOperationException("Name-entry confirmation button is missing.");
        begin.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var dreams = Descendants<DreamEscapeController>(main).ToArray();
        if (dreams.Length != 1)
            throw new InvalidOperationException("New game did not enter the dream prologue.");
        var dream = dreams[0];
        var reminder = dream.FindChild("EntryControlReminder", true, false) as AcceptDialog
            ?? throw new InvalidOperationException("New-game control reminder is missing.");
        if (!dream.ControlReminderVisible || !reminder.DialogText.Contains("I 或 Tab", StringComparison.Ordinal) ||
            !reminder.DialogText.Contains("Xbox 手柄", StringComparison.Ordinal) ||
            !GetNode<GameSession>("/root/GameSession").Modal.IsBlocked)
        {
            throw new InvalidOperationException(
                "New game did not show the blocking Esc and inventory control reminder.");
        }
        reminder.GetOkButton().EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (dream.ControlReminderVisible || GetNode<GameSession>("/root/GameSession").Modal.IsBlocked)
            throw new InvalidOperationException("Control reminder did not release its modal lock.");

        await DispatchKey(Key.Tab);
        if (!dream.InventoryVisible || main.PauseVisible)
            throw new InvalidOperationException("Tab did not open inventory in the composed dream tree.");
        await DispatchKey(Key.Escape);
        if (dream.InventoryVisible || main.PauseVisible)
            throw new InvalidOperationException("One Esc did not close inventory without cascading into pause.");

        await DispatchKey(Key.Escape);
        if (!main.PauseVisible || Input.MouseMode != Input.MouseModeEnum.Visible)
            throw new InvalidOperationException("Dream pause did not open and release the pointer.");
        var resume = main.FindChild("PauseResume", true, false) as Button
            ?? throw new InvalidOperationException("Dream pause resume button is missing.");
        resume.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (main.PauseVisible)
            throw new InvalidOperationException("Dream pause did not close from Continue.");

        await DispatchKey(Key.Escape);
        var quit = main.FindChild("PauseQuit", true, false) as Button
            ?? throw new InvalidOperationException("Dream pause quit button is missing.");
        quit.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var quitDialogs = Descendants<ConfirmationDialog>(main)
            .Where(dialog => dialog.Title == "退出游戏").ToArray();
        if (!main.QuitConfirmationVisible || quitDialogs.Length != 1)
            throw new InvalidOperationException("Dream quit confirmation was not shown exactly once.");
        quit.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        quitDialogs = Descendants<ConfirmationDialog>(main)
            .Where(dialog => dialog.Title == "退出游戏").ToArray();
        if (!main.QuitConfirmationVisible || quitDialogs.Length != 1)
            throw new InvalidOperationException("Repeated quit request duplicated or dismissed confirmation.");
        quitDialogs[0].Hide();
    }

    private async Task ValidateDreamInventoryShortcuts(DreamEscapeController dream)
    {
        if (dream.ControlReminderVisible)
            throw new InvalidOperationException("Direct or restored dream load repeated the new-game reminder.");
        var areaBadge = dream.FindChild("AreaBadge", true, false) as PanelContainer
            ?? throw new InvalidOperationException("Compact dream area badge is missing.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (areaBadge.Size.X >= 300f || areaBadge.Size.Y > 48f ||
            dream.FindChild("AreaName", true, false) is not Label)
        {
            throw new InvalidOperationException(
                $"Dream HUD still occupies a task-bar-sized region: badge={areaBadge.Size}.");
        }

        var session = GetNode<GameSession>("/root/GameSession");
        session.State.DreamEscape!.Items.Add(DreamEscapeIds.ItemChalk);
        await DispatchKey(Key.Tab);
        if (!dream.InventoryVisible || GetViewport().GuiGetFocusOwner() is null ||
            Input.MouseMode != Input.MouseModeEnum.Visible)
        {
            throw new InvalidOperationException(
                "Tab did not open dream inventory with focus and a visible pointer.");
        }
        var inventory = Descendants<DreamInventoryPanel>(dream).Single();
        inventory.Refresh();
        var inventoryButtons = Descendants<Button>(inventory).Where(button => button.Visible).ToArray();
        if (inventoryButtons.Any(button => button.Text is "选用" or "取消选用") ||
            !inventoryButtons.Any(button => button.Text == "对自己使用"))
        {
            throw new InvalidOperationException(
                "Dream inventory still exposes ordinary-item selection or lost the pin action.");
        }

        await DispatchKey(Key.Escape);
        if (dream.InventoryVisible || GetViewport().GuiGetFocusOwner() is not null)
            throw new InvalidOperationException("Esc did not close dream inventory before opening pause.");

        await DispatchKey(Key.Tab);
        if (!dream.InventoryVisible || GetViewport().GuiGetFocusOwner() is null)
            throw new InvalidOperationException(
                $"Tab did not reopen dream inventory after Esc close: visible={dream.InventoryVisible} " +
                $"focus={GetViewport().GuiGetFocusOwner()?.Name} " +
                $"pause={Descendants<MainController>(GetTree().Root).FirstOrDefault()?.PauseVisible} " +
                $"mouse={Input.MouseMode}.");

        await DispatchKey(Key.Tab);
        if (dream.InventoryVisible || GetViewport().GuiGetFocusOwner() is not null)
        {
            throw new InvalidOperationException(
                "Tab did not close inventory and release its hidden GUI focus owner.");
        }
        await DispatchKey(Key.I);
        if (!dream.InventoryVisible)
            throw new InvalidOperationException("I did not open dream inventory.");
        await DispatchKey(Key.I);
        if (dream.InventoryVisible)
            throw new InvalidOperationException("I did not close dream inventory.");
        await DispatchKey(Key.Backspace);
        if (dream.InventoryVisible)
            throw new InvalidOperationException("Backspace still opened dream inventory.");
    }

    private async Task DispatchKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = true,
            Echo = false
        });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = false,
            Echo = false
        });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void ValidateDisplaySettings()
    {
        var window = GetTree().Root;
        if (window.ContentScaleSize != new Vector2I(640, 360) ||
            window.ContentScaleMode != Window.ContentScaleModeEnum.CanvasItems ||
            window.ContentScaleAspect != Window.ContentScaleAspectEnum.Keep ||
            window.ContentScaleStretch != Window.ContentScaleStretchEnum.Fractional)
        {
            throw new InvalidOperationException(
                $"Display stretch configuration is invalid: size={window.ContentScaleSize} " +
                $"mode={window.ContentScaleMode} aspect={window.ContentScaleAspect} " +
                $"stretch={window.ContentScaleStretch}.");
        }

        foreach (var preset in DisplaySettingsController.WindowedPresets)
        {
            if (DisplaySettingsController.ResolveWindowedSize(
                    preset.X,
                    preset.Y,
                    new Vector2I(2560, 1440)) != preset)
            {
                throw new InvalidOperationException($"Window preset {preset} did not round trip.");
            }
        }

        if (DisplaySettingsController.ResolveWindowedSize(
                1920,
                1080,
                new Vector2I(1366, 768)) != new Vector2I(1280, 720))
        {
            throw new InvalidOperationException("Oversized window preset was not clamped to the usable display.");
        }
        if (DisplaySettingsController.ResolveWindowedSize(
                1280,
                720,
                new Vector2I(1024, 700)) != new Vector2I(1024, 576))
        {
            throw new InvalidOperationException("Small displays did not receive a fitted 16:9 window.");
        }
    }

    private static void ValidateDreamArchiveRoom3D(DreamArchiveRoomController3D area)
    {
        var pairs = new[]
        {
            ("observe:target.archive.registry_rule", "puzzle:archive_signal_registry"),
            ("observe:target.archive.registry_choir", "puzzle:archive_signal_registry"),
            ("observe:target.archive.registry_broadcast", "puzzle:archive_signal_registry"),
            ("observe:target.archive.registry_art", "puzzle:archive_signal_registry"),
            ("observe:target.archive.registry_track", "puzzle:archive_signal_registry"),
            ("pickup:dream.archive_index_strip", "puzzle:archive_drawer_route"),
            ("observe:target.archive.fuse_route", "puzzle:archive_fuse_repair"),
            ("target.archive.recorder", "puzzle:archive_editing_counter")
        };
        var mapper = new AreaCoordinateMapper3D(
            new Rect2(16f, 54f, 608f, 272f), new Rect2(-5.5f, -4f, 11f, 8f));
        ValidateDreamLateRoom3D(area, area.ArchiveVisual, DreamEscapeIds.AreaArchive,
            "ArchiveModel", 2, mapper, pairs);
    }

    private static void ValidateDreamAuditorium3D(DreamAuditoriumController3D area)
    {
        var pairs = new[]
        {
            ("observe:target.auditorium.line_set_notches", "target.auditorium.line_set"),
            ("observe:target.auditorium.projection_order", "target.auditorium.projector"),
            ("observe:target.auditorium.tape_order", "puzzle:auditorium_master_tape_splice")
        };
        var mapper = new AreaCoordinateMapper3D(
            new Rect2(16f, 54f, 608f, 272f), new Rect2(-8.5f, -5.5f, 17f, 11f));
        ValidateDreamLateRoom3D(area, area.AuditoriumVisual, DreamEscapeIds.AreaAuditorium,
            "AuditoriumModel", 3, mapper, pairs);
    }

    private static void ValidateDreamLateRoom3D(
        WorldArea3D area,
        DreamLateRoomVisual3D visual,
        string areaId,
        string modelNodeName,
        int expectedReflectionProbes,
        AreaCoordinateMapper3D mapper,
        IReadOnlyList<(string Clue, string Mechanism)> separatedPairs)
    {
        var markers = area.GetNode<Node3D>("InteractionMarkers").GetChildren()
            .OfType<InteractionMarker3D>().ToArray();
        var byId = markers.ToDictionary(marker => marker.TargetId, StringComparer.Ordinal);
        var collisionBodies = visual.GetNode<Node3D>("CollisionBodies").GetChildren()
            .OfType<StaticBody3D>().ToArray();
        var reflectionProbes = area.GetChildren().OfType<ReflectionProbe>().Count();
        var tooClose = separatedPairs.Where(pair =>
                new Vector2(byId[pair.Clue].GlobalPosition.X, byId[pair.Clue].GlobalPosition.Z)
                    .DistanceTo(new Vector2(byId[pair.Mechanism].GlobalPosition.X,
                        byId[pair.Mechanism].GlobalPosition.Z)) < 2f)
            .Select(pair => $"{pair.Clue}->{pair.Mechanism}")
            .ToArray();
        var entryStored = DreamAreaSpawnCatalog.Resolve(DreamEscapeIds.AreaCorridor, areaId).StoredPosition;
        var entryWorld = visual.ResolveSpawn(mapper.ToWorld(entryStored));
        var autoFocused = markers.FirstOrDefault(marker =>
            new Vector2(marker.GlobalPosition.X, marker.GlobalPosition.Z).DistanceTo(
                new Vector2(entryWorld.X, entryWorld.Z)) <= marker.InteractionRange);
        var roundTripStored = mapper.ToStored(mapper.ToWorld(entryStored));
        var invalidStoredResolved = visual.ResolveSpawn(mapper.ToWorld(new Vector2(-500f, 900f)));

        if (area.CameraControlMode != CameraControlMode.Orbit || area.InteractionMarkerCount != 15 ||
            markers.Length != 15 || byId.Count != 15 || tooClose.Length > 0 ||
            autoFocused is not null || collisionBodies.Length != visual.CollisionBodyCount ||
            visual.FurnitureCollisionCount < 8 || expectedReflectionProbes != reflectionProbes ||
            area.GetNodeOrNull<Node3D>(modelNodeName) is null ||
            entryStored.DistanceTo(roundTripStored) > 0.01f || visual.IsBlocked(entryWorld) ||
            visual.IsBlocked(invalidStoredResolved) ||
            collisionBodies.All(body => !body.Name.ToString().Contains("Ceiling", StringComparison.Ordinal)) ||
            collisionBodies.All(body => !body.Name.ToString().Contains("Barrier", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Late-room 3D composition invalid: area={areaId} markers={markers.Length} " +
                $"collisions={collisionBodies.Length}/{visual.CollisionBodyCount} " +
                $"furniture={visual.FurnitureCollisionCount} probes={reflectionProbes} " +
                $"autoFocus={autoFocused?.TargetId ?? "none"} tooClose={string.Join(',', tooClose)}.");
        }

        ValidateLateRoomMarkerReachability(visual, markers, entryWorld, areaId);
    }

    private static void ValidateLateRoomMarkerReachability(
        DreamLateRoomVisual3D visual,
        IReadOnlyList<InteractionMarker3D> markers,
        Vector3 startWorld,
        string areaId)
    {
        const float step = 0.20f;
        var bounds = visual.WalkableBounds;
        var width = Mathf.FloorToInt(bounds.Size.X / step) + 1;
        var height = Mathf.FloorToInt(bounds.Size.Y / step) + 1;
        var visited = new bool[width, height];
        var queue = new Queue<Vector2I>();
        var start = new Vector2I(
            Mathf.Clamp(Mathf.RoundToInt((startWorld.X - bounds.Position.X) / step), 0, width - 1),
            Mathf.Clamp(Mathf.RoundToInt((startWorld.Z - bounds.Position.Y) / step), 0, height - 1));
        visited[start.X, start.Y] = true;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
            {
                var next = current + direction;
                if (next.X < 0 || next.X >= width || next.Y < 0 || next.Y >= height ||
                    visited[next.X, next.Y]) continue;
                var position = new Vector3(bounds.Position.X + next.X * step, 0f,
                    bounds.Position.Y + next.Y * step);
                if (visual.IsBlocked(position)) continue;
                visited[next.X, next.Y] = true;
                queue.Enqueue(next);
            }
        }

        var unreachable = markers.Where(marker =>
        {
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                if (!visited[x, y]) continue;
                var point = new Vector2(bounds.Position.X + x * step, bounds.Position.Y + y * step);
                if (point.DistanceTo(new Vector2(marker.GlobalPosition.X, marker.GlobalPosition.Z)) <=
                    marker.InteractionRange) return false;
            }
            return true;
        }).Select(marker => marker.TargetId).ToArray();
        if (unreachable.Length > 0)
            throw new InvalidOperationException(
                $"3D interaction targets are unreachable in {areaId}: {string.Join(',', unreachable)}.");
    }

    private async Task ValidateDream3DLockedMarkers(GameSession session)
    {
        session.NewGame("锁后标记", "同学");
        var dream = session.State.DreamEscape
            ?? throw new InvalidOperationException("Dream state missing during locked-marker smoke.");
        dream.XiaoyuJoined = true;
        dream.UnlockedAreas.Add(DreamEscapeIds.AreaArchive);
        dream.CurrentAreaId = DreamEscapeIds.AreaArchive;

        var archivePacked = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamArchiveRoom.tscn")
            ?? throw new InvalidOperationException("Could not load archive room for locked-marker smoke.");
        var archive = archivePacked.Instantiate<DreamArchiveRoomController3D>();
        AddChild(archive);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (archive.ActiveInteractionIds.Contains("observe:target.archive.fuse_route") ||
            !archive.ActiveInteractionIds.Contains("puzzle:archive_signal_registry") ||
            archive.ArchiveVisual.RegistrySolved)
        {
            throw new InvalidOperationException("Archive fuse-route clue was visible before its reward cabinet opened.");
        }
        archive.ApplyTransientState(new DreamAreaTransientState(false,
            DreamEscapeIds.PuzzleArchiveSignalRegistry, ["choir", "broadcast"]));
        if (archive.ArchiveVisual.RegistryPreviewCount != 2)
            throw new InvalidOperationException("Archive registry did not apply its transient panel preview.");
        archive.ApplyTransientState(new DreamAreaTransientState(false, null, null));
        if (archive.ArchiveVisual.RegistryPreviewCount != 0)
            throw new InvalidOperationException("Archive registry preview survived panel close.");
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleArchiveSignalRegistry);
        archive.RefreshState();
        if (!archive.ActiveInteractionIds.Contains("observe:target.archive.fuse_route") ||
            archive.ActiveInteractionIds.Contains("puzzle:archive_signal_registry") ||
            !archive.ArchiveVisual.RegistrySolved)
        {
            throw new InvalidOperationException("Archive order completion did not swap in the fuse-route clue.");
        }
        dream.DiscoveredClues.Add(DreamEscapeIds.ClueArchiveFuseRoute);
        archive.RefreshState();
        if (archive.ActiveInteractionIds.Contains("observe:target.archive.fuse_route") ||
            !archive.ArchiveVisual.RegistrySolved)
            throw new InvalidOperationException("Recorded archive fuse-route clue remained interactive.");
        archive.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        dream.UnlockedAreas.Add(DreamEscapeIds.AreaAuditorium);
        dream.CurrentAreaId = DreamEscapeIds.AreaAuditorium;
        var auditoriumPacked = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamAuditorium.tscn")
            ?? throw new InvalidOperationException("Could not load auditorium for locked-marker smoke.");
        var auditorium = auditoriumPacked.Instantiate<DreamAuditoriumController3D>();
        AddChild(auditorium);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (auditorium.ActiveInteractionIds.Contains("observe:target.auditorium.projection_order") ||
            auditorium.ActiveInteractionIds.Contains("observe:target.auditorium.tape_order") ||
            auditorium.AuditoriumVisual.CurtainRaised ||
            auditorium.AuditoriumVisual.ProjectorSolved)
        {
            throw new InvalidOperationException("Auditorium lock-behind clues were visible before their prerequisites.");
        }
        auditorium.ApplyTransientState(new DreamAreaTransientState(false,
            DreamEscapeIds.PuzzleAuditoriumLineSetBalance, ["3", "1", "4", "2"]));
        if (!auditorium.AuditoriumVisual.LineSetValues.SequenceEqual([3, 1, 4, 2]))
            throw new InvalidOperationException("Auditorium line set did not apply its transient panel preview.");
        auditorium.ApplyTransientState(new DreamAreaTransientState(false, null, null));
        if (!auditorium.AuditoriumVisual.LineSetValues.SequenceEqual([1, 1, 1, 1]))
            throw new InvalidOperationException("Auditorium line-set preview survived panel close.");
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAuditoriumLineSetBalance);
        auditorium.RefreshState();
        if (!auditorium.ActiveInteractionIds.Contains("observe:target.auditorium.projection_order") ||
            auditorium.ActiveInteractionIds.Contains("target.auditorium.line_set") ||
            !auditorium.AuditoriumVisual.CurtainRaised)
        {
            throw new InvalidOperationException("Raised curtain did not swap in the projection clue.");
        }
        dream.DiscoveredClues.Add(DreamEscapeIds.ClueAuditoriumProjectionOrder);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAuditoriumShadowProjector);
        auditorium.RefreshState();
        if (!auditorium.ActiveInteractionIds.Contains("observe:target.auditorium.tape_order") ||
            auditorium.ActiveInteractionIds.Contains("target.auditorium.projector") ||
            !auditorium.AuditoriumVisual.CurtainRaised ||
            !auditorium.AuditoriumVisual.ProjectorSolved)
        {
            throw new InvalidOperationException("Projector completion did not swap in the tape-order card.");
        }
        dream.DiscoveredClues.Add(DreamEscapeIds.ClueAuditoriumTapeOrder);
        auditorium.RefreshState();
        if (auditorium.ActiveInteractionIds.Contains("observe:target.auditorium.tape_order") ||
            !auditorium.AuditoriumVisual.ProjectorSolved)
            throw new InvalidOperationException("Recorded auditorium tape-order clue remained interactive.");
        auditorium.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task ValidateAuditoriumTwoSidedExit(GameSession session)
    {
        session.NewGame("双侧出口", "同学");
        var dream = session.State.DreamEscape
            ?? throw new InvalidOperationException("Dream state missing during auditorium exit smoke.");
        dream.UnlockedAreas.Add(DreamEscapeIds.AreaAuditorium);
        dream.CurrentAreaId = DreamEscapeIds.AreaAuditorium;
        dream.PositionX = 320f;
        dream.PositionY = 270f;
        dream.XiaoyuJoined = true;
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAuditoriumLightConsole);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAuditoriumSoundConsole);
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleAuditoriumBroadcastConsole);
        dream.InstalledParts.Add(DreamEscapeIds.SlotAuditoriumMasterTape);

        var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamEscape.tscn")
            ?? throw new InvalidOperationException("Could not load dream root for auditorium exit smoke.");
        var dreamRoot = packed.Instantiate<DreamEscapeController>();
        AddChild(dreamRoot);
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            for (var frame = 0; frame < 120 && session.Modal.IsBlocked; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (session.Modal.IsBlocked)
                throw new InvalidOperationException("A previous smoke transition left the auditorium modal-blocked.");
            var whiteNoise = dreamRoot.GetNode<AudioStreamPlayer>("MasterTapeWhiteNoise");
            if (whiteNoise.Stream is null || !whiteNoise.Playing)
                throw new InvalidOperationException("Installed master tape did not start the offline white-noise stream.");
            var auditorium = dreamRoot.GetNode<DreamAuditoriumController3D>("AreaHost/DreamAuditorium3D");
            var markerById = auditorium.GetNode<Node3D>("InteractionMarkers").GetChildren()
                .OfType<InteractionMarker3D>()
                .ToDictionary(marker => marker.TargetId, StringComparer.Ordinal);
            var player = auditorium.PlayerActor;

            async Task InteractAt(string targetId)
            {
                player.GlobalPosition = markerById[targetId].GlobalPosition;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (auditorium.CurrentInteractionTargetId != targetId)
                {
                    throw new InvalidOperationException(
                        $"Auditorium focus mismatch: expected={targetId} " +
                        $"actual={auditorium.CurrentInteractionTargetId} position={player.GlobalPosition}.");
                }
                Input.ParseInputEvent(new InputEventAction { Action = "interact", Pressed = true });
                Input.ParseInputEvent(new InputEventAction { Action = "interact", Pressed = false });
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            await InteractAt("action:open_exit");
            if (dream.Status != DreamEscapeStatus.InProgress || dreamRoot.XiaoyuHoldingExitLever)
                throw new InvalidOperationException("Right master switch opened without the temporary left-lever hold.");

            await InteractAt("action:left_lever");
            if (!dreamRoot.XiaoyuHoldingExitLever || !auditorium.AuditoriumVisual.ExitLeverHeld)
                throw new InvalidOperationException("Left-lever interaction did not stage Xiaoyu's temporary hold.");

            await InteractAt($"area:{DreamEscapeIds.AreaCorridor}");
            for (var frame = 0; frame < 180 &&
                 dreamRoot.GetNodeOrNull<DreamMansionController3D>("AreaHost/DreamMansion3D") is null; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            for (var frame = 0; frame < 120 && session.Modal.IsBlocked; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (dreamRoot.GetNodeOrNull<DreamMansionController3D>("AreaHost/DreamMansion3D") is null ||
                dreamRoot.XiaoyuHoldingExitLever || session.Modal.IsBlocked)
            {
                throw new InvalidOperationException("Leaving the auditorium did not cancel the temporary lever hold.");
            }
        }
        finally
        {
            dreamRoot.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        dream.CurrentAreaId = DreamEscapeIds.AreaAuditorium;
        dream.PositionX = 320f;
        dream.PositionY = 270f;
        dreamRoot = packed.Instantiate<DreamEscapeController>();
        var exitOpenedEvent = false;
        dreamRoot.ExitOpened += () => exitOpenedEvent = true;
        AddChild(dreamRoot);
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            for (var frame = 0; frame < 120 && session.Modal.IsBlocked; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (session.Modal.IsBlocked)
                throw new InvalidOperationException("Reloaded auditorium remained modal-blocked.");
            var auditorium = dreamRoot.GetNode<DreamAuditoriumController3D>("AreaHost/DreamAuditorium3D");
            if (auditorium.AuditoriumVisual.ExitLeverHeld)
                throw new InvalidOperationException("Reloaded auditorium restored a transient lever pose.");
            var markerById = auditorium.GetNode<Node3D>("InteractionMarkers").GetChildren()
                .OfType<InteractionMarker3D>()
                .ToDictionary(marker => marker.TargetId, StringComparer.Ordinal);
            var player = auditorium.PlayerActor;

            async Task InteractAt(string targetId)
            {
                player.GlobalPosition = markerById[targetId].GlobalPosition;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (auditorium.CurrentInteractionTargetId != targetId)
                {
                    throw new InvalidOperationException(
                        $"Auditorium focus mismatch: expected={targetId} " +
                        $"actual={auditorium.CurrentInteractionTargetId} position={player.GlobalPosition}.");
                }
                Input.ParseInputEvent(new InputEventAction { Action = "interact", Pressed = true });
                Input.ParseInputEvent(new InputEventAction { Action = "interact", Pressed = false });
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            await InteractAt("action:open_exit");
            await InteractAt("action:left_lever");
            await InteractAt("action:open_exit");
            if (dream.Status != DreamEscapeStatus.ExitOpened || !exitOpenedEvent ||
                dreamRoot.XiaoyuHoldingExitLever)
            {
                throw new InvalidOperationException("Two-sided auditorium exit sequence did not complete atomically.");
            }
        }
        finally
        {
            dreamRoot.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task ValidateDreamMusicRoom3D(
        GameSession session,
        DreamMusicRoomController3D musicRoom)
    {
        var dream = session.State.DreamEscape
            ?? throw new InvalidOperationException("Dream state missing during music-room smoke.");
        var visual = musicRoom.MusicVisual;
        var markers = musicRoom.GetNode<Node3D>("InteractionMarkers").GetChildren()
            .OfType<InteractionMarker3D>().ToArray();
        var expectedMarkerIds = new HashSet<string>(StringComparer.Ordinal)
        {
            $"area:{DreamEscapeIds.AreaCorridor}",
            "target.music.score",
            "puzzle:music_cage",
            "pickup:dream.rubber_mallet",
            "pickup:dream.listening_cone",
            "pickup:dream.metronome_weight",
            "pickup:dream.torn_piano_roll",
            "pickup:dream.wax_patch",
            "target.music.metronome",
            "puzzle:music_tuning_forks",
            "puzzle:music_piano_roll",
            "puzzle:music_acoustic_wall",
            "puzzle:music_attendance_reader",
            "target.music.piano_bench",
            "hint:xiaoyu",
            "observe:target.music.tempo_notice",
            "observe:target.music.fork_reference",
            "observe:target.music.roll_template"
        };
        var actualMarkerIds = markers.Select(marker => marker.TargetId)
            .ToHashSet(StringComparer.Ordinal);
        var collisionNodes = visual.GetNode<Node3D>("CollisionBodies").GetChildren()
            .OfType<StaticBody3D>().Count();
        var reflectionProbes = musicRoom.GetChildren().OfType<ReflectionProbe>().Count();
        var route = DreamAreaSpawnCatalog.Resolve(
            DreamEscapeIds.AreaCorridor,
            DreamEscapeIds.AreaMusic);
        var mapper = new AreaCoordinateMapper3D(
            new Rect2(16f, 54f, 608f, 272f),
            new Rect2(-5.25f, -3.75f, 10.5f, 7.5f));
        var expectedSpawn = mapper.ToWorld(route.StoredPosition);
        var safeFallback = visual.ResolveSpawn(new Vector3(0.8f, 0f, 3.32f));
        var playerShape = musicRoom.PlayerActor.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
        var musicAudioPaths = new[]
        {
            "res://assets/generated/dream_escape/audio/music-room-ambience.wav",
            "res://assets/generated/dream_escape/audio/music-metronome-72.wav",
            "res://assets/generated/dream_escape/audio/music-tuning-forks-a3-c4-e4-a4.wav",
            "res://assets/generated/dream_escape/audio/music-acoustic-wall.wav"
        };
        var missingAudio = musicAudioPaths
            .Where(path => ResourceLoader.Load<AudioStream>(path) is null)
            .ToArray();

        ValidateDefaultCharacterVisual(musicRoom.PlayerActor);
        if (musicRoom.CameraControlMode != CameraControlMode.Orbit ||
            musicRoom.InteractionMarkerCount != 18 || markers.Length != 18 ||
            !actualMarkerIds.SetEquals(expectedMarkerIds) ||
            collisionNodes != 16 || visual.CollisionBodyCount != 16 ||
            visual.FurnitureCollisionCount != 7 || reflectionProbes != 2 ||
            playerShape is null || missingAudio.Length > 0 ||
            musicRoom.AmbiencePlayer.Stream is null || !musicRoom.AmbiencePlayer.Playing ||
            visual.IsBlocked(expectedSpawn) ||
            musicRoom.PlayerActor.GlobalPosition.DistanceTo(expectedSpawn) > 0.001f ||
            visual.IsBlocked(safeFallback) || safeFallback.IsEqualApprox(new Vector3(0.8f, 0f, 3.32f)))
        {
            throw new InvalidOperationException(
                $"3D music-room composition invalid: mode={musicRoom.CameraControlMode} " +
                $"markers={markers.Length}/{musicRoom.InteractionMarkerCount} " +
                $"collisions={collisionNodes}/{visual.FurnitureCollisionCount} " +
                $"reflectionProbes={reflectionProbes} playerShape={playerShape is not null} " +
                $"audio={musicAudioPaths.Length - missingAudio.Length}/{musicAudioPaths.Length} " +
                $"spawn={musicRoom.PlayerActor.GlobalPosition} fallback={safeFallback}.");
        }

        var rig = musicRoom.CameraController;
        if (rig.Camera.Projection != Camera3D.ProjectionType.Perspective ||
            Math.Abs(rig.Camera.Fov - AdoptedCameraFov) > 0.01f ||
            Math.Abs(rig.FixedPitchDegrees - AdoptedCameraPitch) > 0.01f ||
            Math.Abs(rig.FollowTargetHeight - AdoptedCameraFollowHeight) > 0.01f ||
            Math.Abs(rig.SpringArm.SpringLength - AdoptedCameraSpringLength) > 0.01f ||
            Math.Abs(rig.Camera.Position.X - AdoptedCameraShoulderOffset) > 0.01f)
        {
            throw new InvalidOperationException("Music-room Orbit camera no longer matches the adopted framing.");
        }

        ValidateMusicMarkerReachability(visual, markers, expectedSpawn);
        ValidateMusicTextAlternatives(visual);
        var markerById = markers.ToDictionary(marker => marker.TargetId, StringComparer.Ordinal);
        var musicSeparated = new[]
        {
            ("observe:target.music.tempo_notice", "target.music.metronome"),
            ("observe:target.music.fork_reference", "puzzle:music_tuning_forks"),
            ("observe:target.music.roll_template", "puzzle:music_piano_roll")
        };
        if (musicSeparated.Any(pair => new Vector2(
                    markerById[pair.Item1].Position.X, markerById[pair.Item1].Position.Z)
                .DistanceTo(new Vector2(
                    markerById[pair.Item2].Position.X, markerById[pair.Item2].Position.Z)) < 2f))
            throw new InvalidOperationException("Music-room clue/mechanism separation fell below two metres.");

        var originalJoined = dream.XiaoyuJoined;
        var originalSolved = dream.SolvedPuzzles.ToArray();
        var originalInstalled = dream.InstalledParts.ToArray();
        try
        {
            dream.XiaoyuJoined = false;
            dream.SolvedPuzzles.Clear();
            dream.InstalledParts.Clear();
            musicRoom.RefreshState();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (visual.ScoreRevealed || visual.CageOpen || visual.VisiblePickupCount != 0 ||
                visual.MetronomeWeightInstalled || visual.MetronomeSolved ||
                visual.TuningForksSolved || visual.PianoRollInstalled ||
                visual.AcousticWallSolved || visual.AttendanceReaderOpen ||
                visual.PianoBenchOpen || musicRoom.Companion.IsFollowing ||
                !musicRoom.Companion.Visible ||
                musicRoom.Companion.GlobalPosition.DistanceTo(new Vector3(3.8f, 0f, -3.15f)) > 0.001f ||
                musicRoom.ActiveInteractionMarkerCount != 6)
            {
                throw new InvalidOperationException("Initial music-room Core-derived visual state is invalid.");
            }

            dream.SolvedPuzzles.UnionWith([
                DreamEscapeIds.PuzzleMusicScore,
                DreamEscapeIds.PuzzleMusicCage
            ]);
            dream.XiaoyuJoined = true;
            musicRoom.RefreshState();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var companionDistance = musicRoom.Companion.GlobalPosition
                .DistanceTo(musicRoom.PlayerActor.GlobalPosition);
            if (!visual.ScoreRevealed || !visual.CageOpen || visual.VisiblePickupCount != 5 ||
                !musicRoom.Companion.IsFollowing || !musicRoom.Companion.Visible ||
                companionDistance is < 1.15f or > 1.35f ||
                musicRoom.Companion.Sprite.Billboard != BaseMaterial3D.BillboardModeEnum.FixedY ||
                musicRoom.ActiveInteractionMarkerCount != 16)
            {
                throw new InvalidOperationException(
                    $"Joined music-room state is invalid: pickups={visual.VisiblePickupCount} " +
                    $"companionDistance={companionDistance:F2} " +
                    $"activeMarkers={musicRoom.ActiveInteractionMarkerCount}.");
            }

            musicRoom.Companion.GlobalPosition = new Vector3(100f, 0f, 100f);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (musicRoom.Companion.GlobalPosition.DistanceTo(musicRoom.PlayerActor.GlobalPosition) > 1.35f)
                throw new InvalidOperationException("Music-room companion did not recover from abnormal distance.");

            dream.InstalledParts.UnionWith([
                DreamEscapeIds.SlotMusicMetronomeWeight,
                DreamEscapeIds.SlotMusicPianoRoll
            ]);
            musicRoom.RefreshState();
            if (!visual.MetronomeWeightInstalled || !visual.PianoRollInstalled)
                throw new InvalidOperationException("Music-room installed parts are not derived from Core slots.");

            dream.SolvedPuzzles.UnionWith([
                DreamEscapeIds.PuzzleMusicMetronome,
                DreamEscapeIds.PuzzleMusicTuningForks,
                DreamEscapeIds.PuzzleMusicPianoRoll,
                DreamEscapeIds.PuzzleMusicAcousticWall,
                DreamEscapeIds.PuzzleMusicAttendanceReader,
                DreamEscapeIds.PuzzleMusicPianoBenchCache
            ]);
            musicRoom.RefreshState();
            if (!visual.MetronomeSolved || !visual.TuningForksSolved ||
                !visual.PianoRollInstalled || !visual.AcousticWallSolved ||
                !visual.AttendanceReaderOpen || !visual.PianoBenchOpen)
            {
                throw new InvalidOperationException("Solved music-room visuals did not follow Core puzzle state.");
            }

            var closedYaw = visual.DoorHinge.Rotation.Y;
            await musicRoom.PlayExitTransitionAsync(
                $"area:{DreamEscapeIds.AreaCorridor}", CancellationToken.None);
            if (Math.Abs(Mathf.AngleDifference(closedYaw, visual.DoorHinge.Rotation.Y)) <
                Mathf.DegToRad(60f))
            {
                throw new InvalidOperationException("Music-room exit door did not play its controlled transition.");
            }
            musicRoom.ResetExitTransition();
            if (Math.Abs(Mathf.AngleDifference(closedYaw, visual.DoorHinge.Rotation.Y)) > 0.001f)
                throw new InvalidOperationException("Music-room exit door did not reset after its transition.");
        }
        finally
        {
            dream.XiaoyuJoined = originalJoined;
            dream.SolvedPuzzles.Clear();
            dream.SolvedPuzzles.UnionWith(originalSolved);
            dream.InstalledParts.Clear();
            dream.InstalledParts.UnionWith(originalInstalled);
            musicRoom.ResetExitTransition();
            musicRoom.RefreshState();
        }
    }

    private static void ValidateMusicTextAlternatives(DreamMusicVisual3D visual)
    {
        var geometry = visual.GetNode<Node3D>("MusicRoomGeometry");
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["BeatGlyphs"] = "○   ◇   □   △",
            ["ForkMap"] = "○ A3    ◇ C4    □ E4    △ A4",
            ["PitchInstrumentMap"] = "A3 木鱼   C4 钢琴   E4 三角铁   A4 手铃"
        };
        var invalid = expected.Where(pair =>
                geometry.FindChild(pair.Key, true, false) is not Label3D label ||
                label.Text != pair.Value)
            .Select(pair => pair.Key)
            .ToArray();
        if (invalid.Length > 0)
        {
            throw new InvalidOperationException(
                $"Music-room non-audio puzzle labels are missing: {string.Join(',', invalid)}.");
        }
    }

    private static void ValidateMusicMarkerReachability(
        DreamMusicVisual3D visual,
        IReadOnlyList<InteractionMarker3D> markers,
        Vector3 startWorld)
    {
        const float step = 0.20f;
        var bounds = visual.WalkableBounds;
        var width = Mathf.FloorToInt(bounds.Size.X / step) + 1;
        var height = Mathf.FloorToInt(bounds.Size.Y / step) + 1;
        var visited = new bool[width, height];
        var queue = new Queue<Vector2I>();
        var start = new Vector2I(
            Mathf.RoundToInt((startWorld.X - bounds.Position.X) / step),
            Mathf.RoundToInt((startWorld.Z - bounds.Position.Y) / step));
        visited[start.X, start.Y] = true;
        queue.Enqueue(start);
        var directions = new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in directions)
            {
                var next = current + direction;
                if (next.X < 0 || next.X >= width || next.Y < 0 || next.Y >= height ||
                    visited[next.X, next.Y]) continue;
                var position = new Vector3(
                    bounds.Position.X + next.X * step,
                    0f,
                    bounds.Position.Y + next.Y * step);
                if (visual.IsBlocked(position)) continue;
                visited[next.X, next.Y] = true;
                queue.Enqueue(next);
            }
        }

        var unreachable = markers.Where(marker =>
        {
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                if (!visited[x, y]) continue;
                var position = new Vector2(
                    bounds.Position.X + x * step,
                    bounds.Position.Y + y * step);
                var markerPosition = new Vector2(marker.GlobalPosition.X, marker.GlobalPosition.Z);
                if (position.DistanceTo(markerPosition) <= marker.InteractionRange) return false;
            }
            return true;
        }).Select(marker => marker.TargetId).ToArray();
        if (unreachable.Length > 0)
        {
            throw new InvalidOperationException(
                $"3D music-room interaction markers are unreachable: {string.Join(',', unreachable)}.");
        }
    }

    private async Task ValidateDreamMansion3D(
        GameSession session,
        DreamMansionController3D mansion)
    {
        var dream = session.State.DreamEscape
            ?? throw new InvalidOperationException("Dream state missing during mansion smoke.");
        var visual = mansion.MansionVisual;
        ValidateDefaultCharacterVisual(mansion.PlayerActor);
        var markers = mansion.GetNode<Node3D>("InteractionMarkers").GetChildren()
            .OfType<InteractionMarker3D>().ToArray();
        var expectedMarkerIds = new HashSet<string>(StringComparer.Ordinal)
        {
            $"area:{DreamEscapeIds.AreaClassroom}",
            "target.corridor.room_two_door",
            $"area:{DreamEscapeIds.AreaMusic}",
            "target.corridor.room_three_door",
            $"area:{DreamEscapeIds.AreaArchive}",
            "target.corridor.room_four_door",
            $"area:{DreamEscapeIds.AreaAuditorium}",
            "locked:mansion_front_door"
        };
        var actualMarkerIds = markers.Select(marker => marker.TargetId)
            .ToHashSet(StringComparer.Ordinal);
        var collisionNodes = visual.GetNode<Node3D>("CollisionBodies").GetChildren()
            .OfType<StaticBody3D>().ToArray();
        var expectedFurniturePositions = new Dictionary<string, Vector3>(StringComparer.Ordinal)
        {
            ["Fireplace"] = new Vector3(-3.31f, 1.03f, -3.45f),
            ["ConsoleTable"] = new Vector3(3.06f, 0.75f, -1.72f),
            ["FoyerBench"] = new Vector3(3.00f, 0.75f, 6.80f)
        };
        var misalignedFurniture = expectedFurniturePositions
            .Where(expected => collisionNodes.FirstOrDefault(body => body.Name == expected.Key) is not { } body ||
                               body.Position.DistanceTo(expected.Value) > 0.001f)
            .Select(expected => expected.Key)
            .ToArray();
        var blockedMarkers = markers
            .Where(marker => visual.IsBlocked(marker.GlobalPosition))
            .Select(marker => marker.TargetId)
            .ToArray();
        var reflectionProbes = mansion.GetChildren().OfType<ReflectionProbe>().Count();
        if (mansion.CameraControlMode != CameraControlMode.Orbit ||
            mansion.InteractionMarkerCount != 8 || markers.Length != 8 ||
            !actualMarkerIds.SetEquals(expectedMarkerIds) ||
            mansion.ActiveInteractionMarkerCount != 5 ||
            collisionNodes.Length != 14 || visual.CollisionBodyCount != 14 || reflectionProbes != 2 ||
            visual.FurnitureCollisionCount != 3 || visual.DoorBarrierCount != 5 ||
            visual.DoorHingeCount != 7 || blockedMarkers.Length > 0 ||
            misalignedFurniture.Length > 0)
        {
            throw new InvalidOperationException(
                $"3D mansion composition invalid: mode={mansion.CameraControlMode} " +
                $"markers={markers.Length}/{mansion.ActiveInteractionMarkerCount} " +
                $"collisions={collisionNodes.Length} furniture={visual.FurnitureCollisionCount} " +
                $"doors={visual.DoorBarrierCount}/{visual.DoorHingeCount} " +
                $"reflectionProbes={reflectionProbes} " +
                $"blockedMarkers={string.Join(',', blockedMarkers)} " +
                $"misalignedFurniture={string.Join(',', misalignedFurniture)}.");
        }

        ValidateMansionArtPass(visual);
        ValidateMansionCoordinateMapping();
        await ValidateMansionSpawnRoutes(session, mansion);
        ValidateMansionMarkerReachability(visual, markers);
        await ValidateMansionCameraAndDoors(session, mansion);

        var originalUnlocked = dream.UnlockedAreas.ToArray();
        try
        {
            dream.UnlockedAreas.UnionWith([
                DreamEscapeIds.AreaMusic,
                DreamEscapeIds.AreaArchive,
                DreamEscapeIds.AreaAuditorium
            ]);
            mansion.RefreshState();
            if (mansion.ActiveInteractionMarkerCount != 5)
                throw new InvalidOperationException("Mansion open/locked marker pairs were not mutually exclusive.");
        }
        finally
        {
            dream.UnlockedAreas.Clear();
            dream.UnlockedAreas.UnionWith(originalUnlocked);
            mansion.RefreshState();
        }
    }

    private static void ValidateMansionArtPass(DreamMansionVisual3D visual)
    {
        var model = visual.ModelRoot;
        var requiredNodes = new[]
        {
            "Door_Room1_Hinge",
            "Door_Room2_Hinge",
            "Door_Room3_Hinge",
            "Door_Room4_Hinge_L",
            "Door_Room4_Hinge_R",
            "Door_Front_Hinge_L",
            "Door_Front_Hinge_R",
            "FULL_Manor_DoorMoulding_01",
            "FULL_Manor_DoorMoulding_07",
            "FULL_Manor_WainscotEast",
            "FULL_Manor_SconceBrasswork",
            "FULL_Manor_FireplaceMoulding",
            "FULL_Manor_ReservedModules"
        };
        var missing = requiredNodes.Where(name => model.FindChild(name, true, false) is null).ToArray();
        var meshes = Descendants<MeshInstance3D>(model).ToArray();
        var materials = meshes
            .SelectMany(mesh => Enumerable.Range(0, mesh.Mesh?.GetSurfaceCount() ?? 0)
                .Select(surface => mesh.GetActiveMaterial(surface)))
            .OfType<BaseMaterial3D>()
            .ToArray();
        var materialNames = materials
            .Select(material => material.ResourceName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);
        var requiredMaterialNames = new[]
        {
            "M_Cinematic_Manor_Plaster",
            "M_Cinematic_Manor_Floor",
            "M_Cinematic_Manor_Wood",
            "M_Cinematic_Manor_Rug",
            "M_Cinematic_Manor_Brass"
        };
        var missingMaterials = requiredMaterialNames
            .Where(name => !materialNames.Contains(name))
            .ToArray();
        var textured = materials.Count(material => material.AlbedoTexture is not null);
        var normalMapped = materials.Count(material => material.NormalTexture is not null);
        var ormMapped = materials.Count(material => material.OrmTexture is not null ||
                                                     material.RoughnessTexture is not null);
        var expectedHingePositions = new Dictionary<string, Vector3>(StringComparer.Ordinal)
        {
            ["Door_Front_Hinge_L"] = new Vector3(-1.23f, 0f, -6.80f),
            ["Door_Front_Hinge_R"] = new Vector3(1.23f, 0f, -6.80f),
            ["Door_Room1_Hinge"] = new Vector3(-3.39f, 0f, 2.62f),
            ["Door_Room2_Hinge"] = new Vector3(3.39f, 0f, 3.78f),
            ["Door_Room3_Hinge"] = new Vector3(-3.39f, 0f, 11.42f),
            ["Door_Room4_Hinge_L"] = new Vector3(-1.36f, 0f, 18.0f),
            ["Door_Room4_Hinge_R"] = new Vector3(1.36f, 0f, 18.0f)
        };
        var misalignedHinges = expectedHingePositions
            .Where(expected => model.FindChild(expected.Key, true, false) is not Node3D hinge ||
                               hinge.Position.DistanceTo(expected.Value) > 0.02f)
            .Select(expected => expected.Key)
            .ToArray();
        if (meshes.Length == 0 || missing.Length > 0 || missingMaterials.Length > 0 ||
            misalignedHinges.Length > 0 ||
            materialNames.Count < 11 ||
            textured == 0 || normalMapped == 0 || ormMapped == 0 ||
            materials.Any(material => material.TextureFilter !=
                BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic))
        {
            throw new InvalidOperationException(
                $"Dream mansion art pass invalid: meshes={meshes.Length} " +
                $"missing={string.Join(',', missing)} textured={textured} " +
                $"materials={materialNames.Count} missingMaterials={string.Join(',', missingMaterials)} " +
                $"misalignedHinges={string.Join(',', misalignedHinges)} " +
                $"normal={normalMapped} orm={ormMapped}.");
        }
    }

    private static void ValidateMansionCoordinateMapping()
    {
        var mapper = new DreamMansionCoordinateMapper3D();
        var southWest = mapper.ToWorld(Vector2.Zero);
        var northEast = mapper.ToWorld(new Vector2(640f, 360f));
        if (southWest.DistanceTo(new Vector3(-3.8f, 0f, -6.8f)) > 0.001f ||
            northEast.DistanceTo(new Vector3(3.8f, 0f, 18f)) > 0.001f)
        {
            throw new InvalidOperationException(
                $"Mansion coordinate bounds are invalid: {southWest} -> {northEast}.");
        }

        foreach (var stored in new[]
                 {
                     Vector2.Zero,
                     new Vector2(640f, 360f),
                     new Vector2(121.25f, 83.75f),
                     new Vector2(512.5f, 244.25f)
                 })
        {
            var roundTrip = mapper.ToStored(mapper.ToWorld(stored));
            if (roundTrip.DistanceTo(stored) > 0.001f)
                throw new InvalidOperationException($"Mansion coordinate round trip failed: {stored} -> {roundTrip}.");
        }
    }

    private async Task ValidateMansionSpawnRoutes(
        GameSession session,
        DreamMansionController3D mansion)
    {
        var mapper = new DreamMansionCoordinateMapper3D();
        var routes = new[]
        {
            (DreamEscapeIds.AreaClassroom, new Vector3(-2.2f, 0f, 3.2f), Vector3.Right,
                $"area:{DreamEscapeIds.AreaClassroom}"),
            (DreamEscapeIds.AreaMusic, new Vector3(2.2f, 0f, 3.2f), Vector3.Left,
                "target.corridor.room_two_door"),
            (DreamEscapeIds.AreaArchive, new Vector3(-2.2f, 0f, 13.2f), Vector3.Right,
                "target.corridor.room_three_door"),
            (DreamEscapeIds.AreaAuditorium, new Vector3(0f, 0f, 16.8f), Vector3.Forward,
                "target.corridor.room_four_door")
        };
        var markerById = mansion.GetNode<Node3D>("InteractionMarkers").GetChildren()
            .OfType<InteractionMarker3D>()
            .ToDictionary(marker => marker.TargetId, StringComparer.Ordinal);
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamCorridor.tscn")
            ?? throw new InvalidOperationException("Could not load mansion for doorway spawn probes.");
        foreach (var (source, expectedWorld, expectedFacing, markerId) in routes)
        {
            var route = DreamAreaSpawnCatalog.Resolve(source, DreamEscapeIds.AreaCorridor);
            var actualWorld = mapper.ToWorld(route.StoredPosition);
            var markerPosition = markerById[markerId].GlobalPosition;
            if (actualWorld.DistanceTo(expectedWorld) > 0.001f ||
                markerPosition.DistanceTo(expectedWorld) > 0.001f ||
                route.EntryFacing.DistanceTo(expectedFacing) > 0.001f ||
                mansion.MansionVisual.IsBlocked(actualWorld))
            {
                throw new InvalidOperationException(
                    $"Mansion route '{source}' is invalid: position={actualWorld} facing={route.EntryFacing}.");
            }

            session.MoveDreamToArea(
                DreamEscapeIds.AreaCorridor,
                route.StoredPosition.X,
                route.StoredPosition.Y);
            var probe = packed.Instantiate<DreamMansionController3D>();
            AddChild(probe);
            try
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (probe.PlayerActor.GlobalPosition.DistanceTo(expectedWorld) > 0.001f ||
                    probe.MansionVisual.IsBlocked(probe.PlayerActor.GlobalPosition))
                {
                    throw new InvalidOperationException(
                        $"Mansion scene did not restore '{source}' at its doorway: " +
                        $"{probe.PlayerActor.GlobalPosition}.");
                }
            }
            finally
            {
                probe.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        var classroomRoute = DreamAreaSpawnCatalog.Resolve(
            DreamEscapeIds.AreaCorridor,
            DreamEscapeIds.AreaClassroom);
        if (classroomRoute.StoredPosition.DistanceTo(new Vector2(592.4f, 305.8f)) > 0.001f ||
            classroomRoute.EntryFacing.DistanceTo(Vector3.Left) > 0.001f)
        {
            throw new InvalidOperationException("Mansion-to-classroom doorway route is invalid.");
        }

        var dream = session.State.DreamEscape!;
        var roomOneRoute = DreamAreaSpawnCatalog.Resolve(
            DreamEscapeIds.AreaClassroom,
            DreamEscapeIds.AreaCorridor);
        session.MoveDreamToArea(
            DreamEscapeIds.AreaCorridor,
            roomOneRoute.StoredPosition.X,
            roomOneRoute.StoredPosition.Y);
        if (dream.CurrentAreaId != DreamEscapeIds.AreaCorridor ||
            mansion.PlayerActor.GlobalPosition.DistanceTo(new Vector3(-2.2f, 0f, 3.2f)) > 0.001f)
        {
            throw new InvalidOperationException(
                $"Classroom-to-mansion transition did not spawn outside room one: " +
                $"area={dream.CurrentAreaId} player={mansion.PlayerActor.GlobalPosition}.");
        }
    }

    private static void ValidateMansionMarkerReachability(
        DreamMansionVisual3D visual,
        IReadOnlyList<InteractionMarker3D> markers)
    {
        const float step = 0.20f;
        var bounds = visual.WalkableBounds;
        var width = Mathf.FloorToInt(bounds.Size.X / step) + 1;
        var height = Mathf.FloorToInt(bounds.Size.Y / step) + 1;
        var visited = new bool[width, height];
        var queue = new Queue<Vector2I>();
        var startWorld = visual.ResolveSpawn(DreamAreaSpawnCatalog.MansionRoomOneEntryWorld);
        var start = new Vector2I(
            Mathf.RoundToInt((startWorld.X - bounds.Position.X) / step),
            Mathf.RoundToInt((startWorld.Z - bounds.Position.Y) / step));
        visited[start.X, start.Y] = true;
        queue.Enqueue(start);
        var directions = new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in directions)
            {
                var next = current + direction;
                if (next.X < 0 || next.X >= width || next.Y < 0 || next.Y >= height ||
                    visited[next.X, next.Y]) continue;
                var position = new Vector3(
                    bounds.Position.X + next.X * step,
                    0f,
                    bounds.Position.Y + next.Y * step);
                if (visual.IsBlocked(position)) continue;
                visited[next.X, next.Y] = true;
                queue.Enqueue(next);
            }
        }

        var unreachable = markers.Where(marker =>
        {
            for (var x = 0; x < width; x++)
            for (var z = 0; z < height; z++)
            {
                if (!visited[x, z]) continue;
                var position = new Vector2(
                    bounds.Position.X + x * step,
                    bounds.Position.Y + z * step);
                var markerPosition = new Vector2(marker.GlobalPosition.X, marker.GlobalPosition.Z);
                if (position.DistanceTo(markerPosition) <= marker.InteractionRange) return false;
            }
            return true;
        }).Select(marker => marker.TargetId).ToArray();
        if (unreachable.Length > 0)
        {
            throw new InvalidOperationException(
                $"Dream mansion interaction markers are unreachable: {string.Join(',', unreachable)}.");
        }
    }

    private async Task ValidateMansionCameraAndDoors(
        GameSession session,
        DreamMansionController3D mansion)
    {
        var visual = mansion.MansionVisual;
        var rig = mansion.CameraController;
        if (Math.Abs(rig.Camera.Fov - AdoptedCameraFov) > 0.001f ||
            Math.Abs(rig.SpringArm.SpringLength - AdoptedCameraSpringLength) > 0.001f ||
            Math.Abs(rig.FixedPitchDegrees - AdoptedCameraPitch) > 0.001f ||
            Math.Abs(rig.FollowTargetHeight - AdoptedCameraFollowHeight) > 0.001f ||
            Math.Abs(rig.Camera.Position.X - AdoptedCameraShoulderOffset) > 0.001f)
        {
            throw new InvalidOperationException("Dream mansion did not use the adopted Orbit camera constants.");
        }
        mansion.ApplyEntryPose(Vector3.Left, cameraDoorOffsetDegrees: null);
        if (mansion.PlayerActor.FacingWorld.DistanceTo(Vector3.Left) > 0.001f ||
            rig.HorizontalForward.DistanceTo(Vector3.Left) > 0.001f)
        {
            throw new InvalidOperationException("2D-to-3D mansion entry did not apply its default door-facing pose.");
        }

        var dream = session.State.DreamEscape!;
        var stateBeforeLockedDoor = DreamStateFingerprint(dream);
        var frontLeft = visual.ModelRoot.FindChild("Door_Front_Hinge_L", true, false) as Node3D
            ?? throw new InvalidOperationException("Dream mansion front-left hinge is missing.");
        var frontRight = visual.ModelRoot.FindChild("Door_Front_Hinge_R", true, false) as Node3D
            ?? throw new InvalidOperationException("Dream mansion front-right hinge is missing.");
        var frontClosed = new Vector2(frontLeft.Rotation.Y, frontRight.Rotation.Y);
        await mansion.PlayExitTransitionAsync("locked:mansion_front_door", CancellationToken.None);
        if (DreamStateFingerprint(dream) != stateBeforeLockedDoor ||
            new Vector2(frontLeft.Rotation.Y, frontRight.Rotation.Y).DistanceTo(frontClosed) > 0.001f)
        {
            throw new InvalidOperationException("Locked mansion front door changed business or visual state.");
        }
        await ValidateLockedFrontDoorThroughDreamRoot(session);

        var roomOneHinge = visual.ModelRoot.FindChild("Door_Room1_Hinge", true, false) as Node3D
            ?? throw new InvalidOperationException("Dream mansion room-one hinge is missing.");
        var roomOneClosedYaw = roomOneHinge.Rotation.Y;
        using (var cancellation = new CancellationTokenSource())
        {
            var cancelledTransition = mansion.PlayExitTransitionAsync(
                $"area:{DreamEscapeIds.AreaClassroom}",
                cancellation.Token);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var reentrantTransition = mansion.PlayExitTransitionAsync(
                $"area:{DreamEscapeIds.AreaMusic}",
                CancellationToken.None);
            if (!reentrantTransition.IsCompletedSuccessfully || !mansion.DoorTransitionPlaying ||
                mansion.PlayerActor.InputEnabled || rig.InputActive)
            {
                throw new InvalidOperationException(
                    "Mansion door transition did not reject reentry while keeping runtime input locked.");
            }
            cancellation.Cancel();
            try
            {
                await cancelledTransition;
            }
            catch (OperationCanceledException)
            {
                // Expected: the presentation transition must restore its door and input before propagating.
            }
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (mansion.DoorTransitionPlaying || !mansion.PlayerActor.InputEnabled || !rig.InputActive ||
            Math.Abs(Mathf.AngleDifference(roomOneHinge.Rotation.Y, roomOneClosedYaw)) > 0.001f)
        {
            throw new InvalidOperationException("Cancelled mansion transition did not restore door and input state.");
        }
        await mansion.PlayExitTransitionAsync(
            $"area:{DreamEscapeIds.AreaClassroom}",
            CancellationToken.None);
        if (mansion.PlayerActor.InputEnabled ||
            Math.Abs(Mathf.AngleDifference(roomOneHinge.Rotation.Y, roomOneClosedYaw)) <
            Mathf.DegToRad(60f))
        {
            throw new InvalidOperationException("Dream mansion room transition did not lock input or open its door.");
        }
        mansion.ResetExitTransition();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (Math.Abs(Mathf.AngleDifference(roomOneHinge.Rotation.Y, roomOneClosedYaw)) > 0.001f ||
            !mansion.PlayerActor.InputEnabled)
        {
            throw new InvalidOperationException("Dream mansion transition reset did not close its door and unlock input.");
        }

        if (!visual.IsBlocked(new Vector3(0f, 0f, -6.54f)) ||
            !visual.IsBlocked(new Vector3(-3.34f, 0f, 3.2f)) ||
            visual.IsBlocked(new Vector3(0f, 0f, 8.8f)))
        {
            throw new InvalidOperationException("Dream mansion deterministic collision probes failed.");
        }
        mansion.PlayerActor.GlobalPosition = new Vector3(0f, 0f, -5.4f);
        if (!mansion.PlayerActor.TestMove(
                mansion.PlayerActor.GlobalTransform,
                new Vector3(0f, 0f, -2f)))
        {
            throw new InvalidOperationException("Dream mansion physics probe crossed the locked front door.");
        }

        rig.SetYawImmediate(47f);
        var cameraOffset = mansion.CaptureCameraDoorOffsetDegrees(Vector3.Left);
        if (Math.Abs(cameraOffset + 43f) > 0.01f)
        {
            throw new InvalidOperationException(
                $"Mansion camera-to-door offset used an unexpected sign or basis: {cameraOffset:F3}°.");
        }
        var classroomRoute = DreamAreaSpawnCatalog.Resolve(
            DreamEscapeIds.AreaCorridor,
            DreamEscapeIds.AreaClassroom);
        var move = session.MoveDreamToArea(
            DreamEscapeIds.AreaClassroom,
            classroomRoute.StoredPosition.X,
            classroomRoute.StoredPosition.Y);
        if (!move.Changed)
            throw new InvalidOperationException("Could not stage the mansion-to-classroom doorway smoke route.");

        var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamClassroom.tscn")
            ?? throw new InvalidOperationException("Could not load classroom for doorway continuity smoke.");
        var classroom = packed.Instantiate<DreamAreaController3D>();
        AddChild(classroom);
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            classroom.ApplyEntryPose(classroomRoute.EntryFacing, cameraOffset);
            var mapper = new AreaCoordinateMapper3D(
                new Rect2(16f, 178f, 608f, 152f),
                new Rect2(-5.7f, -4.2f, 11.4f, 8.4f));
            var requestedWorld = mapper.ToWorld(classroomRoute.StoredPosition);
            var expectedWorld = classroom.ClassroomVisual.ResolveSpawn(requestedWorld);
            var restoredOffset = classroom.CaptureCameraDoorOffsetDegrees(
                classroomRoute.EntryFacing);
            var expectedCameraForward = classroomRoute.EntryFacing.Rotated(
                Vector3.Up,
                Mathf.DegToRad(cameraOffset)).Normalized();
            if (classroom.PlayerActor.GlobalPosition.DistanceTo(expectedWorld) > 0.001f ||
                expectedWorld.DistanceTo(requestedWorld) > 0.35f ||
                classroom.ClassroomVisual.IsBlocked(expectedWorld) ||
                classroom.PlayerActor.FacingWorld.DistanceTo(classroomRoute.EntryFacing) > 0.001f ||
                classroom.CameraController.HorizontalForward.DistanceTo(expectedCameraForward) > 0.001f ||
                Math.Abs(Mathf.AngleDifference(
                    Mathf.DegToRad(restoredOffset),
                    Mathf.DegToRad(cameraOffset))) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"Mansion-to-classroom doorway pose failed: position={classroom.PlayerActor.GlobalPosition} " +
                    $"facing={classroom.PlayerActor.FacingWorld} offset={restoredOffset}/{cameraOffset}.");
            }
        }
        finally
        {
            classroom.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var roomOneRoute = DreamAreaSpawnCatalog.Resolve(
                DreamEscapeIds.AreaClassroom,
                DreamEscapeIds.AreaCorridor);
            session.MoveDreamToArea(
                DreamEscapeIds.AreaCorridor,
                roomOneRoute.StoredPosition.X,
                roomOneRoute.StoredPosition.Y);
            mansion.PlayerActor.GlobalPosition = new Vector3(-2.2f, 0f, 3.2f);
            mansion.PlayerActor.SetFacingImmediate(Vector3.Right);
            rig.SetYawImmediate(0f);
            mansion.ResetExitTransition();
        }
    }

    private async Task ValidateLockedFrontDoorThroughDreamRoot(GameSession session)
    {
        var mapper = new DreamMansionCoordinateMapper3D();
        var frontDoorStored = mapper.ToStored(new Vector3(0f, 0f, -5.65f));
        session.MoveDreamToArea(
            DreamEscapeIds.AreaCorridor,
            frontDoorStored.X,
            frontDoorStored.Y);
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamEscape.tscn")
            ?? throw new InvalidOperationException("Could not load dream root for front-door interaction smoke.");
        var dreamRoot = packed.Instantiate<DreamEscapeController>();
        AddChild(dreamRoot);
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var runtime = dreamRoot.GetNode<DreamMansionController3D>("AreaHost/DreamMansion3D");
            runtime.PlayerActor.GlobalPosition = new Vector3(0f, 0f, -5.65f);
            runtime.PlayerActor.SetFacingImmediate(Vector3.Forward);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var before = DreamStateFingerprint(session.State.DreamEscape!);
            Input.ParseInputEvent(new InputEventAction { Action = "interact", Pressed = true });
            Input.ParseInputEvent(new InputEventAction { Action = "interact", Pressed = false });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var lockedFeedbackVisible = Descendants<Label>(dreamRoot).Any(label =>
                label.Visible && label.Text.Contains("双叶大门纹丝不动", StringComparison.Ordinal));
            if (!lockedFeedbackVisible || DreamStateFingerprint(session.State.DreamEscape!) != before)
            {
                throw new InvalidOperationException(
                    "Locked mansion front-door interaction did not show feedback or changed Core dream state.");
            }
        }
        finally
        {
            dreamRoot.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var roomOneRoute = DreamAreaSpawnCatalog.Resolve(
                DreamEscapeIds.AreaClassroom,
                DreamEscapeIds.AreaCorridor);
            session.MoveDreamToArea(
                DreamEscapeIds.AreaCorridor,
                roomOneRoute.StoredPosition.X,
                roomOneRoute.StoredPosition.Y);
        }
    }

    private static string DreamStateFingerprint(DreamEscapeState state) => string.Join('|',
        state.Status,
        state.CurrentAreaId,
        state.PositionX.ToString("R", CultureInfo.InvariantCulture),
        state.PositionY.ToString("R", CultureInfo.InvariantCulture),
        state.XiaoyuJoined,
        string.Join(',', state.Items.Order(StringComparer.Ordinal)),
        string.Join(',', state.SolvedPuzzles.Order(StringComparer.Ordinal)),
        string.Join(',', state.UnlockedAreas.Order(StringComparer.Ordinal)),
        string.Join(',', state.DiscoveredClues.Order(StringComparer.Ordinal)),
        string.Join(',', state.HintLevels.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{item.Key}:{item.Value}")));

    private static void ValidateMarkerReachability(
        DreamClassroomVisual3D visual,
        IReadOnlyList<InteractionMarker3D> markers)
    {
        const float step = 0.20f;
        var bounds = visual.WalkableBounds;
        var width = Mathf.FloorToInt(bounds.Size.X / step) + 1;
        var height = Mathf.FloorToInt(bounds.Size.Y / step) + 1;
        var visited = new bool[width, height];
        var queue = new Queue<Vector2I>();
        var startWorld = visual.ResolveSpawn(new Vector3(0f, 0f, -2.55f));
        var start = new Vector2I(
            Mathf.RoundToInt((startWorld.X - bounds.Position.X) / step),
            Mathf.RoundToInt((startWorld.Z - bounds.Position.Y) / step));
        visited[start.X, start.Y] = true;
        queue.Enqueue(start);
        var directions = new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in directions)
            {
                var next = current + direction;
                if (next.X < 0 || next.X >= width || next.Y < 0 || next.Y >= height ||
                    visited[next.X, next.Y]) continue;
                var position = new Vector3(
                    bounds.Position.X + next.X * step,
                    0f,
                    bounds.Position.Y + next.Y * step);
                if (visual.IsBlocked(position)) continue;
                visited[next.X, next.Y] = true;
                queue.Enqueue(next);
            }
        }

        var unreachable = markers.Where(marker =>
        {
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                if (!visited[x, y]) continue;
                var position = new Vector2(
                    bounds.Position.X + x * step,
                    bounds.Position.Y + y * step);
                var markerPosition = new Vector2(marker.GlobalPosition.X, marker.GlobalPosition.Z);
                if (position.DistanceTo(markerPosition) <= marker.InteractionRange) return false;
            }
            return true;
        }).Select(marker => marker.TargetId).ToArray();
        if (unreachable.Length > 0)
            throw new InvalidOperationException($"3D interaction markers are unreachable: {string.Join(',', unreachable)}.");
    }

    private static void ValidateClassroomVisualStates(DreamClassroomVisual3D visual)
    {
        var state = new DreamEscapeState();
        visual.ApplyState(state);
        if (visual.WindowCacheOpen || !visual.ClockHandPickupVisible ||
            visual.BlackboardVariant != "blank" || visual.ClockVariant != "missing" ||
            visual.RosterVariant != "missing" || visual.TeacherDeskVariant != "closed" ||
            visual.MapCabinetVariant != "closed" || visual.TransomCacheVariant != "closed")
        {
            throw new InvalidOperationException("Initial 3D classroom visual state is invalid.");
        }

        state.SolvedPuzzles.UnionWith([
            DreamEscapeIds.PuzzleWindowLatches,
            DreamEscapeIds.PickupClockHand,
            DreamEscapeIds.PuzzleBlackboard,
            DreamEscapeIds.PuzzleClockRestore,
            DreamEscapeIds.PuzzleRosterSlider,
            DreamEscapeIds.PuzzleTeacherDesk,
            DreamEscapeIds.PuzzleMapCabinet,
            DreamEscapeIds.PuzzleTransomCache
        ]);
        visual.ApplyState(state);
        if (!visual.WindowCacheOpen || visual.ClockHandPickupVisible ||
            visual.BlackboardVariant != "revealed" || visual.ClockVariant != "restored" ||
            visual.RosterVariant != "aligned" || visual.TeacherDeskVariant != "open" ||
            visual.MapCabinetVariant != "open" || visual.TransomCacheVariant != "open")
        {
            throw new InvalidOperationException("Solved 3D classroom visual state is invalid.");
        }
    }

    private static void ValidateClassroomArtPass(DreamAreaController3D classroom)
    {
        var model = classroom.GetNode<Node3D>("ClassroomModel");
        var desks = Descendants<Node3D>(model)
            .Count(node => node.Name.ToString().StartsWith("StudentDeskGroup_", StringComparison.Ordinal));
        var requiredDetails = new[]
        {
            "Room_Ceiling",
            "Window_West",
            "WindowCache_Open",
            "Clock_Restored",
            "Roster_Aligned",
            "TransomCache_Open",
            "Door_East_Hinge",
            "DoorV4_Leaf",
            "FULL_Classroom_DeskHardware",
            "FULL_Classroom_WindowHardware",
            "FULL_Classroom_DoorMoulding",
            "FULL_MapCabinet_Closed_Hardware",
            "FULL_TeacherDesk_Closed_Lockset",
            "FULL_Classroom_CeilingHardware"
        };
        var missingDetails = requiredDetails
            .Where(name => model.FindChild(name, true, false) is null)
            .ToArray();
        var materials = Descendants<MeshInstance3D>(model)
            .SelectMany(mesh => Enumerable.Range(0, mesh.Mesh?.GetSurfaceCount() ?? 0)
                .Select(surface => mesh.GetActiveMaterial(surface)))
            .OfType<BaseMaterial3D>()
            .ToArray();
        var textured = materials.Count(material => material.AlbedoTexture is not null);
        var normalMapped = materials.Count(material => material.NormalTexture is not null);
        var ormMapped = materials.Count(material => material.OrmTexture is not null ||
                                                     material.RoughnessTexture is not null);
        if (desks != 18 || missingDetails.Length > 0 ||
            textured == 0 || normalMapped == 0 || ormMapped == 0 ||
            materials.Any(material => material.TextureFilter !=
                BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic))
        {
            throw new InvalidOperationException(
                $"Classroom art pass invalid: desks={desks} " +
                $"missing={string.Join(',', missingDetails)} textured={textured} " +
                $"normal={normalMapped} orm={ormMapped}.");
        }
    }

    private async Task ValidateThirdPersonCamera(
        DreamAreaController3D classroom,
        IReadOnlyCollection<InteractionMarker3D> markers)
    {
        var rig = classroom.CameraController;
        if (rig.Camera.Projection != Camera3D.ProjectionType.Perspective ||
            Math.Abs(rig.Camera.Fov - AdoptedCameraFov) > 0.01f ||
            Math.Abs(rig.FixedPitchDegrees - AdoptedCameraPitch) > 0.01f ||
            Math.Abs(rig.FollowTargetHeight - AdoptedCameraFollowHeight) > 0.01f ||
            Math.Abs(rig.SpringArm.SpringLength - AdoptedCameraSpringLength) > 0.01f ||
            Math.Abs(rig.Camera.Position.X - AdoptedCameraShoulderOffset) > 0.01f)
        {
            throw new InvalidOperationException(
                $"Third-person camera projection or framing constants are invalid: " +
                $"projection={rig.Camera.Projection} fov={rig.Camera.Fov} " +
                $"pitch={rig.FixedPitchDegrees} height={rig.FollowTargetHeight} " +
                $"spring={rig.SpringArm.SpringLength} shoulder={rig.Camera.Position.X}.");
        }

        var mapper = new AreaCoordinateMapper3D(
            new Rect2(16f, 178f, 608f, 152f),
            new Rect2(-5.7f, -4.2f, 11.4f, 8.4f));
        var expectedStart = mapper.ToWorld(new Vector2(
            DreamEscapeState.NewGameClassroomPositionX,
            DreamEscapeState.NewGameClassroomPositionY));
        var player = classroom.PlayerActor;
        var activeAtStart = new HashSet<string>(StringComparer.Ordinal)
        {
            $"area:{DreamEscapeIds.AreaCorridor}",
            "puzzle:window_latches",
            "observe:target.classroom.maintenance_card",
            "observe:target.classroom.morning_schedule",
            "observe:target.classroom.duty_note",
            "pickup:dream.clock_hand",
            "target.classroom.blackboard",
            "target.classroom.clock",
            "target.classroom.duty_roster",
            "puzzle:teacher_desk",
            "target.classroom.map_cabinet",
            "target.classroom.transom_cache"
        };
        var tooClose = markers
            .Where(marker => activeAtStart.Contains(marker.TargetId))
            .Where(marker => new Vector2(marker.GlobalPosition.X, marker.GlobalPosition.Z)
                .DistanceTo(new Vector2(expectedStart.X, expectedStart.Z)) <= marker.InteractionRange)
            .Select(marker => marker.TargetId)
            .ToArray();
        if (player.GlobalPosition.DistanceTo(expectedStart) > 0.001f ||
            classroom.ClassroomVisual.IsBlocked(expectedStart) ||
            tooClose.Length > 0 ||
            Math.Abs(rig.YawDegrees) > 0.001f)
        {
            throw new InvalidOperationException(
                $"Adopted classroom start is invalid: actual={player.GlobalPosition} " +
                $"expected={expectedStart} blocked={classroom.ClassroomVisual.IsBlocked(expectedStart)} " +
                $"near={string.Join(',', tooClose)} yaw={rig.YawDegrees}.");
        }
        rig.SnapToTarget();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (rig.SpringArm.GetHitLength() < AdoptedCameraSpringLength - 0.05f)
        {
            throw new InvalidOperationException(
                $"Adopted classroom start compressed the camera arm to {rig.SpringArm.GetHitLength():F3}m.");
        }

        var model = classroom.GetNode<Node3D>("ClassroomModel");
        var wallNames = new[] { "Wall_North", "Wall_South", "Wall_East", "Wall_West" };
        if (wallNames.Any(name => (model.FindChild(name, true, false) as Node3D)?.Visible != true))
            throw new InvalidOperationException("Third-person classroom unexpectedly hid a wall.");

        foreach (var yaw in new[] { -179f, -73f, 0f, 46f, 179f })
        {
            rig.SetYawImmediate(yaw);
            foreach (var input in new[] { Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down })
            {
                var worldMotion = CameraRelativeMovement.ToWorld(input, rig.Camera);
                var projected = CameraRelativeMovement.ToScreen(worldMotion, rig.Camera);
                if (projected.DistanceTo(input) > 0.001f || Math.Abs(worldMotion.Y) > 0.001f)
                    throw new InvalidOperationException($"Yaw {yaw} did not preserve screen-relative movement {input}.");
            }
        }

        classroom.ApplySettings(new PlayerSettings { CameraLookSensitivity = 1.35f });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (Math.Abs(rig.LookSensitivity - 1.35f) > 0.001f || !rig.InputActive ||
            !classroom.PlayerActor.InputEnabled)
            throw new InvalidOperationException("Orbit sensitivity or simultaneous player input was not applied.");

        rig.SetYawImmediate(0f);
        if (!rig.TryApplyMouseMotion(new Vector2(40f, 0f), pointerCaptured: true) ||
            Math.Abs(rig.YawDegrees) < 1f)
            throw new InvalidOperationException("Captured mouse motion did not change orbit yaw.");
        var yawAfterCapturedMotion = rig.YawDegrees;
        if (rig.TryApplyMouseMotion(new Vector2(40f, 0f), pointerCaptured: false) ||
            Math.Abs(rig.YawDegrees - yawAfterCapturedMotion) > 0.001f)
        {
            throw new InvalidOperationException("Visible-pointer mouse motion changed orbit yaw.");
        }

        var session = GetNode<GameSession>("/root/GameSession");
        using (session.Modal.Acquire("smoke.third_person_pointer"))
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (rig.InputActive || classroom.PlayerActor.InputEnabled ||
                Input.MouseMode != Input.MouseModeEnum.Visible)
                throw new InvalidOperationException("Third-person input remained active while a modal was open.");
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!rig.InputActive || !classroom.PlayerActor.InputEnabled ||
            (DisplayServer.GetName() != "headless" &&
             Input.MouseMode != Input.MouseModeEnum.Captured))
            throw new InvalidOperationException("Third-person input did not resume after closing a modal.");

        rig.SetYawImmediate(63f);
        classroom.RefreshState();
        if (Math.Abs(Mathf.AngleDifference(rig.Rotation.Y, Mathf.DegToRad(63f))) > 0.001f)
            throw new InvalidOperationException("In-place classroom state refresh reset orbit yaw.");
        rig.SetYawImmediate(0f);
    }

    private async Task ProbeClassroomPhysics(DreamAreaController3D classroom)
    {
        var player = classroom.PlayerActor;
        var visual = classroom.ClassroomVisual;
        if (!visual.IsBlocked(new Vector3(0.85f, 0, 1.45f)) ||
            visual.IsBlocked(new Vector3(-1.70f, 0, 2.20f)) ||
            visual.IsBlocked(visual.ResolveSpawn(new Vector3(0.85f, 0, 1.45f))))
        {
            throw new InvalidOperationException("3D classroom collision layout rejected deterministic probes.");
        }

        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        player.GlobalPosition = new Vector3(0.85f, 0, 4.05f);
        if (!player.TestMove(player.GlobalTransform, new Vector3(0, 0, -1.7f)))
            throw new InvalidOperationException("3D physics probe crossed a student desk.");
        player.GlobalPosition = new Vector3(-1.70f, 0, 4.05f);
        if (player.TestMove(player.GlobalTransform, new Vector3(0, 0, -6.0f)))
            throw new InvalidOperationException("3D physics probe blocked a main aisle.");
        player.GlobalPosition = new Vector3(0.55f, 0, -2.15f);
        if (!player.TestMove(player.GlobalTransform, new Vector3(0, 0, -1.0f)))
            throw new InvalidOperationException("3D physics probe crossed the teacher desk.");
        player.GlobalPosition = new Vector3(-3.90f, 0, -3.78f);
        if (!player.TestMove(player.GlobalTransform, new Vector3(-1.2f, 0, 0)))
            throw new InvalidOperationException("3D physics probe crossed the map cabinet.");
        player.GlobalPosition = new Vector3(0f, 0, 4.05f);
        if (!player.TestMove(player.GlobalTransform, new Vector3(0, 0, 1.0f)))
            throw new InvalidOperationException("3D physics probe crossed a classroom wall.");
        player.GlobalPosition = new Vector3(5.10f, 0, 3.35f);
        if (!player.TestMove(player.GlobalTransform, new Vector3(1.0f, 0, 0)))
            throw new InvalidOperationException("3D physics probe crossed the closed east door.");

        player.GlobalPosition = new Vector3(0f, 0, 4.05f);
        classroom.CameraController.SetYawImmediate(0f);
        classroom.CameraController.SnapToTarget();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (classroom.CameraController.SpringArm.GetHitLength() >=
            classroom.CameraController.SpringArm.SpringLength - 0.05f)
            throw new InvalidOperationException("Third-person spring arm did not retract at the south wall.");

        var closedYaw = visual.DoorHinge.Rotation.Y;
        await classroom.PlayExitTransitionAsync(
            $"area:{DreamEscapeIds.AreaCorridor}",
            CancellationToken.None);
        if (player.InputEnabled ||
            Math.Abs(Mathf.AngleDifference(visual.DoorHinge.Rotation.Y, closedYaw)) < Mathf.DegToRad(60f))
            throw new InvalidOperationException("Door transition did not lock movement or open the door.");
        classroom.ResetDoor();
        if (Math.Abs(Mathf.AngleDifference(visual.DoorHinge.Rotation.Y, closedYaw)) > 0.001f)
            throw new InvalidOperationException("Door transition did not restore the closed state.");
    }

    private static IEnumerable<T> Descendants<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void ValidateDefaultCharacterVisual(PlayerController3D player)
    {
        var modelAvailable = ResourceLoader.Exists(PlayerController3D.DefaultCharacterModelPath);
        if (player.CharacterSprite is null || player.UsesDefault3DModel != modelAvailable ||
            player.CharacterSprite.Visible == modelAvailable)
        {
            throw new InvalidOperationException(
                $"Default character selection is invalid: available={modelAvailable} " +
                $"model={player.UsesDefault3DModel} spriteVisible={player.CharacterSprite?.Visible}.");
        }
        if (!modelAvailable) return;

        var model = player.CharacterModel!;
        var animationPlayer = Descendants<AnimationPlayer>(model).SingleOrDefault();
        var animations = animationPlayer?.GetAnimationList()
            .Select(name => name.ToString())
            .ToHashSet(StringComparer.Ordinal) ?? [];
        if (model.Name != "DefaultCharacterModel" ||
            !animations.IsSupersetOf(["idle", "walk", "jog"]) ||
            animationPlayer?.CurrentAnimation != "idle")
        {
            throw new InvalidOperationException(
                $"Default 3D character model is incomplete: node={model.Name} " +
                $"animations={string.Join(',', animations)} current={animationPlayer?.CurrentAnimation}.");
        }

        player.SetFacingImmediate(Vector3.Left);
        if (model.GlobalBasis.Z.Normalized().Dot(Vector3.Left) < 0.999f)
            throw new InvalidOperationException("Default 3D character did not face the requested world direction.");
        player.SetFacingImmediate(Vector3.Forward);
    }

    private async Task SaveCapture(
        string outputDirectory,
        string fileName,
        Vector2I? expectedSize = null)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        if (expectedSize is { } requiredSize && image.GetSize() != requiredSize)
        {
            throw new InvalidOperationException(
                $"Capture '{fileName}' must be {requiredSize}, actual image is {image.GetSize()}.");
        }
        var error = image.SavePng(Path.Combine(outputDirectory, fileName));
        if (error != Error.Ok)
        {
            throw new IOException($"Could not save dream art capture '{fileName}': {error}");
        }
    }
}
