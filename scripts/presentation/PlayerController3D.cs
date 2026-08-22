using Godot;

namespace TownRpg.Presentation;

public partial class PlayerController3D : CharacterBody3D
{
    [Export] public float Speed { get; set; } = 3.1f;

    public event Action<Vector3>? Moved;

    public Camera3D? MovementCamera { get; set; }
    public bool InputEnabled { get; set; } = true;
    public Vector3 FacingWorld { get; private set; } = Vector3.Forward;
    public AnimatedSprite3D? CharacterSprite { get; private set; }

    private string _facingAnimation = "down";
    private MeshInstance3D? _contactShadow;

    public override void _PhysicsProcess(double delta)
    {
        if (!InputEnabled || MovementCamera is null)
        {
            Velocity = Vector3.Zero;
            StopAnimation();
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
        UpdateAnimation(actualMotion, input.LengthSquared() > 0.01f);
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

    private void UpdateAnimation(Vector3 actualMotion, bool hasInput)
    {
        if (CharacterSprite is null || MovementCamera is null) return;
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
        if (CharacterSprite is null) return;
        CharacterSprite.Animation = _facingAnimation;
        CharacterSprite.Stop();
        CharacterSprite.Frame = 0;
    }
}
