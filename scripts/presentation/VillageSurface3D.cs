using Godot;

namespace TownRpg.Presentation;

/// <summary>Presentation-only ground access. Bridges remain independent physical surfaces.</summary>
public interface IVillageSurface3D
{
    float GroundHeight(float x,float z);
    void SetActive(bool active);
    bool Ready { get; }
}

public sealed class OriginalVillageSurface3D(VillageLayoutProfile3D? layout=null) : IVillageSurface3D
{
    public float GroundHeight(float x,float z)=>layout?.Compact==true ? layout.GroundHeight(x,z) : VillageLayout3D.TriangulatedHeight(x,z);
    public void SetActive(bool active) { }
    public bool Ready=>true;
}

/// <summary>No GDExtension types leak outside this adapter or enter saved game state.</summary>
public sealed class TerrainVillageSurface3D : IVillageSurface3D
{
    public const string Folder="res://assets/generated/village/terrain-sample-v1/";
    private readonly Node3D _terrain;
    private readonly GodotObject _data;
    private readonly GodotObject _collision;
    public TerrainVillageSurface3D(Node3D host,string folder=Folder)
    {
        if(!ClassDB.ClassExists("Terrain3D")) throw new InvalidOperationException("Terrain3D 1.0.2 is unavailable. Use the original village preview.");
        _terrain=ResourceLoader.Load<PackedScene>(folder+"terrain.tscn").Instantiate<Node3D>();
        _terrain.Set("free_editor_textures",false);
        host.AddChild(_terrain);
        _data=_terrain.Get("data").AsGodotObject();
        _collision=_terrain.Get("collision").AsGodotObject();
    }
    public float GroundHeight(float x,float z)=>_data.Call("get_height",new Vector3(x,0,z)).AsSingle();
    public bool Ready=>_terrain.IsInsideTree() && _data.Call("get_region_count").AsInt32()==4 && _collision.Get("mode").AsInt32()==3;
    public void SetActive(bool active)=>_collision.Set("mode",active?3:0);
}
