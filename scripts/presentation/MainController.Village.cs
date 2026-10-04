using Godot;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Presentation;

public partial class MainController
{
    private VillageAreaHost? _village;
    private VillageOpeningController? _opening;
    public VillageAreaHost? VillageRuntime => _village;

    public override void _Process(double delta)
    {
        _village?.SetHostLocked(_session.Modal.IsBlocked || _flowRunning || _quitConfirmation.Visible);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest && _quitConfirmation is not null) AskQuit();
    }

    private void DisconnectVillage()
    {
        if (_village is null) return;
        _village.CheckpointReached -= OnVillageCheckpoint;
        _village.SetHostLocked(true);
        _village.QueueFree();
        _village = null;
        _opening = null;
    }

    private void OnVillageCheckpoint() => SaveVillageCheckpoint();

    private bool SaveVillageCheckpoint()
    {
        if (_village is null) return true;
        if (_village.IsTransitioning) return false;
        try
        {
            var service = new VillageExplorationService(_session.State);
            service.Capture(_village.IsInterior ? VillageExplorationState.HomeLocation : VillageExplorationState.VillageLocation,
                _village.Capture());
            _session.SaveCheckpoint();
            return true;
        }
        catch (Exception exception)
        {
            ShowToast($"保存失败：{exception.Message}。请从暂停菜单重试保存。");
            return false;
        }
    }

    private async Task WakeAndExploreAsync()
    {
        if (_flowRunning) return;
        _flowRunning = true;
        using var lease = _session.Modal.Acquire("village-wake");
        var shade = new ColorRect { Name="WakeFade",Color=new(0,0,0,0),MouseFilter=Control.MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);_uiRoot.AddChild(shade);
        try
        {
            var fade = CreateTween();fade.TweenProperty(shade,"color:a",1f,.35);
            await ToSignal(fade,Tween.SignalName.Finished);
            if (!IsInsideTree()) return;
            var service = new VillageExplorationService(_session.State);
            service.BeginWake();
            // Commit the recoverable wake boundary before unloading the dream.
            try { _session.SaveCheckpoint(); }
            catch (Exception exception) { ShowToast($"保存失败：{exception.Message}。稍后可在暂停菜单重试。"); }
            var previousDream=_dream;var previousWorld=_world;
            DisconnectDream();DisconnectWorld();DisconnectVillage();CloseCompletion();
            previousDream?.QueueFree();previousWorld?.QueueFree();
            _hud.Visible=false;
            var runtime=new VillageAreaHost { Name="VillageExploration",EmbeddedInGame=true };
            _village=runtime;_worldHost.AddChild(runtime);
            runtime.ApplySettings(_session.State.Settings);runtime.SetGamepadPrompts(_usingGamepad);
            await runtime.RestoreAsync(_session.State.Player.LocationId,_session.State.VillageExploration!);
            if (!IsInsideTree() || _village != runtime) return;
            runtime.CheckpointReached+=OnVillageCheckpoint;
            fade=CreateTween();fade.TweenProperty(shade,"color:a",0f,.45);
            await ToSignal(fade,Tween.SignalName.Finished);
            shade.MouseFilter=Control.MouseFilterEnum.Ignore;
            if (!_session.State.VillageExploration!.WakeIntroductionComplete)
            {
                var pin=_session.State.DreamEscape!.Status==DreamEscapeStatus.CompletedPin;
                await Line(pin ? "wake.pin" : "wake.escape", "", UiText.Get(pin ? "VILLAGE_WAKE_PIN" : "VILLAGE_WAKE_ESCAPE"));
                await Line("wake.saturday", "", UiText.Get("VILLAGE_WAKE_SATURDAY"));
                service.CompleteIntroduction();
            }
            _opening = new VillageOpeningController { Name = "VillageOpening", Host = runtime };
            runtime.AddChild(_opening);
            SaveVillageCheckpoint();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            ShowCompletionLayout("WakeRecovery","wake-recovery","暂时无法进入村庄",$"{exception.Message}\n返回标题后可继续最近的存档。");
        }
        finally
        {
            _dialogue.CloseDialogue();
            if (GodotObject.IsInstanceValid(shade)) shade.QueueFree();
            _flowRunning=false;
        }
    }
}
