using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TownRpg.Presentation;

/// <summary>Independent, read-only exterior. Geometry and collisions share VillageLayout3D.</summary>
public partial class VillageExterior3D : WorldArea3D
{
    public event Action? HomeRequested;
    public bool Graybox { get; init; }
    public bool UseV1 { get; init; }
    public bool UseCompactLayout { get; init; }
    public VillageLayoutProfile3D Layout { get; private set; }=VillageLayoutProfile3D.Legacy;
    public bool UseTerrainSample { get; init; }
    public bool UseArtSample { get; init; }
    public bool UseHouseArtSample { get; init; }
    public bool UseHomeTreesSample { get; init; }
    public IVillageSurface3D Surface { get; private set; } = new OriginalVillageSurface3D();
    public float WalkHeight(float x,float z) => Layout.Geometry.Bridges.FirstOrDefault(b=>b.Contains(new(x,z))) is { } bridge ? bridge.DeckY : Surface.GroundHeight(x,z);
    public Vector3 SurfacePoint(float x,float z,float offset=0)=>new(x,WalkHeight(x,z)+offset,z);
    public PlayerController3D PlayerActor => Player;
    public ThirdPersonCameraRig CameraController => CameraRig;
    public Node3D ModelRoot { get; private set; } = null!;
    private bool _hostLocked;
    private StaticBody3D _homeSolid = null!;
    protected override bool InputBlocked => _hostLocked;
    public Vector3 HomeEntry
    {
        get
        {
            var entry=HomeLayout3D.Layout.Doors.Single(d=>d.Id=="entry");
            var p=Layout.Home.ToWorld(new(entry.Position[0],entry.Position[2]));
            return new(p.X,Layout.Home.Elevation+entry.Position[1],p.Y);
        }
    }
    public Vector3 HomeForward => Vector3.Back.Rotated(Vector3.Up,Mathf.DegToRad(Layout.Home.Yaw));
    public Vector3 HomeReturnPoint => HomeEntry+HomeForward*1.45f+Vector3.Up*.04f;

