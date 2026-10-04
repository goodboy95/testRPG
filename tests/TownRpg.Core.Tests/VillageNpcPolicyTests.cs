using System.Text.Json;
using TownRpg.Presentation;

namespace TownRpg.Core.Tests;

public class VillageNpcPolicyTests
{
    private static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "project.godot"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Project root missing.");
        }
    }

    [Fact]
    public void PilotCatalogReferencesRealAssetsAndDistinctLiteralDialogue()
    {
        var catalog = VillageNpcPolicy.Parse(File.ReadAllText(Path.Combine(Root, "data/previews/village-npcs.json")));
        var lines = new HashSet<string>();
        foreach (var npc in catalog)
        {
            Assert.True(File.Exists(Path.Combine(Root, npc.Model[6..])));
            var dialogue = File.ReadAllText(Path.Combine(Root, npc.Dialogue[6..]));
            foreach (var id in VillageNpcPolicy.ValidateDialogue(dialogue))
            {
                Assert.True(lines.Add(id));
                Assert.Contains(id + ",", File.ReadAllText(Path.Combine(Root, "localization/zh_CN.csv")));
            }
            Assert.Contains(npc.Id, File.ReadAllText(Path.Combine(Root, "data/characters.json")));
            Assert.Contains(npc.Prompt + ",", File.ReadAllText(Path.Combine(Root, "localization/zh_CN.csv")));
        }
        foreach (var path in Directory.GetFiles(Path.Combine(Root, "dialogue/events"), "*.dialogue"))
            Assert.DoesNotContain(lines, id => File.ReadAllText(path).Contains("[ID:" + id + "]", StringComparison.Ordinal));
        Assert.Single(catalog.Single(n => n.Id == "aunt_qiao").Points);
        Assert.Equal(3, catalog.Single(n => n.Id == "shen_yan").Points.Length);
    }

    [Theory]
    [InlineData("do Narrative.Execute(\"complete_event\", \"test\")")]
    [InlineData("if Narrative.Evaluate(\"has_fact\", \"test\")")]
    [InlineData("乔姨: {{Narrative.Execute()}} [ID:NPC_PILOT_BAD]")]
    [InlineData("乔姨: [do Narrative.Execute()] [ID:NPC_PILOT_BAD]")]
    [InlineData("=> missing")]
    public void DialogueRejectsExpressionsCommandsAndMissingDestinations(string bad)
    {
        Assert.Throws<InvalidDataException>(() => VillageNpcPolicy.ValidateDialogue("~ start\n乔姨: 你好。 [ID:NPC_PILOT_HELLO]\n" + bad));
    }

    [Fact]
    public void DuplicateCatalogAndTextIdsFail()
    {
        var entries = VillageNpcPolicy.Parse(File.ReadAllText(Path.Combine(Root, "data/previews/village-npcs.json")));
        Assert.Throws<InvalidDataException>(() => VillageNpcPolicy.Parse(JsonSerializer.Serialize(new[] { entries[0], entries[0] })));
        Assert.Throws<InvalidDataException>(() => VillageNpcPolicy.ValidateDialogue("~ start\n乔姨: 你好。 [ID:NPC_PILOT_A]\n乔姨: 再见。 [ID:NPC_PILOT_A]"));
    }

    [Theory]
    [InlineData("res://assets/generated/village/npc-art/shen_yan_spring.glb")]
    [InlineData("res://assets/generated/characters/shen-yan-v2/shen-yan-static-v2.glb")]
    public void RetiredAndUnapprovedStaticArtCannotEnterFunctionalPreview(string model)
    {
        var entries = VillageNpcPolicy.Parse(File.ReadAllText(Path.Combine(Root, "data/previews/village-npcs.json")));
        entries[1] = entries[1] with { Model = model, VisualProfile = "art" };
        Assert.Throws<InvalidDataException>(() => VillageNpcPolicy.Parse(JsonSerializer.Serialize(entries)));
    }

    [Fact]
    public void LodHysteresisAndConversationProtection()
    {
        Assert.Equal(0, VillageNpcPolicy.Lod(24, 0, false));
        Assert.Equal(1, VillageNpcPolicy.Lod(26, 0, false));
        Assert.Equal(2, VillageNpcPolicy.Lod(56, 1, false));
        Assert.Equal(2, VillageNpcPolicy.Lod(52, 2, false));
        Assert.Equal(1, VillageNpcPolicy.Lod(49, 2, false));
        Assert.Equal(0, VillageNpcPolicy.Lod(100, 2, true));
        Assert.True(VillageNpcPolicy.WithinReach(2, .85f));
        Assert.False(VillageNpcPolicy.WithinReach(2.01f, 0));
        Assert.False(VillageNpcPolicy.WithinReach(1, -.86f));
    }
}
