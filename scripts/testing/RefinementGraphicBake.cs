using Godot;
using System.Security.Cryptography;
using System.Text.Json;

namespace TownRpg.Scripts.Testing;

public partial class RefinementGraphicBake : Node
{
    public override async void _Ready()
    {
        try
        {
            var directory = ProjectSettings.GlobalizePath("res://assets/generated/environment-refinement-v1/graphics");
            System.IO.Directory.CreateDirectory(directory);
            var records = new List<object>();
            foreach (var kind in RefinementGraphic.Kinds)
            {
                var size=RefinementGraphic.SizeFor(kind);
                var view = new SubViewport { Size = size, TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                AddChild(view); view.AddChild(new RefinementGraphic { Kind = kind });
                for (var i=0;i<3;i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var path = System.IO.Path.Combine(directory,kind+".png");
                using var image = view.GetTexture().GetImage();
                if (image.SavePng(path) != Error.Ok) throw new System.IO.IOException(path);
                var importPath=path+".import";
                using var settings=new ConfigFile();
                if(System.IO.File.Exists(importPath))settings.Load(importPath);
                settings.SetValue("remap","importer","texture");
                settings.SetValue("remap","type","CompressedTexture2D");
                settings.SetValue("deps","source_file",ProjectSettings.LocalizePath(path));
                settings.SetValue("params","mipmaps/generate",true);
                settings.SetValue("params","compress/mode",0);
                settings.SetValue("params","detect_3d/compress_to",0);
                if(settings.Save(importPath)!=Error.Ok)throw new IOException(importPath);
                records.Add(new { kind, width=size.X, height=size.Y, sha256=Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(path))) });
                view.QueueFree(); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"manifest.json"),JsonSerializer.Serialize(new {
                generator="Godot 4.6.3 CanvasItem drawing and Source Han Sans SC 2.005R; no generative text",
                source="scripts/testing/RefinementGraphic.cs", clockHandsBaked=false, clueKeys=RefinementGraphic.ClueKeys, images=records
            },new JsonSerializerOptions { WriteIndented=true }));
            GD.Print("REFINEMENT_GRAPHICS_PASS images=6 clockHandsBaked=false"); GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