    public override void _Ready()
    {
        if(UseCompactLayout) Layout=VillageLayoutProfile3D.Parse(Godot.FileAccess.GetFileAsString("res://assets/generated/village/layout-v4.json"));
        var stage=Time.GetTicksUsec();
        ModelRoot=Graybox ? new Node3D { Name="VillageGraybox" } : ResourceLoader.Load<PackedScene>(UseV1
            ? "res://assets/generated/village/3d/village-v1.glb" : UseHomeTreesSample && UseCompactLayout ? "res://assets/generated/village/3d/village-v6.glb" : UseHouseArtSample && UseCompactLayout ? "res://assets/generated/village/3d/village-v5.glb" : UseCompactLayout ? "res://assets/generated/village/3d/village-v4.glb" : UseArtSample ? "res://assets/generated/village/3d/village-v3.glb" : "res://assets/generated/village/3d/village-v2.glb").Instantiate<Node3D>();
        AddChild(ModelRoot);
        if(OS.GetCmdlineUserArgs().Contains("--v4-load")) GD.Print("V4_LOAD_STAGE model="+(Time.GetTicksUsec()-stage)/1000d);
        stage=Time.GetTicksUsec();
        foreach(var mesh in Descendants(ModelRoot).OfType<MeshInstance3D>())
        {
            var name=mesh.Name.ToString();
            if(name is "Terrain" or "StreamWater" or "VillageSquare" or "ScenicMountainTerrain" || name.StartsWith("Road",StringComparison.Ordinal))
                mesh.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
            if(UseArtSample || UseCompactLayout)
                for(var surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++)
                    if(mesh.GetActiveMaterial(surface) is StandardMaterial3D material && material.ResourceName=="V3_Leaves")
                    {
                        // MASK is explicit in the GLB; enforce the same editor/runtime foliage contract.
                        material.Transparency=BaseMaterial3D.TransparencyEnum.AlphaScissor;
                        material.AlphaScissorThreshold=.45f;
                        material.CullMode=BaseMaterial3D.CullModeEnum.Disabled;
                        material.BacklightEnabled=true;
                        material.Backlight=new Color(.12f,.17f,.06f);
                        material.TextureFilter=BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
                    }
        }
        if(UseTerrainSample)
        {
            foreach(var mesh in Descendants(ModelRoot).OfType<MeshInstance3D>().Where(m=>m.Name=="Terrain")) mesh.Visible=false;
            Surface=new TerrainVillageSurface3D(this,UseCompactLayout ? "res://assets/generated/village/terrain-v4/" : TerrainVillageSurface3D.Folder);
            VillageTerrainDetails3D.AddTo(this,Surface,UseCompactLayout ? "res://assets/generated/village/terrain-v4/" : TerrainVillageSurface3D.Folder);
        }
        else { Surface=new OriginalVillageSurface3D(Layout);BuildTerrain(); }
        if(OS.GetCmdlineUserArgs().Contains("--v4-load")) GD.Print("V4_LOAD_STAGE ground="+(Time.GetTicksUsec()-stage)/1000d);
        stage=Time.GetTicksUsec();
        BuildBuildings(); BuildBridgesAndBanks(); BuildBoundaries(); BuildLandmarks();
        if(!UseV1) BuildExteriorBatch();
        BuildActor(); BuildLighting();
        var markers=new Node3D { Name="InteractionMarkers" };AddChild(markers);
        markers.AddChild(new InteractionMarker3D { Name="HomeEntrance", TargetId="village.home.enter", Prompt="VILLAGE_ENTER_HOME", Position=HomeEntry, InteractionRange=1.65f });
        base._Ready();
        Player.ConfigureDefaultCharacterVisual(DreamCharacterFrames.Create(ResourceLoader.Load<Texture2D>("res://assets/generated/dream_escape/pixel/protagonist-walk-4x4-v1.png")));
        ResetPosition();
        if(OS.GetCmdlineUserArgs().Contains("--v4-load")) GD.Print("V4_LOAD_STAGE fixtures_actor="+(Time.GetTicksUsec()-stage)/1000d);
        InteractionRequested += HandleInteraction;
    }
    public override void _ExitTree() { base._ExitTree(); }
    public void SetSurfaceActive(bool value)=>Surface.SetActive(value);
    private void HandleInteraction(string id) { if(id=="village.home.enter") TryEnterHome(); }
    public bool TryEnterHome()
    {
        if(_hostLocked || !CanReachHome()) return false;
        HomeRequested?.Invoke(); return true;
    }
    public bool CanReachHome()
    {
        if(Player is null || !TerrainMotionRules.HeightAllowsInteraction(Player.GlobalPosition.Y,HomeEntry.Y)) return false;
        if((Player.GlobalPosition-HomeEntry).Dot(HomeForward)<.25f) return false;
        if(new Vector2(Player.GlobalPosition.X-HomeEntry.X,Player.GlobalPosition.Z-HomeEntry.Z).Length()>1.65f) return false;
        using var query=PhysicsRayQueryParameters3D.Create(Player.GlobalPosition+Vector3.Up*1.15f,HomeEntry+Vector3.Up*1.15f,1);
        query.Exclude=new Godot.Collections.Array<Rid> { Player.GetRid(),_homeSolid.GetRid() };
        return GetWorld3D().DirectSpaceState.IntersectRay(query).Count==0;
    }
    protected override bool IsMarkerActive(InteractionMarker3D marker) => CanReachHome();
    protected override string BuildPrompt(InteractionMarker3D marker) => $"[{(UseGamepadPrompts?"A":"F")}] {UiText.Get(marker.Prompt)}";
    public void SetHostLocked(bool value) { _hostLocked=value; SetRuntimeInputLocked(value); }
    public void ResetPosition()
    {
        var p=Layout.Geometry.Spawn;
        Player.GlobalPosition=SurfacePoint(p[0],p[2],.04f);Player.SetFacingImmediate(HomeForward);
        CameraRig.SetYawImmediate(180);CameraRig.SnapToTarget();
    }

