using Godot;
using TownRpg.Application;

namespace TownRpg.Presentation;

public partial class DreamCompanion3D : Node3D
{
    public const string DefaultModelPath = "res://tests/xiaoyu-model-runtime/xiaoyu-motion-v3.glb";
    [Export] public bool ShowBeforeJoin { get; set; }
    [Export] public Vector3 PreJoinPosition { get; set; }
    [Export] public float FollowDistance { get; set; } = 1.2f;
    [Export] public float FollowSpeed { get; set; } = 3.4f;
    private GameSession _session = null!;
    private PlayerController3D _player = null!;
    private AnimatedSprite3D _sprite = null!;
    private readonly CompanionMotionState _motion = new();
    private readonly CharacterLocomotion _locomotion = new();
    private CharacterAnimationDriver? _animation;
    private bool _initialized, _focused = true;
    private float _lean;
    public bool IsFollowing => _session.State.DreamEscape?.XiaoyuJoined == true;
    public AnimatedSprite3D Sprite => _sprite;
    public Node3D? CharacterModel { get; private set; }
    public string AnimationState => _animation?.State ?? "billboard";
    public float GaitPhase => _animation?.Phase ?? 0;
    public bool PoseArrived => _motion.Arrived;
    public string? RequestedPose => _motion.Pose;
    public Vector3 FacingWorld => new(Mathf.Sin(_locomotion.Yaw), 0, Mathf.Cos(_locomotion.Yaw));
    public Func<Vector3, Vector3>? FollowPositionResolver { get; set; }
    public Func<Vector3, Vector3, float, Vector3>? FollowStepResolver { get; set; }
    public bool UseRoutedMotion { get; set; } = true;
    public Func<bool>? RoutedMotionPaused { get; set; }
    public Func<Vector3, bool>? FollowPositionIsBlocked { get; set; }
    private static System.Numerics.Vector3 Numerics(Vector3 v) => new(v.X, v.Y, v.Z);
    private static Vector3 GodotVector(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        _player = GetNode<PlayerController3D>("../Player");
        _sprite = new AnimatedSprite3D
        {
            Name = "XiaoyuBillboard", PixelSize = .0085f,
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            DoubleSided = true, Position = new(0, .82f, 0),
            SpriteFrames = DreamCharacterFrames.Create(ResourceLoader.Load<Texture2D>(
                "res://assets/generated/dream_escape/pixel/lin-xiaoyu-walk-4x4-v2.png")),
            Animation = "down"
        };
        AddChild(_sprite);
        var shadowMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(.015f, .025f, .03f, .22f)
        };
        AddChild(new MeshInstance3D
        {
            Name = "ContactShadow", Position = new(0, .008f, 0),
            Mesh = new CylinderMesh { TopRadius = .25f, BottomRadius = .25f, Height = .012f, RadialSegments = 24, Material = shadowMaterial },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        ConfigureCharacterVisual();
        RefreshImmediate(force: true);
        _initialized = false; // Parent configures player spawn and safe-route callbacks after child _Ready.
    }

    public void ConfigureCharacterVisual(string modelPath = DefaultModelPath)
    {
        CharacterModel?.QueueFree(); CharacterModel = null; _animation = null; _sprite.Visible = true;
        if (!ResourceLoader.Exists(modelPath)) return;
        var root = ResourceLoader.Load<PackedScene>(modelPath)?.Instantiate();
        if (root is not Node3D model) { root?.Free(); return; }
        var player = model.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (player is null || new[] { "idle", "walk", "jog" }.Any(n => !player.HasAnimation(n) || player.GetAnimation(n).Length <= 0))
        {
            model.Free(); GD.PushWarning("Xiaoyu model has no valid base animations; using billboard."); return;
        }
        CharacterModel = model; model.Name = "XiaoyuModel"; AddChild(model);
        _animation = new CharacterAnimationDriver(model, player, 1.73f);
        _sprite.Visible = false;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut)
        {
            _focused = false; ResetPresentation();
        }
        else if (what == NotificationWMWindowFocusIn) _focused = true;
    }

    public override void _Process(double delta)
    {
        var dt = Mathf.Clamp((float)delta, 0, .1f);
        if (!_focused || dt <= 0) return;
        Visible = IsFollowing || ShowBeforeJoin;
        if (!IsFollowing)
        {
            GlobalPosition = PreJoinPosition;
            _motion.Reset(); _locomotion.Reset(_locomotion.Yaw);
            UpdateAnimation(Vector3.Zero, dt);
            return;
        }
        if (_motion.Pose is not null && !_motion.Held && _session.Modal.IsBlocked && !_session.Modal.HasOwner("dream-story"))
        {
            _motion.CancelPose(); _animation?.ClearAction();
        }
        var paused = _motion.Pose is null && (RoutedMotionPaused?.Invoke() ?? _session.Modal.IsBlocked);
        var desired = _motion.Pose is null ? DesiredFollowPosition() : GodotVector(_motion.Destination);
        var previous = GlobalPosition;
        if (FollowPositionIsBlocked?.Invoke(previous) == true)
        {
            GlobalPosition = FollowPositionResolver?.Invoke(desired) ?? desired;
            _motion.Stop(); _locomotion.Reset(_locomotion.Yaw);
            UpdateAnimation(Vector3.Zero, dt);
            return;
        }
        var distance = previous.DistanceTo(desired);
        var travel = _motion.Step(distance, FollowSpeed, dt, paused);
        var ahead = FollowStepResolver?.Invoke(previous, desired, .16f) ?? previous.MoveToward(desired, .16f);
        var direction = (ahead - previous).Normalized();
        if (paused || travel <= 0)
            _locomotion.Reset(_locomotion.Yaw);
        else
        {
            var input = new System.Numerics.Vector2(direction.X, direction.Z) * (_motion.Speed / FollowSpeed);
            var velocity = _locomotion.Step(input, false, FollowSpeed, dt, _animation?.Enhanced == true);
            var step = Math.Min(travel, velocity.Length() * dt);
            GlobalPosition = FollowStepResolver?.Invoke(previous, desired, step) ?? previous.MoveToward(desired, step);
        }
        var movement = GlobalPosition - previous;
        _locomotion.ObserveMotion(new System.Numerics.Vector2(movement.X, movement.Z) / dt, dt);
        _motion.TickPose(GlobalPosition.DistanceTo(desired), dt, paused);
        if (_motion.Pose is { } pose && _motion.Arrived)
        {
            FaceDirection(GodotVector(_motion.Facing), dt);
            _animation?.SetAction(pose, held: true);
        }
        else if (_motion.Pose is not null || !paused) _animation?.ClearAction();
        UpdateAnimation(movement, dt);
    }

