using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TownRpg.Scripts.Testing;

/// <summary>
/// Review-only 1080p capture harness for the cinematic environment samples.
/// The scenes never participate in normal game loading.
/// </summary>
public partial class EnvironmentCinematicSampleCapture : Node3D
{
    private static readonly Vector2I CaptureSize = new(1920, 1080);

    [Export]
    public string SampleKind { get; set; } = string.Empty;

    public override async void _Ready()
    {
        ConfigureReviewState();
        var argument = OS.GetCmdlineUserArgs()
            .FirstOrDefault(value => value.StartsWith("--capture-environment-cinematic=", StringComparison.Ordinal));
        if (argument is null)
        {
            return;
        }

        try
        {
            var outputDirectory = Path.GetFullPath(argument["--capture-environment-cinematic=".Length..]);
            Directory.CreateDirectory(outputDirectory);
            DisplayServer.WindowSetSize(CaptureSize);
            await WaitForRendering();
            await Capture(outputDirectory);
            GD.Print($"ENVIRONMENT_CINEMATIC_GODOT_CAPTURE_PASS kind={SampleKind} captures=2 size=1920x1080");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task Capture(string outputDirectory)
    {
        var camera = GetNode<Camera3D>("Camera3D");
        var (defaultPosition, defaultTarget, closePosition, closeTarget, prefix) = SampleKind switch
        {
            "classroom" => (
                new Vector3(4.45f, 3.10f, 3.75f), new Vector3(0.10f, 1.38f, -2.55f),
                new Vector3(2.35f, 1.95f, -0.15f), new Vector3(0.35f, 0.90f, -3.30f),
                "classroom-v5"),
            "manor" => (
                new Vector3(1.10f, 2.45f, 5.55f), new Vector3(-0.10f, 1.30f, -3.80f),
                new Vector3(0.20f, 1.82f, 3.20f), new Vector3(-3.15f, 1.38f, 3.20f),
                "manor-v2"),
            _ => throw new InvalidOperationException($"Unknown cinematic sample kind '{SampleKind}'."),
        };

        camera.Position = defaultPosition;
        camera.LookAt(defaultTarget, Vector3.Up);
        await WaitForRendering();
        SavePng(outputDirectory, $"{prefix}-godot-default.png");

        camera.Position = closePosition;
        camera.LookAt(closeTarget, Vector3.Up);
        await WaitForRendering();
        SavePng(outputDirectory, $"{prefix}-godot-closeup.png");
    }

    private void ConfigureReviewState()
    {
        if (SampleKind != "classroom")
        {
            return;
        }

        foreach (var name in new[]
                 {
                     "Blackboard_Revealed", "TeacherDesk_Open", "WindowCache_Open", "Clock_HandOnly",
                     "Clock_KeyOnly", "Clock_Ready", "Clock_Restored", "Roster_Inserted", "Roster_Aligned",
                     "MapCabinet_Open", "TransomCache_Open",
                 })
        {
            if (FindChild(name, true, false) is Node3D node)
            {
                node.Visible = false;
            }
        }
    }

    private async Task WaitForRendering()
    {
        for (var index = 0; index < 8; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void SavePng(string outputDirectory, string fileName)
    {
        var image = GetViewport().GetTexture().GetImage();
        if (image.GetWidth() != CaptureSize.X || image.GetHeight() != CaptureSize.Y)
        {
            throw new InvalidOperationException(
                $"Cinematic capture must be {CaptureSize}, actual image is {image.GetWidth()}x{image.GetHeight()}.");
        }

        var error = image.SavePng(Path.Combine(outputDirectory, fileName));
        if (error != Error.Ok)
        {
            throw new IOException($"Could not save cinematic sample '{fileName}': {error}.");
        }
    }
}
