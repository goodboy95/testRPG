using Godot;

namespace TownRpg.Presentation;

// Each instance owns its animation graph and phase. Imported shared resources stay unchanged.
public sealed class CharacterAnimationDriver
{
    private readonly AnimationPlayer _player;
    private readonly AnimationTree? _tree;
    private readonly AnimationNodeAnimation? _turnNode;
    private readonly AnimationNodeAnimation? _actionNode;
    private readonly float _idleOffset;
    private float _idleTime;
    private float _actionTime;
    private float _actionBlend;
    private string? _action;
    private bool _actionHeld;
    private float _phase;
    private float _moveBlend;
    private float _turnBlend;
    private string _current = string.Empty;
    public bool Enhanced => _tree is not null;
    public string State { get; private set; } = "idle";
    public float Phase => _phase;

    public CharacterAnimationDriver(Node3D model, AnimationPlayer player, float idleOffset = 0f)
    {
        _player = player;
        _idleOffset = idleOffset;
        var names = new[] { "idle", "walk", "jog", "turn_left_90", "turn_right_90", "turn_left_180", "turn_right_180" };
        // Duplicate the library before setting loops: models can coexist during transitions.
        foreach (var libraryName in player.GetAnimationLibraryList())
        {
            var copy = (AnimationLibrary)player.GetAnimationLibrary(libraryName).Duplicate(true);
            player.RemoveAnimationLibrary(libraryName);
            player.AddAnimationLibrary(libraryName, copy);
        }
        foreach (var name in names.Where(name => player.HasAnimation(name)))
            player.GetAnimation(name).LoopMode = name.StartsWith("turn_", StringComparison.Ordinal)
                ? Animation.LoopModeEnum.None : Animation.LoopModeEnum.Linear;
        foreach (var name in new[] { "observe", "pickup", "operate", "talk", "hold" }.Where(name => player.HasAnimation(name)))
            player.GetAnimation(name).LoopMode = Animation.LoopModeEnum.None;
        if (!names.All(name => player.HasAnimation(name) && player.GetAnimation(name).Length > 0))
        {
            Reset();
            return;
        }

        var graph = new AnimationNodeBlendTree();
        foreach (var name in new[] { "idle", "walk", "jog" })
            graph.AddNode(name, new AnimationNodeAnimation { Animation = name });
        graph.AddNode("idle_seek", new AnimationNodeTimeSeek());
        graph.ConnectNode("idle_seek", 0, "idle");
        graph.AddNode("walk_seek", new AnimationNodeTimeSeek());
        graph.AddNode("jog_seek", new AnimationNodeTimeSeek());
        graph.ConnectNode("walk_seek", 0, "walk");
        graph.ConnectNode("jog_seek", 0, "jog");
        graph.AddNode("gait", new AnimationNodeBlend2 { Sync = true });
        graph.ConnectNode("gait", 0, "walk_seek");
        graph.ConnectNode("gait", 1, "jog_seek");
        graph.AddNode("moving", new AnimationNodeBlend2 { Sync = true });
        graph.ConnectNode("moving", 0, "idle_seek");
        graph.ConnectNode("moving", 1, "gait");
        _turnNode = new AnimationNodeAnimation { Animation = "turn_left_90" };
        graph.AddNode("turn", _turnNode);
        graph.AddNode("turn_seek", new AnimationNodeTimeSeek());
        graph.ConnectNode("turn_seek", 0, "turn");
        graph.AddNode("turning", new AnimationNodeBlend2 { Sync = true });
        graph.ConnectNode("turning", 0, "moving");
        graph.ConnectNode("turning", 1, "turn_seek");
        _actionNode = new AnimationNodeAnimation { Animation = "idle" };
        graph.AddNode("action", _actionNode);
        graph.AddNode("action_seek", new AnimationNodeTimeSeek());
        graph.ConnectNode("action_seek", 0, "action");
        graph.AddNode("acting", new AnimationNodeBlend2 { Sync = true });
        graph.ConnectNode("acting", 0, "turning");
        graph.ConnectNode("acting", 1, "action_seek");
        graph.ConnectNode("output", 0, "acting");
        _tree = new AnimationTree
        {
            Name = "LocomotionTree",
            TreeRoot = graph,
            CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual
        };
        model.AddChild(_tree);
        _tree.AnimPlayer = _tree.GetPathTo(player);
        _tree.Active = true;
        Reset();
    }