    public void RefreshImmediate(bool force = false)
    {
        Visible = IsFollowing || ShowBeforeJoin;
        if (_initialized && !force) return;
        _initialized = true;
        ResetPresentation();
        GlobalPosition = IsFollowing ? DesiredFollowPosition() : PreJoinPosition;
    }

    public void ResetPresentation()
    {
        _motion.Reset(); _locomotion.Reset(_locomotion.Yaw); _lean = 0;
        _animation?.Reset();
    }

    public void SetHeldPose(bool active, Vector3 worldPosition, string pose = "observe", Vector3? facing = null)
    {
        if (active)
            _motion.Request(pose, Numerics(worldPosition), Numerics(facing ?? Vector3.Forward), 0, true);
        else if (_motion.Held)
        {
            _motion.CancelPose(); _animation?.ClearAction();
        }
    }

    public void PlayScriptedPose(Vector3 worldPosition, double durationSeconds, string pose = "operate", Vector3? facing = null)
    {
        if (_motion.Held || !IsFollowing) return;
        _motion.Request(pose, Numerics(worldPosition), Numerics(facing ?? Vector3.Forward), (float)durationSeconds, false);
    }

    public void PlayConversationPose(Vector3 target)
    {
        if (!IsFollowing || _motion.Pose is not null) return;
        FaceDirection(target - GlobalPosition, .1f);
        _animation?.SetAction("talk");
    }

    private void FaceDirection(Vector3 direction, float dt)
    {
        if (direction.LengthSquared() < .0001f) return;
        var yaw = _locomotion.Yaw + Mathf.Clamp(Mathf.AngleDifference(_locomotion.Yaw, Mathf.Atan2(direction.X, direction.Z)), -3f * dt, 3f * dt);
        _locomotion.Reset(yaw);
    }

    public void ConfigureBillboardHeight(float heightMetres)
    {
        if (_sprite.SpriteFrames is null) return;
        using var frame = _sprite.SpriteFrames.GetFrameTexture("down", 0).GetImage();
        var bounds = frame.GetUsedRect();
        if (bounds.Size.Y <= 0) throw new InvalidOperationException("Companion frame has no visible pixels.");
        _sprite.PixelSize = heightMetres / bounds.Size.Y;
        _sprite.Position = new(0, (bounds.End.Y - frame.GetHeight() / 2f) * _sprite.PixelSize, 0);
        _sprite.AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass;
    }

    private Vector3 DesiredFollowPosition()
    {
        var facing = _player.FacingWorld;
        if (facing.LengthSquared() <= .0001f) facing = Vector3.Forward;
        var desired = _player.GlobalPosition - facing.Normalized() * FollowDistance + Vector3.Up.Cross(facing).Normalized() * .28f;
        desired.Y = 0;
        return FollowPositionResolver?.Invoke(desired) ?? desired;
    }

    private void UpdateAnimation(Vector3 movement, float dt)
    {
        if (CharacterModel is not null)
        {
            var turn = Mathf.AngleDifference(CharacterModel.Rotation.Y, _locomotion.Yaw);
            var lean = _locomotion.Moving ? Mathf.Clamp(-turn / dt * _locomotion.ActualSpeed * .003f, -.04f, .04f) : 0;
            _lean = Mathf.Lerp(_lean, lean, 1 - Mathf.Exp(-12 * dt));
            CharacterModel.Rotation = new(0, _locomotion.Yaw, _lean);
            _animation?.Update(_locomotion, dt);
            if (_player.MovementCamera is { } clearanceCamera)
            {
                var distance = clearanceCamera.GlobalPosition.DistanceTo(GlobalPosition + Vector3.Up * 1.15f);
                CharacterModel.Visible = distance >= (CharacterModel.Visible ? .50f : .65f);
            }
            return;
        }
        if (!_locomotion.Moving)
        {
            _sprite.Pause(); _sprite.Frame = 0; return;
        }
        var screen = _player.MovementCamera is { } camera ? CameraRelativeMovement.ToScreen(movement, camera) : new Vector2(movement.X, movement.Z);
        _sprite.Animation = Math.Abs(screen.X) > Math.Abs(screen.Y) ? screen.X >= 0 ? "right" : "left" : screen.Y >= 0 ? "down" : "up";
        _sprite.SpeedScale = Mathf.Clamp(_locomotion.ActualSpeed / 1.4f, .3f, 2.4f);
        _sprite.Play();
    }
}
