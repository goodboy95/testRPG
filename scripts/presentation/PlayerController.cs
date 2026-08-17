using Godot;
using TownRpg.Application;

namespace TownRpg.Presentation;

public partial class PlayerController : CharacterBody2D
{
    [Export] public float Speed { get; set; } = 88f;
    [Export] public Rect2 MovementBounds { get; set; } = new(18, 40, 604, 292);

    private GameSession Session => GetNode<GameSession>("/root/GameSession");
    private Vector2 _facing = Vector2.Down;
    private AnimatedSprite2D? _animatedSprite;
    private string _facingAnimation = "down";

    public override void _Ready()
    {
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Session.Modal.IsBlocked)
        {
            Velocity = Vector2.Zero;
            StopAnimation();
            return;
        }

        var input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        Velocity = input * Speed;
        if (input.LengthSquared() > 0.01f)
        {
            _facing = input.Normalized();
            var previousPosition = Position;
            MoveAndSlide();
            Position = new Vector2(
                Mathf.Clamp(Position.X, MovementBounds.Position.X, MovementBounds.End.X),
                Mathf.Clamp(Position.Y, MovementBounds.Position.Y, MovementBounds.End.Y));
            if (Position.DistanceSquaredTo(previousPosition) > 0.001f)
            {
                if (Session.State.DreamEscape is null ||
                    Session.State.Player.LocationId != TownRpg.Core.Domain.DreamEscapeIds.Location)
                {
                    Session.AdvanceMovingTime(delta);
                }
            }
            if (Session.State.DreamEscape is not null &&
                Session.State.Player.LocationId == TownRpg.Core.Domain.DreamEscapeIds.Location)
            {
                Session.State.DreamEscape.PositionX = Position.X;
                Session.State.DreamEscape.PositionY = Position.Y;
            }
            else
            {
                Session.State.Player.PositionX = Position.X;
                Session.State.Player.PositionY = Position.Y;
            }
            QueueRedraw();
        }
        UpdateAnimation(input);
    }

    public Vector2 Facing => _facing;

    public void ConfigureCharacterFrames(SpriteFrames frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        _animatedSprite?.QueueFree();
        _animatedSprite = new AnimatedSprite2D
        {
            Name = "CharacterSprite",
            SpriteFrames = frames,
            Animation = "down",
            Frame = 0,
            Position = new Vector2(0, 1),
            ZIndex = 1
        };
        AddChild(_animatedSprite);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_animatedSprite is not null) return;
        DrawRect(new Rect2(-5, -14, 10, 12), new Color("e6d5b8"));
        DrawRect(new Rect2(-7, -2, 14, 13), new Color("b6535d"));
        DrawRect(new Rect2(-6, 11, 5, 4), new Color("283247"));
        DrawRect(new Rect2(1, 11, 5, 4), new Color("283247"));
        DrawLine(Vector2.Zero, _facing * 10f, new Color(1f, 1f, 1f, 0.42f), 1f);
    }

    private void UpdateAnimation(Vector2 input)
    {
        if (_animatedSprite is null) return;
        if (input.LengthSquared() > 0.01f)
        {
            _facingAnimation = Math.Abs(input.X) > Math.Abs(input.Y)
                ? input.X < 0 ? "left" : "right"
                : input.Y < 0 ? "up" : "down";
            if (_animatedSprite.Animation != _facingAnimation)
            {
                _animatedSprite.Play(_facingAnimation);
            }
            else if (!_animatedSprite.IsPlaying())
            {
                _animatedSprite.Play();
            }
            return;
        }
        StopAnimation();
    }

    private void StopAnimation()
    {
        if (_animatedSprite is null) return;
        _animatedSprite.Animation = _facingAnimation;
        _animatedSprite.Stop();
        _animatedSprite.Frame = 0;
    }
}
