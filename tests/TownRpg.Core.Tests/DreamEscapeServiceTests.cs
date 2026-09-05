using System.Text.Json;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;

namespace TownRpg.Core.Tests;

public sealed class DreamEscapeServiceTests
{
    [Fact]
    public void NewDream_UsesContentVersionFiveAndOnlyStartsWithPin()
    {
        var state = GameState.CreateDreamPrologue();

        Assert.Equal(5, state.DreamEscape!.ContentVersion);
        Assert.Equal([DreamEscapeIds.ItemPin], state.DreamEscape.Items);
        Assert.Equal(28, DreamEscapeIds.Puzzles.Count);
        Assert.Equal(34, DreamEscapeIds.Items.Count);
        Assert.Equal(11, DreamEscapeIds.InstalledPartSlots.Count);
        Assert.DoesNotContain(RetiredDreamEscapeIds.PuzzleRoomTwoDoor, DreamEscapeIds.Puzzles);
        Assert.DoesNotContain(RetiredDreamEscapeIds.PuzzleArchiveClubOrder, DreamEscapeIds.Puzzles);
        Assert.DoesNotContain(RetiredDreamEscapeIds.PuzzleAuditoriumCurtainFly, DreamEscapeIds.Puzzles);
        Assert.Empty(DreamEscapeIds.Puzzles.Intersect(RetiredDreamEscapeIds.Puzzles));
        Assert.Empty(DreamEscapeIds.Clues.Intersect(RetiredDreamEscapeIds.Clues));
    }

