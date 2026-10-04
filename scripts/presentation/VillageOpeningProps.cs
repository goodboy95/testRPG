using Godot;

namespace TownRpg.Presentation;

/// <summary>Original functional props in metres; no external character or retired NPC geometry.</summary>
public static class VillageOpeningProps
{
    public static MeshInstance3D Box(Node3D parent, Vector3 at, Vector3 size, string color)
    {
        var mesh = new MeshInstance3D { Position = at, Mesh = new BoxMesh { Size = size }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new(color), Roughness = .88f } };
        parent.AddChild(mesh); return mesh;
    }
    public static Node3D Stool()
    {
        var root = new Node3D { Name = "RepairedStool" };
        Box(root, new(0,.46f,0), new(.64f,.09f,.43f), "947048");
        foreach (var x in new[] { -.24f, .24f }) foreach (var z in new[] { -.15f, .15f }) Box(root,new(x,.22f,z),new(.065f,.44f,.065f),"745236");
        Box(root,new(0,.18f,.15f),new(.5f,.045f,.045f),"ba935e"); return root;
    }
    public static CharacterBody3D Cart()
    {
        var root = new CharacterBody3D { Name = "DeliveryCart", CollisionLayer = 1, CollisionMask = 1,
            FloorSnapLength = .4f, FloorMaxAngle = Mathf.DegToRad(40), SafeMargin = .01f, FloorConstantSpeed = true };
        root.AddChild(new CollisionShape3D { Position = new(0,.6f,-.3f), Shape = new BoxShape3D { Size = new(1.08f,1.1f,2.3f) } });
        Box(root,new(0,.52f,.12f),new(.94f,.13f,1.08f),"967444");
        Box(root,new(0,.71f,.65f),new(.98f,.32f,.06f),"6b573b");
        foreach (var x in new[] { -.44f, .44f })
        {
            Box(root,new(x,.7f,-.75f),new(.06f,.07f,1.25f),"71664d");
            root.AddChild(new MeshInstance3D { Position = new(x,.29f,.12f), Rotation = new(0,0,Mathf.Pi/2), Mesh = new CylinderMesh { TopRadius = .27f, BottomRadius = .27f, Height = .08f, RadialSegments = 16 }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new("373c3e") } });
        }
        return root;
    }
    public static Node3D Package(string name, string label, bool bulk)
    {
        var root = new Node3D { Name = name };
        Box(root,new(0,bulk?.21f:.1f,0),bulk?new(.42f,.4f,.5f):new(.24f,.2f,.3f),bulk?"c4b790":"dac69d");
        if (bulk) Box(root,new(0,.53f,0),new(.38f,.24f,.45f),"ede1bf");
        root.AddChild(new Label3D { Text = label, Position = new(0,bulk?.88f:.43f,0), PixelSize = .003f, FontSize = 26, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = false, OutlineSize = 6 });
        return root;
    }
}
