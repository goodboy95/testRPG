using Xunit;

namespace TownRpg.Core.Tests;

public sealed class HomeAccessTests
{
    [Fact]
    public void RepairedApproachPreservesHomeAndOnlyChangesRecordedLocalPatch()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null && !File.Exists(Path.Combine(root.FullName,"project.godot")))root=root.Parent;
        Assert.NotNull(root);
        var folder=Path.Combine(root.FullName,"art/terrain/village-v4");
        var before=File.ReadAllBytes(Path.Combine(folder,"edits/home-access/height.bin"));
        var after=File.ReadAllBytes(Path.Combine(folder,"height.bin"));
        var mask=File.ReadAllBytes(Path.Combine(folder,"edits/home-access/repair-mask.bin"));
        var changed=0;
        for(var j=0;j<512;j++)for(var i=0;i<512;i++)
        {
            var k=j*512+i;var a=BitConverter.ToSingle(after,k*4);var b=BitConverter.ToSingle(before,k*4);
            if(mask[k]==0)Assert.Equal(b,a);
            else {changed++;Assert.InRange(i*.5f-128,-62,-50);Assert.InRange(j*.5f-128,52.01f,62);}
        }
        Assert.Equal(276,changed);
        foreach(var x in new[]{-57f,-56f,-55f})
        for(var z=52f;z<61;z+=.5f)
        {
            var k=(int)((z+128)*2)*512+(int)((x+128)*2);
            var slope=Math.Abs(BitConverter.ToSingle(after,(k+512)*4)-BitConverter.ToSingle(after,k*4))/.5;
            Assert.True(slope<Math.Tan(20*Math.PI/180),$"approach slope {x},{z}");
        }
    }
}
