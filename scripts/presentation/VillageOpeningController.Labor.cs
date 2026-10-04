using Godot;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Presentation;

public partial class VillageOpeningController
{
    private Node3D _stool = null!;
    private CharacterBody3D _cart = null!;
    private CollisionShape3D _carryCollision = null!;
    private readonly Dictionary<string,Vector3> _receiving = [];
    private readonly Dictionary<string,Node3D> _smallGoods = [];
    private readonly Dictionary<string,Node3D> _bulkGoods = [];
    private Node3D _bag = null!;
    private Vector3 _stoolHome, _porch, _cartHome;
    private bool _placing, _pushing;
    private float _placeYaw;
    private Vector3 _placePosition;
    private bool _placeValid, _placePorch;
    private float _normalSpeed;
    private double _followStuck;
    private readonly Queue<Vector3> _footsteps = [];
    private Vector3 _lastFootstep;
    public CharacterBody3D CartBody => _cart;
    public Node3D StoolObject => _stool;
    public IReadOnlyDictionary<string,Vector3> ReceivingPoints => _receiving;
    public bool Pushing => _pushing;
    public bool Placing => _placing;
    public Vector3 StoolPorch => _porch;
    public string ClearanceDiagnostic(Vector3 p,float radius)
    {
        using var q = new PhysicsShapeQueryParameters3D { Shape = new CylinderShape3D { Radius = radius,Height = .65f }, Transform = new(Basis.Identity,p+Vector3.Up*.6f),CollisionMask = 1,Exclude = new Godot.Collections.Array<Rid> { Player.GetRid(),_cart.GetRid() } };
        return string.Join(",",Area.GetWorld3D().DirectSpaceState.IntersectShape(q,12).Select(r=>r["collider"].AsGodotObject() is Node n ? n.GetPath().ToString() : "terrain"));
    }

