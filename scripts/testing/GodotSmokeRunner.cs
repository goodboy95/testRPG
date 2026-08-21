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
            ValidateInputBindings();

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
                GD.Print("CLASSROOM_3D_CAPTURE_PASS files=9");
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
            if (ResourceLoader.Load<Shader>("res://assets/shaders/wall_cutaway.gdshader") is null ||
                ResourceLoader.Load<PackedScene>(
                    "res://assets/generated/dream_escape/3d/classroom-mvp.glb") is null)
            {
                throw new InvalidOperationException("Could not load 3D classroom GLB or cutaway shader.");
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
                    var player = classroom.PlayerActor;
                    var playerShape = player.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
                    if (markerCount != 9 || collisionNodes != 19 ||
                        visual.CollisionBodyCount != 19 || visual.FurnitureCollisionCount != 12 ||
                        playerShape is null || blockedMarkers.Length > 0)
                    {
                        throw new InvalidOperationException(
                            $"3D classroom composition invalid: collisions={collisionNodes} " +
                            $"furniture={visual.FurnitureCollisionCount} markers={markerCount} " +
                            $"playerShape={playerShape is not null} blockedMarkers={string.Join(',', blockedMarkers)}.");
                    }
                    ValidateMarkerReachability(visual, markers);
                    ValidateCoordinateMapping();
                    ValidateClassroomVisualStates(visual);
                    await ValidateCameraAndWalls(classroom);
                    await ProbeClassroomPhysics(classroom);
                }
                else if (instance is IDreamAreaRuntime runtime && runtime.SupportsQuarterTurnCamera)
                {
                    throw new InvalidOperationException($"2D area '{scenePath}' unexpectedly enabled camera rotation.");
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

    private async Task CaptureDreamClassroom3D(GameSession session, string outputDirectory)
    {
        DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/dream/DreamClassroom.tscn")
            ?? throw new InvalidOperationException("Could not load 3D dream classroom for capture.");
        var classroom = packed.Instantiate<DreamAreaController3D>();
        AddChild(classroom);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var visual = classroom.ClassroomVisual;
        var dream = session.State.DreamEscape
            ?? throw new InvalidOperationException("Dream state missing during 3D capture.");

        dream.SolvedPuzzles.Add("dream.pickup.chalk");
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleBlackboard);
        visual.ApplyState(dream);
        var viewNames = new[] { "southeast", "southwest", "northwest", "northeast" };
        for (var index = 0; index < viewNames.Length; index++)
        {
            classroom.CameraController.SetViewImmediate(index);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await SaveCapture(outputDirectory, $"{index + 1:00}-view-{viewNames[index]}.png");
        }

        classroom.CameraController.SetViewImmediate(0);
        dream.SolvedPuzzles.Clear();
        visual.ApplyState(dream);
        await SaveCapture(outputDirectory, "05-state-initial.png");
        dream.SolvedPuzzles.Add("dream.pickup.chalk");
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleBlackboard);
        visual.ApplyState(dream);
        await SaveCapture(outputDirectory, "06-state-blackboard.png");
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleTeacherDesk);
        visual.ApplyState(dream);
        await SaveCapture(outputDirectory, "07-state-teacher-desk.png");
        dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleMapCabinet);
        visual.ApplyState(dream);
        await SaveCapture(outputDirectory, "08-state-map-cabinet.png");
        visual.SetCollisionDebug(true);
        await SaveCapture(outputDirectory, "09-collision-debug.png");
        visual.SetCollisionDebug(false);
        classroom.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
            var rotatingFrameTimes = new List<double>();
            var previous = Time.GetTicksUsec();
            for (var index = 0; index < 240; index++)
            {
                if (index % 60 == 0) classroom.CameraController.TryRotate(1);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var current = Time.GetTicksUsec();
                var milliseconds = (current - previous) / 1000.0;
                previous = current;
                frameTimes.Add(milliseconds);
                if (classroom.CameraController.IsRotating) rotatingFrameTimes.Add(milliseconds);
            }

            var averageFrame = frameTimes.Average();
            var worstFrame = frameTimes.Max();
            var worstRotation = rotatingFrameTimes.Count == 0 ? 0.0 : rotatingFrameTimes.Max();
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
            new Rect2(-4.52f, -3.30f, 9.04f, 6.60f));
        foreach (var point in new[] { new Vector2(16, 178), new Vector2(320, 280), new Vector2(624, 330) })
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
        static bool HasButton(StringName action, JoyButton button) => InputMap.ActionGetEvents(action)
            .OfType<InputEventJoypadButton>()
            .Any(input => input.ButtonIndex == button);

        if (!HasKey("interact", Key.F) || !HasKey("interact", Key.Space) ||
            !HasButton("interact", JoyButton.A) ||
            !HasKey("camera_rotate_left", Key.Q) ||
            !HasKey("camera_rotate_right", Key.E) ||
            !HasButton("camera_rotate_left", JoyButton.LeftShoulder) ||
            !HasButton("camera_rotate_right", JoyButton.RightShoulder))
        {
            throw new InvalidOperationException("Keyboard or Xbox interaction/camera bindings are incomplete.");
        }
    }

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
        var startWorld = visual.ResolveSpawn(new Vector3(0f, 0f, 2.75f));
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
        if (!visual.ChalkVisible || visual.BlackboardVariant != "blank" ||
            visual.TeacherDeskVariant != "closed" || visual.MapCabinetVariant != "closed")
        {
            throw new InvalidOperationException("Initial 3D classroom visual state is invalid.");
        }

        state.SolvedPuzzles.UnionWith([
            "dream.pickup.chalk",
            DreamEscapeIds.PuzzleBlackboard,
            DreamEscapeIds.PuzzleTeacherDesk,
            DreamEscapeIds.PuzzleMapCabinet
        ]);
        visual.ApplyState(state);
        if (visual.ChalkVisible || visual.BlackboardVariant != "revealed" ||
            visual.TeacherDeskVariant != "open" || visual.MapCabinetVariant != "open")
        {
            throw new InvalidOperationException("Solved 3D classroom visual state is invalid.");
        }
    }

    private async Task ValidateCameraAndWalls(DreamAreaController3D classroom)
    {
        var rig = classroom.CameraController;
        var walls = classroom.ClassroomVisual.WallCutaway;
        var expected = new[]
        {
            new[] { "Wall_North", "Wall_West" },
            new[] { "Wall_North", "Wall_East" },
            new[] { "Wall_South", "Wall_East" },
            new[] { "Wall_South", "Wall_West" }
        };
        for (var view = 0; view < 4; view++)
        {
            rig.SetViewImmediate(view);
            var expectedYaw = Mathf.DegToRad(45f - view * 90f);
            if (Math.Abs(Mathf.AngleDifference(rig.Rotation.Y, expectedYaw)) > 0.001f)
                throw new InvalidOperationException($"View {view} is not separated by an exact quarter turn.");
            foreach (var input in new[] { Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down })
            {
                var worldMotion = CameraRelativeMovement.ToWorld(input, rig.Camera);
                var projected = CameraRelativeMovement.ToScreen(worldMotion, rig.Camera);
                if (projected.DistanceTo(input) > 0.001f || Math.Abs(worldMotion.Y) > 0.001f)
                    throw new InvalidOperationException($"View {view} did not preserve screen-relative movement {input}.");
            }
            var visible = walls.VisibleWalls().Order(StringComparer.Ordinal).ToArray();
            var wanted = expected[view].Order(StringComparer.Ordinal).ToArray();
            if (!visible.SequenceEqual(wanted))
                throw new InvalidOperationException($"View {view} has incorrect far walls: {string.Join(',', visible)}.");
        }

        rig.SetViewImmediate(0);
        classroom.ApplySettings(new PlayerSettings { ReduceFlashing = false });
        if (!rig.TryRotate(1) || rig.TryRotate(1) || !rig.IsRotating)
            throw new InvalidOperationException("Quarter-turn camera did not reject input reentry.");
        if (classroom.PlayerActor.InputEnabled)
            throw new InvalidOperationException("Camera rotation did not lock player movement immediately.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (classroom.PlayerActor.InputEnabled ||
            !new[] { "Wall_North", "Wall_South", "Wall_East", "Wall_West" }
                .Any(name => walls.GetWallAlpha(name) is > 0.001f and < 0.999f))
        {
            throw new InvalidOperationException("Normal camera rotation did not pause movement and fade walls.");
        }
        await ToSignal(GetTree().CreateTimer(0.34), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (rig.IsRotating || rig.CurrentViewIndex != 1)
            throw new InvalidOperationException("Quarter-turn camera did not complete its 0.3 second rotation.");
        if (!classroom.PlayerActor.InputEnabled)
            throw new InvalidOperationException("Player movement did not resume after camera rotation.");

        classroom.ApplySettings(new PlayerSettings { ReduceFlashing = true });
        if (!rig.TryRotate(1))
            throw new InvalidOperationException("Reduced-flashing camera rotation did not start.");
        if (new[] { "Wall_North", "Wall_South", "Wall_East", "Wall_West" }
            .Any(name => walls.GetWallAlpha(name) is > 0.001f and < 0.999f))
        {
            throw new InvalidOperationException("Reduced-flashing wall cutaway used intermediate alpha.");
        }
        await ToSignal(GetTree().CreateTimer(0.34), SceneTreeTimer.SignalName.Timeout);

        rig.SetViewImmediate(2);
        classroom.RefreshState();
        if (rig.CurrentViewIndex != 2)
            throw new InvalidOperationException("In-place classroom state refresh reset the camera view.");
        rig.SetViewImmediate(0);
    }

    private async Task ProbeClassroomPhysics(DreamAreaController3D classroom)
    {
        var player = classroom.PlayerActor;
        var visual = classroom.ClassroomVisual;
        if (!visual.IsBlocked(new Vector3(0, 0, 1.63f)) ||
            visual.IsBlocked(new Vector3(0.84f, 0, 2.75f)) ||
            visual.IsBlocked(visual.ResolveSpawn(new Vector3(0, 0, 1.15f))))
        {
            throw new InvalidOperationException("3D classroom collision layout rejected deterministic probes.");
        }

        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        player.GlobalPosition = new Vector3(0, 0, 2.75f);
        if (!player.TestMove(player.GlobalTransform, new Vector3(0, 0, -1.4f)))
            throw new InvalidOperationException("3D physics probe crossed a student desk.");
        player.GlobalPosition = new Vector3(0.84f, 0, 2.75f);
        if (player.TestMove(player.GlobalTransform, new Vector3(0, 0, -3.0f)))
            throw new InvalidOperationException("3D physics probe blocked a main aisle.");
        player.GlobalPosition = new Vector3(0.4f, 0, -1.25f);
        if (!player.TestMove(player.GlobalTransform, new Vector3(0, 0, -1.2f)))
            throw new InvalidOperationException("3D physics probe crossed the teacher desk.");
        player.GlobalPosition = new Vector3(-2.9f, 0, -2.8f);
        if (!player.TestMove(player.GlobalTransform, new Vector3(-1.2f, 0, 0)))
            throw new InvalidOperationException("3D physics probe crossed the map cabinet.");
        player.GlobalPosition = new Vector3(0f, 0, 3.0f);
        if (!player.TestMove(player.GlobalTransform, new Vector3(0, 0, 1.0f)))
            throw new InvalidOperationException("3D physics probe crossed a classroom wall.");
        player.GlobalPosition = new Vector3(4.25f, 0, 2.2f);
        if (player.TestMove(player.GlobalTransform, new Vector3(0.8f, 0, 0)))
            throw new InvalidOperationException("3D physics probe blocked the east doorway.");
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
