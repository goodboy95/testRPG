using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TownRpg.Core.Content;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public sealed class ContentIntegrityTests
{
    private const int ExpectedSchemaVersion = 1;

    [Fact]
    public void ArchiveV2ExportsItsActualLayoutAndPhysicalStateAnchors()
    {
        ValidateLateRoomV2Asset("archive", "ArchiveRoomLayout3D", "DreamArchiveRoom", 14, 12,
            ["ArchiveRoom", "WindowGlass", "WestStacks", "NorthStacks", "SouthCabinet", "RegistryTable", "DrawerMatrix",
             "RepairBench", "RecorderBench", "TimelineTable", "IndexTable", "RouteTable", "Registry_Solved", "Drawer_Open",
             "Drawer_Closed", "Fuse_Repaired", "Recorder_Reel", "Recorder_Reel_Right", "Recorder_Crank", "Recorder_Fuse",
             "Recorder_Running", "Timeline_Sorted", "Timeline_Unsorted", "Pickup_IndexStrip", "RegistryRulePaper", "ChoirRecordPaper",
             "BroadcastRecordPaper", "ArtRecordPaper", "TrackRecordPaper", "FuseRoutePaper"], 180000, 220, 24, 60);
    }

    [Fact]
    public void AuditoriumV2ExportsLinkedSeatingAndPhysicalStateAnchors()
    {
        ValidateLateRoomV2Asset("auditorium", "AuditoriumLayout3D", "DreamAuditorium", 22, 16,
            ["AuditoriumRoom", "WindowGlass", "Stage", "Proscenium", "MainCurtain", "StageScreen", "StageExitClosed", "Exit_Open",
             "Projector", "FlyRail", "SpliceBench", "MasterPlayer", "LightConsole", "SoundConsole", "BroadcastConsole", "SideStand",
             "Projection_Clue", "Projector_Lens", "Projector_Solved", "Tape_Order", "Tape_Unspliced", "Splice_Reel", "Master_Tape",
             "Master_Player_Light", "Light_Ready", "Sound_Ready", "Broadcast_Ready", "Left_Lever_Held", "Left_Handle", "Right_Handle",
             "Pickup_Counterweight", "Pickup_Lens", "Installed_Counterweight", "FrontFramePlate", "HandoffCircuitPlate"], 260000, 320, 32, 80);
        var bytes = File.ReadAllBytes(ProjectPath("assets/generated/dream_escape/3d/dream-auditorium-room-v2.glb"));
        using var document = JsonDocument.Parse(bytes.AsMemory(20, BitConverter.ToInt32(bytes, 12)));
        var nodes = document.RootElement.GetProperty("nodes").EnumerateArray().ToArray();
        var seats = nodes.Where(n => n.TryGetProperty("name", out var name) && Regex.IsMatch(name.GetString()!, "^Seat[0-9]{2}$")).ToArray();
        Assert.Equal(32, seats.Length);
        Assert.Single(seats.Select(n => n.GetProperty("mesh").GetInt32()).Distinct());
        Assert.Equal(4, nodes.Count(n => n.TryGetProperty("name", out var name) && Regex.IsMatch(name.GetString()!, "^Line_Weight[0-3]$")));
        Assert.Equal(3, nodes.Count(n => n.TryGetProperty("name", out var name) && Regex.IsMatch(name.GetString()!, "^Projector_Disc[0-2]$")));
    }

    private static void ValidateLateRoomV2Asset(string room, string layout, string sceneName, float width, float depth,
        string[] required, int maximumTriangles, int maximumMeshes, int maximumMaterials, int maximumMiB)
    {
        var asset = $"assets/generated/dream_escape/3d/dream-{room}-room-v2";
        var bytes = File.ReadAllBytes(ProjectPath(asset + ".glb"));
        Assert.Equal(0x46546C67u, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 4));
        Assert.InRange(bytes.Length, 1, maximumMiB * 1024 * 1024);
        using var document = JsonDocument.Parse(bytes.AsMemory(20, BitConverter.ToInt32(bytes, 12)));
        var root = document.RootElement;
        var nodes = root.GetProperty("nodes").EnumerateArray().ToArray();
        var byName = nodes.Where(n => n.TryGetProperty("name", out _)).ToDictionary(n => n.GetProperty("name").GetString()!);
        foreach (var name in required.Concat(["Room_Floor", "Room_Ceiling", "RoomFixtures"])) Assert.Contains(name, byName.Keys);
        Assert.DoesNotContain(byName.Keys, name => Regex.IsMatch(name, "(?i)caelus|protagonist|armature|xiaoyu"));
        foreach (var name in new[] { "skins", "animations", "cameras" }) Assert.False(root.TryGetProperty(name, out _));
        using var manifest = JsonDocument.Parse(File.ReadAllText(ProjectPath(asset + "-manifest.json")));
        var data = manifest.RootElement;
        Assert.False(data.GetProperty("graybox").GetBoolean());
        Assert.Equal(2, data.GetProperty("layoutVersion").GetInt32());
        Assert.Equal(Sha256File(ProjectPath($"scripts/presentation/{layout}.cs")), data.GetProperty("layoutSha256").GetString());
        Assert.Equal(Sha256File(ProjectPath(asset + ".glb")), data.GetProperty("runtimeGlbSha256").GetString());
        Assert.Equal(Sha256File(ProjectPath(data.GetProperty("sourceFile").GetString()!)), data.GetProperty("sourceSha256").GetString());
        var geometry = data.GetProperty("geometry");
        var accessors = root.GetProperty("accessors").EnumerateArray().ToArray();
        var meshes = root.GetProperty("meshes").EnumerateArray().ToArray();
        var triangles = meshes.Select(m => m.GetProperty("primitives").EnumerateArray().Sum(p => accessors[p.GetProperty("indices").GetInt32()].GetProperty("count").GetInt32() / 3)).ToArray();
        var total = nodes.Where(n => n.TryGetProperty("mesh", out _)).Sum(n => triangles[n.GetProperty("mesh").GetInt32()]);
        Assert.Equal(geometry.GetProperty("triangleCount").GetInt32(), total);
        Assert.InRange(total, 1, maximumTriangles);
        var meshCount = nodes.Count(n => n.TryGetProperty("mesh", out _));
        Assert.Equal(geometry.GetProperty("meshObjectCount").GetInt32(), meshCount);
        Assert.InRange(meshCount, 1, maximumMeshes);
        Assert.Equal(geometry.GetProperty("materialCount").GetInt32(), root.GetProperty("materials").GetArrayLength());
        Assert.InRange(root.GetProperty("materials").GetArrayLength(), 1, maximumMaterials);
        var floorMesh = meshes[byName["Room_Floor"].GetProperty("mesh").GetInt32()];
        var floorPositions = accessors[floorMesh.GetProperty("primitives")[0].GetProperty("attributes").GetProperty("POSITION").GetInt32()];
        var min = floorPositions.GetProperty("min").EnumerateArray().Select(n => n.GetSingle()).ToArray();
        var max = floorPositions.GetProperty("max").EnumerateArray().Select(n => n.GetSingle()).ToArray();
        Assert.InRange(max[0] - min[0], width - .002f, width + .002f);
        Assert.InRange(max[2] - min[2], depth - .002f, depth + .002f);
        var scene = File.ReadAllText(ProjectPath($"scenes/dream/{sceneName}.tscn"));
        Assert.Contains($"dream-{room}-room-v2.glb", scene);
        Assert.Contains("ambient_light_source = 2", scene);
        Assert.DoesNotContain("ssao_enabled = true", scene);
        Assert.Equal(6, DreamEscapeState.CurrentContentVersion);
    }

    [Fact]
    public void MusicV2ExportsItsActualLayoutAndPersistentStateAnchors()
    {
        const string asset = "assets/generated/dream_escape/3d/music-room-art-pass-v2";
        var bytes = File.ReadAllBytes(ProjectPath(asset + ".glb"));
        Assert.Equal(0x46546C67u, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 4));
        using var document = JsonDocument.Parse(bytes.AsMemory(20, BitConverter.ToInt32(bytes, 12)));
        var root = document.RootElement;
        var nodes = root.GetProperty("nodes").EnumerateArray().ToArray();
        var byName = nodes.Where(node => node.TryGetProperty("name", out _)).ToDictionary(node => node.GetProperty("name").GetString()!);
        foreach (var name in new[] { "MusicRoom", "Room_Floor", "Room_Ceiling", "Wall_West", "WindowGlass", "EquipmentWalls",
            "Door_West_Hinge", "EquipmentDoorHinge", "EquipmentDoorGlass", "DoorNumberPlate", "Piano", "PianoBench", "CalibrationInstruments",
            "Score_Revealed", "Metronome_Weight", "Metronome_Solved", "Calibration_Solved", "Roll_Installed", "Acoustic_Solved", "Photo_Solved",
            "Pickup_Mallet", "Pickup_Cone", "Pickup_Weight", "Pickup_Roll", "Pickup_Wax", "FrontWaveTraces", "SideWaveTrace" })
            Assert.Contains(name, byName.Keys);
        Assert.Equal(6, byName.Keys.Count(name => Regex.IsMatch(name, "^Chair0[1-6]$")));
        Assert.DoesNotContain(byName.Keys, name => Regex.IsMatch(name, "(?i)caelus|protagonist|armature|xiaoyu"));
        Assert.False(root.TryGetProperty("skins", out _));
        Assert.False(root.TryGetProperty("animations", out _));
        Assert.False(root.TryGetProperty("cameras", out _));
        Assert.DoesNotMatch("\"texCoord\"\\s*:\\s*-", root.GetRawText());
        using var manifest = JsonDocument.Parse(File.ReadAllText(ProjectPath(asset + "-manifest.json")));
        var data = manifest.RootElement;
        Assert.False(data.GetProperty("graybox").GetBoolean());
        Assert.Equal(2, data.GetProperty("layoutVersion").GetInt32());
        Assert.Equal(Sha256File(ProjectPath("scripts/presentation/MusicRoomLayout3D.cs")), data.GetProperty("layoutSha256").GetString());
        Assert.Equal(Sha256File(ProjectPath(asset + ".glb")), data.GetProperty("runtimeGlbSha256").GetString());
        Assert.Equal(Sha256File(ProjectPath(data.GetProperty("sourceFile").GetString()!)), data.GetProperty("sourceSha256").GetString());
        var geometry = data.GetProperty("geometry");
        var accessors = root.GetProperty("accessors").EnumerateArray().ToArray();
        var meshes = root.GetProperty("meshes").EnumerateArray().ToArray();
        var triangles = meshes.Select(mesh => mesh.GetProperty("primitives").EnumerateArray()
            .Sum(p => accessors[p.GetProperty("indices").GetInt32()].GetProperty("count").GetInt32() / 3)).ToArray();
        Assert.Equal(geometry.GetProperty("triangleCount").GetInt32(), nodes.Where(n => n.TryGetProperty("mesh", out _)).Sum(n => triangles[n.GetProperty("mesh").GetInt32()]));
        Assert.Equal(geometry.GetProperty("meshObjectCount").GetInt32(), nodes.Count(n => n.TryGetProperty("mesh", out _)));
        Assert.Equal(geometry.GetProperty("materialCount").GetInt32(), root.GetProperty("materials").GetArrayLength());
        foreach (var (name, key) in new[] { ("Door_West_Hinge", "ExitHinge"), ("EquipmentDoorHinge", "EquipmentHinge") })
        {
            var actual = byName[name].GetProperty("translation").EnumerateArray().Select(n => n.GetSingle()).ToArray();
            var expected = data.GetProperty("layout").GetProperty(key).EnumerateArray().Select(n => n.GetSingle()).ToArray();
            for (var i = 0; i < 3; i++) Assert.InRange(Math.Abs(actual[i] - expected[i]), 0, .002f);
        }
        var floorMesh = meshes[byName["Room_Floor"].GetProperty("mesh").GetInt32()];
        var floorPositions = accessors[floorMesh.GetProperty("primitives")[0].GetProperty("attributes").GetProperty("POSITION").GetInt32()];
        var min = floorPositions.GetProperty("min").EnumerateArray().Select(n => n.GetSingle()).ToArray();
        var max = floorPositions.GetProperty("max").EnumerateArray().Select(n => n.GetSingle()).ToArray();
        Assert.InRange(max[0] - min[0], 11.999f, 12.001f);
        Assert.InRange(max[2] - min[2], 9.999f, 10.001f);
        var scene = File.ReadAllText(ProjectPath("scenes/dream/DreamMusicRoom.tscn"));
        Assert.Contains("music-room-art-pass-v2.glb", scene);
        Assert.Contains("ambient_light_source = 2", scene);
        Assert.DoesNotContain("ssao_enabled = true", scene);
        Assert.Equal(6, DreamEscapeState.CurrentContentVersion);
    }

    [Fact]
    public void ManorV3ExportsItsActualLayoutAndOnlyEnvironmentAssets()
    {
        var bytes = File.ReadAllBytes(ProjectPath("assets/generated/dream_escape/3d/dream-manor-hall-v3.glb"));
        Assert.Equal(0x46546C67u, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 4));
        using var document = JsonDocument.Parse(bytes.AsMemory(20, BitConverter.ToInt32(bytes, 12)));
        var root = document.RootElement;
        var nodes = root.GetProperty("nodes").EnumerateArray().ToArray();
        var byName = nodes.Where(node => node.TryGetProperty("name", out _))
            .ToDictionary(node => node.GetProperty("name").GetString()!);
        foreach (var anchor in new[] { "DreamManorHall", "Room_Floor", "Room_Ceiling", "V3_PortalJoinery",
            "Door_Front_Hinge_L", "Door_Front_Hinge_R", "Door_Room1_Hinge", "Door_Room2_Hinge",
            "Door_Room3_Hinge", "Door_Room4_Hinge_L", "Door_Room4_Hinge_R", "ReservedModule_A", "ReservedModule_B",
            "V3_StairStructure", "V3_StairScreen", "V3_Fireplace", "V3_ChairSouth", "V3_ChairNorth" })
            Assert.Contains(anchor, byName.Keys);
        Assert.DoesNotContain(byName.Keys, name => name.Contains("Protagonist", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Caelus", StringComparison.OrdinalIgnoreCase) || name.Contains("Armature", StringComparison.OrdinalIgnoreCase));
        Assert.False(root.TryGetProperty("skins", out _));
        Assert.False(root.TryGetProperty("animations", out _));
        Assert.False(root.TryGetProperty("cameras", out _));
        Assert.DoesNotMatch("\"texCoord\"\\s*:\\s*-", root.GetRawText());

        using var manifest = JsonDocument.Parse(File.ReadAllText(ProjectPath("assets/generated/dream_escape/3d/dream-manor-hall-v3-manifest.json")));
        var data = manifest.RootElement;
        var geometry = data.GetProperty("geometry");
        var accessors = root.GetProperty("accessors").EnumerateArray().ToArray();
        var meshes = root.GetProperty("meshes").EnumerateArray().ToArray();
        var trianglesByMesh = meshes.Select(mesh => mesh.GetProperty("primitives").EnumerateArray()
            .Sum(primitive => accessors[primitive.GetProperty("indices").GetInt32()].GetProperty("count").GetInt32() / 3)).ToArray();
        Assert.Equal(geometry.GetProperty("triangleCount").GetInt32(), nodes.Where(node => node.TryGetProperty("mesh", out _))
            .Sum(node => trianglesByMesh[node.GetProperty("mesh").GetInt32()]));
        Assert.Equal(geometry.GetProperty("meshObjectCount").GetInt32(), nodes.Count(node => node.TryGetProperty("mesh", out _)));
        Assert.Equal(geometry.GetProperty("materialCount").GetInt32(), root.GetProperty("materials").GetArrayLength());
        Assert.Equal(3, data.GetProperty("layoutVersion").GetInt32());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ProjectPath("scripts/presentation/MansionLayout3D.cs")))).ToLowerInvariant(),
            data.GetProperty("layoutSha256").GetString());
        var expected = new Dictionary<string, float[]>
        {
            ["Door_Front_Root"] = [0, 0, -8], ["Door_Room1_Root"] = [-3.5f, 0, 2.4f],
            ["Door_Room2_Root"] = [3.5f, 0, 2.4f], ["Door_Room3_Root"] = [-3.5f, 0, 8.8f],
            ["Door_Room4_Root"] = [0, 0, 12]
        };
        foreach (var (name, position) in expected)
        {
            var actual = byName[name].GetProperty("translation").EnumerateArray().Select(n => n.GetSingle()).ToArray();
            for (var i = 0; i < 3; i++) Assert.InRange(Math.Abs(actual[i] - position[i]), 0, .002f);
        }
        var scene = File.ReadAllText(ProjectPath("scenes/dream/DreamCorridor.tscn"));
        Assert.Contains("dream-manor-hall-v3.glb", scene);
        Assert.DoesNotContain("dream-manor-exterior", scene);
        Assert.DoesNotContain("ssao_enabled = true", scene);
        Assert.Equal(6, DreamEscapeState.CurrentContentVersion);
    }

    [Fact]
    public void ClassroomV6ExportsSharedDesksAndPreservesStateAnchors()
    {
        var bytes = File.ReadAllBytes(ProjectPath("assets/generated/dream_escape/3d/classroom-art-pass-v6.glb"));
        Assert.Equal(0x46546C67u, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 4));
        using var document = JsonDocument.Parse(bytes.AsMemory(20, BitConverter.ToInt32(bytes, 12)));
        var nodes = document.RootElement.GetProperty("nodes").EnumerateArray()
            .Where(node => node.TryGetProperty("name", out _))
            .ToDictionary(node => node.GetProperty("name").GetString()!);
        Assert.Equal(12, nodes.Keys.Count(name => name.StartsWith("StudentDeskGroup_", StringComparison.Ordinal)));
        var meshIndices = Enumerable.Range(1, 12).Select(index =>
            nodes[$"StudentDeskArt_{index:00}"].GetProperty("mesh").GetInt32()).Distinct();
        Assert.Single(meshIndices);
        foreach (var anchor in new[] { "Door_East_Hinge", "WindowCache_Closed", "WindowCache_Open",
            "ClockHand_Pickup", "Blackboard_Blank", "Blackboard_Revealed", "Clock_Missing", "Clock_HandOnly",
            "Clock_KeyOnly", "Clock_Ready", "Clock_Restored", "Roster_Missing", "Roster_Inserted", "Roster_Aligned",
            "TeacherDesk_Closed", "TeacherDesk_Open", "MapCabinet_Closed", "MapCabinet_Open", "V6_ScheduleStand" })
            Assert.Contains(anchor, nodes.Keys);
        Assert.DoesNotContain("ProtagonistRoot", nodes.Keys);
        var meshes = document.RootElement.GetProperty("meshes").EnumerateArray().ToArray();
        var accessors = document.RootElement.GetProperty("accessors").EnumerateArray().ToArray();
        var trianglesByMesh = meshes.Select(mesh => mesh.GetProperty("primitives").EnumerateArray()
            .Sum(primitive => accessors[primitive.GetProperty("indices").GetInt32()].GetProperty("count").GetInt32() / 3)).ToArray();
        var actualTriangles = nodes.Values.Where(node => node.TryGetProperty("mesh", out _))
            .Sum(node => trianglesByMesh[node.GetProperty("mesh").GetInt32()]);
        using var manifest = JsonDocument.Parse(File.ReadAllText(ProjectPath("assets/generated/dream_escape/3d/classroom-art-pass-v6-manifest.json")));
        var geometry = manifest.RootElement.GetProperty("geometry");
        Assert.Equal(geometry.GetProperty("triangleCount").GetInt32(), actualTriangles);
        Assert.Equal(geometry.GetProperty("meshObjectCount").GetInt32(), nodes.Values.Count(node => node.TryGetProperty("mesh", out _)));
        // A missing source UV previously produced an invalid glTF texCoord=-1 material variant.
        Assert.DoesNotMatch("\"texCoord\"\\s*:\\s*-", document.RootElement.GetRawText());
        var positions = Enumerable.Range(1, 12).Select(index =>
            nodes[$"StudentDeskGroup_{index:00}"].GetProperty("translation").EnumerateArray()
                .Select(value => value.GetSingle()).ToArray()).ToArray();
        Assert.All(positions, position => Assert.Equal(0f, position[1]));
        Assert.True(positions.Min(p => Math.Abs(p[0])) * 2f - .82f >= 2.4f);
        Assert.True(5.7f - positions.Max(p => Math.Abs(p[0])) - .41f >= 1.2f);
        Assert.True(4.2f - positions.Max(p => p[2]) - .71f >= 1.2f);
        Assert.Contains("DREAM_V6_WORLD_BLACKBOARD_ORDER", ReadLocalizationRows().Select(row => row.Key));
    }

    [Fact]
    public void RemadeUiReferencesHaveNonemptyChineseTranslations()
    {
        var translations = ReadLocalizationRows().ToDictionary(row => row.Key, row => row.Value);
        var files = Directory.GetFiles(ProjectPath("scripts/presentation"), "*.cs");
        var keys = files.SelectMany(file => Regex.Matches(File.ReadAllText(file), "\"(UI_[A-Z0-9_]+)\"")
            .Select(match => match.Groups[1].Value)).Distinct().ToArray();
        Assert.True(keys.Length >= 70);
        foreach (var key in keys) Assert.True(translations.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text), key);
    }

    [Fact]
    public void PrologueV6HasEveryRecordPromptHintAndOnlyAtomicCompletionStories()
    {
        var localization = ReadLocalizationRows().ToDictionary(row => row.Key, row => row.Value);
        foreach (Match match in Regex.Matches(File.ReadAllText(ProjectPath("src/TownRpg.Core/Services/DreamEscapeService.cs")), "\"(DREAM_FEEDBACK_[A-Z0-9_]+)\""))
            Assert.Contains(match.Groups[1].Value, localization.Keys);
        foreach (var id in DreamEscapeIds.Clues) Assert.Contains(DreamPrologueCatalog.TextKey(id), localization.Keys);
        foreach (var id in DreamEscapeIds.Puzzles)
        {
            Assert.Contains(DreamPrologueCatalog.TextKey(id) + "_NAME", localization.Keys);
            Assert.Contains(DreamPrologueCatalog.TextKey(id) + "_PROMPT", localization.Keys);
            for (var level = 1; level <= 3; level++)
                Assert.Contains("DREAM_HINT_" + id.Replace("dream.", "", StringComparison.Ordinal).Replace('.', '_').ToUpperInvariant() + "_" + level, localization.Keys);
        }
        var expected = new[] { "intro" }.Concat(DreamPrologueCatalog.Groups.Select(group => group.Id))
            .Select(DreamPrologueCatalog.EventId).ToHashSet();
        var events = LoadCatalog<EventCatalog>("data/events.json").Events.Where(e => e.Id.StartsWith("dream.prologue.")).ToArray();
        Assert.True(expected.SetEquals(events.Select(e => e.Id)));
        foreach (var entry in events)
        {
            var path = ResolveResourcePath(entry.DialoguePath);
            var command = Assert.Single(ReadMutationCalls(path));
            Assert.Equal("complete_event", command.Command);
            Assert.Equal(entry.Id, Assert.Single(command.Arguments));
            Assert.DoesNotContain(File.ReadAllLines(path), line => line.TrimStart().StartsWith("- "));
        }
        foreach (var path in Directory.EnumerateFiles(ProjectPath("scripts/presentation"), "Dream*.cs")
                     .Concat(Directory.EnumerateFiles(ProjectPath("scenes/dream"), "*.tscn")))
            foreach (Match match in Regex.Matches(File.ReadAllText(path), "\"(DREAM_V6_[A-Z0-9_]+)\""))
                Assert.Contains(match.Groups[1].Value, localization.Keys);
    }

    [Fact]
    public void CharacterThoughtsAndActiveHintsAreLocalizedWithoutCompleteAnswers()
    {
        var texts = ReadLocalizationRows().ToDictionary(row => row.Key, row => row.Value);
        foreach (var key in DreamThoughtRules.AllTextKeys)
            Assert.True(texts.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text), key);
        foreach (var id in DreamEscapeIds.Puzzles)
        {
            var prefix = "DREAM_HINT_" + id.Replace("dream.", "", StringComparison.Ordinal).Replace('.', '_').ToUpperInvariant();
            var hints = Enumerable.Range(1, 3).Select(level => texts[prefix + "_" + level]).ToArray();
            Assert.All(hints, text => Assert.StartsWith("林晓雨：“", text));
            var prose = Regex.Replace(texts[DreamPrologueCatalog.TextKey(id) + "_PROMPT"] + string.Concat(hints), @"[\s、，/＝=→]+", "");
            foreach (var answer in new[] { "372", "1423", "2413", "3142", "C1A4D2B3", "上下中", "木鱼钢琴三角铁手铃" })
                Assert.DoesNotContain(answer, prose);
        }
        foreach (var group in DreamThoughtRules.AllTextKeys.GroupBy(key => key[..^1]))
            Assert.Equal(2, group.Select(key => texts[key]).Distinct().Count());
    }

    private static readonly Lazy<string> ProjectRoot = new(FindProjectRoot);

    private static readonly Regex MutationRegex = new(
        @"Narrative\.Execute\((?<arguments>[^)]*)\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StringLiteralRegex = new(
        "\"(?<value>[^\"]*)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StaticIdRegex = new(
        @"\[ID:(?<id>[A-Z0-9_]+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TitleRegex = new(
        @"^\s*~\s+(?<title>[A-Za-z0-9_]+)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex JumpRegex = new(
        @"=>\s*(?<target>[A-Za-z0-9_!]+)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void ContentCatalogs_DeserializeWithCurrentSchema()
    {
        var characters = LoadCatalog<CharacterCatalog>("data/characters.json");
        var schedules = LoadCatalog<ScheduleCatalog>("data/schedules.json");
        var events = LoadCatalog<EventCatalog>("data/events.json");
        var items = LoadCatalog<ItemCatalog>("data/items.json");

        Assert.Equal(ExpectedSchemaVersion, characters.SchemaVersion);
        Assert.Equal(ExpectedSchemaVersion, schedules.SchemaVersion);
        Assert.Equal(ExpectedSchemaVersion, events.SchemaVersion);
        Assert.Equal(ExpectedSchemaVersion, items.SchemaVersion);
        Assert.NotEmpty(characters.Characters);
        Assert.NotEmpty(schedules.Entries);
        Assert.NotEmpty(events.Events);
        Assert.Equal(31, items.Items.Count);

        AssertUnique(characters.Characters.Select(character => character.Id), "character ID");
        AssertUnique(events.Events.Select(definition => definition.Id), "event ID");
        AssertUnique(items.Items.Select(item => item.Id), "item ID");
        Assert.True(DreamEscapeIds.Items.SetEquals(items.Items.Select(item => item.Id)));
        Assert.All(items.Items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.NameKey));
            Assert.False(string.IsNullOrWhiteSpace(item.DescriptionKey));
            Assert.StartsWith("res://assets/generated/dream_escape/", item.IconPath, StringComparison.Ordinal);
            Assert.True(File.Exists(ResolveResourcePath(item.IconPath)), $"Missing item icon '{item.IconPath}'.");
        });

        Assert.All(characters.Characters, character =>
        {
            Assert.False(string.IsNullOrWhiteSpace(character.NameKey));
            Assert.False(string.IsNullOrWhiteSpace(character.RoleKey));
            Assert.Matches("^#[0-9A-Fa-f]{6}$", character.ColorHex);
        });
    }

    [Fact]
    public void CharacterAndScheduleReferences_AreValidAndSchedulesDoNotOverlap()
    {
        var characters = LoadCatalog<CharacterCatalog>("data/characters.json");
        var schedules = LoadCatalog<ScheduleCatalog>("data/schedules.json");
        var characterIds = characters.Characters
            .Select(character => character.Id)
            .ToHashSet(StringComparer.Ordinal);
        var errors = new List<string>();

        foreach (var entry in schedules.Entries)
        {
            if (!characterIds.Contains(entry.NpcId))
            {
                errors.Add($"Schedule references unknown character '{entry.NpcId}'.");
            }
        }

        errors.AddRange(new ScheduleService(schedules.Entries).Validate());

        foreach (var file in GetDialogueFiles())
        {
            foreach (var mutation in ReadMutationCalls(file))
            {
                IEnumerable<string> referencedCharacters = mutation.Command switch
                {
                    "change_affinity" or "change_trust" or "remember"
                        when mutation.Arguments.Count >= 2 => mutation.Arguments.Take(2),
                    "create_promise" when mutation.Arguments.Count >= 3 =>
                        mutation.Arguments.Skip(1).Take(2),
                    _ => []
                };

                foreach (var characterId in referencedCharacters)
                {
                    if (!characterIds.Contains(characterId))
                    {
                        errors.Add(
                            $"{RelativePath(file)}:{mutation.LineNumber} references unknown character " +
                            $"'{characterId}'.");
                    }
                }
            }
        }

        AssertNoErrors(errors);
    }

    [Fact]
    public void CoreNpcs_HaveRecurringSchedulesAfterSecondDay()
    {
        var schedules = LoadCatalog<ScheduleCatalog>("data/schedules.json");
        var scheduleService = new ScheduleService(schedules.Entries);
        var world = new WorldState
        {
            Day = 3,
            MinuteOfDay = 600
        };
        world.AddFact("slice.d1.awake");

        var npcIds = new[]
        {
            CharacterIds.AbilityHolder,
            CharacterIds.KnowledgeFriend,
            CharacterIds.TokenHolder,
            CharacterIds.Shopkeeper
        };

        Assert.All(npcIds, npcId => Assert.NotNull(scheduleService.Current(npcId, world)));
    }

    [Fact]
    public void Events_ReferenceExistingDialogueAndCompleteExactlyOnce()
    {
        var events = LoadCatalog<EventCatalog>("data/events.json");
        var eventIds = events.Events
            .Select(definition => definition.Id)
            .ToHashSet(StringComparer.Ordinal);
        var referencedDialogue = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var completionIds = new List<string>();
        var errors = new List<string>();

        foreach (var definition in events.Events)
        {
            if (!definition.DialoguePath.StartsWith("res://", StringComparison.Ordinal))
            {
                errors.Add($"Event '{definition.Id}' has a non-resource dialogue path.");
                continue;
            }

            var dialoguePath = ResolveResourcePath(definition.DialoguePath);
            referencedDialogue.Add(Path.GetFullPath(dialoguePath));
            if (!File.Exists(dialoguePath))
            {
                errors.Add(
                    $"Event '{definition.Id}' references missing dialogue " +
                    $"'{definition.DialoguePath}'.");
                continue;
            }

            var titles = ReadTitles(dialoguePath, errors);
            if (!titles.Contains(definition.StartTitle))
            {
                errors.Add(
                    $"Event '{definition.Id}' starts at missing title " +
                    $"'{definition.StartTitle}'.");
            }

            var ownCompletions = ReadMutationCalls(dialoguePath)
                .Where(mutation => mutation.Command == "complete_event")
                .Select(mutation => mutation.Arguments.SingleOrDefault())
                .Where(id => id is not null)
                .Cast<string>()
                .ToArray();
            completionIds.AddRange(ownCompletions);

            if (ownCompletions.Count(id => id == definition.Id) != 1)
            {
                errors.Add(
                    $"Event '{definition.Id}' must complete itself exactly once; found " +
                    $"{ownCompletions.Count(id => id == definition.Id)} matching commands.");
            }
        }

        foreach (var completionId in completionIds.Where(id => !eventIds.Contains(id)))
        {
            errors.Add($"Dialogue completes unknown event '{completionId}'.");
        }

        var actualDialogue = GetDialogueFiles()
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var orphan in actualDialogue.Except(referencedDialogue))
        {
            errors.Add($"Dialogue '{RelativePath(orphan)}' is not referenced by an event.");
        }

        AssertNoErrors(errors);
    }

    [Fact]
    public void DialogueScripts_UseWhitelistedCommandsWithValidArgumentsAndJumps()
    {
        var errors = new List<string>();
        var allowedCommands = NarrativeCommandService.AllowedCommands;

        var requiredArgumentCounts = NarrativeCommandService.AllowedArgumentCounts;
        if (!requiredArgumentCounts.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(allowedCommands))
        {
            errors.Add(
                "The content-test argument contract is out of sync with " +
                "NarrativeCommandService.AllowedCommands.");
        }

        foreach (var file in GetDialogueFiles())
        {
            foreach (var mutation in ReadMutationCalls(file))
            {
                if (!allowedCommands.Contains(mutation.Command))
                {
                    errors.Add(
                        $"{RelativePath(file)}:{mutation.LineNumber} uses non-whitelisted command " +
                        $"'{mutation.Command}'.");
                    continue;
                }

                if (requiredArgumentCounts.TryGetValue(mutation.Command, out var expected) &&
                    mutation.Arguments.Count != expected)
                {
                    errors.Add(
                        $"{RelativePath(file)}:{mutation.LineNumber} command '{mutation.Command}' " +
                        $"expects {expected} arguments but has {mutation.Arguments.Count}.");
                }
            }

            var titles = ReadTitles(file, errors);
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (!line.Contains("=>", StringComparison.Ordinal))
                {
                    continue;
                }

                var match = JumpRegex.Match(line);
                if (!match.Success)
                {
                    errors.Add(
                        $"{RelativePath(file)}:{index + 1} has a non-literal or malformed jump.");
                    continue;
                }

                var target = match.Groups["target"].Value;
                if (target is not ("END" or "END!") && !titles.Contains(target))
                {
                    errors.Add(
                        $"{RelativePath(file)}:{index + 1} jumps to missing title '{target}'.");
                }
            }
        }

        AssertNoErrors(errors);
    }

    [Fact]
    public void DialogueFallbackTextMatchesItsChineseLocalization()
    {
        var translations = ReadLocalizationRows().ToDictionary(row => row.Key, row => row.Value);
        foreach (var file in GetDialogueFiles())
        foreach (var line in File.ReadLines(file).Where(IsPrintableDialogueLine))
        {
            var id = StaticIdRegex.Match(line);
            if (!id.Success) continue; // The ID contract is checked separately.
            var text = line[..id.Index].Trim();
            text = text.StartsWith("- ", StringComparison.Ordinal) ? text[2..] : text[(text.IndexOf(':') + 1)..].Trim();
            text = Regex.Replace(text, @"\[#.*?\]", "").Trim();
            Assert.True(translations[id.Groups["id"].Value] == text, $"Fallback/translation mismatch at {RelativePath(file)}: {id.Value}");
        }
    }

    [Fact]
    public void StaticIdsAndReferencedLocalizationKeys_AreUniqueAndTranslated()
    {
        var characters = LoadCatalog<CharacterCatalog>("data/characters.json");
        var schedules = LoadCatalog<ScheduleCatalog>("data/schedules.json");
        var items = LoadCatalog<ItemCatalog>("data/items.json");
        var localizationRows = ReadLocalizationRows();
        var errors = new List<string>();
        var staticIds = new List<string>();

        foreach (var duplicate in localizationRows
                     .GroupBy(row => row.Key, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Localization key '{duplicate.Key}' is duplicated.");
        }

        foreach (var row in localizationRows)
        {
            if (string.IsNullOrWhiteSpace(row.Key) || string.IsNullOrWhiteSpace(row.Value))
            {
                errors.Add("Localization contains an empty key or translation.");
            }
        }

        foreach (var file in GetDialogueFiles())
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                var idsOnLine = StaticIdRegex.Matches(line)
                    .Select(match => match.Groups["id"].Value)
                    .ToArray();
                staticIds.AddRange(idsOnLine);

                if (IsPrintableDialogueLine(line) && idsOnLine.Length != 1)
                {
                    errors.Add(
                        $"{RelativePath(file)}:{index + 1} must contain exactly one static ID; " +
                        $"found {idsOnLine.Length}.");
                }
            }
        }

        foreach (var duplicate in staticIds
                     .GroupBy(id => id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Dialogue static ID '{duplicate.Key}' is duplicated.");
        }

        var requiredKeys = staticIds.ToHashSet(StringComparer.Ordinal);
        requiredKeys.UnionWith(characters.Characters.Select(character => character.NameKey));
        requiredKeys.UnionWith(characters.Characters.Select(character => character.RoleKey));
        requiredKeys.UnionWith(schedules.Entries.Select(entry => entry.ActivityKey));
        requiredKeys.UnionWith(items.Items.Select(item => item.NameKey));
        requiredKeys.UnionWith(items.Items.Select(item => item.DescriptionKey));
        requiredKeys.UnionWith(ReadEventDisplayNameKeys());

        foreach (var mutation in GetDialogueFiles().SelectMany(ReadMutationCalls))
        {
            if (mutation.Command == "timeline" && mutation.Arguments.Count == 1)
            {
                requiredKeys.Add(mutation.Arguments[0]);
            }
            else if (mutation.Command == "create_promise" && mutation.Arguments.Count == 4)
            {
                requiredKeys.Add(mutation.Arguments[3]);
            }
        }

        var localizationKeys = localizationRows
            .Select(row => row.Key)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var missing in requiredKeys.Except(localizationKeys).Order(StringComparer.Ordinal))
        {
            errors.Add($"Localization key '{missing}' is required but missing.");
        }

        AssertNoErrors(errors);
    }

    [Fact]
    public void GodotScenesAndConfiguredResourcePaths_Exist()
    {
        var errors = new List<string>();
        var requiredResources = new[]
        {
            "assets/fonts/source-han-sans/SourceHanSansSC-Regular.otf",
            "assets/fonts/source-han-sans/SourceHanSansSC-Medium.otf",
            "assets/fonts/source-han-sans/LICENSE.txt",
            "scenes/testing/CinematicUiProbe.tscn",
            "scenes/app/Main.tscn",
            "scenes/world/OldHouse.tscn",
            "scenes/world/StationQuarter.tscn",
            "scenes/dream/DreamEscape.tscn",
            "scenes/dream/DreamCorridor.tscn",
            "scenes/dream/DreamClassroom.tscn",
            "scenes/dream/DreamMusicRoom.tscn",
            "scenes/dream/DreamArchiveRoom.tscn",
            "scenes/dream/DreamAuditorium.tscn",
            "scenes/testing/GodotSmokeRunner.tscn",
            "scripts/presentation/MainController.cs",
            "scripts/application/GameSession.cs",
            "scripts/testing/GodotSmokeRunner.cs",
            "scripts/presentation/DreamEscapeController.cs",
            "scripts/presentation/DreamInventoryPanel.cs",
            "scripts/presentation/DreamPuzzlePanel.cs",
            "scripts/presentation/DreamMansionController3D.cs",
            "scripts/presentation/DreamMansionVisual3D.cs",
            "scripts/presentation/DreamMansionCoordinateMapper3D.cs",
            "scripts/presentation/DreamAreaSpawnCatalog.cs",
            "scripts/tools/build_dream_manor_3d.py",
            "scripts/tools/build_dream_manor_textures.py",
            "scripts/tools/expand_environment_cinematic_full.py",
            "scripts/tools/export_environment_cinematic_samples.py",
            "data/items.json",
            "assets/generated/dream_escape/item-pin.png",
            "assets/generated/dream_escape/item-chalk.png",
            "assets/generated/dream_escape/item-room-two-key.png",
            "assets/generated/dream_escape/item-maintenance-key.png",
            "assets/generated/dream_escape/item-red-filter.png",
            "assets/generated/dream_escape/item-class-photo.png",
            "assets/generated/dream_escape/item-tape-reel.png",
            "assets/generated/dream_escape/item-recorder-crank.png",
            "assets/generated/dream_escape/lin-xiaoyu-neutral.png",
            "assets/generated/dream_escape/lin-xiaoyu-walk-4x3.png",
            "assets/generated/dream_escape/class-photo-filter-closeup.png",
            "assets/generated/dream_escape/recorder-closeup.png",
            "assets/generated/dream_escape/auditorium-control-board.png",
            "assets/generated/dream_escape/3d/dream-manor-hall-v1.glb",
            "assets/generated/dream_escape/3d/dream-manor-hall-v2.glb",
            "assets/generated/dream_escape/3d/dream-manor-hall-v3.glb",
            "assets/generated/dream_escape/3d/dream-manor-hall-v3-manifest.json",
            "art/blender/dream_manor/dream-manor-hall-v3.blend",
            "scripts/presentation/MansionLayout3D.cs",
            "scripts/tools/remodel_manor_v3.py",
            "assets/generated/dream_escape/3d/dream-manor-exterior-v1.glb",
            "assets/generated/dream_escape/3d/dream-manor-v1-manifest.json",
            "assets/generated/dream_escape/3d/dream-manor-hall-v2-manifest.json",
            "assets/generated/dream_escape/3d/classroom-art-pass-v6.glb",
            "assets/generated/dream_escape/3d/classroom-art-pass-v6-manifest.json",
            "assets/generated/dream_escape/3d/dream-archive-room-v1.glb",
            "assets/generated/dream_escape/3d/dream-archive-room-v1-manifest.json",
            "assets/generated/dream_escape/3d/dream-auditorium-v1.glb",
            "assets/generated/dream_escape/3d/dream-auditorium-v1-manifest.json",
            "art/blender/dream_late_rooms/dream-archive-room-v1.blend",
            "art/blender/dream_late_rooms/dream-auditorium-v1.blend",
            "art/blender/dream_manor/dream-manor-v1.blend",
            "art/blender/dream_manor/textures-v1/dream-manor-textures-v1-manifest.json",
            "art/concepts/dream_manor/manor-floor-plan-v1.png",
            "art/concepts/dream_manor/manor-concept-board-v2.png",
            "assets/generated/title-town-dusk.png",
            "assets/generated/provenance.json",
            "localization/zh_CN.csv",
            "localization/zh_CN.zh_CN.translation"
        };
        foreach (var resource in requiredResources.Where(resource => !File.Exists(ProjectPath(resource))))
        {
            errors.Add($"Required Godot resource '{resource}' is missing.");
        }

        var manorManifestPath = ProjectPath(
            "assets/generated/dream_escape/3d/dream-manor-v1-manifest.json");
        if (File.Exists(manorManifestPath))
        {
            try
            {
                using var manorManifest = JsonDocument.Parse(File.ReadAllText(manorManifestPath));
                var root = manorManifest.RootElement;
                if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
                    root.GetProperty("currentRoomDoors").GetArrayLength() != 4 ||
                    root.GetProperty("reservedExpansionModules").GetArrayLength() != 2 ||
                    root.GetProperty("exportProfile").GetProperty("runtimeLoadsExterior").GetBoolean())
                {
                    errors.Add(
                        "Dream manor manifest must declare schema v1, four current room doors, " +
                        "two reserved modules and a runtime-excluded exterior.");
                }

                ValidateManorBinary(
                    root,
                    "hallRuntimeFile",
                    "hallRuntimeBytes",
                    "hallRuntimeSha256",
                    maximumBytes: 55L * 1024L * 1024L,
                    errors);
                ValidateManorBinary(
                    root,
                    "exteriorReviewFile",
                    "exteriorReviewBytes",
                    "exteriorReviewSha256",
                    maximumBytes: 35L * 1024L * 1024L,
                    errors);
                if (root.GetProperty("hall").GetProperty("triangleCount").GetInt32() > 120_000 ||
                    root.GetProperty("hall").GetProperty("meshObjectCount").GetInt32() > 160 ||
                    root.GetProperty("hall").GetProperty("materialCount").GetInt32() > 18 ||
                    root.GetProperty("exterior").GetProperty("triangleCount").GetInt32() > 80_000)
                {
                    errors.Add("Dream manor manifest exceeds an adopted mesh, material or triangle budget.");
                }

                foreach (var texture in root.GetProperty("textureFiles").EnumerateObject())
                {
                    var texturePath = ProjectPath($"art/blender/dream_manor/textures-v1/{texture.Name}");
                    if (!File.Exists(texturePath))
                    {
                        errors.Add($"Dream manor source texture '{texture.Name}' is missing.");
                    }
                    else if (!Sha256File(texturePath).Equals(
                                 texture.Value.GetString(),
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"Dream manor source texture '{texture.Name}' does not match its manifest hash.");
                    }
                }

                ValidateManorImportArtifacts(root, errors);
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                errors.Add($"Dream manor manifest is malformed: {exception.Message}");
            }
        }

        ValidateCinematicRuntimeManifest(
            "assets/generated/dream_escape/3d/classroom-art-pass-v6-manifest.json",
            expectedRuntimeFile: "assets/generated/dream_escape/3d/classroom-art-pass-v6.glb",
            maximumTriangles: 150_000,
            maximumVisibleMeshes: 200,
            maximumMaterials: 24,
            maximumBytes: 60L * 1024L * 1024L,
            errors);
        ValidateLateRoomRuntimeManifest(
            "assets/generated/dream_escape/3d/dream-archive-room-v1-manifest.json",
            expectedRuntimeFile: "assets/generated/dream_escape/3d/dream-archive-room-v1.glb",
            maximumMeshes: 130,
            maximumTriangles: 20_000,
            maximumBytes: 60L * 1024L * 1024L,
            errors);
        ValidateLateRoomRuntimeManifest(
            "assets/generated/dream_escape/3d/dream-auditorium-v1-manifest.json",
            expectedRuntimeFile: "assets/generated/dream_escape/3d/dream-auditorium-v1.glb",
            maximumMeshes: 170,
            maximumTriangles: 24_000,
            maximumBytes: 60L * 1024L * 1024L,
            errors);
        ValidateCinematicRuntimeManifest(
            "assets/generated/dream_escape/3d/dream-manor-hall-v3-manifest.json",
            expectedRuntimeFile: "assets/generated/dream_escape/3d/dream-manor-hall-v3.glb",
            maximumTriangles: 180_000,
            maximumVisibleMeshes: 220,
            maximumMaterials: 24,
            maximumBytes: 60L * 1024L * 1024L,
            errors);

        ValidateCinematicRuntimeManifest(
            "assets/generated/dream_escape/3d/music-room-art-pass-v2-manifest.json",
            expectedRuntimeFile: "assets/generated/dream_escape/3d/music-room-art-pass-v2.glb",
            maximumTriangles: 180_000,
            maximumVisibleMeshes: 220,
            maximumMaterials: 24,
            maximumBytes: 60L * 1024L * 1024L,
            errors);

        ValidateCinematicRuntimeManifest(
            "assets/generated/dream_escape/3d/dream-archive-room-v2-manifest.json",
            "assets/generated/dream_escape/3d/dream-archive-room-v2.glb",
            180_000, 220, 24, 60L * 1024L * 1024L, errors);
        ValidateCinematicRuntimeManifest(
            "assets/generated/dream_escape/3d/dream-auditorium-room-v2-manifest.json",
            "assets/generated/dream_escape/3d/dream-auditorium-room-v2.glb",
            260_000, 320, 32, 80L * 1024L * 1024L, errors);

        var provenancePath = ProjectPath("assets/generated/provenance.json");
        if (File.Exists(provenancePath))
        {
            try
            {
                using var provenance = JsonDocument.Parse(File.ReadAllText(provenancePath));
                var ids = provenance.RootElement.GetProperty("assets").EnumerateArray()
                    .Select(asset => asset.GetProperty("id").GetString())
                    .Where(id => id is not null)
                    .Cast<string>()
                    .ToHashSet(StringComparer.Ordinal);
                var missingProvenance = new[]
                {
                    "dream_manor_concept_direction_v1",
                    "dream_manor_pbr_textures_v1",
                    "dream_manor_3d_source_v1",
                    "dream_manor_hall_runtime_v1",
                    "dream_manor_exterior_review_v1",
                    "dream_classroom_cinematic_runtime_v6",
                    "dream_manor_hall_cinematic_runtime_v2",
                    "dream_manor_hall_runtime_v3",
                    "dream_archive_room_3d_runtime_v1",
                    "dream_auditorium_3d_runtime_v1",
                    "dream_archive_room_runtime_v2",
                    "dream_auditorium_runtime_v2"
                }.Where(id => !ids.Contains(id)).ToArray();
                if (missingProvenance.Length > 0)
                {
                    errors.Add(
                        $"Dream manor provenance entries are missing: {string.Join(", ", missingProvenance)}.");
                }
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                errors.Add($"Dream manor provenance is malformed: {exception.Message}");
            }
        }

        var project = File.ReadAllText(ProjectPath("project.godot"));
        foreach (var resourcePath in Regex.Matches(project, "res://[A-Za-z0-9_./-]+")
                     .Select(match => match.Value)
                     .Distinct(StringComparer.Ordinal))
        {
            if (!File.Exists(ResolveResourcePath(resourcePath)))
            {
                errors.Add($"project.godot references missing resource '{resourcePath}'.");
            }
        }

        AssertNoErrors(errors);
    }

    private static T LoadCatalog<T>(string relativePath) where T : class =>
        ContentJson.Deserialize<T>(File.ReadAllText(ProjectPath(relativePath)));

    private static IReadOnlyList<string> GetDialogueFiles() =>
        Directory.GetFiles(
            ProjectPath("dialogue/events"),
            "*.dialogue",
            SearchOption.TopDirectoryOnly);

    private static IReadOnlyList<MutationCall> ReadMutationCalls(string file)
    {
        var calls = new List<MutationCall>();
        var lines = File.ReadAllLines(file);
        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].Contains("Narrative.Execute", StringComparison.Ordinal))
            {
                continue;
            }

            var match = MutationRegex.Match(lines[index]);
            if (!match.Success)
            {
                throw new InvalidDataException(
                    $"{RelativePath(file)}:{index + 1} contains a malformed Narrative.Execute call.");
            }

            var literals = StringLiteralRegex.Matches(match.Groups["arguments"].Value)
                .Select(item => item.Groups["value"].Value)
                .ToArray();
            if (literals.Length == 0)
            {
                throw new InvalidDataException(
                    $"{RelativePath(file)}:{index + 1} does not specify a command string.");
            }

            calls.Add(new MutationCall(literals[0], literals.Skip(1).ToArray(), index + 1));
        }

        return calls;
    }

    private static HashSet<string> ReadTitles(string file, ICollection<string> errors)
    {
        var titles = new HashSet<string>(StringComparer.Ordinal);
        var lines = File.ReadAllLines(file);
        for (var index = 0; index < lines.Length; index++)
        {
            var match = TitleRegex.Match(lines[index]);
            if (!match.Success)
            {
                continue;
            }

            var title = match.Groups["title"].Value;
            if (!titles.Add(title))
            {
                errors.Add($"{RelativePath(file)}:{index + 1} duplicates title '{title}'.");
            }
        }

        return titles;
    }

    private static IReadOnlyList<LocalizationRow> ReadLocalizationRows()
    {
        using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(ProjectPath("localization/zh_CN.csv"));
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = false;
        var header = parser.ReadFields();
        if (header is null)
        {
            throw new InvalidDataException("localization/zh_CN.csv is empty.");
        }

        if (header.Length != 2 || header[0] != "keys" || header[1] != "zh_CN")
        {
            throw new InvalidDataException(
                "localization/zh_CN.csv must start with the header 'keys,zh_CN'.");
        }

        var rows = new List<LocalizationRow>();
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields()!;
            if (fields.Length != 2)
            {
                throw new InvalidDataException(
                    $"localization/zh_CN.csv:{parser.LineNumber} must contain exactly two columns.");
            }

            rows.Add(new LocalizationRow(fields[0], fields[1]));
        }

        return rows;
    }

    private static IReadOnlyList<string> ReadEventDisplayNameKeys()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ProjectPath("data/events.json")));
        return document.RootElement
            .GetProperty("events")
            .EnumerateArray()
            .Select(element => element.GetProperty("displayNameKey").GetString())
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Cast<string>()
            .ToArray();
    }

    private static bool IsPrintableDialogueLine(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("- ", StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmed.Length == 0 ||
            trimmed.StartsWith("~", StringComparison.Ordinal) ||
            trimmed.StartsWith("=>", StringComparison.Ordinal) ||
            trimmed.StartsWith("do ", StringComparison.Ordinal) ||
            trimmed.StartsWith("if ", StringComparison.Ordinal) ||
            trimmed.StartsWith("elif ", StringComparison.Ordinal) ||
            trimmed.Equals("else", StringComparison.Ordinal) ||
            trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            return false;
        }

        return trimmed.Contains(':', StringComparison.Ordinal);
    }

    private static string ResolveResourcePath(string resourcePath)
    {
        var relativePath = resourcePath["res://".Length..]
            .Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(ProjectRoot.Value, relativePath));
        var rootPrefix = Path.GetFullPath(ProjectRoot.Value) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Resource path '{resourcePath}' escapes the project directory.");
        }

        return fullPath;
    }

    private static string ProjectPath(string relativePath) =>
        Path.Combine(
            ProjectRoot.Value,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string RelativePath(string path) =>
        Path.GetRelativePath(ProjectRoot.Value, path).Replace(Path.DirectorySeparatorChar, '/');

    private static void ValidateManorBinary(
        JsonElement manifest,
        string pathProperty,
        string bytesProperty,
        string hashProperty,
        long maximumBytes,
        ICollection<string> errors)
    {
        var relativePath = manifest.GetProperty(pathProperty).GetString();
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            errors.Add($"Dream manor manifest property '{pathProperty}' is empty.");
            return;
        }

        var path = ProjectPath(relativePath);
        if (!File.Exists(path))
        {
            errors.Add($"Dream manor binary '{relativePath}' is missing.");
            return;
        }

        var expectedBytes = manifest.GetProperty(bytesProperty).GetInt64();
        var actualBytes = new FileInfo(path).Length;
        if (actualBytes != expectedBytes || actualBytes > maximumBytes)
        {
            errors.Add(
                $"Dream manor binary '{relativePath}' has {actualBytes} bytes; " +
                $"manifest={expectedBytes}, budget={maximumBytes}.");
        }

        var expectedHash = manifest.GetProperty(hashProperty).GetString();
        var actualHash = Sha256File(path);
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"Dream manor binary '{relativePath}' SHA-256 is {actualHash}; expected {expectedHash}.");
        }
    }

    private static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void ValidateCinematicRuntimeManifest(
        string manifestRelativePath,
        string expectedRuntimeFile,
        int maximumTriangles,
        int maximumVisibleMeshes,
        int maximumMaterials,
        long maximumBytes,
        ICollection<string> errors)
    {
        var manifestPath = ProjectPath(manifestRelativePath);
        if (!File.Exists(manifestPath))
        {
            return;
        }

        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = manifest.RootElement;
            var runtimeFile = root.GetProperty("runtimeGlbFile").GetString();
            if (root.GetProperty("schemaVersion").GetInt32() != 2 ||
                root.GetProperty("status").GetString() != "cinematic-full-runtime" ||
                root.GetProperty("sampleOnly").GetBoolean() ||
                !root.GetProperty("runtimeDefault").GetBoolean() ||
                runtimeFile != expectedRuntimeFile)
            {
                errors.Add(
                    $"Cinematic manifest '{manifestRelativePath}' must declare schema v2, " +
                    "cinematic-full-runtime, sampleOnly=false, runtimeDefault=true and the adopted GLB path.");
                return;
            }

            var runtimePath = ProjectPath(runtimeFile);
            if (!File.Exists(runtimePath))
            {
                errors.Add($"Cinematic runtime GLB '{runtimeFile}' is missing.");
                return;
            }

            var expectedBytes = root.GetProperty("runtimeGlbBytes").GetInt64();
            var actualBytes = new FileInfo(runtimePath).Length;
            if (actualBytes != expectedBytes || actualBytes > maximumBytes)
            {
                errors.Add(
                    $"Cinematic runtime GLB '{runtimeFile}' has {actualBytes} bytes; " +
                    $"manifest={expectedBytes}, budget={maximumBytes}.");
            }

            var expectedHash = root.GetProperty("runtimeGlbSha256").GetString();
            var actualHash = Sha256File(runtimePath);
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Cinematic runtime GLB '{runtimeFile}' SHA-256 is {actualHash}; expected {expectedHash}.");
            }

            var geometry = root.GetProperty("geometry");
            if (geometry.GetProperty("triangleCount").GetInt32() > maximumTriangles ||
                geometry.GetProperty("maximumVisibleMeshCount").GetInt32() > maximumVisibleMeshes ||
                geometry.GetProperty("materialCount").GetInt32() > maximumMaterials)
            {
                errors.Add($"Cinematic manifest '{manifestRelativePath}' exceeds an adopted geometry budget.");
            }

            var limits = root.GetProperty("limits");
            if (limits.GetProperty("triangles").GetInt32() != maximumTriangles ||
                limits.GetProperty("visibleMeshes").GetInt32() != maximumVisibleMeshes ||
                limits.GetProperty("materials").GetInt32() != maximumMaterials ||
                limits.GetProperty("glbBytes").GetInt64() != maximumBytes)
            {
                errors.Add($"Cinematic manifest '{manifestRelativePath}' does not record the adopted budgets.");
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            errors.Add($"Cinematic manifest '{manifestRelativePath}' is malformed: {exception.Message}");
        }
    }

    private static void ValidateLateRoomRuntimeManifest(
        string manifestRelativePath,
        string expectedRuntimeFile,
        int maximumMeshes,
        int maximumTriangles,
        long maximumBytes,
        ICollection<string> errors)
    {
        var manifestPath = ProjectPath(manifestRelativePath);
        if (!File.Exists(manifestPath)) return;

        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = manifest.RootElement;
            var runtimeFile = root.GetProperty("gameFile").GetString();
            if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
                root.GetProperty("status").GetString() != "adopted-runtime-default" ||
                root.GetProperty("sampleOnly").GetBoolean() ||
                !root.GetProperty("runtimeDefault").GetBoolean() ||
                root.GetProperty("missingExternalFiles").GetInt32() != 0 ||
                runtimeFile != expectedRuntimeFile)
            {
                errors.Add(
                    $"Late-room manifest '{manifestRelativePath}' must declare schema v1, " +
                    "adopted-runtime-default, sampleOnly=false, runtimeDefault=true, no external files " +
                    "and the adopted GLB path.");
                return;
            }

            var runtimePath = ProjectPath(runtimeFile);
            if (!File.Exists(runtimePath))
            {
                errors.Add($"Late-room runtime GLB '{runtimeFile}' is missing.");
                return;
            }

            var expectedBytes = root.GetProperty("bytes").GetInt64();
            var actualBytes = new FileInfo(runtimePath).Length;
            if (actualBytes != expectedBytes || actualBytes > maximumBytes)
                errors.Add(
                    $"Late-room runtime GLB '{runtimeFile}' has {actualBytes} bytes; " +
                    $"manifest={expectedBytes}, budget={maximumBytes}.");

            var expectedHash = root.GetProperty("sha256").GetString();
            var actualHash = Sha256File(runtimePath);
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                errors.Add(
                    $"Late-room runtime GLB '{runtimeFile}' SHA-256 is {actualHash}; expected {expectedHash}.");

            if (root.GetProperty("meshObjects").GetInt32() > maximumMeshes ||
                root.GetProperty("trianglesUpperBound").GetInt32() > maximumTriangles)
                errors.Add($"Late-room manifest '{manifestRelativePath}' exceeds an adopted geometry budget.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            errors.Add($"Late-room manifest '{manifestRelativePath}' is malformed: {exception.Message}");
        }
    }

    private static void ValidateManorImportArtifacts(
        JsonElement manifest,
        ICollection<string> errors)
    {
        var textureHashes = manifest.GetProperty("textureFiles").EnumerateObject()
            .ToDictionary(
                texture => texture.Name,
                texture => texture.Value.GetString() ?? string.Empty,
                StringComparer.Ordinal);
        var imports = new[]
        {
            (
                ManifestPath: manifest.GetProperty("hallRuntimeFile").GetString(),
                Textures: new[]
                {
                    "manor-building-albedo.png",
                    "manor-building-normal.png",
                    "manor-building-orm.png",
                    "manor-furnishings-albedo.png",
                    "manor-furnishings-normal.png",
                    "manor-furnishings-orm.png",
                    "manor-rug-albedo.png",
                    "manor-rug-normal.png",
                    "manor-rug-orm.png"
                }),
            (
                ManifestPath: manifest.GetProperty("exteriorReviewFile").GetString(),
                Textures: new[]
                {
                    "manor-building-albedo.png",
                    "manor-building-normal.png",
                    "manor-building-orm.png",
                    "manor-furnishings-albedo.png",
                    "manor-furnishings-normal.png",
                    "manor-furnishings-orm.png"
                })
        };

        foreach (var (manifestPath, textures) in imports)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                errors.Add("Dream manor import path is missing from the manifest.");
                continue;
            }

            var glbImportPath = ProjectPath($"{manifestPath}.import");
            if (!File.Exists(glbImportPath))
            {
                errors.Add($"Dream manor Godot import configuration '{manifestPath}.import' is missing.");
                continue;
            }

            var importText = File.ReadAllText(glbImportPath);
            var expectedSource = $"source_file=\"res://{manifestPath}\"";
            if (!importText.Contains(expectedSource, StringComparison.Ordinal) ||
                !importText.Contains("gltf/embedded_image_handling=1", StringComparison.Ordinal))
            {
                errors.Add(
                    $"Dream manor Godot import '{manifestPath}.import' must extract embedded PBR images " +
                    "with its exact source path.");
            }

            var directory = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/') ?? string.Empty;
            var stem = Path.GetFileNameWithoutExtension(manifestPath);
            foreach (var texture in textures)
            {
                var extractedRelative = $"{directory}/{stem}_{texture}";
                var extractedPath = ProjectPath(extractedRelative);
                var extractedImportPath = $"{extractedPath}.import";
                if (!File.Exists(extractedPath) || !File.Exists(extractedImportPath))
                {
                    errors.Add(
                        $"Dream manor extracted texture or import metadata is missing: '{extractedRelative}'.");
                    continue;
                }

                if (!Sha256File(extractedPath).Equals(
                        textureHashes[texture],
                        StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"Dream manor extracted texture '{extractedRelative}' differs from its source texture.");
                }
            }
        }
    }

    private static string FindProjectRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "project.godot")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find project.godot above '{AppContext.BaseDirectory}'.");
    }

    private static void AssertUnique(IEnumerable<string> values, string label)
    {
        var duplicates = values
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        Assert.True(
            duplicates.Length == 0,
            $"Duplicate {label} values: {string.Join(", ", duplicates)}");
    }

    private static void AssertNoErrors(IReadOnlyCollection<string> errors) =>
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));

    private sealed record MutationCall(
        string Command,
        IReadOnlyList<string> Arguments,
        int LineNumber);

    private sealed record LocalizationRow(string Key, string Value);
}
