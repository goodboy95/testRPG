using Godot;
using System.Text.Json;

namespace TownRpg.Presentation;

public static class VillageTerrainDetails3D
{
    private sealed record Detail(float X,float Y,float Z,float Yaw,float Scale,string Kind);
    public static void AddTo(Node3D host,IVillageSurface3D ground,string folder=TerrainVillageSurface3D.Folder,bool spatialBatches=false,float detailDistance=38)
    {
        var items=JsonSerializer.Deserialize<Detail[]>(Godot.FileAccess.GetFileAsString(folder+"details.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive=true })!;
        var grass=new SurfaceTool();grass.Begin(Mesh.PrimitiveType.Triangles);
        for(var i=0;i<7;i++)
        {
            var angle=i*2.39996f;var center=new Vector3(Mathf.Cos(angle)*.12f,0,Mathf.Sin(angle)*.12f);
            var side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.023f;
            var tip=center+new Vector3(Mathf.Cos(angle)*.06f,.17f+(i%3)*.035f,Mathf.Sin(angle)*.06f);
            foreach(var p in new[]{center-side,tip,center+side}) { grass.SetColor(new Color(.38f+i*.014f,.44f+i*.012f,.22f));grass.AddVertex(p); }
        }
        grass.GenerateNormals();var grassMesh=grass.Commit();grassMesh.SurfaceSetMaterial(0,new StandardMaterial3D {
            VertexColorUseAsAlbedo=true,CullMode=BaseMaterial3D.CullModeEnum.Disabled,Roughness=1 });
        var rock=new SphereMesh { Radius=.14f,Height=.20f,RadialSegments=7,Rings=3,
            Material=new StandardMaterial3D { AlbedoColor=new Color("8b887a"),Roughness=.95f } };
        foreach(var kind in new[]{"grass","stone"})
        {
          foreach(var cell in items.Where(p=>p.Kind==kind).GroupBy(p=>spatialBatches?((int)MathF.Floor(p.X/16),(int)MathF.Floor(p.Z/16)):(0,0)))
          {
            var group=cell.ToArray();
            var multi=new MultiMesh { TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,Mesh=kind=="grass"?grassMesh:rock,InstanceCount=group.Length };
            for(var i=0;i<group.Length;i++)
            {
                var p=group[i];var y=ground.GroundHeight(p.X,p.Z);
                multi.SetInstanceTransform(i,new Transform3D(new Basis(Vector3.Up,p.Yaw).Scaled(Vector3.One*p.Scale),new(p.X,y+(kind=="stone"?.035f:-.015f),p.Z)));
            }
            host.AddChild(new MultiMeshInstance3D { Name="TerrainDetail_"+kind+"_"+cell.Key.Item1+"_"+cell.Key.Item2,Multimesh=multi,
                VisibilityRangeEnd=spatialBatches?detailDistance:0,VisibilityRangeEndMargin=spatialBatches?4:0,
                CastShadow=GeometryInstance3D.ShadowCastingSetting.Off });
          }
        }
    }
}
