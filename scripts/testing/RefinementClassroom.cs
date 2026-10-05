using Godot;
using TownRpg.Core.Domain;
using TownRpg.Presentation;

namespace TownRpg.Scripts.Testing;

/// <summary>Local visual-state playground; no GameSession, quest commands or save access.</summary>
public partial class RefinementClassroom : WorldArea3D
{
    [Export] public bool Baseline { get; set; }
    private bool _paused;
    private int _clockIndex;
    public PlayerController3D PlayerActor => Player;
    public ThirdPersonCameraRig CameraController => CameraRig;
    public DreamClassroomVisual3D Visual => GetNode<DreamClassroomVisual3D>("ClassroomVisual");
    public string ClockState => new[]{"Missing","HandOnly","KeyOnly","Ready","Restored"}[_clockIndex];
    protected override bool InputBlocked => _paused;
    protected override bool IsMarkerActive(InteractionMarker3D marker) => false;
    public override void _Ready()
    {
        base._Ready();
        Visual.ApplyState(new DreamEscapeState());
        Player.ConfigureDefaultCharacterVisual(DreamCharacterFrames.Create(ResourceLoader.Load<Texture2D>(
            "res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png")));
        Player.GlobalPosition=new(0,0,2.8f);Player.SetFacingImmediate(Vector3.Forward);
        CameraRig.SetYawImmediate(0);CameraRig.SnapToTarget();
        if(!Baseline)RefinementArt.Classroom(this);
    }
    public void SetClockState(int index)
    {
        if(index is <0 or >4)throw new ArgumentOutOfRangeException(nameof(index));
        _clockIndex=index;
        foreach(var s in new[]{"Missing","HandOnly","KeyOnly","Ready","Restored"})
            ((Node3D)GetNode("ClassroomModel").FindChild("Clock_"+s,true,false)!).Visible=s==ClockState;
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if(e is not InputEventKey {Pressed:true,Echo:false} key)return;
        if(key.Keycode==Key.T)SetClockState((_clockIndex+1)%5);
        if(key.Keycode==Key.Escape)
        { _paused=!_paused;SetRuntimeInputLocked(_paused);Input.MouseMode=_paused?Input.MouseModeEnum.Visible:Input.MouseModeEnum.Captured; }
    }
}