    private Vector3 At(string building,float x,float z)
    {
        var b = Area.Layout.Geometry.Buildings.Single(b => b.Id == building);
        var p = b.ToWorld(new(x,z)); return Area.SurfacePoint(p.X,p.Y,.035f);
    }
    private void BuildLabor()
    {
        _normalSpeed = Player.Speed;
        _stoolHome = At("workshop",4.8f,8.6f); _porch = At("house02",1.8f,4.6f); _cartHome = At("shop",2.8f,10.3f);
        // Replace this single decorative ready stool in the formal instance; saved v6 sources stay intact.
        Area.GetNode<StaticBody3D>("ExteriorBatchCollisions/ReadyStool").CollisionLayer = 0;
        foreach (var visual in Area.ModelRoot.FindChildren("*ReadyStool*","MeshInstance3D",true,false).OfType<MeshInstance3D>()) visual.Visible = false;
        Area.GetNode<StaticBody3D>("ExteriorBatchCollisions/Handcart").CollisionLayer = 0;
        foreach (var visual in Area.ModelRoot.FindChildren("*Handcart*","MeshInstance3D",true,false).OfType<MeshInstance3D>()) visual.Visible = false;
        _stool = VillageOpeningProps.Stool(); Area.AddChild(_stool);
        _cart = VillageOpeningProps.Cart(); Area.AddChild(_cart); _cart.Position = _cartHome;
        _cart.AddChild(new Label3D { Text = "米面推车", Position = new(0,1.55f,0), PixelSize = .003f, FontSize = 26, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled });
        AddMarker("stool",_stoolHome,"OPENING_STOOL");
        AddMarker("cart",_cartHome,"OPENING_CART",2.6f);
        AddMarker("table",At("shop",-3.3f,7.2f),"OPENING_TABLE",2.4f);
        foreach (var house in VillageOpeningIds.Houses)
        {
            var b = Area.Layout.Geometry.Buildings.Single(b => b.Id == house);
            // Outside the yard mouth; never occupy a doorway or require a cart through a household gate.
            var p = At(house,0,b.Size[2]/2+6.2f); _receiving.Add(house,p);
            AddMarker("receive_"+house,p,"OPENING_RECEIVE",2.8f);
            var plate = new Node3D { Position = p }; Area.AddChild(plate);
            VillageOpeningProps.Box(plate,new(0,.015f,0),new(1.5f,.025f,1.4f),"8c896e");
            plate.AddChild(new Label3D { Text = VillageOpeningService.HouseName(house)+"收货处", Position = new(0,1.5f,0), PixelSize = .003f, FontSize = 26, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled });
            var small = VillageOpeningProps.Package("Small_"+house,VillageOpeningService.HouseName(house)+" "+Goods[Array.IndexOf(VillageOpeningIds.Houses,house)],false);
            Area.AddChild(small); _smallGoods.Add(house,small);
            var bulk = VillageOpeningProps.Package("Bulk_"+house,VillageOpeningService.HouseName(house)+" 米／面",true);
            Area.AddChild(bulk); _bulkGoods.Add(house,bulk);
        }
        var porchMarker = new Node3D { Position = _porch }; Area.AddChild(porchMarker);
        VillageOpeningProps.Box(porchMarker,new(0,.008f,0),new(.85f,.012f,.7f),"b9a881");
        porchMarker.AddChild(new Label3D { Text = "木凳靠墙放，不挡门", Position = new(0,1,0), PixelSize = .0028f, FontSize = 26, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled });
        _bag = VillageOpeningProps.Package("DeliveryBag","配送提袋",false); Player.AddChild(_bag); _bag.Position = new(.35f,.7f,0);
        _carryCollision = new CollisionShape3D { Name = "CarriedStoolClearance", Position = new(0,.85f,0), Shape = new CylinderShape3D { Height = .65f, Radius = .65f }, Disabled = true };
        Player.AddChild(_carryCollision);
    }
    private void RecoverProps()
    {
        // Runtime physics revalidates saved safe poses against this actual adopted scene.
        if (State.Stool == StoolLocation.Carried)
            Service.RecoverCarriedStool(ToPose(FindSafeGround(Host.IsInterior ? Area.HomeReturnPoint : Player.Position,.4f),0));
        if (State.Stool == StoolLocation.Ground && State.StoolGround is { } ground)
        {
            var p = FindSafeGround(FromPose(ground),.4f); State.StoolGround = ToPose(p,ground.Yaw);
        }
        _cart.Position = State.CartParking is { } parking ? FindSafeGround(FromPose(parking),1.1f) : _cartHome;
        _cart.Rotation = new(0,State.CartParking?.Yaw ?? 0,0);
        _footsteps.Clear(); _lastFootstep = Player.Position;
    }
    private void RefreshProps()
    {
        _stool.Position = State.Stool switch { StoolLocation.Workshop => _stoolHome, StoolLocation.Ground => FromPose(State.StoolGround!), StoolLocation.Porch or StoolLocation.Accepted => _porch, _ => Player.Position + Player.FacingWorld*.55f + Vector3.Up*.5f };
        if (State.Stool is StoolLocation.Ground or StoolLocation.Porch or StoolLocation.Accepted) _stool.Rotation = new(0,State.StoolGround?.Yaw ?? 0,0);
        _markers["stool"].Position = _stool.Position;
        Player.Speed = State.Stool == StoolLocation.Carried ? 2.4f : _normalSpeed;
        _carryCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled,State.Stool != StoolLocation.Carried);
        _bag.Visible = State.Packed.Except(State.Delivered).Any();
        var index = 0;
        foreach (var h in VillageOpeningIds.Houses)
        {
            _smallGoods[h].Visible = !State.Packed.Contains(h) || State.Delivered.Contains(h);
            _smallGoods[h].Position = State.Delivered.Contains(h) ? At(h,2.2f,5.6f) : At("shop",-2.6f+index*.55f,6.8f);
            _bulkGoods[h].Visible = State.Invited;
            _bulkGoods[h].Position = State.Unloaded.Contains(h) ? At(h,2.4f,6.4f) : State.Loaded.Contains(h) ? _cart.Position : At("shop",.2f+index*.65f,6.7f);
            index++;
        }
        var shen = Residents.Single(n => n.Definition.Id == "shen_yan");
        var xu = Residents.Single(n=>n.Definition.Id=="grandma_xu");
        xu.Sitting = State.Stool == StoolLocation.Accepted;
        if (xu.Sitting) xu.Position = _porch;
        if (Service.Finished) shen.Position = At("shop",-5f,8.7f);
        else if (State.Invited && shen.Position.DistanceTo(Player.Position)>12 && Area.IsInsideTree())
            shen.Position = FindSafeGround(Player.Position-Player.FacingWorld*2,.35f);
        UpdateCargo();
    }
    private void UpdateCargo()
    {
        var i=0;
        foreach (var h in VillageOpeningIds.Houses)
        {
            foreach (var label in _bulkGoods[h].GetChildren().OfType<Label3D>()) label.Visible = !State.Loaded.Contains(h) || State.Unloaded.Contains(h);
            if (State.Loaded.Contains(h) && !State.Unloaded.Contains(h))
            { _bulkGoods[h].GlobalPosition = _cart.ToGlobal(i switch { 0=>new(-.23f,.6f,-.15f),1=>new(.23f,.6f,-.15f),_=>new(0,.6f,.38f) }); _bulkGoods[h].Rotation = _cart.Rotation; }
            i++;
        }
    }
    private void TakeStool()
    {
        if (!Clear(Player.Position,.68f)) { Toast("周围有东西挡着，先挪开一点再拿。"); return; }
        Commit("take_stool"); Player.PlayInteractionPose("pickup",_stool.Position);
    }
    private void ConfirmPlacement()
    {
        if (!_placeValid) { Toast("这里会挡门或碰到东西。换一块平地，再确认放下。"); return; }
        Commit(_placePorch ? "porch_stool" : "put_stool",pose:ToPose(_placePosition,_placeYaw));
        _placing = false; Player.ExternalMotion = false; RefreshProps();
        Toast(_placePorch ? "木凳放稳了。请许婆婆试坐，再回彭师傅处交差。" : "先放稳了。回来仍可拿起同一张凳子。");
    }
    private async Task CartMenu()
    {
        if (!State.Invited) { await Say("乔姨","先把手头这份办妥，有别的我会登到牌上。"); return; }
        if (Service.Can("return_cart") && _cart.Position.DistanceTo(_cartHome) <= 3)
        {
            if (await Choose("空推车","把推车归还杂货铺？",["停回原处","再等一下"]) == 0)
            { _cart.Position = _cartHome; _cart.Rotation = Vector3.Zero; Service.ParkCart(ToPose(_cartHome,0)); Commit("return_cart"); }
            return;
        }
        if (Service.Finished || State.CartReturned) { await Say("乔姨","推车归位了，谢谢。米面这一趟都记下了。"); return; }
        if (!State.LoadSecured) { await Table(); return; }
        if (await Choose("推车","前后推进／倒退，左右转向；镜头仍可独立转动。交互键停靠，院口停稳后卸货。",["握住车把","先停着"]) == 0) BeginPush();
    }
    public bool BeginPush()
    {
        if (!State.LoadSecured || State.CartReturned || _placing || State.Stool == StoolLocation.Carried || !Area.IsInsideTree()) return false;
        var handle = _cart.ToGlobal(new(0,0,-1.22f)); handle.Y = Area.WalkHeight(handle.X,handle.Z)+.035f;
        if (!Clear(handle,.28f)) { Toast("车把旁边有人或东西挡着，挪开再接手。"); return false; }
        _pushing = true; Player.ExternalMotion = true; Player.AddCollisionExceptionWith(_cart); _cart.AddCollisionExceptionWith(Player);
        Player.Position = handle; Player.SetFacingImmediate(_cart.GlobalBasis.Z); LaborPose(true); return true;
    }
    private void ParkCart()
    {
        if (!_pushing) return;
        _pushing = false; _cart.Velocity = Vector3.Zero; Player.ExternalMotion = false;
        var dock = _receiving.Values.Append(_cartHome).OrderBy(p=>p.DistanceTo(_cart.Position)).First();
        if (dock.DistanceTo(_cart.Position)<2.2f)
        {
            var transform = _cart.GlobalTransform; transform.Origin = dock;
            if (CartClear(transform)) _cart.Position = dock;
        }
        // Exit beside the handle, never inside the cart's combined body/handle clearance hull.
        Player.Position = FindSafeGround(_cart.ToGlobal(new(1.05f,0,-1.1f)),.3f);
        Player.RemoveCollisionExceptionWith(_cart); _cart.RemoveCollisionExceptionWith(Player);
        Service.ParkCart(ToPose(_cart.Position,_cart.Rotation.Y)); LaborPose(false); Checkpoint();
        Toast("车停稳了。到收货处核对卸货，或回车旁接着推。");
    }
    private void PrepareHome()
    {
        if (!_worldReady) return;
        if (_pushing) ParkCart();
        _placing = false; Player.ExternalMotion = false;
        if (State.Stool == StoolLocation.Carried) Service.RecoverCarriedStool(ToPose(FindSafeGround(Player.Position-Player.FacingWorld,.4f),0));
        RefreshProps(); Checkpoint();
    }
    public void StopLaborForRelocation()
    { if (_worldReady && !Host.IsInterior) PrepareHome(); }
    private void AfterAreaChange()
    {
        InitializeHome();
        if (Host.IsInterior) return;
        InitializeExterior();
        if (State.Invited && !Service.Finished) Residents.Single(n => n.Definition.Id == "shen_yan").Position = FindSafeGround(Player.Position-Player.FacingWorld*2,.35f);
        _footsteps.Clear(); _lastFootstep = Player.Position; RefreshProps();
    }
    public override void _Process(double delta)
    {
        if (_leaving) return;
        UpdateTracking();
        Host.SetOpeningPhase(Service.CanInvite);
        _status.Visible = !Busy && !JournalPanel.Visible && !Host.HostLocked;
        if (_toastTime > 0) _toastTime -= delta;
        else _status.Text = _placing ? "左右调整朝向；确认放下，取消继续携带。　"+_inputHint : _pushing ? "前后推／倒退　左右转向　交互键停靠" : State.Stool == StoolLocation.Carried ? "木凳送往许婆婆家。交互键放置，可随时暂放。　"+_inputHint : "周六 · "+(Service.Finished ? "歇脚之后，自由走走" : Service.CanInvite ? "午间" : "早晨")+"　"+_inputHint;
        if (!_worldReady) return;
        foreach (var npc in Residents) npc.Held = Host.IsInterior || Host.IsTransitioning || Host.HostLocked || Busy || !_focus;
        if (!Host.IsInterior && Area.IsInsideTree())
        {
            _markers["cart"].Position = _cart.Position;
            if (State.Stool == StoolLocation.Carried && !_placing)
            { _stool.Position = Player.Position+Player.FacingWorld*.55f+Vector3.Up*.5f; _stool.Rotation = new(0,Mathf.Atan2(Player.FacingWorld.X,Player.FacingWorld.Z),0); }
            UpdateCargo();
        }
    }
    public override void _PhysicsProcess(double delta)
    {
        if (_leaving || !_worldReady || !Area.IsInsideTree() || Host.IsInterior || Host.IsTransitioning || Host.HostLocked || Busy || !_focus) return;
        if (_placing)
        {
            _placeYaw += Input.GetAxis("move_left","move_right")*(float)delta*1.8f;
            _placePorch = Player.Position.DistanceTo(_porch)<2.5f;
            _placePosition = _placePorch ? _porch : Player.Position+Player.FacingWorld*1.05f;
            _placePosition.Y = Area.WalkHeight(_placePosition.X,_placePosition.Z)+.025f;
            _placeValid = Clear(_placePosition,.41f) && MathF.Abs(_placePosition.Y-Player.Position.Y)<.45f;
            _stool.Position = _placePosition; _stool.Rotation = new(0,_placeYaw,0);
        }
        if (_pushing)
        {
            var forward = -Input.GetAxis("move_up","move_down");
            var steering = Input.GetAxis("move_left","move_right");
            var oldYaw = _cart.Rotation.Y;
            var wanted = oldYaw-steering*(float)delta*.9f;
            var transform = _cart.GlobalTransform; transform.Basis = new Basis(Vector3.Up,wanted);
            if (CartClear(transform)) _cart.Rotation = new(0,wanted,0);
            var direction = _cart.GlobalBasis.Z*forward*2.4f;
            _cart.Velocity = new(direction.X,TerrainMotionRules.VerticalVelocity(_cart.Velocity.Y,_cart.IsOnFloor(),(float)delta),direction.Z);
            _cart.MoveAndSlide();
            var handle = _cart.ToGlobal(new(0,0,-1.22f)); handle.Y = Area.WalkHeight(handle.X,handle.Z)+.035f;
            Player.Position = handle; Player.SetFacingImmediate(_cart.GlobalBasis.Z);
        }
        FollowCompanion(delta);
    }
    private bool CartClear(Transform3D transform)
    {
        using var query = new PhysicsShapeQueryParameters3D { Shape = new BoxShape3D { Size = new(1.06f,.7f,2.28f) }, Transform = transform.TranslatedLocal(new(0,.85f,-.3f)), CollisionMask = 1,
            Exclude = new Godot.Collections.Array<Rid> { Player.GetRid(),_cart.GetRid() } };
        return Area.GetWorld3D().DirectSpaceState.IntersectShape(query,1).Count == 0;
    }
    private void FollowCompanion(double delta)
    {
        var npc = Residents.Single(n => n.Definition.Id == "shen_yan");
        if (!State.Invited || Service.Finished) { npc.FollowPoint = null; return; }
        if (_pushing)
        {
            npc.EndTalk(); npc.LaborPose = true;
            npc.FollowPoint = _cart.ToGlobal(new(1.3f,0,.1f)); return;
        }
        if (Player.Position.DistanceTo(_lastFootstep)>.8f) { _footsteps.Enqueue(Player.Position); _lastFootstep = Player.Position; }
        while (_footsteps.Count>1 && npc.Position.DistanceTo(_footsteps.Peek())<1.2f) _footsteps.Dequeue();
        if (_footsteps.Count>200) _footsteps.Dequeue();
        var target = _footsteps.Count>0 ? _footsteps.Peek() : Player.Position;
        npc.FollowPoint = npc.Position.DistanceTo(Player.Position)>2.2f ? target : null;
        if (npc.Position.DistanceTo(Player.Position)>8) _followStuck += delta; else _followStuck = 0;
        if (_followStuck>8)
        {
            var safe = FindSafeGround(Player.Position-Player.FacingWorld*2.5f,.35f);
            if (Clear(safe,.35f)) { npc.Position = safe; _footsteps.Clear(); _followStuck = 0; }
        }
    }
    private bool Clear(Vector3 position,float radius)
    {
        if (!float.IsFinite(position.Y)) return false;
        using var q = new PhysicsShapeQueryParameters3D { Shape = new CylinderShape3D { Radius = radius, Height = .65f }, Transform = new(Basis.Identity,position+Vector3.Up*.6f), CollisionMask = 1,
            Exclude = new Godot.Collections.Array<Rid> { Player.GetRid(),_cart.GetRid() } };
        return Area.GetWorld3D().DirectSpaceState.IntersectShape(q,1).Count == 0;
    }
    private Vector3 FindSafeGround(Vector3 near,float radius)
    {
        foreach (var center in new[] { near, radius>.7f ? _cartHome : _stoolHome, Area.HomeReturnPoint })
        for (var ring=0;ring<8;ring++) for(var i=0;i<12;i++)
        {
            var p = center+new Vector3(MathF.Cos(i*Mathf.Tau/12),0,MathF.Sin(i*Mathf.Tau/12))*ring*.7f;
            p.Y = Area.WalkHeight(p.X,p.Z)+.035f;
            if (Clear(p,radius) && MathF.Abs(p.Y-center.Y)<1.3f) return p;
        }
        throw new InvalidOperationException("没有找到可恢复物件的安全落点，请重新载入最近存档。");
    }
    private static OpeningPose ToPose(Vector3 p,float yaw) => new(p.X,p.Y,p.Z,yaw);
    private static Vector3 FromPose(OpeningPose p) => new(p.X,p.Y,p.Z);
}
