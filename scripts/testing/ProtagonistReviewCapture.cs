using Godot;
using System.Globalization;
using System.Text;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Presentation;

namespace TownRpg.Testing;

/// <summary>
/// Review-only 1080p capture harness for a static protagonist GLB. This scene
/// deliberately replaces neither DreamClassroom.tscn nor the runtime billboard.
/// </summary>
public partial class ProtagonistReviewCapture : Node
{
    private const string DefaultReviewGlb =
        "res://assets/generated/characters/3d/protagonist-art-pass-v2.glb";
    private static readonly Vector2I CaptureSize = new(1920, 1080);

    private sealed record CaptureAngle(string Id, string Label, float YawDegrees);

    private sealed record ProjectionSample(
        string File,
        string Label,
        float YawDegrees,
        float HeightPixels,
        float HeightRatio,
        float HeadRatio,
        float FeetRatio,
        float RequestedArmLength,
        float ActualArmLength);

    private static readonly CaptureAngle[] CaptureAngles =
    [
        new("default", "Scheme A default", 0f),
        new("front_left", "Front-left orbit", -45f),
        new("rear_left", "Rear-left orbit", -135f),
        new("rear_right", "Rear-right orbit", 135f),
        new("front_right", "Front-right orbit", 45f)
    ];

    public override void _Ready() => CallDeferred(MethodName.RunCapture);

