using Godot;

namespace TownRpg.Presentation;

public partial class PlayerController3D : CharacterBody3D
{
    // This local non-commercial asset is intentionally excluded from Git and exports.
    // A clean checkout must continue to work through the sprite fallback below.
    public const string DefaultCharacterModelPath =
        "res://tests/mc-model-test-runtime/caelus-pbr-test.glb";

    [Export] public float Speed { get; set; } = 3.1f;

    public event Action<Vector3>? Moved;

    public Camera3D? MovementCamera { get; set; }
    private bool _inputEnabled = true;
    public bool InputEnabled
    {
        get => _inputEnabled;
        set
        {
            if (_inputEnabled == value) return;
            _inputEnabled = value;
            if (!value) { StopLocomotion(); _animationDriver?.Update(_locomotion, 0); }
        }
    }
    public Vector3 FacingWorld { get; private set; } = Vector3.Forward;
    public AnimatedSprite3D? CharacterSprite { get; private set; }
    public Node3D? CharacterModel { get; private set; }
    public bool UsesDefault3DModel => CharacterModel is not null;

    private string _facingAnimation = "down";
    private readonly CharacterLocomotion _locomotion = new();
    private CharacterAnimationDriver? _animationDriver;
    public string AnimationState => _animationDriver?.State ?? "billboard";
    public bool UsesEnhancedAnimation => _animationDriver?.Enhanced == true;
    public float GaitPhase => _animationDriver?.Phase ?? 0f;
    private float _lean;
    private MeshInstance3D? _contactShadow;
    private bool _modelVisible = true;
    private bool _windowFocused = true;
    private Vector3? _interactionFacing;

