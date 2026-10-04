using Godot;

namespace TownRpg.Presentation;

/// <summary>Small visual-only planting at sample fence/tree edges. No road or collision changes.</summary>
public static class VillageAnimeDetails3D
{
    public static void AddTo(Node3D host,IVillageSurface3D ground)
    {
        var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        for(var i=0;i<14;i++)
        {
            var a=i*2.39996f;var side=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.065f;
            var root=new Vector3(Mathf.Sin(a)*.14f,0,Mathf.Cos(a)*.14f);
            var mid=root+new Vector3(Mathf.Cos(a)*.12f,.18f+(i%3)*.04f,Mathf.Sin(a)*.12f);
            var tip=mid+new Vector3(Mathf.Cos(a)*.15f,.07f,Mathf.Sin(a)*.15f);
            surface.SetColor(new Color("77925e"));
            foreach(var v in new[]{root,mid-side,mid+side})surface.AddVertex(v);
            surface.SetColor(new Color(i%3==0?"b5bc7a":"9bb16e"));
            foreach(var v in new[]{mid-side,tip,mid+side})surface.AddVertex(v);
        }
        surface.GenerateNormals();var mesh=surface.Commit();surface.Dispose();
        mesh.SurfaceSetMaterial(0,new StandardMaterial3D { VertexColorUseAsAlbedo=true,
            CullMode=BaseMaterial3D.CullModeEnum.Disabled,Roughness=1,BacklightEnabled=true,Backlight=new(.10f,.13f,.06f) });
        var points=new List<Vector2>();
        foreach(var x in new[]{-63.5f,-62.9f,-61.4f,-60.8f,-51.7f,-51.2f,-49.8f,-49.1f})points.Add(new(x,52.35f));
        foreach(var x in new[]{-63.8f,-63.3f,-62.8f})points.Add(new(x,50.8f));
        foreach(var center in new[]{new Vector2(-28,29),new Vector2(-62.2426f,23.7574f),new Vector2(-40.3f,28.5f)})
            for(var i=0;i<9;i++){var a=i*2.39996f;points.Add(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(.48f+(i%3)*.13f));}
        var multi=new MultiMesh { TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,Mesh=mesh,InstanceCount=points.Count };
        for(var i=0;i<points.Count;i++)
        {
            var p=points[i];var scale=.8f+(i%4)*.11f;
            multi.SetInstanceTransform(i,new Transform3D(new Basis(Vector3.Up,i*2.39996f).Scaled(Vector3.One*scale),new(p.X,ground.GroundHeight(p.X,p.Y)+.005f,p.Y)));
        }
        host.AddChild(new MultiMeshInstance3D {Name="V8SamplePlanting",Multimesh=multi,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
    }
}