    private async void RunCapture()
    {
        try
        {
            var arguments = OS.GetCmdlineUserArgs();
            var outputDirectory = ReadRequiredArgument(arguments, "--capture-protagonist-v2=");
            var reviewGlb = ReadOptionalArgument(arguments, "--review-glb=") ?? DefaultReviewGlb;
            var modelYaw = ReadOptionalFloat(arguments, "--review-model-yaw=") ?? 0f;

            await CaptureAsync(
                Path.GetFullPath(outputDirectory),
                reviewGlb,
                modelYaw);
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"PROTAGONIST_V2_CLASSROOM_CAPTURE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task CaptureAsync(string outputDirectory, string reviewGlb, float modelYaw)
    {
        DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
        await ConfigureCaptureWindow();

        var session = GetNode<GameSession>("/root/GameSession");
        using var persistenceSuppression = session.SuppressPersistence();
        session.NewGame("主角静态验收", "同学");
        using var modalLease = session.Modal.Acquire("capture.protagonist_v2");

        var classroomPacked = ResourceLoader.Load<PackedScene>(
                                  "res://scenes/dream/DreamClassroom.tscn")
                              ?? throw new InvalidOperationException(
                                  "Could not load the runtime DreamClassroom scene.");
        var classroom = classroomPacked.Instantiate<DreamAreaController3D>();
        AddChild(classroom);
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            var dream = session.State.DreamEscape
                        ?? throw new InvalidOperationException(
                            "Dream state is missing during protagonist review capture.");
            dream.SolvedPuzzles.Add("dream.pickup.chalk");
            dream.SolvedPuzzles.Add(DreamEscapeIds.PuzzleBlackboard);
            classroom.ClassroomVisual.ApplyState(dream);

            var billboard = classroom.PlayerActor.CharacterSprite
                            ?? throw new InvalidOperationException(
                                "The classroom runtime billboard was not created.");
            billboard.Visible = false;

            var reviewVisual = LoadReviewModel(reviewGlb);
            reviewVisual.Name = "ProtagonistV2ReviewVisual";
            classroom.PlayerActor.AddChild(reviewVisual);
            reviewVisual.Position = Vector3.Zero;
            reviewVisual.RotationDegrees = new Vector3(0f, modelYaw, 0f);

            var headAnchor = FindRequiredAnchor(reviewVisual, "HeadAnchor");
            var feetAnchor = FindRequiredAnchor(reviewVisual, "FeetAnchor");
            ConfigureSchemeA(classroom);

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var samples = new List<ProjectionSample>(CaptureAngles.Length);
            for (var index = 0; index < CaptureAngles.Length; index++)
            {
                var angle = CaptureAngles[index];
                classroom.CameraController.SetYawImmediate(angle.YawDegrees);
                classroom.CameraController.SnapToTarget();
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                var fileName = $"{index + 1:00}-{angle.Id}-1080p.png";
                var sample = MeasureProjection(
                    fileName,
                    angle,
                    classroom.CameraController,
                    headAnchor,
                    feetAnchor);
                await SaveCapture(outputDirectory, fileName);
                samples.Add(sample);
                GD.Print(string.Create(
                    CultureInfo.InvariantCulture,
                    $"PROTAGONIST_V2_CLASSROOM_FRAME file={fileName} yaw={angle.YawDegrees:F1} height={sample.HeightRatio:P2} head={sample.HeadRatio:P2} feet={sample.FeetRatio:P2} arm={sample.ActualArmLength:F3}/{sample.RequestedArmLength:F3}"));
            }

            WriteReports(
                outputDirectory,
                reviewGlb,
                modelYaw,
                classroom.PlayerActor.GlobalPosition,
                samples);
            var defaultSample = samples[0];
            var targetMet = defaultSample.HeightRatio is >= 0.28f and <= 0.31f;
            GD.Print(string.Create(
                CultureInfo.InvariantCulture,
                $"PROTAGONIST_V2_CLASSROOM_CAPTURE_PASS images={samples.Count} resolution={CaptureSize.X}x{CaptureSize.Y} defaultHeight={defaultSample.HeightRatio:P2} target28to31={targetMet.ToString().ToLowerInvariant()} runtimeReplaced=false"));
        }
        finally
        {
            classroom.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    private async Task ConfigureCaptureWindow()
    {
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetSize(CaptureSize);
        DisplayServer.WindowSetPosition(Vector2I.Zero);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var actualSize = DisplayServer.WindowGetSize();
        if (actualSize != CaptureSize)
        {
            throw new InvalidOperationException(
                $"Protagonist review capture requires {CaptureSize}, actual window is {actualSize}.");
        }
    }

    private static void ConfigureSchemeA(DreamAreaController3D classroom)
    {
        var rig = classroom.CameraController;
        rig.Camera.Fov = 60f;
        rig.FixedPitchDegrees = -21f;
        rig.FollowTargetHeight = 1.30f;
        rig.SpringArm.RotationDegrees = new Vector3(-21f, 0f, 0f);
        rig.SpringArm.SpringLength = 3.4f;
        rig.Camera.Position = new Vector3(0.22f, 0f, 0f);
        rig.SetYawImmediate(0f);
        rig.SnapToTarget();
    }

    private static Node3D LoadReviewModel(string path)
    {
        if (path.StartsWith("res://", StringComparison.Ordinal))
        {
            var imported = ResourceLoader.Load<PackedScene>(path);
            if (imported is not null)
                return imported.Instantiate<Node3D>();
        }

        var absolutePath = path.StartsWith("res://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(path)
            : Path.GetFullPath(path);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("Review-only protagonist GLB was not found.", absolutePath);

        var document = new GltfDocument();
        var state = new GltfState();
        var basePath = Path.GetDirectoryName(absolutePath)?.Replace('\\', '/') ?? string.Empty;
        var error = document.AppendFromFile(absolutePath.Replace('\\', '/'), state, 0, basePath);
        if (error != Error.Ok)
            throw new InvalidOperationException($"Could not parse review GLB '{absolutePath}': {error}.");
        return document.GenerateScene(state) as Node3D
               ?? throw new InvalidOperationException(
                   $"Godot could not generate a Node3D scene from review GLB '{absolutePath}'.");
    }

    private static Node3D FindRequiredAnchor(Node3D reviewVisual, string name)
    {
        var anchor = reviewVisual.FindChild(name, true, false) as Node3D;
        return anchor ?? throw new InvalidOperationException(
            $"Review GLB is missing required {name}; the capture tool does not infer bounds from mesh pixels.");
    }

    private ProjectionSample MeasureProjection(
        string fileName,
        CaptureAngle angle,
        ThirdPersonCameraRig rig,
        Node3D headAnchor,
        Node3D feetAnchor)
    {
        if (rig.Camera.IsPositionBehind(headAnchor.GlobalPosition) ||
            rig.Camera.IsPositionBehind(feetAnchor.GlobalPosition))
        {
            throw new InvalidOperationException(
                $"Character anchors are behind the camera at yaw {angle.YawDegrees}.");
        }

        var viewportSize = GetViewport().GetVisibleRect().Size;
        var headY = rig.Camera.UnprojectPosition(headAnchor.GlobalPosition).Y;
        var feetY = rig.Camera.UnprojectPosition(feetAnchor.GlobalPosition).Y;
        var top = Math.Min(headY, feetY);
        var bottom = Math.Max(headY, feetY);
        var ratio = (bottom - top) / viewportSize.Y;
        var outputScale = CaptureSize.Y / viewportSize.Y;
        return new ProjectionSample(
            fileName,
            angle.Label,
            angle.YawDegrees,
            (bottom - top) * outputScale,
            ratio,
            top / viewportSize.Y,
            bottom / viewportSize.Y,
            rig.SpringArm.SpringLength,
            rig.SpringArm.GetHitLength());
    }

    private async Task SaveCapture(string outputDirectory, string fileName)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        if (image.GetSize() != CaptureSize)
        {
            throw new InvalidOperationException(
                $"Capture '{fileName}' must be {CaptureSize}, actual image is {image.GetSize()}.");
        }
        var error = image.SavePng(Path.Combine(outputDirectory, fileName));
        if (error != Error.Ok)
            throw new IOException($"Could not save protagonist review capture '{fileName}': {error}.");
    }

    private static void WriteReports(
        string outputDirectory,
        string reviewGlb,
        float modelYaw,
        Vector3 spawn,
        IReadOnlyList<ProjectionSample> samples)
    {
        var culture = CultureInfo.InvariantCulture;
        var csv = new StringBuilder();
        csv.AppendLine(
            "file,label,yaw_degrees,height_pixels,height_ratio,head_ratio,feet_ratio," +
            "requested_arm,actual_arm");
        foreach (var sample in samples)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                sample.File,
                sample.Label,
                sample.YawDegrees.ToString("0.###", culture),
                sample.HeightPixels.ToString("0.###", culture),
                sample.HeightRatio.ToString("0.######", culture),
                sample.HeadRatio.ToString("0.######", culture),
                sample.FeetRatio.ToString("0.######", culture),
                sample.RequestedArmLength.ToString("0.###", culture),
                sample.ActualArmLength.ToString("0.###", culture)
            }));
        }
        File.WriteAllText(
            Path.Combine(outputDirectory, "protagonist-v2-classroom-metrics.csv"),
            csv.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var report = new
        {
            schemaVersion = 1,
            reviewOnly = true,
            runtimeReplaced = false,
            sourceGlb = reviewGlb,
            resolution = new[] { CaptureSize.X, CaptureSize.Y },
            camera = new
            {
                profile = "scheme-a",
                fovDegrees = 60.0,
                pitchDegrees = 21.0,
                springArmMeters = 3.4,
                followHeightMeters = 1.30,
                shoulderOffsetMeters = 0.22
            },
            spawn = new[] { spawn.X, spawn.Y, spawn.Z },
            modelYawDegrees = modelYaw,
            defaultHeightTarget = new[] { 0.28, 0.31 },
            defaultHeightTargetMet = samples[0].HeightRatio is >= 0.28f and <= 0.31f,
            captures = samples
        };
        File.WriteAllText(
            Path.Combine(outputDirectory, "protagonist-v2-classroom-report.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string ReadRequiredArgument(string[] arguments, string prefix) =>
        ReadOptionalArgument(arguments, prefix)
        ?? throw new ArgumentException(
            $"Missing required '{prefix}<value>' argument. " +
            "Run this dedicated scene with Godot user arguments after '--'.");

    private static string? ReadOptionalArgument(string[] arguments, string prefix) =>
        arguments.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.Ordinal))?
            [prefix.Length..];

    private static float? ReadOptionalFloat(string[] arguments, string prefix)
    {
        var value = ReadOptionalArgument(arguments, prefix);
        if (value is null) return null;
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            throw new ArgumentException($"Argument '{prefix}' requires an invariant floating-point value.");
        return parsed;
    }
}
