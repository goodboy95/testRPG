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
    public bool InputEnabled { get; set; } = true;
    public Vector3 FacingWorld { get; private set; } = Vector3.Forward;
    public AnimatedSprite3D? CharacterSprite { get; private set; }
    public Node3D? CharacterModel { get; private set; }
    public bool UsesDefault3DModel => CharacterModel is not null;

    private string _facingAnimation = "down";
    private string _modelAnimation = string.Empty;
    private AnimationPlayer? _modelAnimationPlayer;
    private MeshInstance3D? _contactShadow;
    private bool _modelVisible = true;

    private const float ModelTurnSpeed = 10f;
    private const float CameraHideDistance = 0.65f;
    private const float CameraShowDistance = 0.80f;

    public override void _PhysicsProcess(double delta)
    {
        if (!InputEnabled || MovementCamera is null)
        {
            Velocity = Vector3.Zero;
            StopAnimation();
            UpdateCameraClearance();
            return;
        }

        var input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        var requestedMotion = CameraRelativeMovement.ToWorld(input, MovementCamera);
        Velocity = requestedMotion * Speed;
        var previous = GlobalPosition;
        MoveAndSlide();
        GlobalPosition = new Vector3(GlobalPosition.X, 0f, GlobalPosition.Z);
        var actualMotion = GlobalPosition - previous;
        if (actualMotion.LengthSquared() > 0.000001f)
        {
            FacingWorld = actualMotion.Normalized();
            Moved?.Invoke(GlobalPosition);
        }
        UpdateAnimation(actualMotion, input.LengthSquared() > 0.01f, (float)delta);
        UpdateCameraClearance();
    }

    public void ConfigureDefaultCharacterVisual(SpriteFrames fallbackFrames)
    {
        ArgumentNullException.ThrowIfNull(fallbackFrames);
        ConfigureCharacterFrames(fallbackFrames);

        if (!ResourceLoader.Exists(DefaultCharacterModelPath)) return;
        var packedModel = ResourceLoader.Load<PackedScene>(DefaultCharacterModelPath);
        if (packedModel is null) return;

        var root = packedModel.Instantiate();
        if (root is not Node3D model)
        {
            root.Free();
            GD.PushWarning($"Default character model root is not Node3D: {DefaultCharacterModelPath}");
            return;
        }

        var animationPlayer = FindAnimationPlayer(model);
        var requiredAnimations = new StringName[] { "idle", "walk", "jog" };
        if (animationPlayer is null || requiredAnimations.Any(name => !animationPlayer.HasAnimation(name)))
        {
            model.Free();
            GD.PushWarning(
                $"Default character model is missing AnimationPlayer or idle/walk/jog: {DefaultCharacterModelPath}");
            return;
        }

        CharacterModel?.QueueFree();
        CharacterModel = model;
        CharacterModel.Name = "DefaultCharacterModel";
        CharacterModel.Position = Vector3.Zero;
        CharacterModel.Rotation = new Vector3(0f, Mathf.Pi, 0f);
        AddChild(CharacterModel);
        _modelAnimationPlayer = animationPlayer;
        _modelAnimation = string.Empty;
        _modelVisible = true;
        if (CharacterSprite is not null) CharacterSprite.Visible = false;
        PlayModelAnimation("idle");
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
        StopAnimation();
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

    private void UpdateAnimation(Vector3 actualMotion, bool hasInput, float delta)
    {
        if (CharacterModel is not null)
        {
            if (hasInput && actualMotion.LengthSquared() > 0.000001f)
            {
                var speed = delta <= 0f ? Speed : actualMotion.Length() / delta;
                PlayModelAnimation(speed < Speed * 0.70f ? "walk" : "jog");
                var direction = actualMotion.Normalized();
                var targetYaw = Mathf.Atan2(direction.X, direction.Z);
                var weight = 1f - Mathf.Exp(-ModelTurnSpeed * Math.Max(delta, 0f));
                CharacterModel.Rotation = new Vector3(
                    CharacterModel.Rotation.X,
                    Mathf.LerpAngle(CharacterModel.Rotation.Y, targetYaw, weight),
                    CharacterModel.Rotation.Z);
            }
            else
            {
                PlayModelAnimation("idle");
            }
        }

        if (CharacterSprite is null || MovementCamera is null || CharacterModel is not null) return;
        if (hasInput && actualMotion.LengthSquared() > 0.000001f)
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
        PlayModelAnimation("idle");
        if (CharacterSprite is null) return;
        CharacterSprite.Animation = _facingAnimation;
        CharacterSprite.Stop();
        CharacterSprite.Frame = 0;
    }

    private void PlayModelAnimation(StringName animation)
    {
        if (_modelAnimationPlayer is null || _modelAnimation == animation.ToString()) return;
        _modelAnimation = animation.ToString();
        _modelAnimationPlayer.Play(animation, 0.16);
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
