using Godot;
using TownRpg.Presentation;

namespace TownRpg.Testing;

public static class CharacterMotionProbe
{
    public static async Task Run(Node host)
    {
        var stage = new Node3D { Name = "CharacterMotionProbe" };
        host.AddChild(stage);
        var player = new PlayerController3D();
        player.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, .85f, 0),
            Shape = new CapsuleShape3D { Radius = .2f, Height = 1.7f }
        });
        stage.AddChild(player);
        var camera = new Camera3D();
        stage.AddChild(camera);
        player.MovementCamera = camera;
        var frames = DreamCharacterFrames.Create(ResourceLoader.Load<Texture2D>("res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png"));
        player.ConfigureDefaultCharacterVisual(frames);
        async Task Wait(int count)
        {
            for (var i = 0; i < count; i++) await host.ToSignal(host.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Character locomotion: " + message);
        }
        try
        {
            ValidateLegacyAnimationDriver(stage);
            ValidateIndependentAnimationInstances(stage);
            ValidateWaypointCorner();
            player.SetFacingImmediate(Vector3.Forward);
            Input.ActionPress("move_up");
            await Wait(20);
            Require(Mathf.Abs(player.Velocity.Length() - 3.1f) < .02f, "jog speed");
            if (player.UsesDefault3DModel) Require(player.AnimationState == "jog", "jog blend state");
            Input.ActionPress("move_slow");
            await Wait(15);
            Require(Mathf.Abs(player.Velocity.Length() - 1.4f) < .02f, "Shift walk speed");
            if (player.UsesDefault3DModel) Require(player.AnimationState == "walk", "walk blend state");
            var phase = player.GaitPhase;
            var skeleton = player.CharacterModel?.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
            var leg = skeleton?.FindBone("右足D") ?? -1;
            var rotation = leg >= 0 ? skeleton!.GetBonePoseRotation(leg) : Quaternion.Identity;
            await Wait(2);
            if (player.UsesEnhancedAnimation) Require(player.GaitPhase != phase, "gait phase is frozen");
            if (leg >= 0) Require(rotation.AngleTo(skeleton!.GetBonePoseRotation(leg)) > .0001f, "animation graph did not deform the leg");
            player.Notification((int)Node.NotificationWMWindowFocusOut);
            var focusPosition = player.GlobalPosition;
            await Wait(3);
            Require(player.Velocity == Vector3.Zero && player.GlobalPosition == focusPosition, "focus loss retained motion");
            player.Notification((int)Node.NotificationWMWindowFocusIn);
            Input.ActionRelease("move_slow");
            Input.ActionRelease("move_up");
            await Wait(12);
            Require(player.Velocity.Length() < .001f, "release did not stop");
            var heading = player.CharacterModel?.Rotation.Y;
            camera.Rotation = new Vector3(0, 1f, 0);
            await Wait(4);
            Require(player.CharacterModel?.Rotation.Y == heading, "camera rotated idle character");
            camera.Rotation = Vector3.Zero;

            // Repeated input locks must stop travel without pinning the breathing clip to frame zero.
            if (player.UsesEnhancedAnimation && skeleton is not null)
            {
                player.InputEnabled = false;
                var center = skeleton.FindBone("センター");
                await Wait(3);
                var beforeIdle = skeleton.GetBonePosePosition(center);
                var idleChange = 0f;
                for (var i = 0; i < 55; i++)
                {
                    player.InputEnabled = false; await Wait(1);
                    idleChange = Math.Max(idleChange, beforeIdle.DistanceTo(skeleton.GetBonePosePosition(center)));
                }
                Require(idleChange > .0001f, "modal froze idle breathing");
                player.PlayInteractionPose("operate", player.GlobalPosition + Vector3.Forward);
                await Wait(12);
                var animations = player.CharacterModel!.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().First();
                if (animations.HasAnimation("operate")) Require(player.AnimationState == "operate", "modal erased interaction pose");
                player.InputEnabled = true;
                player.SetFacingImmediate(Vector3.Forward);
            }

            if (player.UsesDefault3DModel)
            {
                var clearanceRig = new ThirdPersonCameraRig { Name = "ThirdPersonCameraRig" };
                var arm = new SpringArm3D { Name = "SpringArm3D", SpringLength = .5f };
                var socket = new Node3D { Name = "CameraSocket" };
                socket.AddChild(new Camera3D { Name = "Camera3D" });
                arm.AddChild(socket);
                clearanceRig.AddChild(arm);
                stage.AddChild(clearanceRig);
                await Wait(4);
                Require(!player.CharacterModel!.Visible, "near camera did not hide mesh");
                arm.SpringLength = 2f;
                await Wait(4);
                Require(player.CharacterModel.Visible, "camera clearance did not restore mesh");
                clearanceRig.QueueFree();
                await Wait(2);
            }

            if (player.UsesEnhancedAnimation)
            {
                Input.ActionPress("move_down");
                await Wait(3);
                Require(player.AnimationState.StartsWith("turn_", StringComparison.Ordinal), "reverse did not pivot");
                player.InputEnabled = false;
                Require(player.AnimationState == "idle" && player.Velocity == Vector3.Zero, "modal did not reset immediately");
                Input.ActionRelease("move_down");
                player.InputEnabled = true;
                player.SetFacingImmediate(Vector3.Right);
                Require(player.GaitPhase == 0f && player.AnimationState == "idle", "spawn retained animation");
            }
            var wall = new StaticBody3D { Position = new Vector3(0, 1f, -1f) };
            wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(10f, 2f, .2f) } });
            stage.AddChild(wall);
            player.GlobalPosition = Vector3.Zero;
            player.SetFacingImmediate(Vector3.Forward);
            Input.ActionPress("move_up");
            await Wait(55);
            Require(player.GlobalPosition.Z > -1f && player.Velocity.Length() < .025f, "wall collision");
            if (player.UsesDefault3DModel) Require(player.AnimationState == "idle", "walking against wall");
            Input.ActionPress("move_right");
            await Wait(30);
            Require(player.GlobalPosition.X > .25f && player.GlobalPosition.Z > -1f, "wall slide");
            Input.ActionRelease("move_right");
            Input.ActionRelease("move_up");
            player.ConfigureDefaultCharacterVisual(frames, "res://tests/missing-character-motion.glb");
            Require(!player.UsesDefault3DModel && player.CharacterSprite!.Visible, "missing resource fallback");
            player.GlobalPosition = Vector3.Zero;
            player.SetFacingImmediate(Vector3.Forward);
            Input.ActionPress("move_right");
            await Wait(15);
            Require(player.GlobalPosition.X > .2f, "billboard movement");
            GD.Print("CHARACTER_MOTION_PASS jog walk pivot modal focus wall slide legacy fallback");
        }
        finally
        {
            foreach (var action in new[] { "move_up", "move_down", "move_right", "move_left", "move_slow" }) Input.ActionRelease(action);
            stage.QueueFree();
            await Wait(2);
        }
    }

    private static void ValidateLegacyAnimationDriver(Node3D stage)
    {
        var model = new Node3D { Name = "LegacyAnimationFixture" };
        stage.AddChild(model);
        var player = new AnimationPlayer();
        model.AddChild(player);
        var library = new AnimationLibrary();
        foreach (var name in new[] { "idle", "walk", "jog", "turn_left_90" })
        {
            var animation = new Animation { Length = 1f };
            var track = animation.AddTrack(Animation.TrackType.Value);
            animation.TrackSetPath(track, ".:position");
            animation.TrackInsertKey(track, 0, Vector3.Zero);
            library.AddAnimation(name, animation);
        }
        player.AddAnimationLibrary("", library);
        var driver = new CharacterAnimationDriver(model, player);
        var motion = new CharacterLocomotion();
        motion.Reset(0);
        motion.ObserveMotion(new System.Numerics.Vector2(0, 1.4f), 1f / 60f);
        driver.Update(motion, 1f / 60f);
        if (driver.Enhanced || player.CurrentAnimation != "walk" ||
            library.GetAnimation("walk").LoopMode != Animation.LoopModeEnum.None)
            throw new InvalidOperationException("Legacy animation fallback mutated shared resources or required optional turns.");
        driver.Reset();
        if (player.CurrentAnimation != "idle" || player.SpeedScale != 1f)
            throw new InvalidOperationException("Legacy animation reset retained movement playback speed.");
        model.QueueFree();
    }

    private static void ValidateIndependentAnimationInstances(Node3D stage)
    {
        var library = new AnimationLibrary();
        foreach (var name in new[] { "idle", "walk", "jog", "turn_left_90", "turn_right_90", "turn_left_180", "turn_right_180" })
            library.AddAnimation(name, new Animation { Length = 1 });
        var models = new[] { new Node3D(), new Node3D() };
        var drivers = new List<CharacterAnimationDriver>();
        foreach (var model in models)
        {
            stage.AddChild(model);
            var animator = new AnimationPlayer(); model.AddChild(animator);
            animator.AddAnimationLibrary("", library);
            drivers.Add(new CharacterAnimationDriver(model, animator));
        }
        var motion = new CharacterLocomotion(); motion.ObserveMotion(new(0, 1.4f), .1f);
        drivers[0].Update(motion, .1f); var phase = drivers[0].Phase;
        drivers[1].SetAction("missing-optional-gesture"); drivers[1].Reset();
        if (phase <= 0 || drivers[0].Phase != phase || drivers[1].Phase != 0 ||
            library.GetAnimation("walk").LoopMode != Animation.LoopModeEnum.None)
            throw new InvalidOperationException("Animation instances shared phase or modified imported resources.");
        drivers[1].Update(new CharacterLocomotion(), .1f);
        if (drivers[1].State != "idle") throw new InvalidOperationException("Missing gesture did not fall back to idle.");
        foreach (var fps in new[] { 30, 60, 120 })
        {
            drivers[0].Reset();
            for (var i = 0; i < fps * 7 / 10; i++) drivers[0].Update(motion, 1f / fps);
            if (Math.Abs(drivers[0].Phase - .7f) > .0001f)
                throw new InvalidOperationException("Distance-driven gait phase changed with frame rate.");
        }
        foreach (var model in models) model.QueueFree();
    }

    private static void ValidateWaypointCorner()
    {
        static bool Blocked(Vector3 p) => p.X >= .005f && p.X <= .6f && p.Z >= -.6f && p.Z <= -.005f;
        foreach (var fps in new[] { 30, 60, 120 })
        {
            var navigator = new GroundWaypointNavigator3D([Vector3.Zero], Blocked, p => p);
            var position = new Vector3(-1, 0, -1);
            var destination = Vector3.Right;
            for (var i = 0; i < fps * 5; i++)
            {
                var next = navigator.Advance(position, destination, 1f / fps);
                if (Blocked(next) || position.DistanceTo(next) > 1f / fps + .00001f)
                    throw new InvalidOperationException("Follower cut a waypoint corner or teleported.");
                position = next;
            }
            if (position.DistanceTo(destination) > .001f)
                throw new InvalidOperationException("Follower stalled at a waypoint corner.");
        }
    }
}