    [Fact]
    public void SignalRegistry_UsesFiveNewSpatialRecordsAndWrongOrderIsSideEffectFree()
    {
        var (state, service) = NewService();
        state.DreamEscape!.UnlockedAreas.Add(DreamEscapeIds.AreaArchive);
        Move(service, DreamEscapeIds.AreaArchive);

        foreach (var target in new[]
                 {
                     "target.archive.registry_rule", "target.archive.registry_choir",
                     "target.archive.registry_broadcast", "target.archive.registry_art",
                     "target.archive.registry_track"
                 })
            Assert.True(service.Observe(target).Changed);

        Assert.Empty(service.MissingInputClues(DreamEscapeIds.PuzzleArchiveSignalRegistry));
        var before = Fingerprint(state.DreamEscape);
        Assert.False(service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveSignalRegistry,
            "broadcast", "choir", "art", "track").Changed);
        Assert.Equal(before, Fingerprint(state.DreamEscape));
        Assert.True(service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveSignalRegistry,
            "choir", "broadcast", "art", "track").Changed);
    }

    [Fact]
    public void LineSetBalance_RequiresInstalledWeightAndUsesFourNotchHeights()
    {
        var (state, service) = NewService();
        state.DreamEscape!.UnlockedAreas.Add(DreamEscapeIds.AreaAuditorium);
        Move(service, DreamEscapeIds.AreaAuditorium);

        Assert.False(service.SubmitPuzzle(DreamEscapeIds.PuzzleAuditoriumLineSetBalance,
            "3", "1", "4", "2").Changed);
        state.DreamEscape.Items.Add(DreamEscapeIds.ItemCurtainCounterweight);
        Assert.True(service.UseItem(DreamEscapeIds.ItemCurtainCounterweight,
            "target.auditorium.line_set").Changed);
        var before = Fingerprint(state.DreamEscape);
        Assert.False(service.SubmitPuzzle(DreamEscapeIds.PuzzleAuditoriumLineSetBalance,
            "3", "1", "2", "4").Changed);
        Assert.Equal(before, Fingerprint(state.DreamEscape));
        Assert.True(service.SubmitPuzzle(DreamEscapeIds.PuzzleAuditoriumLineSetBalance,
            "3", "1", "4", "2").Changed);
    }

    [Fact]
    public void ClassroomClues_AreSeparatedOptionalAndIdempotent()
    {
        var (state, service) = NewService();
        Move(service, DreamEscapeIds.AreaClassroom);

        Assert.True(service.Observe("target.classroom.maintenance_card").Changed);
        Assert.True(service.Observe("target.classroom.morning_schedule").Changed);
        Assert.True(service.Observe("target.classroom.duty_note").Changed);
        Assert.False(service.Observe("target.classroom.maintenance_card").Changed);
        Assert.Empty(service.MissingInputClues(DreamEscapeIds.PuzzleWindowLatches));

        var before = Fingerprint(state.DreamEscape!);
        Assert.False(service.SubmitPuzzle(DreamEscapeIds.PuzzleWindowLatches, "up", "middle", "down").Changed);
        Assert.Equal(before, Fingerprint(state.DreamEscape!));
        Assert.True(service.SubmitPuzzle(DreamEscapeIds.PuzzleWindowLatches, "up", "down", "middle").Changed);
    }

    [Fact]
    public void CorrectAnswersCanBeSubmittedWithoutObservingOptionalInputClues()
    {
        var (state, service) = NewService();
        SolveClassroom(service, observeClues: false);

        Assert.DoesNotContain(DreamEscapeIds.ClueClassroomWindow, state.DreamEscape!.DiscoveredClues);
        Assert.DoesNotContain(DreamEscapeIds.ClueClassroomClock, state.DreamEscape.DiscoveredClues);
        Assert.DoesNotContain(DreamEscapeIds.ClueClassroomDutyRoster, state.DreamEscape.DiscoveredClues);
        Assert.Contains(DreamEscapeIds.AreaMusic, state.DreamEscape.UnlockedAreas);
    }

    [Theory]
    [InlineData("metronome", "forks", "roll")]
    [InlineData("roll", "metronome", "forks")]
    [InlineData("forks", "roll", "metronome")]
    public void MusicBranches_CanBeSolvedInAnyOrderWithoutOptionalClueRecords(
        string first, string second, string third)
    {
        var (state, service) = NewService();
        SolveClassroom(service);
        Move(service, DreamEscapeIds.AreaMusic);
        SolveMusicRescue(service);
        PickupMusicTools(service);
        service.UseItem(DreamEscapeIds.ItemMetronomeWeight, "target.music.metronome");

        foreach (var branch in new[] { first, second, third })
        {
            var result = branch switch
            {
                "metronome" => service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicMetronome, "72"),
                "forks" => service.SubmitPuzzle(
                    DreamEscapeIds.PuzzleMusicTuningForks, "A3", "C4", "E4", "A4"),
                "roll" => service.SubmitPuzzle(
                    DreamEscapeIds.PuzzleMusicPianoRoll, "2", "4", "1", "3"),
                _ => throw new ArgumentOutOfRangeException(nameof(branch), branch, null)
            };
            Assert.True(result.Changed);
        }

        Assert.True(service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicAcousticWall,
            "woodblock", "piano", "triangle", "handbell").Changed);
        Assert.DoesNotContain(DreamEscapeIds.ClueMusicTempoNotice, state.DreamEscape!.DiscoveredClues);
        Assert.DoesNotContain(DreamEscapeIds.ClueMusicForkReference, state.DreamEscape.DiscoveredClues);
        Assert.DoesNotContain(DreamEscapeIds.ClueMusicRollTemplate, state.DreamEscape.DiscoveredClues);
    }

    [Fact]
    public void LockedCluesBecomeAvailableBeforeTheirConsumerAndCannotCreateCycles()
    {
        var (_, service) = NewService();
        SolveClassroom(service);
        Move(service, DreamEscapeIds.AreaMusic);

        Assert.False(service.Observe("target.music.fork_reference").Changed);
        SolveMusicRescue(service);
        Assert.True(service.Observe("target.music.fork_reference").Changed);
        PickupMusicTools(service);
        SolveMusic(service);
        Move(service, DreamEscapeIds.AreaCorridor);
        service.UseItem(DreamEscapeIds.ItemRoomThreeKey, "target.corridor.room_three_door");
        Move(service, DreamEscapeIds.AreaArchive);
        Assert.False(service.Observe("target.archive.fuse_route").Changed);
        service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveSignalRegistry, "choir", "broadcast", "art", "track");
        Assert.True(service.Observe("target.archive.fuse_route").Changed);
    }

    [Fact]
    public void FullTwentyEightPuzzleChain_UsesTwoBacktracksAndOpensExit()
    {
        var (state, service) = NewService();
        CompleteDream(service);

        Assert.Equal(28, state.DreamEscape!.SolvedPuzzles.Count(DreamEscapeIds.Puzzles.Contains));
        Assert.Equal(DreamEscapeStatus.ExitOpened, state.DreamEscape.Status);
        Assert.True(state.DreamEscape.UnlockedAreas.SetEquals(DreamEscapeIds.Areas));
        Assert.False(service.OpenExit().Changed);
    }

    [Fact]
    public void EveryCatalogItemEntersInventoryAndCombinationProductsExistBeforeInstallation()
    {
        var (state, service) = NewService();
        var seen = new HashSet<string>(state.DreamEscape!.Items, StringComparer.Ordinal);
        void Track() => seen.UnionWith(state.DreamEscape!.Items);

        Move(service, DreamEscapeIds.AreaClassroom);
        service.SubmitPuzzle(DreamEscapeIds.PuzzleWindowLatches, "up", "down", "middle"); Track();
        service.Pickup(DreamEscapeIds.ItemClockHand); Track();
        service.UseItem(DreamEscapeIds.ItemChalk, "target.classroom.blackboard");
        service.UseItem(DreamEscapeIds.ItemClockHand, "target.classroom.clock");
        service.UseItem(DreamEscapeIds.ItemClockKey, "target.classroom.clock");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleClockRestore, "7", "00");
        service.UseItem(DreamEscapeIds.ItemRosterSlider, "target.classroom.duty_roster");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleRosterSlider, "center", "2");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleTeacherDesk, "3", "7", "2"); Track();
        Move(service, DreamEscapeIds.AreaCorridor);
        service.UseItem(DreamEscapeIds.ItemRoomTwoKey, "target.corridor.room_two_door");
        Move(service, DreamEscapeIds.AreaMusic);
        SolveMusicRescue(service); PickupMusicTools(service); Track();
        SolveMusic(service); Track();
        Move(service, DreamEscapeIds.AreaCorridor);
        service.UseItem(DreamEscapeIds.ItemRoomThreeKey, "target.corridor.room_three_door");
        Move(service, DreamEscapeIds.AreaArchive);
        service.Pickup(DreamEscapeIds.ItemArchiveIndexStrip); Track();
        service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveSignalRegistry, "choir", "broadcast", "art", "track"); Track();
        service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveDrawerRoute, "C1", "A4", "D2", "B3"); Track();
        service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveFuseRepair, "left", "middle", "right"); Track();
        Assert.Contains(DreamEscapeIds.ItemRepairedFuse, state.DreamEscape.Items);
        Move(service, DreamEscapeIds.AreaClassroom);
        service.UseItem(DreamEscapeIds.ItemMaintenanceKey, "target.classroom.map_cabinet"); Track();
        Move(service, DreamEscapeIds.AreaMusic);
        service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicAttendanceReader, "B2", "D1", "A3", "C4"); Track();
        Move(service, DreamEscapeIds.AreaArchive);
        service.UseItem(DreamEscapeIds.ItemCalibrationReel, "target.archive.recorder");
        service.UseItem(DreamEscapeIds.ItemRecorderCrank, "target.archive.recorder");
        service.UseItem(DreamEscapeIds.ItemRepairedFuse, "target.archive.recorder");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveRecorderRebuild, "confirm");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveEditingCounter, "12", "24", "31", "45"); Track();
        Move(service, DreamEscapeIds.AreaCorridor);
        service.UseItem(DreamEscapeIds.ItemRoomFourKey, "target.corridor.room_four_door");
        Move(service, DreamEscapeIds.AreaAuditorium);
        service.Pickup(DreamEscapeIds.ItemCurtainCounterweight);
        service.Pickup(DreamEscapeIds.ItemProjectorLens); Track();
        SolveProjector(service); Track();
        CompleteSecondBacktrack(service); Track();
        Move(service, DreamEscapeIds.AreaAuditorium);
        service.SubmitPuzzle(DreamEscapeIds.PuzzleAuditoriumMasterTapeSplice,
            "classroom", "music", "archive"); Track();
        Assert.Contains(DreamEscapeIds.ItemRestoredMasterTape, state.DreamEscape.Items);
        service.UseItem(DreamEscapeIds.ItemRestoredMasterTape, "target.auditorium.master_player"); Track();

        Assert.True(DreamEscapeIds.Items.SetEquals(seen));
    }

    [Fact]
    public void RecorderAndMasterTape_RequireAllSlotsAndProductsEnterBackpackFirst()
    {
        var (state, service) = NewService();
        SolveThroughArchiveBranches(service);
        CompleteFirstBacktrack(service);
        Move(service, DreamEscapeIds.AreaArchive);

        Assert.False(service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveRecorderRebuild, "confirm").Changed);
        service.UseItem(DreamEscapeIds.ItemCalibrationReel, "target.archive.recorder");
        service.UseItem(DreamEscapeIds.ItemRecorderCrank, "target.archive.recorder");
        Assert.False(service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveRecorderRebuild, "confirm").Changed);
        Assert.Contains(DreamEscapeIds.ItemRepairedFuse, state.DreamEscape!.Items);
        service.UseItem(DreamEscapeIds.ItemRepairedFuse, "target.archive.recorder");
        Assert.True(service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveRecorderRebuild, "confirm").Changed);
        Assert.Contains(DreamEscapeIds.ClueArchiveEditingStops, state.DreamEscape.DiscoveredClues);
    }

    [Fact]
    public void WrongRepeatedAndWrongAreaActionsHaveNoSideEffects()
    {
        var (state, service) = NewService();
        var before = Fingerprint(state.DreamEscape!);

        Assert.False(service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicCage, "1", "4", "2", "3").Changed);
        Assert.Equal(before, Fingerprint(state.DreamEscape!));
        Move(service, DreamEscapeIds.AreaClassroom);
        Assert.True(service.SubmitPuzzle(DreamEscapeIds.PuzzleWindowLatches, "up", "down", "middle").Changed);
        var solved = Fingerprint(state.DreamEscape!);
        Assert.False(service.SubmitPuzzle(DreamEscapeIds.PuzzleWindowLatches, "up", "down", "middle").Changed);
        Assert.Equal(solved, Fingerprint(state.DreamEscape!));
    }

    [Fact]
    public void TargetItemUseCatalog_ContainsOnlyKnownItemsAndExcludesThePin()
    {
        Assert.NotEmpty(DreamEscapeIds.TargetItemUses);
        foreach (var (targetId, itemIds) in DreamEscapeIds.TargetItemUses)
        {
            Assert.StartsWith("target.", targetId, StringComparison.Ordinal);
            Assert.NotEmpty(itemIds);
            Assert.Equal(itemIds.Count, itemIds.Distinct(StringComparer.Ordinal).Count());
            Assert.All(itemIds, itemId => Assert.Contains(itemId, DreamEscapeIds.Items));
            Assert.DoesNotContain(DreamEscapeIds.ItemPin, itemIds);
        }
    }

    [Fact]
    public void UseAvailableItems_AppliesSingleAndAllHeldMatchingPartsInCatalogOrder()
    {
        var (state, service) = NewService();
        Move(service, DreamEscapeIds.AreaClassroom);
        service.SubmitPuzzle(DreamEscapeIds.PuzzleWindowLatches, "up", "down", "middle");

        Assert.True(service.UseAvailableItems("target.classroom.blackboard").Changed);
        Assert.Contains(DreamEscapeIds.PuzzleBlackboard, state.DreamEscape!.SolvedPuzzles);
        Assert.Contains(DreamEscapeIds.ItemChalk, state.DreamEscape.Items);

        service.Pickup(DreamEscapeIds.ItemClockHand);
        var result = service.UseAvailableItems("target.classroom.clock");

        Assert.True(result.Changed);
        Assert.Equal("DREAM_FEEDBACK_MATCHING_ITEMS_INSTALLED", result.FeedbackKey);
        Assert.Contains(DreamEscapeIds.SlotClassroomClockHand, state.DreamEscape.InstalledParts);
        Assert.Contains(DreamEscapeIds.SlotClassroomClockKey, state.DreamEscape.InstalledParts);
        Assert.DoesNotContain(DreamEscapeIds.ItemClockHand, state.DreamEscape.Items);
        Assert.DoesNotContain(DreamEscapeIds.ItemClockKey, state.DreamEscape.Items);
    }

    [Fact]
    public void UseAvailableItems_PartiallyInstallsHeldPartsAndLaterCompletesTheTarget()
    {
        var (state, service) = NewService();
        Move(service, DreamEscapeIds.AreaClassroom);
        service.SubmitPuzzle(DreamEscapeIds.PuzzleWindowLatches, "up", "down", "middle");

        Assert.True(service.UseAvailableItems("target.classroom.clock").Changed);
        Assert.Contains(DreamEscapeIds.SlotClassroomClockKey, state.DreamEscape!.InstalledParts);
        Assert.DoesNotContain(DreamEscapeIds.SlotClassroomClockHand, state.DreamEscape.InstalledParts);

        var afterPartialInstall = Fingerprint(state.DreamEscape);
        Assert.False(service.UseAvailableItems("target.classroom.clock").Changed);
        Assert.Equal(afterPartialInstall, Fingerprint(state.DreamEscape));

        service.Pickup(DreamEscapeIds.ItemClockHand);
        Assert.True(service.UseAvailableItems("target.classroom.clock").Changed);
        Assert.Contains(DreamEscapeIds.SlotClassroomClockHand, state.DreamEscape.InstalledParts);
    }

    [Fact]
    public void UseAvailableItems_InstallsAllRecorderPartsInOneAction()
    {
        var (state, service) = NewService();
        SolveThroughArchiveBranches(service);
        CompleteFirstBacktrack(service);
        Move(service, DreamEscapeIds.AreaArchive);

        var result = service.UseAvailableItems("target.archive.recorder");

        Assert.True(result.Changed);
        Assert.Equal("DREAM_FEEDBACK_RECORDER_PLAYING", result.FeedbackKey);
        Assert.Contains(DreamEscapeIds.PuzzleArchiveRecorderRebuild, state.DreamEscape!.SolvedPuzzles);
        Assert.True(state.DreamEscape.InstalledParts.IsSupersetOf([
            DreamEscapeIds.SlotArchiveRecorderReel,
            DreamEscapeIds.SlotArchiveRecorderCrank,
            DreamEscapeIds.SlotArchiveRecorderFuse
        ]));
        Assert.DoesNotContain(DreamEscapeIds.ItemCalibrationReel, state.DreamEscape.Items);
        Assert.DoesNotContain(DreamEscapeIds.ItemRecorderCrank, state.DreamEscape.Items);
        Assert.DoesNotContain(DreamEscapeIds.ItemRepairedFuse, state.DreamEscape.Items);
    }

    [Fact]
    public void UseAvailableItems_InvalidAreaPrerequisiteAndMissingItemHaveNoSideEffects()
    {
        var (state, service) = NewService();
        var dream = state.DreamEscape!;
        Move(service, DreamEscapeIds.AreaClassroom);
        service.SubmitPuzzle(DreamEscapeIds.PuzzleWindowLatches, "up", "down", "middle");
        var beforeWrongArea = Fingerprint(dream);

        Assert.False(service.UseAvailableItems("target.corridor.room_two_door").Changed);
        Assert.Equal(beforeWrongArea, Fingerprint(dream));

        dream.Items.Add(DreamEscapeIds.ItemMaintenanceKey);
        var beforePrerequisite = Fingerprint(dream);
        Assert.False(service.UseAvailableItems("target.classroom.map_cabinet").Changed);
        Assert.Equal(beforePrerequisite, Fingerprint(dream));

        dream.Items.Remove(DreamEscapeIds.ItemChalk);
        var beforeMissingItem = Fingerprint(dream);
        Assert.False(service.UseAvailableItems("target.classroom.blackboard").Changed);
        Assert.False(service.UseAvailableItems("target.unknown").Changed);
        Assert.Equal(beforeMissingItem, Fingerprint(dream));
    }

    [Fact]
    public void PuzzleInputClueCatalog_CoversOnlyKnownIdsAndMissingQueryDoesNotBlockSubmit()
    {
        var (_, service) = NewService();
        foreach (var (puzzle, clues) in DreamEscapeIds.PuzzleInputClues)
        {
            Assert.Contains(puzzle, DreamEscapeIds.Puzzles);
            Assert.NotEmpty(clues);
            Assert.All(clues, clue => Assert.Contains(clue, DreamEscapeIds.Clues));
            Assert.Equal(clues.Count, service.MissingInputClues(puzzle).Count);
        }
    }

    [Fact]
    public void PuzzleDependencyGraph_IsAcyclicAndReferencesKnownPuzzles()
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        bool Visit(string puzzle)
        {
            if (visited.Contains(puzzle)) return true;
            if (!visiting.Add(puzzle)) return false;
            foreach (var prerequisite in DreamEscapeIds.PuzzlePrerequisites.GetValueOrDefault(puzzle, []))
            {
                Assert.Contains(prerequisite, DreamEscapeIds.Puzzles);
                if (!Visit(prerequisite)) return false;
            }
            visiting.Remove(puzzle);
            visited.Add(puzzle);
            return true;
        }

        Assert.All(DreamEscapeIds.Puzzles, puzzle => Assert.True(Visit(puzzle), $"Cycle at {puzzle}"));
    }

    [Fact]
    public void SaveRoundTrip_PreservesV5CluesSlotsAndProgress()
    {
        var (state, service) = NewService();
        Move(service, DreamEscapeIds.AreaClassroom);
        service.Observe("target.classroom.maintenance_card");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleWindowLatches, "up", "down", "middle");
        service.Pickup(DreamEscapeIds.ItemClockHand);
        service.UseItem(DreamEscapeIds.ItemClockHand, "target.classroom.clock");
        service.DiscoverPuzzle(DreamEscapeIds.PuzzleClockRestore);

        var restored = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(state));

        Assert.Equal(5, restored.DreamEscape!.ContentVersion);
        Assert.Contains(DreamEscapeIds.ClueClassroomWindow, restored.DreamEscape.DiscoveredClues);
        Assert.Contains(DreamEscapeIds.SlotClassroomClockHand, restored.DreamEscape.InstalledParts);
        Assert.Contains(DreamEscapeIds.PuzzleClockRestore, restored.DreamEscape.DiscoveredPuzzles);
    }

    [Fact]
    public void Deserialize_ActiveContentVersionFourDreamRequiresRestart()
    {
        var state = GameState.CreateDreamPrologue();
        state.DreamEscape!.ContentVersion = 4;

        var exception = Assert.Throws<NotSupportedException>(() =>
            SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(state)));

        Assert.Contains("重新开始", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_CompletedV4AndLegacyWithoutDreamRemainCompatible()
    {
        var state = GameState.CreateDreamPrologue();
        state.DreamEscape!.ContentVersion = 4;
        state.DreamEscape.Status = DreamEscapeStatus.CompletedEscape;
        state.DreamEscape.Items.Clear();
        var restored = SaveGameSerializer.Deserialize(SaveGameSerializer.Serialize(state));
        Assert.Equal(4, restored.DreamEscape!.ContentVersion);

        var legacyJson = SaveGameSerializer.Serialize(GameState.CreateVerticalSlice());
        using var document = JsonDocument.Parse(legacyJson);
        var game = document.RootElement.GetProperty("game").EnumerateObject()
            .Where(property => property.Name != "dreamEscape")
            .ToDictionary(property => property.Name, property => property.Value.Clone());
        var withoutDream = JsonSerializer.Serialize(new { formatVersion = 1, game });
        Assert.Null(SaveGameSerializer.Deserialize(withoutDream).DreamEscape);
    }

    [Fact]
    public void HintsRequireXiaoyuDiscoveryAndStopAtThreeWithoutSolving()
    {
        var (state, service) = NewService();
        service.DiscoverPuzzle(DreamEscapeIds.PuzzleWindowLatches);
        Assert.False(service.AdvanceHint(DreamEscapeIds.PuzzleWindowLatches).Changed);
        SolveClassroom(service);
        Move(service, DreamEscapeIds.AreaMusic);
        SolveMusicRescue(service);
        service.DiscoverPuzzle(DreamEscapeIds.PuzzleMusicAcousticWall);

        Assert.True(service.AdvanceHint(DreamEscapeIds.PuzzleMusicAcousticWall).Changed);
        Assert.True(service.AdvanceHint(DreamEscapeIds.PuzzleMusicAcousticWall).Changed);
        Assert.True(service.AdvanceHint(DreamEscapeIds.PuzzleMusicAcousticWall).Changed);
        Assert.False(service.AdvanceHint(DreamEscapeIds.PuzzleMusicAcousticWall).Changed);
        Assert.Equal(3, state.DreamEscape!.HintLevels[DreamEscapeIds.PuzzleMusicAcousticWall]);
        Assert.DoesNotContain(DreamEscapeIds.PuzzleMusicAcousticWall, state.DreamEscape.SolvedPuzzles);
    }

    [Theory]
    [InlineData(DreamEscapeIds.AreaCorridor)]
    [InlineData(DreamEscapeIds.AreaClassroom)]
    [InlineData(DreamEscapeIds.AreaMusic)]
    [InlineData(DreamEscapeIds.AreaArchive)]
    [InlineData(DreamEscapeIds.AreaAuditorium)]
    public void PinWake_ClearsDreamItemsAndCannotBeRepeated(string areaId)
    {
        var (state, service) = NewService();
        state.DreamEscape!.UnlockedAreas.UnionWith(DreamEscapeIds.Areas);
        Move(service, areaId);
        Assert.True(service.UseItem(DreamEscapeIds.ItemPin, "target.self").Changed);
        Assert.Empty(state.DreamEscape.Items);
        Assert.True(service.CompletePinWake().Changed);
        Assert.Equal(DreamEscapeStatus.CompletedPin, state.DreamEscape.Status);
        Assert.False(service.CompletePinWake().Changed);
    }

    private static void CompleteDream(DreamEscapeService service)
    {
        SolveThroughArchiveBranches(service);
        CompleteFirstBacktrack(service);
        RebuildRecorderAndOpenAuditorium(service);
        Move(service, DreamEscapeIds.AreaAuditorium);
        service.Pickup(DreamEscapeIds.ItemCurtainCounterweight);
        service.Pickup(DreamEscapeIds.ItemProjectorLens);
        SolveProjector(service);
        CompleteSecondBacktrack(service);
        Move(service, DreamEscapeIds.AreaAuditorium);
        service.SubmitPuzzle(DreamEscapeIds.PuzzleAuditoriumMasterTapeSplice,
            "classroom", "music", "archive");
        service.UseItem(DreamEscapeIds.ItemRestoredMasterTape, "target.auditorium.master_player");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleAuditoriumLightConsole, "3", "7", "2");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleAuditoriumSoundConsole,
            "woodblock", "piano", "triangle", "handbell");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleAuditoriumBroadcastConsole,
            "curtain", "clock", "bell", "register");
        service.PrepareLeftLever();
        service.OpenExit();
    }

    private static void SolveThroughArchiveBranches(DreamEscapeService service)
    {
        SolveClassroom(service);
        Move(service, DreamEscapeIds.AreaMusic);
        SolveMusicRescue(service);
        PickupMusicTools(service);
        SolveMusic(service);
        Move(service, DreamEscapeIds.AreaCorridor);
        service.UseItem(DreamEscapeIds.ItemRoomThreeKey, "target.corridor.room_three_door");
        Move(service, DreamEscapeIds.AreaArchive);
        service.Pickup(DreamEscapeIds.ItemArchiveIndexStrip);
        service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveSignalRegistry, "choir", "broadcast", "art", "track");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveDrawerRoute, "C1", "A4", "D2", "B3");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveFuseRepair, "left", "middle", "right");
    }

    private static void CompleteFirstBacktrack(DreamEscapeService service)
    {
        Move(service, DreamEscapeIds.AreaClassroom);
        service.UseItem(DreamEscapeIds.ItemMaintenanceKey, "target.classroom.map_cabinet");
        Move(service, DreamEscapeIds.AreaMusic);
        service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicAttendanceReader, "B2", "D1", "A3", "C4");
    }

    private static void RebuildRecorderAndOpenAuditorium(DreamEscapeService service)
    {
        Move(service, DreamEscapeIds.AreaArchive);
        service.UseItem(DreamEscapeIds.ItemCalibrationReel, "target.archive.recorder");
        service.UseItem(DreamEscapeIds.ItemRecorderCrank, "target.archive.recorder");
        service.UseItem(DreamEscapeIds.ItemRepairedFuse, "target.archive.recorder");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveRecorderRebuild, "confirm");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleArchiveEditingCounter, "12", "24", "31", "45");
        Move(service, DreamEscapeIds.AreaCorridor);
        service.UseItem(DreamEscapeIds.ItemRoomFourKey, "target.corridor.room_four_door");
    }

    private static void SolveProjector(DreamEscapeService service)
    {
        service.UseItem(DreamEscapeIds.ItemCurtainCounterweight, "target.auditorium.line_set");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleAuditoriumLineSetBalance, "3", "1", "4", "2");
        service.UseItem(DreamEscapeIds.ItemProjectorLens, "target.auditorium.projector");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleAuditoriumShadowProjector, "window", "figure", "bell");
    }

    private static void CompleteSecondBacktrack(DreamEscapeService service)
    {
        Move(service, DreamEscapeIds.AreaClassroom);
        service.UseItem(DreamEscapeIds.ItemStageHook, "target.classroom.transom_cache");
        Move(service, DreamEscapeIds.AreaMusic);
        service.UseItem(DreamEscapeIds.ItemBenchClampKey, "target.music.piano_bench");
        Move(service, DreamEscapeIds.AreaArchive);
        service.UseItem(DreamEscapeIds.ItemInspectionMirror, "target.archive.upper_vault");
    }

    private static void SolveClassroom(DreamEscapeService service, bool observeClues = true)
    {
        Move(service, DreamEscapeIds.AreaClassroom);
        if (observeClues)
        {
            service.Observe("target.classroom.maintenance_card");
            service.Observe("target.classroom.morning_schedule");
            service.Observe("target.classroom.duty_note");
        }
        service.SubmitPuzzle(DreamEscapeIds.PuzzleWindowLatches, "up", "down", "middle");
        service.Pickup(DreamEscapeIds.ItemClockHand);
        service.UseItem(DreamEscapeIds.ItemChalk, "target.classroom.blackboard");
        service.UseItem(DreamEscapeIds.ItemClockHand, "target.classroom.clock");
        service.UseItem(DreamEscapeIds.ItemClockKey, "target.classroom.clock");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleClockRestore, "7", "00");
        service.UseItem(DreamEscapeIds.ItemRosterSlider, "target.classroom.duty_roster");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleRosterSlider, "center", "2");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleTeacherDesk, "3", "7", "2");
        Move(service, DreamEscapeIds.AreaCorridor);
        service.UseItem(DreamEscapeIds.ItemRoomTwoKey, "target.corridor.room_two_door");
    }

    private static void SolveMusicRescue(DreamEscapeService service)
    {
        service.UseItem(DreamEscapeIds.ItemChalk, "target.music.score");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicCage, "1", "4", "2", "3");
    }

    private static void PickupMusicTools(DreamEscapeService service)
    {
        service.Pickup(DreamEscapeIds.ItemRubberMallet);
        service.Pickup(DreamEscapeIds.ItemListeningCone);
        service.Pickup(DreamEscapeIds.ItemMetronomeWeight);
        service.Pickup(DreamEscapeIds.ItemTornPianoRoll);
        service.Pickup(DreamEscapeIds.ItemWaxPatch);
    }

    private static void SolveMusic(DreamEscapeService service)
    {
        service.UseItem(DreamEscapeIds.ItemMetronomeWeight, "target.music.metronome");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicMetronome, "72");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicTuningForks, "A3", "C4", "E4", "A4");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicPianoRoll, "2", "4", "1", "3");
        service.SubmitPuzzle(DreamEscapeIds.PuzzleMusicAcousticWall,
            "woodblock", "piano", "triangle", "handbell");
    }

    private static (GameState State, DreamEscapeService Service) NewService()
    {
        var state = GameState.CreateDreamPrologue();
        return (state, new DreamEscapeService(state));
    }

    private static void Move(DreamEscapeService service, string areaId) =>
        service.MoveToArea(areaId, 320f, 260f);

    private static string Fingerprint(DreamEscapeState state) => string.Join('|',
        state.Status,
        state.CurrentAreaId,
        string.Join(',', state.Items.Order()),
        string.Join(',', state.SolvedPuzzles.Order()),
        string.Join(',', state.UnlockedAreas.Order()),
        string.Join(',', state.DiscoveredClues.Order()),
        string.Join(',', state.InstalledParts.Order()),
        state.XiaoyuJoined);
}
