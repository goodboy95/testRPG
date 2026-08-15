using Godot;
using TownRpg.Application;

namespace TownRpg.Presentation;

public partial class PlayerController : CharacterBody2D
{
    [Export] public float Speed { get; set; } = 88f;
    [Export] public Rect2 MovementBounds { get; set; } = new(18, 40, 604, 292);

    private GameSession Session => GetNode<GameSession>("/root/GameSession");
    private Vector2 _facing = Vector2.Down;

    public override void _Ready()
    {
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Session.Modal.IsBlocked)
        {
            Velocity = Vector2.Zero;
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
                Session.AdvanceMovingTime(delta);
            }
            Session.State.Player.PositionX = Position.X;
            Session.State.Player.PositionY = Position.Y;
            QueueRedraw();
        }
    }

    public Vector2 Facing => _facing;

    public override void _Draw()
    {
        DrawRect(new Rect2(-5, -14, 10, 12), new Color("e6d5b8"));
        DrawRect(new Rect2(-7, -2, 14, 13), new Color("b6535d"));
        DrawRect(new Rect2(-6, 11, 5, 4), new Color("283247"));
        DrawRect(new Rect2(1, 11, 5, 4), new Color("283247"));
        DrawLine(Vector2.Zero, _facing * 10f, new Color(1f, 1f, 1f, 0.42f), 1f);
    }
}
