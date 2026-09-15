using System.Numerics;

namespace TownRpg.Presentation;

/// <summary>Door motion and planar capsule clearance, independent of engine and save serialization.</summary>
public sealed class HomeDoorMotion(HomeLayout3D.Door definition)
{
    public const float Duration = .45f;
    public float Progress { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsBlocked { get; private set; }
    public bool IsOpen => Progress >= 1 && !IsMoving;
    public int OpenSide { get; private set; } = DefaultSide(definition);
    public int StableOpenSide { get; private set; } = DefaultSide(definition);
    public bool StableOpen => IsMoving ? !_opening : IsOpen;
    private bool _opening;
    public static int DefaultSide(HomeLayout3D.Door door) => door.Axis == "x" ? -1 : door.Swing;

    public bool Request(Vector2 player)
    {
        if (IsMoving) return false;
        if (OverlapsPlayer(Progress, OpenSide, player)) { IsBlocked=true; return false; }
        _opening = !IsOpen;
        if (_opening)
        {
            var signedDistance=definition.Axis=="x" ? player.Y-definition.Position[2] : player.X-definition.Position[0];
            OpenSide=signedDistance>=0 ? -1 : 1;
        }
        IsBlocked=false; IsMoving=true; return true;
    }

    public float AngleRadians(int leaf) => Angle(Progress,OpenSide,leaf);
    private float Angle(float progress,int side,int leaf)
    {
        var t=progress*progress*(3-2*progress);
        var sign=definition.Axis=="x" ? -side*(leaf==0?1:-1) : side;
        return sign*MathF.PI/2*t;
    }
    public bool OverlapsPlayer(float progress,int side,Vector2 player,float radius=.28f)
    {
        var count=definition.Double?2:1;var length=definition.Width/count;
        for(var i=0;i<count;i++)
        {
            var leafSign=i==0?1:-1;
            var hinge=new Vector2(definition.Position[0],definition.Position[2])+
                (definition.Axis=="x"?Vector2.UnitX:Vector2.UnitY)*((i==0?-1:1)*definition.Width/2);
            var direction=definition.Axis=="x"?new Vector2(leafSign,0):Vector2.UnitY;
            var a=Angle(progress,side,i);direction=new(MathF.Cos(a)*direction.X+MathF.Sin(a)*direction.Y,-MathF.Sin(a)*direction.X+MathF.Cos(a)*direction.Y);
            var t=Math.Clamp(Vector2.Dot(player-hinge,direction),0,length);
            // A capsule around the actual leaf centre-line includes its thickness and a small contact margin.
            if(Vector2.DistanceSquared(player,hinge+direction*t)<MathF.Pow(radius+.0325f+.02f,2))return true;
        }
        return false;
    }

    public void Step(float delta,Vector2? player=null)
    {
        if(!IsMoving || delta<=0)return;
        IsBlocked=false;
        var target=Math.Clamp(Progress+Math.Max(delta,0)/Duration*(_opening?1:-1),0,1);
        // Subdivide even long frames so the leaf cannot jump through the capsule.
        while(Math.Abs(target-Progress)>.000001f)
        {
            var next=Progress+Math.Clamp(target-Progress,-.01f,.01f);
            if(player is { } p && OverlapsPlayer(next,OpenSide,p)){IsBlocked=true;return;}
            Progress=next;
        }
        Progress=target;
        if(Progress is 0 or 1){IsMoving=false;StableOpenSide=OpenSide;}
    }
    public void Reset()=>Restore(false);
    public void Restore(bool open,int? side=null)
    {
        if(side is not (null or -1 or 1))throw new ArgumentOutOfRangeException(nameof(side));
        Progress=open?1:0;IsMoving=false;IsBlocked=false;_opening=open;
        OpenSide=StableOpenSide=side??DefaultSide(definition);
    }
}
