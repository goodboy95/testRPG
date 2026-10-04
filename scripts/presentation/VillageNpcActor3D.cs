using Godot;

namespace TownRpg.Presentation;

/// <summary>Instance-only resident movement and visual state. No session or save access.</summary>
public partial class VillageNpcActor3D : CharacterBody3D
{
    public VillageNpcDefinition Definition { get; private set; } = null!;
    public InteractionMarker3D Marker { get; private set; } = null!;
    public bool Talking { get; private set; }
    public bool Held { get; set; }
    public Vector3? FollowPoint { get; set; }
    public bool LaborPose { get; set; }
    public bool Sitting { get; set; }
    public bool Obstructed { get; private set; }
    public int Lod { get; private set; }
    public double UpdateMs { get; private set; }
    public double MotionMs { get; private set; }
    public double PoseMs { get; private set; }
    public Vector3[] Route { get; private set; } = [];
    public int RouteIndex => _point;
    private string _collider = "";
    public string MotionDiagnostic => $"held={Held} talking={Talking} focus={GetWindow().HasFocus()} point={_point} wait={_wait:F2} retry={_retry:F2} blocked={Obstructed} position={GlobalPosition} collider={_collider}";
    public VillageNpcVisualProfile VisualProfile { get; private set; } = VillageNpcArtPolicy.Pilot;
    public bool UsesImportedAnimations => VisualProfile.UsesImportedAnimations && _animationPlayer is not null;
    public int VisibleTriangles => Lod >= _meshes.Length || !IsVisibleInTree() ? 0 : _triangles[Lod];
    public int VisibleSurfaces => Lod >= _meshes.Length || !IsVisibleInTree() ? 0 : _meshes[Lod].Mesh.GetSurfaceCount();
    public Material SharedMaterial => _meshes[0].GetActiveMaterial(0);
    public void UseSharedMaterial(Material material)
    { foreach (var mesh in _meshes) mesh.MaterialOverride = material; }
    private VillageExterior3D _area = null!;
    private MeshInstance3D[] _meshes = [];
    private Skeleton3D _skeleton = null!;
    private Node3D _visual = null!;
    private int[] _triangles = [];
    private readonly Dictionary<string, int> _bones = [];
    private readonly Dictionary<string, Quaternion> _restPoses = [];
    private readonly KinematicCollision3D _aheadCollision = new();
    private int _point, _step = 1;
    private double _wait = 3, _retry, _phase, _animationClock;
    private double _lookClock;
    private float _wantedYaw;
    private int _lodFloor;
    private AnimationPlayer? _animationPlayer;
    private string _animationState = "";
    private bool _windowFocused = true;

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut) _windowFocused = false;
        else if (what == NotificationWMWindowFocusIn) _windowFocused = true;
    }

    public void Initialize(VillageNpcDefinition definition, VillageExterior3D area, PackedScene model)
    {
        Definition = definition; VisualProfile = VillageNpcArtPolicy.Profile(definition.VisualProfile); _area = area; Name = definition.Id;
        CollisionLayer = 1; CollisionMask = 1; SafeMargin = .005f;
        MotionMode = MotionModeEnum.Grounded; FloorSnapLength = TerrainMotionRules.FloorSnap;
        FloorMaxAngle = Mathf.DegToRad(TerrainMotionRules.MaximumSlopeDegrees);
        // Slow fixed routes need ground + one obstruction resolution, not the player's six-slide solver.
        FloorStopOnSlope = true; FloorConstantSpeed = false; MaxSlides = 2;
        var building = area.Layout.Geometry.Buildings.Single(b => b.Id == definition.Building);
        Route = definition.Points.Select(p => building.ToWorld(new(p[0], p[1])))
            .Select(p => area.SurfacePoint(p.X, p.Y, .04f)).ToArray();
        _point = Route.Length > 1 ? 1 : 0;
        Position = Route[0]; _wantedYaw = Mathf.DegToRad(definition.Yaw);
        AddChild(new CollisionShape3D { Position = new(0, .8f, 0), Shape = new CapsuleShape3D { Height = 1.6f, Radius = .27f } });
        _visual = model.Instantiate<Node3D>(); AddChild(_visual);
        _visual.Rotation = new(0, _wantedYaw, 0);
        var descendants = Descendants(_visual).ToArray();
        _skeleton = descendants.OfType<Skeleton3D>().Single();
        var importedMeshes = descendants.OfType<MeshInstance3D>().ToArray();
        _meshes = Enumerable.Range(0, VisualProfile.VisibleLodCount).Select(lod => importedMeshes.SingleOrDefault(mesh =>
            mesh.Name.ToString().Contains("LOD" + lod, StringComparison.Ordinal) ||
            mesh.Mesh.ResourceName.Contains("LOD" + lod, StringComparison.Ordinal)) ?? throw new InvalidDataException(
                "Missing NPC LOD" + lod + ": " + string.Join(", ", importedMeshes.Select(mesh => mesh.Name + "/" + mesh.Mesh.ResourceName)))).ToArray();
        _triangles = new int[_meshes.Length];
        for (var lod = 0; lod < _meshes.Length; lod++)
        {
            var mesh = _meshes[lod];
            for (var s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
            {
                var arrays = mesh.Mesh.SurfaceGetArrays(s);
                var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                _triangles[lod] += (indices.Length > 0 ? indices.Length : arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length) / 3;
            }
            if (_triangles[lod] > VillageNpcArtPolicy.TriangleLimit(VisualProfile, lod) || mesh.Mesh.GetSurfaceCount() > VisualProfile.MaxSurfaces)
                throw new InvalidDataException("NPC mesh exceeds budget: " + definition.Id);
            mesh.Visible = lod == 0;
        }
        foreach (var bone in new[] { "head", "arm_l", "arm_r", "leg_l", "leg_r" })
        {
            var index = _skeleton.FindBone(bone);
            if (index < 0) throw new InvalidDataException("Missing NPC bone: " + bone);
            _bones.Add(bone, index);
            _restPoses.Add(bone, _skeleton.GetBonePoseRotation(index));
        }
        _animationPlayer = descendants.OfType<AnimationPlayer>().FirstOrDefault();
        SetImportedAnimation("idle");
        Marker = new InteractionMarker3D { Name = "TalkMarker", TargetId = "npc:" + definition.Id,
            Prompt = definition.Prompt, InteractionRange = 2 };
        AddChild(Marker);
    }

    public bool CanTalk()
    {
        if (!IsInsideTree() || Held || Lod == 2) return false;
        var player = _area.PlayerActor;
        var difference = player.GlobalPosition - GlobalPosition;
        if (!VillageNpcPolicy.WithinReach(new Vector2(difference.X, difference.Z).Length(), difference.Y)) return false;
        using var query = PhysicsRayQueryParameters3D.Create(player.GlobalPosition + Vector3.Up * 1.2f,
            GlobalPosition + Vector3.Up * 1.2f, 1);
        query.Exclude = new Godot.Collections.Array<Rid> { player.GetRid(), GetRid() };
        return GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
    }

    public void BeginTalk(Vector3 player)
    {
        Talking = true; Velocity = Vector3.Zero;
        var d = player - GlobalPosition; _wantedYaw = Mathf.Atan2(d.X, d.Z);
        SetLod(0);
        SetImportedAnimation("talk");
    }

    public void EndTalk()
    {
        Talking = false; _wait = 1; _retry = 0;
        SetImportedAnimation("idle");
        if (Route.Length == 1) _wantedYaw = Mathf.DegToRad(Definition.Yaw);
    }

    public void ResetResident()
    {
        Talking = false; Held = false; Position = Route[0]; _point = Route.Length > 1 ? 1 : 0; _step = 1;
        _wait = 3; _retry = 0; _lookClock = 0; Obstructed = false; _wantedYaw = Mathf.DegToRad(Definition.Yaw);
        _lodFloor = 0; SetImportedAnimation("idle");
    }

    /// <summary>Controller-level budget pressure; larger values permit only lower-detail visible meshes.</summary>
    public void SetLodCap(int cap) => _lodFloor = Mathf.Clamp(cap, 0, VisualProfile.VisibleLodCount - 1);

    public bool SegmentClear(Vector3 start, Vector3 end)
    {
        var transform = GlobalTransform;
        transform.Origin = start + Vector3.Up * .12f;
        var blocked = TestMove(transform, end - start, _aheadCollision);
        _collider = blocked ? _aheadCollision.GetCollider()?.ToString() ?? "unknown" : "";
        // A walkable slope is support, not a person or wall blocking the route.
        return !blocked || _aheadCollision.GetNormal().Y >= MathF.Cos(FloorMaxAngle);
    }

    public override void _PhysicsProcess(double delta)
    {
        var start = Time.GetTicksUsec();
        MotionMs = 0; PoseMs = 0;
        try
        {
            var distance = _area.PlayerActor.GlobalPosition.DistanceTo(GlobalPosition);
            var desired = VisualProfile == VillageNpcArtPolicy.Pilot
                ? VillageNpcPolicy.Lod(distance, Lod, Talking)
                : VillageNpcArtPolicy.DesiredLod(distance, Lod, Talking, VisualProfile);
            SetLod(Talking ? 0 : desired >= VisualProfile.VisibleLodCount ? desired : Math.Max(desired, _lodFloor));
            if (!_windowFocused || !GetWindow().HasFocus()) return;
            var moving = false;
            var horizontal = Vector3.Zero;
            if (!Held && !Sitting && !Talking && FollowPoint is { } follow)
            {
                var direction = follow - GlobalPosition; direction.Y = 0;
                if (direction.Length() > .3f)
                {
                    var step = direction.Normalized();
                    if (SegmentClear(GlobalPosition, GlobalPosition + step * .45f)) horizontal = step * Math.Min(4.2f, direction.Length() * 2);
                    else
                    {
                        foreach (var angle in new[] { .7f, -.7f, 1.4f, -1.4f })
                        {
                            var side = step.Rotated(Vector3.Up, angle);
                            if (!SegmentClear(GlobalPosition, GlobalPosition + side * .55f)) continue;
                            horizontal = side * 1.6f; break;
                        }
                    }
                    if (horizontal != Vector3.Zero) _wantedYaw = Mathf.Atan2(horizontal.X, horizontal.Z);
                }
            }
            if (!Held && !Sitting && !Talking && FollowPoint is null && Route.Length > 1)
            {
                _wait -= delta; _retry -= delta; _lookClock -= delta;
                if (_wait <= 0 && _retry <= 0)
                {
                    var target = Route[_point]; var d = target - GlobalPosition; d.Y = 0;
                    if (d.Length() < .05f)
                    {
                        _wait = 3;
                        if (_point == Route.Length - 1) _step = -1;
                        else if (_point == 0) _step = 1;
                        _point += _step;
                    }
                    else
                    {
                        var direction = d.Normalized();
                        var look = GlobalPosition + direction * .35f;
                        // A 0.35 m lead at 1 m/s permits a 10 Hz anticipation check.
                        // Actual collision resolution still runs every physics tick below.
                        if (_lookClock <= 0) { Obstructed = !SegmentClear(GlobalPosition, look); _lookClock = .1; }
                        if (Obstructed) _retry = .5;
                        else { horizontal = direction; _wantedYaw = Mathf.Atan2(direction.X, direction.Z); }
                    }
                }
            }
            var previous = GlobalPosition;
            if (!Held && (horizontal != Vector3.Zero || !IsOnFloor()))
            {
                Velocity = new(horizontal.X, TerrainMotionRules.VerticalVelocity(Velocity.Y, IsOnFloor(), (float)delta), horizontal.Z);
                MoveAndSlide();
            }
            else Velocity = Vector3.Zero;
            moving = new Vector2(GlobalPosition.X - previous.X, GlobalPosition.Z - previous.Z).LengthSquared() > .000001f;
            MotionMs = (Time.GetTicksUsec() - start) / 1000d;
            var poseStart = Time.GetTicksUsec();
            _visual.Rotation = new(0, Mathf.LerpAngle(_visual.Rotation.Y, _wantedYaw, 1 - MathF.Exp(-7 * (float)delta)), 0);
            _visual.Position = new(0,Sitting ? -.38f : 0,0);
            if (VisualProfile.UsesImportedAnimations)
            {
                SetImportedAnimation(Talking ? "talk" : moving ? "walk" : "idle");
                PoseMs = (Time.GetTicksUsec() - poseStart) / 1000d;
                return;
            }
            _phase += delta; _animationClock += delta;
            if (Lod >= _meshes.Length || Lod == 1 && _animationClock < .1) return;
            _animationClock = 0;
            var stride = moving ? MathF.Sin((float)_phase * 7) * .35f : 0;
            Pose("leg_l", Sitting ? -1.15f : stride); Pose("leg_r", Sitting ? -1.15f : -stride);
            Pose("arm_l", LaborPose ? -.9f : -stride * .7f);
            Pose("arm_r", LaborPose ? -.9f : Talking ? -.25f + MathF.Sin((float)_phase * 2) * .12f : stride * .7f);
            Pose("head", MathF.Sin((float)_phase * (Talking ? 2 : 1)) * .035f);
            PoseMs = (Time.GetTicksUsec() - poseStart) / 1000d;
        }
        finally { UpdateMs = (Time.GetTicksUsec() - start) / 1000d; }
    }

    private void Pose(string bone, float radians) => _skeleton.SetBonePoseRotation(_bones[bone], _restPoses[bone] * new Quaternion(Vector3.Right, radians));
    private void SetLod(int lod)
    {
        if (lod == Lod) return;
        Lod = lod;
        for (var i = 0; i < _meshes.Length; i++)
        {
            _meshes[i].Visible = i == lod;
            _meshes[i].CastShadow = lod == 0 ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }

    private void SetImportedAnimation(string requested)
    {
        if (!VisualProfile.UsesImportedAnimations || _animationPlayer is null || _animationState == requested) return;
        var actual = _animationPlayer.GetAnimationList().FirstOrDefault(name => name.EndsWith(requested, StringComparison.OrdinalIgnoreCase));
        if (actual is null) return;
        _animationPlayer.Play(actual);
        _animationState = requested;
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren()) { yield return child; foreach (var node in Descendants(child)) yield return node; }
    }
}
