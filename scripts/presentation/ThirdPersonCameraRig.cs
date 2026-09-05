using Godot;

namespace TownRpg.Presentation;

public partial class ThirdPersonCameraRig : Node3D
{
    [Export] public float FollowHalfLife { get; set; } = 0.10f;
    [Export] public float MouseDegreesPerPixel { get; set; } = 0.12f;
    [Export] public float GamepadDegreesPerSecond { get; set; } = 120f;
    [Export] public float FixedPitchDegrees { get; set; } = -21f;
    [Export] public float FollowTargetHeight { get; set; } = 1.30f;

    public Camera3D Camera { get; private set; } = null!;
    public SpringArm3D SpringArm { get; private set; } = null!;
    public Node3D? FollowTarget { get; private set; }
    public bool InputActive { get; private set; }
    public float LookSensitivity { get; private set; } = 1f;
    public float YawDegrees => RotationDegrees.Y;
    public Vector3 HorizontalForward
    {
        get
        {
            var forward = new Vector3(-Camera.GlobalBasis.Z.X, 0f, -Camera.GlobalBasis.Z.Z);
            return forward.LengthSquared() <= 0.0001f ? Vector3.Forward : forward.Normalized();
        }
    }

    private bool _windowFocused = true;

    public override void _Ready()
    {
        _windowFocused = DisplayServer.WindowIsFocused();
        SpringArm = GetNode<SpringArm3D>("SpringArm3D");
        Camera = SpringArm.GetNode<Camera3D>("CameraSocket/Camera3D");
        SpringArm.RotationDegrees = new Vector3(FixedPitchDegrees, 0f, 0f);
    }

    public override void _ExitTree()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut)
        {
            _windowFocused = false;
            ApplyMouseMode();
        }
        else if (what == NotificationWMWindowFocusIn)
        {
            _windowFocused = true;
            ApplyMouseMode();
        }
    }

    public override void _Process(double delta)
    {
        if (FollowTarget is not null)
        {
            var target = FollowTarget.GlobalPosition + new Vector3(0f, FollowTargetHeight, 0f);
            var halfLife = Math.Max(FollowHalfLife, 0.001f);
            var weight = 1f - Mathf.Pow(0.5f, (float)delta / halfLife);
            GlobalPosition = GlobalPosition.Lerp(target, weight);
        }

        if (!InputActive) return;
        var orbit = Input.GetAxis("camera_orbit_left", "camera_orbit_right");
        if (Math.Abs(orbit) > 0.001f)
            AddYaw(-orbit * GamepadDegreesPerSecond * LookSensitivity * (float)delta);
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!InputActive) return;
        var pointerCaptured = Input.MouseMode == Input.MouseModeEnum.Captured;
        if (!pointerCaptured)
        {
            if (_windowFocused && inputEvent is InputEventMouseButton
                {
                    Pressed: true,
                    ButtonIndex: MouseButton.Left
                })
            {
                ApplyMouseMode();
                GetViewport().SetInputAsHandled();
            }
            return;
        }
        if (inputEvent is not InputEventMouseMotion motion) return;
        TryApplyMouseMotion(motion.ScreenRelative, pointerCaptured);
    }

    public void SetFollowTarget(Node3D target, Rid excludedCollision)
    {
        ArgumentNullException.ThrowIfNull(target);
        FollowTarget = target;
        SpringArm.AddExcludedObject(excludedCollision);
        SnapToTarget();
    }

    public void SetInputActive(bool active)
    {
        if (InputActive == active) return;
        InputActive = active;
        ApplyMouseMode();
    }

    public void SetLookSensitivity(float sensitivity) =>
        LookSensitivity = Mathf.Clamp(sensitivity, 0.5f, 1.5f);

    public void SetYawImmediate(float yawDegrees)
    {
        RotationDegrees = new Vector3(0f, Mathf.Wrap(yawDegrees, -180f, 180f), 0f);
    }

    public void SetHorizontalForwardImmediate(Vector3 worldForward)
    {
        var direction = new Vector3(worldForward.X, 0f, worldForward.Z);
        if (direction.LengthSquared() <= 0.0001f) return;
        direction = direction.Normalized();
        SetYawImmediate(Mathf.RadToDeg(Mathf.Atan2(-direction.X, -direction.Z)));
    }

    public void SnapToTarget()
    {
        if (FollowTarget is null) return;
        GlobalPosition = FollowTarget.GlobalPosition + new Vector3(0f, FollowTargetHeight, 0f);
    }

    public bool TryApplyMouseMotion(Vector2 screenRelative, bool pointerCaptured)
    {
        if (!InputActive || !pointerCaptured || Math.Abs(screenRelative.X) <= 0.001f) return false;
        AddYaw(-screenRelative.X * MouseDegreesPerPixel * LookSensitivity);
        return true;
    }

    private void ApplyMouseMode() => Input.MouseMode = InputActive && _windowFocused
        ? Input.MouseModeEnum.Captured
        : Input.MouseModeEnum.Visible;

    private void AddYaw(float degrees) => SetYawImmediate(YawDegrees + degrees);
}
