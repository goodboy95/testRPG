using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using TownRpg.Core.Content;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using TownRpg.Presentation;

namespace TownRpg.Core.Tests;

public class VillageOpeningContentTests
{
    private static string Root
    {
        get { for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent) if(File.Exists(Path.Combine(d.FullName,"project.godot"))) return d.FullName; throw new DirectoryNotFoundException(); }
    }
    private static string Read(string p)=>File.ReadAllText(Path.Combine(Root,p));
    [Fact]
    public void OpeningCatalogsReferenceOriginalAssetsKnownResidentsAndAdoptedBuildings()
    {
        var residents=VillageOpeningContent.ParseResidents(Read("data/village-opening-npcs.json"));
        var goods=VillageOpeningContent.ParseGoods(Read("data/village-opening-goods.json"));
        var characters=ContentJson.Deserialize<CharacterCatalog>(Read("data/characters.json"));
        var layout=VillageLayoutProfile3D.Parse(Read("assets/generated/village/layout-v4.json"));
        using var manifest=JsonDocument.Parse(Read("assets/generated/village/opening-npcs/manifest.json"));
        var source=Path.Combine(Root,manifest.RootElement.GetProperty("source").GetString()!);
        Assert.Equal(manifest.RootElement.GetProperty("sourceSha256").GetString(),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant());
        foreach(var r in residents)
        {
            Assert.Contains(characters.Characters,c=>c.Id==r.Id);
            Assert.Contains(layout.Geometry.Buildings,b=>b.Id==r.Building);
            Assert.Contains(r.Prompt+",",Read("localization/zh_CN.csv"));
            var bytes=File.ReadAllBytes(Path.Combine(Root,r.Model[6..]));
            var entry=manifest.RootElement.GetProperty("characters").EnumerateArray().Single(x=>x.GetProperty("id").GetString()==r.Id);
            Assert.Equal(entry.GetProperty("sha256").GetString(),Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            using var glb=JsonDocument.Parse(bytes.AsMemory(20,BitConverter.ToInt32(bytes,12)));
            Assert.Equal(2,glb.RootElement.GetProperty("meshes").GetArrayLength());
            Assert.Single(glb.RootElement.GetProperty("skins").EnumerateArray());
            Assert.Equal(6,glb.RootElement.GetProperty("skins")[0].GetProperty("joints").GetArrayLength());
        }
        foreach(var g in goods) { Assert.Contains(residents,r=>r.Id==g.Owner && r.Building==g.House); Assert.Contains(g.ItemKey+",",Read("localization/zh_CN.csv")); }
    }
    [Fact]
    public void EachCoreStoryHasOneMatchingCompleteCommandNoOtherMutationAndOnlyKnownSpeakers()
    {
        var events=ContentJson.Deserialize<EventCatalog>(Read("data/events.json")).Events.Where(e=>e.Id.StartsWith("village.opening.")).ToArray();
        Assert.Equal(17,events.Length);
        Assert.True(events.Select(e=>e.Id).ToHashSet().SetEquals(VillageOpeningContent.StoryKeys.Select(VillageOpeningIds.Event)));
        var ids=new HashSet<string>();
        foreach(var e in events)
        {
            var text=Read(e.DialoguePath[6..]);
            Assert.Single(Regex.Matches(text,"do Narrative.Execute"));
            Assert.Contains($"do Narrative.Execute(\"complete_event\", \"{e.Id}\")",text);
            foreach(var line in text.Split('\n').Where(l=>l.Contains("[ID:")))
            {
                var speaker=line[..line.IndexOf(':')]; Assert.Contains(speaker,new[]{"主角","旁白","乔姨","彭师傅","村长","沈砚","许婆婆","陈叔","刘伯"});
                var id=Regex.Match(line,@"\[ID:([A-Z0-9_]+)\]").Groups[1].Value;
                Assert.True(ids.Add(id)); Assert.Contains(id+",",Read("localization/zh_CN.csv"));
            }
        }
    }
    [Fact]
    public void UnknownOpeningReferencesAndStateKeysFailRatherThanBeingIgnored()
    {
        Assert.Throws<InvalidDataException>(()=>VillageOpeningContent.ParseResidents(Read("data/village-opening-npcs.json").Replace("house15","house99")));
        Assert.Throws<InvalidDataException>(()=>VillageOpeningContent.ParseResidents(Read("data/village-opening-npcs.json").Replace("opening-npcs/uncle_liu.glb","retired/uncle_liu.glb")));
        Assert.Throws<InvalidDataException>(()=>VillageOpeningContent.ParseGoods(Read("data/village-opening-goods.json").Replace("grandma_xu","unknown")));
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<VillageOpeningState>("{\"UnknownQuest\": true}"));
    }
    [Fact]
    public void GenericNarrativeRollbackAlsoRestoresBothRealitySections()
    {
        var g=GameState.CreateDreamPrologue(); g.DreamEscape!.Status=DreamEscapeStatus.CompletedPin; g.DreamEscape.Items.Clear();
        g.VillageExploration=new() { X=12, WakeIntroductionComplete=true }; g.Player.LocationId=VillageExplorationState.VillageLocation;
        var service=new VillageOpeningService(g); service.Initialize(); service.Execute("accept_orders"); service.Execute("pack","house13");
        var before=SaveGameSerializer.Serialize(g);
        Assert.Throws<KeyNotFoundException>(()=>new EventOutcomeService(g).Apply("opening-failure",[
            new() { Command="add_fact",Arguments=["temporary"] },new() { Command="resolve_promise",Arguments=["missing","fulfilled"] }]));
        Assert.Equal(before,SaveGameSerializer.Serialize(g));
    }
}
