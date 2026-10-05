using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace TownRpg.Core.Tests;

public sealed class EnvironmentRefinementTests
{
    private static string Root
    {
        get
        {
            for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory is not null;directory=directory.Parent)
                if(File.Exists(Path.Combine(directory.FullName,"project.godot")))return directory.FullName;
            throw new DirectoryNotFoundException("project.godot");
        }
    }
    private static string PathFor(string relative)=>Path.Combine(Root,relative);
    private static void HashMatches(string path,string expected)=>Assert.Equal(expected.ToLowerInvariant(),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());

    [Fact]
    public void IndependentEnvironmentAssetsPreserveTheirSeedsAndResolveAllTextureDependencies()
    {
        const string folder="assets/generated/environment-refinement-v1/";
        using var manifest=JsonDocument.Parse(File.ReadAllText(PathFor(folder+"manifest.json")));
        Assert.False(manifest.RootElement.GetProperty("formalDefault").GetBoolean());
        foreach(var entry in manifest.RootElement.GetProperty("sources").EnumerateObject())
        {
            var record=entry.Value;
            HashMatches(PathFor(record.GetProperty("source").GetString()!),record.GetProperty("sourceSha256").GetString()!);
            HashMatches(PathFor(record.GetProperty("baseline").GetString()!),record.GetProperty("baselineSha256").GetString()!);
            var path=PathFor(folder+entry.Name+".glb");HashMatches(path,record.GetProperty("glbSha256").GetString()!);
            var bytes=File.ReadAllBytes(path);
            using var glb=JsonDocument.Parse(bytes.AsMemory(20,BitConverter.ToInt32(bytes,12)));
            Assert.False(glb.RootElement.TryGetProperty("skins",out _));
            Assert.False(glb.RootElement.TryGetProperty("animations",out _));
            foreach(var node in glb.RootElement.GetProperty("nodes").EnumerateArray())
            {
                var name=node.GetProperty("name").GetString()!;
                Assert.DoesNotContain("Lookdev",name);Assert.DoesNotContain("Protagonist",name);
            }
            foreach(var image in glb.RootElement.GetProperty("images").EnumerateArray())
            {
                var texture=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!,image.GetProperty("uri").GetString()!));
                Assert.StartsWith(Root+Path.DirectorySeparatorChar,texture);Assert.True(File.Exists(texture),texture);
            }
        }
    }

    [Fact]
    public void WorldGraphicsHaveRecordedPixelDimensionsHashesAndMipmaps()
    {
        const string folder="assets/generated/environment-refinement-v1/graphics/";
        using var manifest=JsonDocument.Parse(File.ReadAllText(PathFor(folder+"manifest.json")));
        Assert.False(manifest.RootElement.GetProperty("clockHandsBaked").GetBoolean());
        foreach(var image in manifest.RootElement.GetProperty("images").EnumerateArray())
        {
            var path=PathFor(folder+image.GetProperty("kind").GetString()+".png");
            HashMatches(path,image.GetProperty("sha256").GetString()!);
            var bytes=File.ReadAllBytes(path);
            Assert.Equal(image.GetProperty("width").GetInt32(),BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4)));
            Assert.Equal(image.GetProperty("height").GetInt32(),BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4)));
            Assert.Contains("mipmaps/generate=true",File.ReadAllText(path+".import"));
        }
    }
}
