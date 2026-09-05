using Godot;

namespace TownRpg.Presentation;

public partial class QuarterTurnCameraRig : Node3D
{
    [Export] public float RotationDuration { get; set; } = 0.3f;

    public event Action<int, int>? RotationStarted;
    public event Action<int>? RotationCompleted;

    public Camera3D Camera { get; private set; } = null!;
    public int CurrentViewIndex { get; private set; }
    public bool IsRotating { get; private set; }

    private float _continuousYawDegrees = 45f;
    private Tween? _rotationTween;

    public override void _Ready()
    {
        Camera = GetNode<Camera3D>("Camera3D");
        RotationDegrees = new Vector3(0f, _continuousYawDegrees, 0f);
        Camera.LookAt(GlobalPosition, Vector3.Up);
    }

    public bool TryRotate(int clockwiseSteps)
    {
        if (IsRotating || clockwiseSteps == 0) return false;
        var direction = Math.Sign(clockwiseSteps);
        var from = CurrentViewIndex;
        var to = PositiveModulo(CurrentViewIndex + direction, 4);
        IsRotating = true;
        RotationStarted?.Invoke(from, to);
        _continuousYawDegrees -= direction * 90f;
        _rotationTween?.Kill();
        _rotationTween = CreateTween();
        _rotationTween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        _rotationTween.TweenProperty(this, "rotation:y", Mathf.DegToRad(_continuousYawDegrees), RotationDuration);
        _rotationTween.TweenCallback(Callable.From(() =>
        {
            CurrentViewIndex = to;
            IsRotating = false;
            RotationCompleted?.Invoke(CurrentViewIndex);
        }));
        return true;
    }

    public void SetViewImmediate(int viewIndex)
    {
        _rotationTween?.Kill();
        IsRotating = false;
        CurrentViewIndex = PositiveModulo(viewIndex, 4);
        _continuousYawDegrees = 45f - CurrentViewIndex * 90f;
        RotationDegrees = new Vector3(0f, _continuousYawDegrees, 0f);
        RotationCompleted?.Invoke(CurrentViewIndex);
    }

    private static int PositiveModulo(int value, int divisor) => (value % divisor + divisor) % divisor;
}
