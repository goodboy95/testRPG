using Godot;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Presentation;

namespace TownRpg.Testing;

public static class CharacterMotionCapture
{
    // Called only from the smoke runner, while its persistence suppression lease is held.
    public static async Task Run(Node host, GameSession session, string directory)
    {
        DirAccess.MakeDirRecursiveAbsolute(directory);
        DisplayServer.WindowSetSize(new Vector2I(1280, 720));
        var label = new Label { Position = new Vector2(12, 12) };
        label.AddThemeFontSizeOverride("font_size", 16);
        host.AddChild(label);
        async Task Wait(int count)
        {
            for (var i = 0; i < count; i++) await host.ToSignal(host.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        var areas = new[]
        {
            ("DreamClassroom", DreamEscapeIds.AreaClassroom, new Vector3(-1.7f, 0, 2.2f)),
            ("DreamMusicRoom", DreamEscapeIds.AreaMusic, new Vector3(0, 0, 2.2f)),
            ("DreamArchiveRoom", DreamEscapeIds.AreaArchive, new Vector3(0, 0, 2.1f)),
            ("DreamAuditorium", DreamEscapeIds.AreaAuditorium, new Vector3(0, 0, 3f)),
            ("DreamCorridor", DreamEscapeIds.AreaCorridor, new Vector3(0, 0, 1f))
        };
        try
        {
            foreach (var (scene, id, requested) in areas)
            {
                session.NewGame("动作检查", "同学");
                session.State.DreamEscape!.UnlockedAreas.Add(id);
                var route = DreamAreaSpawnCatalog.Resolve(
                    id == DreamEscapeIds.AreaCorridor ? DreamEscapeIds.AreaClassroom : DreamEscapeIds.AreaCorridor, id);
                session.MoveDreamToArea(id, route.StoredPosition.X, route.StoredPosition.Y);
                var area = ResourceLoader.Load<PackedScene>($"res://scenes/dream/{scene}.tscn").Instantiate<Node3D>();
                host.AddChild(area);
                try
                {
                    var player = area.GetNode<PlayerController3D>("Player");
                    var rig = area.GetNode<ThirdPersonCameraRig>("ThirdPersonCameraRig");
                    var spawn = area switch
                    {
                        DreamAreaController3D a => a.ClassroomVisual.ResolveSpawn(requested),
                        DreamMusicRoomController3D a => a.MusicVisual.ResolveSpawn(requested),
                        DreamArchiveRoomController3D a => a.ArchiveVisual.ResolveSpawn(requested),
                        DreamAuditoriumController3D a => a.AuditoriumVisual.ResolveSpawn(requested),
                        DreamMansionController3D a => a.MansionVisual.ResolveSpawn(requested),
                        _ => requested
                    };
                    player.GlobalPosition = spawn;
                    player.SetFacingImmediate(Vector3.Forward);
                    rig.SetYawImmediate(0f);
                    rig.SnapToTarget();
                    label.Text = $"{scene} · idle";
                    await Wait(20);
                    foreach (var (action, slow, frames, name) in new[]
                    {
                        ("move_up", true, 30, "walk"), ("", false, 15, "stop"),
                        ("move_down", false, 42, "reverse"), ("", false, 15, "stop"),
                        ("move_right", true, 35, "turn90"), ("", false, 15, "stop")
                    })
                    {
                        label.Text = $"{scene} · {name}";
                        if (slow) Input.ActionPress("move_slow");
                        if (action.Length > 0) Input.ActionPress(action);
                        await Wait(frames);
                        await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        using var image = host.GetViewport().GetTexture().GetImage();
                        image.SavePng(Path.Combine(directory, $"{scene}-{name}.png"));
                        if (action.Length > 0) Input.ActionRelease(action);
                        Input.ActionRelease("move_slow");
                    }
                    GD.Print($"CHARACTER_AREA_CAPTURE_PASS area={id} enhanced={player.UsesEnhancedAnimation}");
                }
                finally { area.QueueFree(); await Wait(2); }
            }
        }
        finally
        {
            foreach (var action in new[] { "move_up", "move_down", "move_right", "move_left", "move_slow" }) Input.ActionRelease(action);
            label.QueueFree();
        }
    }
}