    private const float CameraHideDistance = 0.65f;
    private const float CameraShowDistance = 0.80f;

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut)
        {
            _windowFocused = false;
            ResetMotion();
        }
        else if (what == NotificationWMWindowFocusIn)
        {
            _windowFocused = true;
            ResetMotion();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_windowFocused || MovementCamera is null)
        {
            UpdateCameraClearance();
            return;
        }
        if (!InputEnabled)
        {
            StopLocomotion();
            UpdateInteractionFacing((float)delta);
            UpdateAnimation(Vector3.Zero, (float)delta);
            UpdateCameraClearance();
            return;
        }

        var input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        var requestedMotion = CameraRelativeMovement.ToWorld(input, MovementCamera);
        var step = _locomotion.Step(new System.Numerics.Vector2(requestedMotion.X, requestedMotion.Z),
            Input.IsActionPressed("move_slow"), Speed, (float)delta, UsesEnhancedAnimation);
        Velocity = new Vector3(step.X, 0f, step.Y);
        var previous = GlobalPosition;
        MoveAndSlide();
        GlobalPosition = new Vector3(GlobalPosition.X, 0f, GlobalPosition.Z);
        var actualMotion = GlobalPosition - previous;
        _locomotion.ObserveMotion(delta > 0 ?
            new System.Numerics.Vector2(actualMotion.X, actualMotion.Z) / (float)delta :
            System.Numerics.Vector2.Zero, (float)delta);
        if (actualMotion.LengthSquared() > 0.000001f)
        {
            _interactionFacing = null;
            FacingWorld = actualMotion.Normalized();
            Moved?.Invoke(GlobalPosition);
        }
        if (!_locomotion.Moving && !_locomotion.IsTurning) UpdateInteractionFacing((float)delta);
        UpdateAnimation(actualMotion, (float)delta);
        UpdateCameraClearance();
    }

    public void ConfigureDefaultCharacterVisual(SpriteFrames fallbackFrames, string modelPath = DefaultCharacterModelPath)
    {
        ArgumentNullException.ThrowIfNull(fallbackFrames);
        CharacterModel?.QueueFree();
        CharacterModel = null;
        _animationDriver = null;
        ConfigureCharacterFrames(fallbackFrames);
        ResetMotion();

        if (!ResourceLoader.Exists(modelPath)) return;
        var packedModel = ResourceLoader.Load<PackedScene>(modelPath);
        if (packedModel is null) return;

        var root = packedModel.Instantiate();
        if (root is not Node3D model)
        {
            root.Free();
            GD.PushWarning($"Default character model root is not Node3D: {modelPath}");
            return;
        }

        var animationPlayer = FindAnimationPlayer(model);
        var requiredAnimations = new StringName[] { "idle", "walk", "jog" };
        if (animationPlayer is null || requiredAnimations.Any(name =>
                !animationPlayer.HasAnimation(name) || animationPlayer.GetAnimation(name).Length <= 0))
        {
            model.Free();
            GD.PushWarning(
                $"Default character model is missing AnimationPlayer or idle/walk/jog: {modelPath}");
            return;
        }

        CharacterModel = model;
        CharacterModel.Name = "DefaultCharacterModel";
        CharacterModel.Position = Vector3.Zero;
        CharacterModel.Rotation = new Vector3(0f, Mathf.Atan2(FacingWorld.X, FacingWorld.Z), 0f);
        AddChild(CharacterModel);
        _animationDriver = new CharacterAnimationDriver(model, animationPlayer);
        _modelVisible = true;
        if (CharacterSprite is not null) CharacterSprite.Visible = false;
        ResetMotion();
    }

    public void ConfigureCharacterFrames(SpriteFrames frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        CharacterSprite?.QueueFree();
        EnsureContactShadow();
        CharacterSprite = new AnimatedSprite3D
        {
            Name = "CharacterSprite",
            SpriteFrames = frames,
            Animation = "down",
            Frame = 0,
            Position = new Vector3(0f, 0.83f, 0f),
            PixelSize = 0.0344f,
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Shaded = false,
            DoubleSided = true,
            RenderPriority = 2
        };
        AddChild(CharacterSprite);
    }

    public void SetFacingImmediate(Vector3 worldDirection)
    {
        var flattened = new Vector3(worldDirection.X, 0f, worldDirection.Z);
        if (flattened.LengthSquared() <= 0.0001f) return;
        FacingWorld = flattened.Normalized();
        _locomotion.Reset(Mathf.Atan2(FacingWorld.X, FacingWorld.Z));
        if (CharacterModel is not null)
        {
            CharacterModel.Rotation = new Vector3(
                CharacterModel.Rotation.X,
                Mathf.Atan2(FacingWorld.X, FacingWorld.Z),
                CharacterModel.Rotation.Z);
        }
        if (MovementCamera is not null)
        {
            var screenDirection = CameraRelativeMovement.ToScreen(FacingWorld, MovementCamera);
            _facingAnimation = Math.Abs(screenDirection.X) > Math.Abs(screenDirection.Y)
                ? screenDirection.X < 0f ? "left" : "right"
                : screenDirection.Y < 0f ? "up" : "down";
        }
        ResetMotion();
    }

    private void EnsureContactShadow()
    {
        if (_contactShadow is not null) return;
        var material = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.015f, 0.025f, 0.03f, 0.24f)
        };
        _contactShadow = new MeshInstance3D
        {
            Name = "ContactShadow",
            Position = new Vector3(0f, 0.008f, 0.04f),
            Mesh = new CylinderMesh
            {
                TopRadius = 0.28f,
                BottomRadius = 0.28f,
                Height = 0.012f,
                RadialSegments = 24,
                Material = material
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_contactShadow);
    }

    private void UpdateAnimation(Vector3 actualMotion, float delta)
    {
        if (CharacterModel is not null)
        {
            var yawChange = Mathf.AngleDifference(CharacterModel.Rotation.Y, _locomotion.Yaw);
            var targetLean = !_locomotion.IsTurning && _locomotion.Moving && delta > 0f
                ? Mathf.Clamp(-yawChange / delta * _locomotion.ActualSpeed * .004f, -.06f, .06f) : 0f;
            _lean = Mathf.Lerp(_lean, targetLean, 1f - Mathf.Exp(-16f * delta));
            CharacterModel.Rotation = new Vector3(0f, _locomotion.Yaw, _lean);
            _animationDriver?.Update(_locomotion, delta);
        }

        if (CharacterSprite is null || MovementCamera is null || CharacterModel is not null) return;
        if (_locomotion.Moving)
        {
            var screenMotion = CameraRelativeMovement.ToScreen(actualMotion, MovementCamera);
            _facingAnimation = Math.Abs(screenMotion.X) > Math.Abs(screenMotion.Y)
                ? screenMotion.X < 0 ? "left" : "right"
                : screenMotion.Y < 0 ? "up" : "down";
            if (CharacterSprite.Animation != _facingAnimation)
                CharacterSprite.Play(_facingAnimation);
            else if (!CharacterSprite.IsPlaying())
                CharacterSprite.Play();
            return;
        }
        StopAnimation();
    }

    private void StopAnimation()
    {
        if (CharacterSprite is null) return;
        CharacterSprite.Animation = _facingAnimation;
        CharacterSprite.Stop();
        CharacterSprite.Frame = 0;
    }

    private void ResetMotion()
    {
        _interactionFacing = null;
        StopLocomotion();
        _animationDriver?.Reset();
    }

    private void StopLocomotion()
    {
        Velocity = Vector3.Zero;
        _locomotion.Reset(CharacterModel?.Rotation.Y ?? Mathf.Atan2(FacingWorld.X, FacingWorld.Z));
        _lean = 0f;
        if (CharacterModel is not null) CharacterModel.Rotation = new Vector3(0f, _locomotion.Yaw, 0f);
        StopAnimation();
    }

    public void PlayInteractionPose(string pose, Vector3? target = null)
    {
        StopLocomotion();
        _interactionFacing = target is { } point ? point - GlobalPosition : _interactionFacing ?? FacingWorld;
        _animationDriver?.SetAction(pose);
    }

    private void UpdateInteractionFacing(float delta)
    {
        if (_interactionFacing is not { } facing || new Vector2(facing.X, facing.Z).LengthSquared() < .0001f) return;
        var yaw = _locomotion.Yaw + Mathf.Clamp(Mathf.AngleDifference(_locomotion.Yaw, Mathf.Atan2(facing.X, facing.Z)), -3f * delta, 3f * delta);
        _locomotion.Reset(yaw);
        FacingWorld = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
    }

    private void UpdateCameraClearance()
    {
        if (CharacterModel is null) return;
        var rig = GetParent()?.GetNodeOrNull<ThirdPersonCameraRig>("ThirdPersonCameraRig");
        if (rig?.SpringArm is null) return;
        var actualLength = rig.SpringArm.GetHitLength();
        if (_modelVisible && actualLength < CameraHideDistance)
        {
            _modelVisible = false;
            CharacterModel.Visible = false;
        }
        else if (!_modelVisible && actualLength > CameraShowDistance)
        {
            _modelVisible = true;
            CharacterModel.Visible = true;
        }
    }

    private static AnimationPlayer? FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer animationPlayer) return animationPlayer;
        foreach (var child in node.GetChildren())
        {
            var found = FindAnimationPlayer(child);
            if (found is not null) return found;
        }
        return null;
    }
}
