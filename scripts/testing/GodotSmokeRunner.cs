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
                GD.Print("CLASSROOM_3D_CAPTURE_PASS files=12");
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
                    "res://assets/generated/dream_escape/3d/classroom-art-pass-v2.glb") is null)
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
                    var player = classroom.PlayerActor;
                    var playerShape = player.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
                    if (markerCount != 9 || collisionNodes != 20 ||
                        visual.CollisionBodyCount != 20 || visual.FurnitureCollisionCount != 12 ||
                        playerShape is null || blockedMarkers.Length > 0)
                    {
                        throw new InvalidOperationException(
                            $"3D classroom composition invalid: collisions={collisionNodes} " +
                            $"furniture={visual.FurnitureCollisionCount} markers={markerCount} " +
                            $"playerShape={playerShape is not null} blockedMarkers={string.Join(',', blockedMarkers)}.");
                    }
                    ValidateMarkerReachability(visual, markers);
                    ValidateCoordinateMapping();
                    ValidateClassroomArtPass(classroom);
                    ValidateClassroomVisualStates(visual);
                    await ValidateThirdPersonCamera(classroom, markers);
                    await ProbeClassroomPhysics(classroom);
                }
                else if (instance is IDreamAreaRuntime runtime && runtime.CameraControlMode != CameraControlMode.None)
                {
                    throw new InvalidOperationException($"2D area '{scenePath}' unexpectedly enabled camera control.");
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
        var viewNames = new[] { "blackboard", "east-door", "rear-wall", "west-window" };
        var viewYaw = new[] { 0f, -90f, 180f, 90f };
        for (var index = 0; index < viewNames.Length; index++)
        {
            classroom.CameraController.SetYawImmediate(viewYaw[index]);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await SaveCapture(outputDirectory, $"{index + 1:00}-view-{viewNames[index]}.png");
        }

        classroom.CameraController.SetYawImmediate(0f);
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

        classroom.PlayerActor.GlobalPosition = new Vector3(3.0f, 0f, 2.25f);
        classroom.CameraController.SetYawImmediate(-90f);
        classroom.CameraController.SnapToTarget();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await SaveCapture(outputDirectory, "10-closeup-east-door.png");

        classroom.PlayerActor.GlobalPosition = new Vector3(2.35f, 0f, -1.45f);
        classroom.CameraController.SetYawImmediate(0f);
        classroom.CameraController.SnapToTarget();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await SaveCapture(outputDirectory, "11-closeup-blackboard.png");

        classroom.PlayerActor.GlobalPosition = new Vector3(0.85f, 0f, 0.70f);
        classroom.CameraController.SetYawImmediate(-28f);
        classroom.CameraController.SnapToTarget();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await SaveCapture(outputDirectory, "12-closeup-student-desks.png");
        classroom.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
        captureState.SolvedPuzzles.Add("dream.pickup.chalk");
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
            new Rect2(-4.52f, -3.30f, 9.04f, 6.60f));
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
        await DispatchKey(Key.Tab);
        if (!dream.InventoryVisible || GetViewport().GuiGetFocusOwner() is null ||
            Input.MouseMode != Input.MouseModeEnum.Visible)
        {
            throw new InvalidOperationException(
                "Tab did not open dream inventory with focus and a visible pointer.");
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

    private static void ValidateClassroomArtPass(DreamAreaController3D classroom)
    {
        var model = classroom.GetNode<Node3D>("ClassroomModel");
        var desks = Descendants<Node3D>(model)
            .Count(node => node.Name.ToString().StartsWith("StudentDeskArt_", StringComparison.Ordinal));
        var requiredDetails = new[]
        {
            "Window_Exterior",
            "Wall_FluorescentFixture",
            "Blackboard_Revealed_Writing",
            "TeacherDesk_Closed_LockPlate",
            "Door_East_Hinge",
            "DoorV3_Leaf",
            "ThirdPerson_SouthNoticeBoard",
            "ThirdPerson_WallCap_North"
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
        if (desks != 10 || missingDetails.Length > 0 ||
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
            new Rect2(-4.52f, -3.30f, 9.04f, 6.60f));
        var expectedStart = mapper.ToWorld(new Vector2(
            DreamEscapeState.NewGameClassroomPositionX,
            DreamEscapeState.NewGameClassroomPositionY));
        var player = classroom.PlayerActor;
        var activeAtStart = new HashSet<string>(StringComparer.Ordinal)
        {
            $"area:{DreamEscapeIds.AreaCorridor}",
            "pickup:dream.chalk",
            "target.classroom.blackboard",
            "puzzle:teacher_desk",
            "target.classroom.map_cabinet"
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
        if (!player.TestMove(player.GlobalTransform, new Vector3(0.8f, 0, 0)))
            throw new InvalidOperationException("3D physics probe crossed the closed east door.");

        player.GlobalPosition = new Vector3(0f, 0, 3.0f);
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
