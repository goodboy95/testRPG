using Godot;
using TownRpg.Application;

namespace TownRpg.Presentation;

public partial class DreamCompanion3D : Node3D
{
    [Export] public bool ShowBeforeJoin { get; set; }
    [Export] public Vector3 PreJoinPosition { get; set; }
    [Export] public float FollowDistance { get; set; } = 1.2f;
    [Export] public float FollowSpeed { get; set; } = 3.4f;
    [Export] public float WarpDistance { get; set; } = 4.8f;

    private GameSession _session = null!;
    private PlayerController3D _player = null!;
    private AnimatedSprite3D _sprite = null!;
    private bool _wasJoined;
    private bool _heldPose;
    private Vector3 _heldPosition;
    private double _scriptedPoseRemaining;

    public bool IsFollowing => _session.State.DreamEscape?.XiaoyuJoined == true;
    public AnimatedSprite3D Sprite => _sprite;

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        _player = GetNode<PlayerController3D>("../Player");
        _sprite = new AnimatedSprite3D
        {
            Name = "XiaoyuBillboard",
            PixelSize = 0.0085f,
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            DoubleSided = true,
            Position = new Vector3(0f, 0.82f, 0f)
        };
        var texture = ResourceLoader.Load<Texture2D>(
            "res://assets/generated/dream_escape/pixel/lin-xiaoyu-walk-4x4-v2.png");
        if (texture is not null)
        {
            _sprite.SpriteFrames = DreamCharacterFrames.Create(texture);
            _sprite.Animation = "down";
            _sprite.Play();
        }
        AddChild(_sprite);
        RefreshImmediate();
    }

    public override void _Process(double delta)
    {
        var joined = IsFollowing;
        if (!joined)
        {
            Visible = ShowBeforeJoin;
            GlobalPosition = PreJoinPosition;
            _wasJoined = false;
            return;
        }

        Visible = true;
        if (_heldPose || _scriptedPoseRemaining > 0d)
        {
            GlobalPosition = _heldPosition;
            _scriptedPoseRemaining = Math.Max(0d, _scriptedPoseRemaining - delta);
            if (_sprite.SpriteFrames is not null)
            {
                _sprite.Animation = "up";
                _sprite.Pause();
                _sprite.Frame = 1;
            }
            return;
        }
        var desired = DesiredFollowPosition();
        if (!_wasJoined || GlobalPosition.DistanceTo(desired) > WarpDistance)
            GlobalPosition = desired;
        else
            GlobalPosition = GlobalPosition.MoveToward(desired, FollowSpeed * (float)delta);
        _wasJoined = true;
        UpdateAnimation(desired - GlobalPosition);
    }

    public void RefreshImmediate()
    {
        _wasJoined = IsFollowing;
        Visible = _wasJoined || ShowBeforeJoin;
        GlobalPosition = _heldPose
            ? _heldPosition
            : _wasJoined ? DesiredFollowPosition() : PreJoinPosition;
    }

    public void SetHeldPose(bool active, Vector3 worldPosition)
    {
        _heldPose = active;
        _heldPosition = worldPosition;
        if (active)
        {
            _scriptedPoseRemaining = 0d;
            GlobalPosition = worldPosition;
        }
        else
        {
            RefreshImmediate();
        }
    }

    public void PlayScriptedPose(Vector3 worldPosition, double durationSeconds)
    {
        if (_heldPose || !IsFollowing) return;
        _heldPosition = worldPosition;
        _scriptedPoseRemaining = Math.Max(0d, durationSeconds);
        GlobalPosition = worldPosition;
    }

    private Vector3 DesiredFollowPosition()
    {
        var facing = _player.FacingWorld;
        if (facing.LengthSquared() <= 0.0001f) facing = Vector3.Forward;
        var side = Vector3.Up.Cross(facing).Normalized() * 0.28f;
        var desired = _player.GlobalPosition - facing.Normalized() * FollowDistance + side;
        desired.Y = 0f;
        return desired;
    }

    private void UpdateAnimation(Vector3 movement)
    {
        if (_sprite.SpriteFrames is null) return;
        if (movement.LengthSquared() <= 0.0004f)
        {
            _sprite.Pause();
            _sprite.Frame = 0;
            return;
        }

        var animation = Math.Abs(movement.X) > Math.Abs(movement.Z)
            ? movement.X >= 0f ? "right" : "left"
            : movement.Z >= 0f ? "down" : "up";
        if (_sprite.Animation != animation) _sprite.Animation = animation;
        _sprite.Play();
    }
}
