using System.Text.Json;
using Godot;
using TownRpg.Presentation;

namespace TownRpg.Testing;

public partial class VillageV1Probe
{
    private bool TravelCheck => OS.GetCmdlineUserArgs().Contains("--village-travel-check");
    private bool TravelVideo => OS.GetCmdlineUserArgs().Contains("--village-travel-video");
    private bool TravelVerificationOnly => OS.GetCmdlineUserArgs().Contains("--village-travel-verify");
    private readonly string _travelRun=DateTime.Now.ToString("yyyyMMdd-HHmmss");
    private string TravelOutput => ProjectSettings.GlobalizePath($"res://outputs/village-travel/{_travelRun}-{(TravelVideo?"video":UseV1?"v1":"v2")}");

    private async Task CompareTravel()
    {
        // Same v2 geometry, input steering and physics rate. Override exists only inside this probe.
        Engine.TimeScale=1;
        var defaultSpeed=_village.ActivePlayer.Speed;
        var comparisons=new List<object>();
        try
        {
            foreach(var route in VillageLayout3D.Layout.Routes)
            {
                _village.ActivePlayer.Speed=3.1f;
                var before=await WalkRoute(route);
                _village.ActivePlayer.Speed=defaultSpeed;
                var after=await WalkRoute(route);
                var reduction=1-after/before;
                comparisons.Add(new { route=route.Id,lengthMetres=VillageLayout3D.RouteLength(route),
                    baselineSpeed=3.1f,candidateSpeed=defaultSpeed,beforeSeconds=before,afterSeconds=after,reduction });
                Require(after<before,"travel improves "+route.Id);
                if(route.Id=="home_shop") Require(reduction>=.20,"home to shop improves at least 20 percent");
                GD.Print($"VILLAGE_TRAVEL_COMPARE {route.Id} before={before:F2} after={after:F2} reduction={reduction:P1}");
            }
            File.WriteAllText(Path.Combine(Output,"comparison.json"),JsonSerializer.Serialize(comparisons,new JsonSerializerOptions { WriteIndented=true }));
        }
        finally { ReleaseMovement();_village.ActivePlayer.Speed=defaultSpeed; }
    }

    private async Task TravelStops()
    {
        foreach(var id in new[]{"VillageDesk","DeliveryTable","Handcart","ShopPostRight","WorkTable","WorkshopPostEast"})
        {
            var prop=VillageExteriorLayout3D.Layout.Props.Single(p=>p.Id==id);
            // The plaza tree occupies the cart's south approach; use its unobstructed north side.
            var side=id=="Handcart" ? -1f : 1f;
            var front=prop.Position[2]+side*prop.Size[2]/2;
            await Place(prop.Position[0],front+side*2);
            _village.ActiveCamera.SetYawImmediate(0);_village.ActivePlayer.SetFacingImmediate(side>0?Vector3.Forward:Vector3.Back);
            Input.ActionPress(side>0?"move_up":"move_down");await Frames(60);ReleaseMovement();await Frames(8);
            var player=_village.ActivePlayer;
            var clearance=side*(player.GlobalPosition.Z-front);
            Require(clearance is >=.24f and <.5f,$"run stops at fixture {id}: clearance={clearance:F3}, position={player.GlobalPosition}");
            Require(new Vector2(player.Velocity.X,player.Velocity.Z).Length()<.01f,"no residual motion at fixture "+id);
            var stopped=player.GlobalPosition;
            Input.ActionPress(side>0?"move_down":"move_up");await Frames(40);ReleaseMovement();await Frames(8);
            Require(player.GlobalPosition.DistanceTo(stopped)>.6f,"can retreat from fixture "+id);
        }
        await Place(-70,55);_village.ActiveCamera.SetYawImmediate(0);_village.ActivePlayer.SetFacingImmediate(Vector3.Right);
        Input.ActionPress("move_right");await Frames(45);ReleaseMovement();
        var brakeStart=_village.ActivePlayer.GlobalPosition;await Frames(12);
        Require(_village.ActivePlayer.GlobalPosition.DistanceTo(brakeStart)<.3f,"outdoor release brakes within 30 cm");
        var slowStart=_village.ActivePlayer.GlobalPosition;
        Input.ActionPress("move_slow");Input.ActionPress("move_right");await Frames(30);ReleaseMovement();await Frames(8);
        Require(_village.ActivePlayer.GlobalPosition.DistanceTo(slowStart) is >.5f and <.9f,"slow positioning remains precise");
    }

    private async Task CaptureTravelMotion()
    {
        // Run with --fixed-fps 30; each image is one rendered frame, not a real-time performance sample.
        Engine.TimeScale=1;
        foreach(var size in new[]{new Vector2I(1280,720),new Vector2I(1600,900),new Vector2I(1920,1080)})
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless,true);
            GetWindow().Size=size;
            if(size.X==1920) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            await Frames(20);
            await Place(-78,55);_village.ActiveCamera.SetYawImmediate(0);_village.ActiveCamera.SnapToTarget();
            _village.ActivePlayer.SetFacingImmediate(Vector3.Right);
            var folder=Path.Combine(Output,size.X.ToString());Directory.CreateDirectory(folder);
            var samples=new List<object>();
            for(var frame=0;frame<240;frame++)
            {
                ReleaseMovement();
                if(frame<90) Input.ActionPress("move_right");
                else if(frame<120) Input.ActionPress("move_up");
                else if(frame>=135 && frame<165) { Input.ActionPress("move_slow");Input.ActionPress("move_right"); }
                else if(frame>=180 && frame<210) Input.ActionPress("move_down");
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var image=GetViewport().GetTexture().GetImage();
                if(frame==0) Require(image.GetWidth()==size.X && image.GetHeight()==size.Y,"motion capture resolution "+size);
                Require(image.SaveJpg(Path.Combine(folder,$"{frame:D4}.jpg"),.92f)==Error.Ok,"motion frame saved");
                var p=_village.ActivePlayer;
                samples.Add(new {frame,x=p.GlobalPosition.X,y=p.GlobalPosition.Y,z=p.GlobalPosition.Z,
                    speed=new Vector2(p.Velocity.X,p.Velocity.Z).Length(),animation=p.AnimationState,phase=p.GaitPhase});
            }
            ReleaseMovement();
            File.WriteAllText(Path.Combine(folder,"motion.json"),JsonSerializer.Serialize(new {fps=30,frames=240,width=size.X,height=size.Y,
                note="Fixed simulation timestep; local character footage, not a performance or human acceptance result.",samples},new JsonSerializerOptions { WriteIndented=true }));
        }
    }
}
