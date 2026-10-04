using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public partial class VillageOpeningController
{
    public ObjectiveTrackerView Tracker { get; private set; } = null!;
    private bool _journalAfterConversation;
    private Vector3 ExteriorPoint(Node3D node) => node.IsInsideTree() ? node.GlobalPosition : Area.Transform * node.Position;
    public Vector3 TargetPosition(string id) => id switch
    {
        "stool" => ExteriorPoint(_stool),
        "porch" => _porch,
        "cart_home" => _cartHome,
        _ when id.StartsWith("npc:") => ExteriorPoint(Residents.Single(n => n.Definition.Id == id[4..])),
        _ => ExteriorPoint(_markers[id])
    };
    private void SelectNearestTarget()
    {
        if (!_worldReady || State.Tracking?.Job is not { } job) return;
        var objective = Service.Objective(job);
        if (State.Tracking.Target is not null || objective.Targets.Count == 0) return;
        var origin = Host.IsInterior ? Area.HomeReturnPoint : Player.GlobalPosition;
        var target = objective.Targets.OrderBy(t => TargetPosition(t.Id).DistanceSquaredTo(origin)).First();
        Service.Track(job, target.Id);
    }
    private void TrackingChanged() { SelectNearestTarget(); Checkpoint(); }
    private void UpdateTracking()
    {
        Tracker.Visible = false;
        if ((!_worldReady && !Host.IsInterior) || Host.IsTransitioning || Host.HostLocked || Session.Modal.IsBlocked || Busy || !_focus) return;
        if (State.Tracking?.Job is not { } job) return;
        var objective = Service.Objective(job);
        // An indoor save can be restored before the exterior actors are instantiated.
        // Show its exit without resolving or replacing the saved outdoor destination.
        var target = Host.IsInterior ? objective.Targets.FirstOrDefault() : objective.Targets.FirstOrDefault(t => t.Id == State.Tracking.Target);
        if (target is null) return;
        var id = target.Id;
        Vector3 point;
        var action = target.Action; var name = target.Name;
        if (Host.IsInterior)
        {
            var exit = Host.Interior!.GetNode<InteractionMarker3D>("InteractionMarkers/home_door_entry");
            point = exit.GlobalPosition; action = "先从正门回到村庄"; name = "住宅正门";
        }
        else
        {
            point = TargetPosition(id);
            if (job == OpeningJob.Cart && State.LoadSecured && !State.CartReturned && !_pushing &&
                (!id.StartsWith("receive_") || _cart.GlobalPosition.DistanceTo(point) > 3.2f))
            { point = _cart.GlobalPosition; action = "回到推车旁，继续送货"; name = "停放的推车"; }
        }
        Tracker.ShowTarget(Host.ActiveCamera.Camera, Host.ActivePlayer.GlobalPosition, point, objective.Title, action, name, Session.State.Settings.FontScale);
    }
}