    public void Update(CharacterLocomotion motion, float delta)
    {
        delta = Mathf.Clamp(delta, 0, .1f);
        var speed = motion.ActualSpeed;
        if (motion.Moving || motion.IsTurning) ClearAction();
        if (_action is not null)
        {
            _actionTime += delta;
            if (!_actionHeld && _actionTime >= _player.GetAnimation(_action).Length) ClearAction();
        }
        State = motion.IsTurning ? motion.TurnAnimation : !motion.Moving ? "idle" : speed < 2f ? "walk" : "jog";
        if (_action is not null) State = _action;
        if (_tree is null)
        {
            var clip = State.StartsWith("turn_", StringComparison.Ordinal) ? "idle" : State;
            if (_current != clip) { _player.Play(clip, .12); _current = clip; }
            _player.SpeedScale = clip is "walk" or "jog" ? Mathf.Clamp(speed / (clip == "walk" ? 1.4f : 3.1f), .25f, 1.4f) : 1f;
            return;
        }
        var gait = Mathf.Clamp((speed - CharacterLocomotion.WalkSpeed) /
            (CharacterLocomotion.JogSpeed - CharacterLocomotion.WalkSpeed), 0f, 1f);
        var stride = Mathf.Lerp(1.4f * _player.GetAnimation("walk").Length,
            3.1f * _player.GetAnimation("jog").Length, gait);
        if (motion.Moving && !motion.IsTurning) _phase = (_phase + speed / (float)stride * delta) % 1f;
        _moveBlend = Mathf.MoveToward(_moveBlend, motion.Moving ? Mathf.Clamp(speed / .45f, 0, 1) : 0f, delta / .14f);
        _turnBlend = Mathf.MoveToward(_turnBlend, motion.IsTurning ? 1f : 0f, delta / .06f);
        if (motion.IsTurning)
        {
            _turnNode!.Animation = motion.TurnAnimation;
            _tree.Set("parameters/turn_seek/seek_request", motion.TurnProgress * _player.GetAnimation(motion.TurnAnimation).Length);
        }
        _tree.Set("parameters/walk_seek/seek_request", _phase * _player.GetAnimation("walk").Length);
        _tree.Set("parameters/jog_seek/seek_request", _phase * _player.GetAnimation("jog").Length);
        _tree.Set("parameters/gait/blend_amount", gait);
        _tree.Set("parameters/moving/blend_amount", _moveBlend);
        _tree.Set("parameters/turning/blend_amount", _turnBlend);
        _idleTime += delta;
        _tree.Set("parameters/idle_seek/seek_request", (_idleTime + _idleOffset) % _player.GetAnimation("idle").Length);
        _actionBlend = Mathf.MoveToward(_actionBlend, _action is null ? 0f : 1f, delta / .18f);
        if (_action is not null)
        {
            _actionNode!.Animation = _action;
            var length = (float)_player.GetAnimation(_action).Length;
            var time = Math.Min(_actionTime, length);
            if (_actionHeld && _action is "hold" or "observe" && _actionTime > length * .4f)
            {
                // Smoothly revisit the supported portion, retaining baked breath/cloth motion.
                var wave = .5f - .5f * Mathf.Cos((_actionTime - length * .4f) * Mathf.Tau / 4f);
                time = length * (.4f + .6f * wave);
            }
            _tree.Set("parameters/action_seek/seek_request", time);
        }
        _tree.Set("parameters/acting/blend_amount", _actionBlend);
        _tree.Advance(delta);
    }

    public void SetAction(string name, bool held = false)
    {
        if (!_player.HasAnimation(name) || _player.GetAnimation(name).Length <= 0) { ClearAction(); return; }
        if (_action == name && _actionHeld == held) return;
        _action = name; _actionHeld = held; _actionTime = 0;
    }

    public void ClearAction() => _action = null;

    public void Reset()
    {
        State = "idle";
        ClearAction();
        _idleTime = _actionTime = _actionBlend = 0;
        _phase = _moveBlend = _turnBlend = 0f;
        if (_tree is not null)
        {
            _tree.Set("parameters/moving/blend_amount", 0f);
            _tree.Set("parameters/turning/blend_amount", 0f);
            _tree.Set("parameters/acting/blend_amount", 0f);
            _tree.Set("parameters/idle_seek/seek_request", _idleOffset % _player.GetAnimation("idle").Length);
            _tree.Set("parameters/walk_seek/seek_request", 0f);
            _tree.Set("parameters/jog_seek/seek_request", 0f);
            _tree.Advance(0);
        }
        else
        {
            _player.SpeedScale = 1f;
            _player.Play("idle", .08);
            _current = "idle";
        }
    }
}