    public static Vector3 Point(float x,float z,float offset=0) => new(x,VillageLayout3D.WalkHeight(x,z)+offset,z);
    private static StandardMaterial3D Mat(string color) => new() { AlbedoColor=new Color(color),Roughness=.95f };
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach(var child in root.GetChildren()) { yield return child;foreach(var item in Descendants(child)) yield return item; }
    }
    private StaticBody3D Box(Node parent,string name,Vector3 position,Vector3 size,bool visible=false,string color="989b8f",float yaw=0)
    {
        var body=new StaticBody3D { Name=name,Position=position,RotationDegrees=new(0,yaw,0),CollisionLayer=1,CollisionMask=0 };
        body.AddChild(new CollisionShape3D { Shape=new BoxShape3D { Size=size } });
        if(visible) body.AddChild(new MeshInstance3D { Mesh=new BoxMesh { Size=size },MaterialOverride=Mat(color) });
        parent.AddChild(body);return body;
    }
    private void BuildTerrain()
    {
        var d=Layout.Geometry;var nx=(int)(d.Bounds[2]/d.Grid)+1;var nz=(int)(d.Bounds[3]/d.Grid)+1;
        var vertices=new Vector3[nx*nz];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
        for(var z=0;z<nz;z++) for(var x=0;x<nx;x++)
        {
            var px=d.Bounds[0]+x*d.Grid;var pz=d.Bounds[1]+z*d.Grid;var index=z*nx+x;
            vertices[index]=new(px,Layout.GroundHeight(px,pz),pz);
            normals[index]=new Vector3(Layout.GroundHeight(px-.1f,pz)-Layout.GroundHeight(px+.1f,pz),.2f,
                Layout.GroundHeight(px,pz-.1f)-Layout.GroundHeight(px,pz+.1f)).Normalized();
            uv[index]=new(px/4,pz/4);
        }
        var indices=new int[(nx-1)*(nz-1)*6];var cursor=0;
        for(var z=0;z<nz-1;z++) for(var x=0;x<nx-1;x++)
        {
            var a=z*nx+x;var b=a+1;var c=a+nx;var e=c+1;
            foreach(var i in new[]{a,c,b,b,c,e}) indices[cursor++]=i;
        }
        var shape=new ConcavePolygonShape3D { Data=indices.Select(i=>vertices[i]).ToArray(),BackfaceCollision=true };
        var ground=new StaticBody3D { Name="VillageGroundCollision",CollisionLayer=1,CollisionMask=0 };
        ground.AddChild(new CollisionShape3D { Shape=shape });AddChild(ground);
        if(Graybox)
        {
            var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex]=vertices;arrays[(int)Mesh.ArrayType.Normal]=normals;
            arrays[(int)Mesh.ArrayType.TexUV]=uv;arrays[(int)Mesh.ArrayType.Index]=indices;
            var mesh=new ArrayMesh();mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
            ModelRoot.AddChild(new MeshInstance3D { Name="Ground",Mesh=mesh,MaterialOverride=Mat("7d8e68") });
            foreach(var r in d.Roads)
            for(var i=1;i<r.Points.Length;i++)
            {
                var a=r.Points[i-1];var b=r.Points[i];var length=new Vector2(a[0]-b[0],a[1]-b[1]).Length();
                for(var n=0;n<(int)Math.Ceiling(length);n++)
                {
                    var t=(n+.5f)/MathF.Ceiling(length);var x=Mathf.Lerp(a[0],b[0],t);var z=Mathf.Lerp(a[1],b[1],t);
                    var tile=new MeshInstance3D { Mesh=new BoxMesh { Size=new(r.Width,.018f,1.03f) },Position=Point(x,z,.045f),Rotation=new(0,Mathf.Atan2(b[0]-a[0],b[1]-a[1]),0),MaterialOverride=Mat("b4b1a4") };
                    ModelRoot.AddChild(tile);
                }
            }
        }
    }
    private void BuildBuildings()
    {
        foreach(var b in Layout.Geometry.Buildings)
        {
            var host=new Node3D { Name="Collision_"+b.Id,Position=new(b.Position[0],b.Position[1],b.Position[2]),RotationDegrees=new(0,b.Yaw,0) };AddChild(host);
            var solid=Box(host,"Building",new(0,b.Size[1]/2,0),new(b.Size[0],b.Size[1],b.Size[2]),Graybox,b.Kind=="home"?"dfd6bd":"bfc1b4");
            if(b.Id=="home")
            {
                _homeSolid=solid;
                foreach(var s in HomeLayout3D.Layout.Structure.Where(s=>s.Id.StartsWith("Fence",StringComparison.Ordinal)))
                    Box(host,s.Id,new(s.Position[0],s.Position[1],s.Position[2]),new(s.Size[0],s.Size[1],s.Size[2]),Graybox);
                foreach(var f in HomeLayout3D.Layout.Furniture.Where(f=>f.Id.StartsWith("Plant",StringComparison.Ordinal) || f.Id is "PorchBench" or "YardTap" or "ClothesRack"))
                    Box(host,f.Id,new(f.Position[0],f.Position[1]+f.Size[1]/2,f.Position[2]),new(f.Size[0],f.Size[1],f.Size[2]),Graybox);
                // Open leaves are flush beside the front wall, leaving the full original 2.8 m gate opening.
                Box(host,"OpenGateWest",new(-2.05f,.7f,11.76f),new(1.3f,1.4f,.07f),Graybox,"405448");
                Box(host,"OpenGateEast",new(2.05f,.7f,11.76f),new(1.3f,1.4f,.07f),Graybox,"405448");
                if(UseHomeTreesSample)
                    foreach(var p in HomeIdentityLayout3D.Layout.Props)
                        Box(host,"V6_"+p.Id,new(p.Position[0],p.Position[1]+p.Size[1]/2,p.Position[2]),new(p.Size[0],p.Size[1],p.Size[2]));
            }
            else if(b.Kind=="house")
            {
                var pad=b.LocalPad;var end=pad.Z+pad.Depth;var half=pad.Width/2;var wallHeight=.85f;
                Box(host,"FenceLeft",new(-half,wallHeight/2,pad.Z+pad.Depth/2),new(.18f,wallHeight,pad.Depth),Graybox);
                Box(host,"FenceRight",new(half,wallHeight/2,pad.Z+pad.Depth/2),new(.18f,wallHeight,pad.Depth),Graybox);
                var side=(pad.Width-2.8f)/2;
                Box(host,"FenceFrontLeft",new(-1.4f-side/2,wallHeight/2,end),new(side,wallHeight,.18f),Graybox);
                Box(host,"FenceFrontRight",new(1.4f+side/2,wallHeight/2,end),new(side,wallHeight,.18f),Graybox);
                if(UseHouseArtSample)
                    foreach(var p in VillageHouseArtLayout3D.Layout.Props.Where(p=>p.House==b.Id && p.Solid))
                        Box(host,"V5_"+p.Id,new(p.Position[0],p.Position[1]+p.Size[1]/2,p.Position[2]),new(p.Size[0],p.Size[1],p.Size[2]));
            }
        }
    }
    private void BuildBridgesAndBanks()
    {
        foreach(var b in Layout.Geometry.Bridges)
        {
            Box(this,"BridgeDeck_"+b.Id,new(b.CenterX,b.DeckY-.16f,b.Z),new(b.HalfSpan*2,.32f,b.Width),Graybox,"aaa79b");
            foreach(var side in new[]{-1,1}) Box(this,"BridgeRail_"+b.Id+side,new(b.CenterX,b.DeckY+.53f,b.Z+side*b.Width/2),new(b.HalfSpan*2,1.06f,.16f),Graybox,"827c70");
        }
        for(var z=-111;z<104;z+=2)
        {
            if(Layout.Geometry.Bridges.Any(b=>Math.Abs(z-b.Z)<b.Width/2+1.1f)) continue;
            foreach(var side in new[]{-1,1})
            {
                var x=VillageLayout3D.StreamX(z)+side*8.7f;
                Box(this,"Bank_"+z+"_"+side,new(x,VillageLayout3D.BaseHeight(x,z)+.30f,z),new(.34f,1.1f,2.08f),Graybox,"9a9a87");
            }
        }
    }
    private void BuildBoundaries()
    {
        var b=Layout.Geometry.Bounds;
        Box(this,"WestBoundary",new(b[0],5,b[1]+b[3]/2),new(1,30,b[3]),Graybox,"6b775d");
        Box(this,"EastBoundary",new(b[0]+b[2],5,b[1]+b[3]/2),new(1,30,b[3]),Graybox,"6b775d");
        Box(this,"NorthBoundary",new(b[0]+b[2]/2,5,b[1]),new(b[2],30,1),Graybox,"6b775d");
        Box(this,"SouthBoundary",new(b[0]+b[2]/2,5,b[1]+b[3]),new(b[2],30,1),Graybox,"6b775d");
    }
    private void BuildLandmarks()
    {
        foreach(var l in Layout.Geometry.Landmarks)
        {
            if(!UseV1 && Layout.Fixtures.ReplacedLandmarks.Contains(l.Id)) continue;
            Box(this,l.Id,new(l.Position[0],l.Position[1]+l.Size[1]/2,l.Position[2]),new(l.Size[0],l.Size[1],l.Size[2]),Graybox,"746e54");
        }
        foreach(var t in Layout.Trees)
            Box(this,$"Tree_{t.X}_{t.Z}",new(t.X,Surface.GroundHeight(t.X,t.Z)+1.5f,t.Z),new(.55f,3,.55f),Graybox,"635947");
        foreach(var p in Layout.Geometry.UtilityPoles)
            Box(this,$"UtilityPole_{p[0]}_{p[1]}",new(p[0],Surface.GroundHeight(p[0],p[1])+3.3f,p[1]),new(.19f,6.6f,.19f),Graybox,"888c82");
    }
    private void BuildExteriorBatch()
    {
        var host=new Node3D { Name="ExteriorBatchCollisions" };AddChild(host);
        foreach(var p in Layout.Fixtures.Props.Where(p=>p.Solid))
            Box(host,p.Id,new(p.Position[0],p.Position[1]+p.Size[1]/2,p.Position[2]),
                new(p.Size[0],p.Size[1],p.Size[2]),Graybox,"7d877e");
    }
    private void BuildActor()
    {
        // Outdoor travel tuning belongs to this area, not the shared animation reference speed.
        var player=new PlayerController3D { Name="Player",TerrainMovement=true,Speed=UseV1 ? 3.1f : 4.2f };
        player.AddChild(new CollisionShape3D { Position=new(0,.8f,0),Shape=new CapsuleShape3D { Radius=.28f,Height=1.6f } });AddChild(player);
        var rig=new ThirdPersonCameraRig { Name="ThirdPersonCameraRig",FixedPitchDegrees=-21,FollowTargetHeight=1.3f };
        var arm=new SpringArm3D { Name="SpringArm3D",SpringLength=4.2f,Margin=.1f,CollisionMask=3,Shape=new SphereShape3D { Radius=.18f } };
        var socket=new Node3D { Name="CameraSocket" };socket.AddChild(new Camera3D { Name="Camera3D",Fov=62,Near=.08f,Far=800,Current=true });
        arm.AddChild(socket);rig.AddChild(arm);AddChild(rig);
    }
    private void BuildLighting()
    {
        AddChild(new WorldEnvironment { Environment=new Godot.Environment {
            BackgroundMode=Godot.Environment.BGMode.Sky,
            Sky=new Sky { SkyMaterial=new ProceduralSkyMaterial { SkyTopColor=new Color("6598b7"),SkyHorizonColor=new Color("d0dcda"),GroundHorizonColor=new Color("cad5cc"),GroundBottomColor=new Color("78826b"),SkyCurve=.22f } },
            AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("809199"),AmbientLightEnergy=.45f,
            ReflectedLightSource=Godot.Environment.ReflectionSource.Disabled,TonemapMode=Godot.Environment.ToneMapper.Filmic,TonemapExposure=.95f
        } });
        AddChild(new DirectionalLight3D { Name="SummerMorning",RotationDegrees=new(-43,-36,0),LightColor=new Color("fff0d7"),LightEnergy=.55f,
            ShadowEnabled=true,DirectionalShadowMaxDistance=105,DirectionalShadowMode=DirectionalLight3D.ShadowMode.Parallel4Splits });
    }
}
