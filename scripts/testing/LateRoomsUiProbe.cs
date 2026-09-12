using Godot;
using System.Text.Json;
using TownRpg.Application;
using TownRpg.Core.Domain;
using TownRpg.Presentation;

namespace TownRpg.Testing;

// Render-only fixtures, deliberately separate from the input-earned prologue route.
public partial class LateRoomsUiProbe : Node
{
    private GameSession _session=null!;
    private Control _ui=null!;
    private string _output="";
    private int _captures;
    private readonly List<object> _layouts=[];
    private async Task Frames(int n=8){for(var i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    public override void _Ready()=>CallDeferred(MethodName.Run);
    private async Task Capture(string name,int width,float scale)
    {
        await Frames(12);
        foreach(var frame in UiUxCapture.Find<PanelContainer>(_ui).Where(c=>c.IsVisibleInTree()&&c.Name=="WorkspaceFrame"))
        {var r=frame.GetGlobalRect();Require(r.Position.X>=-.1f&&r.Position.Y>=-.1f&&r.End.X<=640.1f&&r.End.Y<=360.1f,"Workspace overflow: "+name);}
        foreach(var button in UiUxCapture.Find<Button>(_ui).Where(c=>c.IsVisibleInTree()&&(c.Name=="MechanismConfirm"||c.Name=="PuzzleConfirm")))
        {var r=button.GetGlobalRect();Require(r.Position.X>=0&&r.Position.Y>=0&&r.End.X<=640.1f&&r.End.Y<=360.1f,"Unreachable confirm: "+name);}
        foreach(var subtitle in UiUxCapture.Find<SubtitleView>(_ui).Where(c=>c.IsVisibleInTree()))
        {var text=subtitle.TextControl;Require(text.GetLineCount()<=2&&text.GetContentHeight()<=text.Size.Y+1,"Subtitle clips: "+name);Require(subtitle.GetGlobalRect().End.Y<=326.1f,"Subtitle safe area: "+name);}
        _layouts.Add(new{name,width,scale,subtitle=_session.State.Settings.EffectiveSubtitleFontScale,controls=UiUxCapture.Find<Control>(_ui).Where(c=>c.IsVisibleInTree()&&c is Label or RichTextLabel or Button or PanelContainer).Select(c=>new{path=c.GetPath().ToString(),rect=c.GetGlobalRect().ToString(),minimum=c.GetCombinedMinimumSize().ToString()}).ToArray()});
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();
        Require(image.GetWidth()==width&&image.GetHeight()==width*9/16,"Wrong screenshot resolution");
        var directory=Path.Combine(_output,$"{width}-{scale:0.00}");Directory.CreateDirectory(directory);
        Require(image.SavePng(Path.Combine(directory,name+".png"))==Error.Ok,"PNG save failed");_captures++;
    }
    private async void Run()
    {
        try
        {
            _output=ProjectSettings.GlobalizePath("res://outputs/late-rooms-v2/ui");Directory.CreateDirectory(_output);
            _session=GetNode<GameSession>("/root/GameSession");using var suppress=_session.SuppressPersistence();
            foreach(var width in new[]{1280,1600,1920})foreach(var scale in new[]{1f,1.25f,1.5f,2f})
            {
                _session.NewGame("小岚","同学");var settings=_session.State.Settings;settings.FontScale=scale;settings.SubtitleFontScale=scale;settings.WindowWidth=width;settings.WindowHeight=width*9/16;settings.BorderlessFullscreen=width==1920;settings.ReduceFlashing=true;DisplaySettingsController.Apply(settings);
                var state=_session.State.DreamEscape!;state.XiaoyuJoined=true;state.DiscoveredClues.UnionWith(DreamEscapeIds.Clues);
                foreach(var room in new[]{"archive","auditorium"})
                {
                    var id="dream."+room;state.UnlockedAreas.Add(id);var entry=room=="archive"?ArchiveRoomLayout3D.Mapper.ToStored(ArchiveRoomLayout3D.Entry):AuditoriumLayout3D.Mapper.ToStored(AuditoriumLayout3D.Entry);_session.MoveDreamToArea(id,entry.X,entry.Y);
                    var scene=ResourceLoader.Load<PackedScene>(room=="archive"?"res://scenes/dream/DreamArchiveRoom.tscn":"res://scenes/dream/DreamAuditorium.tscn").Instantiate<WorldArea3D>();AddChild(scene);await Frames(10);
                    var layer=new CanvasLayer{Layer=20};AddChild(layer);_ui=new Control{Theme=UiStyles.CreateTheme(scale)};layer.AddChild(_ui);_ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);UiStyles.ApplyScale(_ui,scale);
                    var mechanism=new DreamMechanismPanel();_ui.AddChild(mechanism);var puzzle=new DreamPuzzlePanel();_ui.AddChild(puzzle);var reading=new ReadingPanel();_ui.AddChild(reading);var dialogue=new DialoguePanel();_ui.AddChild(dialogue);
                    var sequences=room=="archive"?new[]{(DreamEscapeIds.PuzzleArchiveRetrieval,"retrieval",new[]{"A4","B3","C1","D2"}),(DreamEscapeIds.PuzzleArchiveWorkTimeline,"timeline",new[]{"open","calibrate","score","backstage"})}:new[]{(DreamEscapeIds.PuzzleAuditoriumTapeReconstruction,"tape",new[]{"classroom","music","archive"}),(DreamEscapeIds.PuzzleAuditoriumHandoffPlan,"handoff",new[]{"position","monitor","lights","release"})};
                    foreach(var (puzzleId,name,tokens) in sequences)
                    {puzzle.Open(puzzleId,DreamPrologueText.Puzzle(puzzleId),DreamPrologueText.Prompt(puzzleId),tokens.Select(t=>(t,t.Length==2?t:DreamPrologueText.Get(t switch{"classroom"=>"DREAM_V6_UI_ESCAPECONTROLLER_035","music"=>"DREAM_V6_UI_ESCAPECONTROLLER_036","archive"=>"DREAM_V6_UI_ESCAPECONTROLLER_037","open"=>"DREAM_V6_UI_ESCAPECONTROLLER_022","calibrate"=>"DREAM_V6_UI_ESCAPECONTROLLER_023","score"=>"DREAM_V6_UI_ESCAPECONTROLLER_024","backstage"=>"DREAM_V6_UI_ESCAPECONTROLLER_025","position"=>"DREAM_V6_UI_ESCAPECONTROLLER_027","monitor"=>"DREAM_V6_UI_ESCAPECONTROLLER_029","lights"=>"DREAM_V6_UI_ESCAPECONTROLLER_026","release"=>"DREAM_V6_UI_ESCAPECONTROLLER_028",_=>t}))).ToArray(),tokens.Length);await Capture(room+"-"+name,width,scale);puzzle.ClosePanel();}
                    if(room=="auditorium")
                    {mechanism.Open(DreamEscapeIds.PuzzleAuditoriumStageAlignment,DreamMechanismMode.StageAlignment);await Capture("auditorium-stage-front",width,scale);UiUxCapture.Find<Button>(mechanism).Single(b=>b.Text==UiText.Get("UI_SIDE_VIEW")).EmitSignal(BaseButton.SignalName.Pressed);await Capture("auditorium-stage-side",width,scale);mechanism.ClosePanel();}
                    var clue=room=="archive"?DreamEscapeIds.ClueArchiveFuseRoute:DreamEscapeIds.ClueTapeSeams;reading.Open(DreamInventoryPanel.ClueTitle(clue),DreamInventoryPanel.ClueText(clue),DreamInventoryPanel.ClueImage(clue));await Capture(room+"-reading",width,scale);reading.ClosePanel();
                    using(var lease=_session.Modal.Acquire("late-room-subtitle-fixture"))
                    {
                        foreach(var subtitleScale in new[]{scale,scale==2f?1f:2f}.Distinct())
                        {settings.SubtitleFontScale=subtitleScale;var line=dialogue.ShowLineAsync("ui.late-room.fixture","林晓雨","这些留下来的痕迹，也许能让我们想起那一天发生的事情。先把眼前能够确认的部分记下来吧。");await Capture(room+$"-subtitle-{subtitleScale:0.00}",width,scale);dialogue.CloseDialogue();try{await line;}catch(TaskCanceledException){}}
                    }
                    settings.SubtitleFontScale=scale;layer.QueueFree();scene.QueueFree();await Frames(12);
                }
            }
            File.WriteAllText(Path.Combine(_output,"layout.json"),JsonSerializer.Serialize(_layouts,new JsonSerializerOptions{WriteIndented=true}));GD.Print($"LATE_ROOMS_UI_PASS captures={_captures} resolutions=3 menu_scales=4 independent_subtitles workspace_confirm_bounds");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
