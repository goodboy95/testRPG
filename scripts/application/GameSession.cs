using Godot;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using GodotFileAccess = Godot.FileAccess;

namespace TownRpg.Application;

public partial class GameSession : Node
{
    public const string SavePath = "user://save-slot-1.json";
    private string? _testSavePath;
    private bool _openingMigrationPending;
    private string EffectiveSavePath => _testSavePath ?? SavePath;

    public GameState State { get; private set; } = GameState.CreateVerticalSlice();
    public ContentRepository Content { get; } = new();
    public ModalGate Modal { get; } = new();
    public TimeService Time { get; private set; } = null!;
    public RelationshipService Relationships { get; private set; } = null!;
    public NarrativeCommandService NarrativeCommands { get; private set; } = null!;
    public EventOutcomeService Outcomes { get; private set; } = null!;
    public DreamEscapeService DreamEscape { get; private set; } = null!;
    public VillageOpeningService VillageOpening { get; private set; } = null!;
    public ResonanceBattle? ActiveBattle { get; set; }
    public string PlayerName => State.Player.Name;
    public string PlayerTitle => State.Player.Title;
    public string InitializationError { get; private set; } = string.Empty;
    public bool HasRecoverableSave
    {
        get
        {
            var saveAbsolute = ProjectSettings.GlobalizePath(EffectiveSavePath);
            return GodotFileAccess.FileExists(EffectiveSavePath) ||
                   GodotFileAccess.FileExists(saveAbsolute + ".bak");
        }
    }

    public event Action? StateChanged;

    private int _persistenceSuppressionCount;

    public override void _Ready()
    {
        try
        {
            Content.Load();
        }
        catch (Exception exception)
        {
            InitializationError = exception.Message;
            GD.PushError(exception.ToString());
        }

        RebindServices();
    }

    public void NewGame(string playerName, string playerTitle)
    {
        _openingMigrationPending = false;
        var retainedSettings = State.Settings;
        State = GameState.CreateDreamPrologue();
        State.Settings = retainedSettings;
        State.Player.Name = string.IsNullOrWhiteSpace(playerName) ? "小岚" : playerName.Trim();
        State.Player.Title = string.IsNullOrWhiteSpace(playerTitle) ? "同学" : playerTitle.Trim();
        RebindServices();
        NotifyChanged();
    }

    public void AdvanceMovingTime(double seconds)
    {
        if (State.VillageExploration is not null) return;
        var before = State.World.MinuteOfDay;
        Time.AdvanceMovementSeconds(seconds, Modal.IsBlocked);
        if (before != State.World.MinuteOfDay)
        {
            NotifyChanged();
        }
    }

    public void Sleep()
    {
        if (State.VillageExploration is not null) throw new InvalidOperationException("The next day is not available yet.");
        Time.Sleep();
        State.Investigation.AddTimeline(State.World.Day, "timeline.new_day");
        SaveCheckpoint();
        NotifyChanged();
    }

    public void Save()
    {
        if (Modal.IsBlocked)
        {
            throw new InvalidOperationException("Cannot save while a modal sequence is active.");
        }

        WriteSave();
    }

    public void SaveCheckpoint() => WriteSave();

    internal IDisposable SuppressPersistence()
    {
        _persistenceSuppressionCount++;
        return new PersistenceSuppressionLease(this);
    }

    // Only the explicit engine acceptance runner can redirect persistence. No setting or save field is added.
    internal IDisposable UseTestSavePath(string path)
    {
        if (!OS.GetCmdlineUserArgs().Any(a => a is "--prologue-e2e" or "--village-opening-e2e"))
            throw new InvalidOperationException("A test save path requires the prologue acceptance runner flag.");
        if (_testSavePath is not null)
            throw new InvalidOperationException("A test save path is already active.");
        var outputRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://outputs")) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(ProjectSettings.GlobalizePath(path));
        if (!candidate.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetExtension(candidate), ".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Acceptance saves must be JSON files below this project's outputs directory.", nameof(path));
        Directory.CreateDirectory(Path.GetDirectoryName(candidate)!);
        _testSavePath = candidate;
        return new TestSavePathLease(this);
    }

    public DreamActionResult MoveDreamToArea(string areaId, float x, float y) =>
        ApplyDreamAction(() => DreamEscape.MoveToArea(areaId, x, y));

    public DreamActionResult PickupDreamItem(string itemId) =>
        ApplyDreamAction(() => DreamEscape.Pickup(itemId));

    public DreamActionResult UseDreamItem(string itemId, string targetId) =>
        ApplyDreamAction(() => DreamEscape.UseItem(itemId, targetId));

    public DreamActionResult UseAvailableDreamItems(string targetId) =>
        ApplyDreamAction(() => DreamEscape.UseAvailableItems(targetId));

    public DreamActionResult ObserveDreamTarget(string targetId) =>
        ApplyDreamAction(() => DreamEscape.Observe(targetId));

    public DreamActionResult DiscoverDreamPuzzle(string puzzleId) =>
        ApplyDreamAction(() => DreamEscape.DiscoverPuzzle(puzzleId));

    public DreamActionResult SubmitDreamPuzzle(string puzzleId, params string[] answer) =>
        ApplyDreamAction(() => DreamEscape.SubmitPuzzle(puzzleId, answer));

    public DreamActionResult AdvanceDreamHint(string puzzleId) =>
        ApplyDreamAction(() => DreamEscape.AdvanceHint(puzzleId));

    public DreamActionResult PrepareDreamLeftLever() =>
        ApplyDreamAction(DreamEscape.PrepareLeftLever);

    public DreamActionResult OpenDreamExit() =>
        ApplyDreamAction(DreamEscape.OpenExit);

    public DreamActionResult CompletePinWake() =>
        ApplyDreamAction(DreamEscape.CompletePinWake);

    public DreamActionResult AssembleDreamFuse() => ApplyDreamAction(DreamEscape.AssembleFuse);

    public DreamActionResult CompleteDreamStory(string eventId) =>
        ApplyDreamAction(() => DreamEscape.CompleteStory(eventId));

    private void WriteSave()
    {
        if (_persistenceSuppressionCount > 0)
        {
            return;
        }

        var json = SaveGameSerializer.Serialize(State);
        var temporaryPath = EffectiveSavePath + ".tmp";
        using (var file = GodotFileAccess.Open(temporaryPath, GodotFileAccess.ModeFlags.Write))
        {
            if (file is null)
            {
                throw new IOException("Could not create temporary save file.");
            }

            if (!file.StoreString(json))
            {
                throw new IOException("Could not write the temporary save file.");
            }

            file.Flush();
            if (file.GetError() != Error.Ok)
            {
                throw new IOException("Could not flush the temporary save file.");
            }
        }

        var saveAbsolute = ProjectSettings.GlobalizePath(EffectiveSavePath);
        var temporaryAbsolute = ProjectSettings.GlobalizePath(temporaryPath);
        if (_openingMigrationPending && System.IO.File.Exists(saveAbsolute))
        {
            // Keep the exact pre-upgrade file, separately from the short-lived atomic-write backup.
            var migrationBackup = saveAbsolute + ".before-village-opening." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff") + ".bak";
            System.IO.File.Copy(saveAbsolute, migrationBackup, overwrite: false);
            _openingMigrationPending = false;
        }
        var backupAbsolute = saveAbsolute + ".bak";
        if (GodotFileAccess.FileExists(backupAbsolute))
        {
            if (DirAccess.RemoveAbsolute(backupAbsolute) != Error.Ok)
            {
                throw new IOException("Could not remove a stale save backup.");
            }
        }

        var hadSave = GodotFileAccess.FileExists(EffectiveSavePath);
        if (hadSave && DirAccess.RenameAbsolute(saveAbsolute, backupAbsolute) != Error.Ok)
        {
            throw new IOException("Could not rotate the previous save file.");
        }

        if (DirAccess.RenameAbsolute(temporaryAbsolute, saveAbsolute) != Error.Ok)
        {
            if (hadSave && DirAccess.RenameAbsolute(backupAbsolute, saveAbsolute) != Error.Ok)
            {
                throw new IOException(
                    "Could not commit the new save or restore the previous file; " +
                    "the backup was preserved for automatic recovery.");
            }

            throw new IOException("Could not commit the new save file.");
        }

        if (hadSave)
        {
            if (DirAccess.RemoveAbsolute(backupAbsolute) != Error.Ok)
            {
                GD.PushWarning("The new save was committed, but the previous backup could not be removed.");
            }
        }
    }

    public bool TryLoad(out string error)
    {
        error = string.Empty;
        var saveAbsolute = ProjectSettings.GlobalizePath(EffectiveSavePath);
        var backupAbsolute = saveAbsolute + ".bak";
        if (!GodotFileAccess.FileExists(EffectiveSavePath) && GodotFileAccess.FileExists(backupAbsolute))
        {
            if (DirAccess.RenameAbsolute(backupAbsolute, saveAbsolute) != Error.Ok)
            {
                error = "找到了备份存档，但无法恢复。";
                return false;
            }
        }

        if (!GodotFileAccess.FileExists(EffectiveSavePath))
        {
            error = "没有找到存档。";
            return false;
        }

        try
        {
            using var file = GodotFileAccess.Open(EffectiveSavePath, GodotFileAccess.ModeFlags.Read);
            if (file is null)
            {
                error = "存档文件存在，但无法打开。";
                return false;
            }

            State = SaveGameSerializer.Deserialize(file.GetAsText());
            _openingMigrationPending = State.VillageExploration is not null && State.VillageOpening is null;
            RebindServices();
            NotifyChanged();
            return true;
        }
        catch (Exception exception)
        {
            error = $"存档无法读取：{exception.Message}";
            return false;
        }
    }

    public void NotifyChanged() => StateChanged?.Invoke();

    private void RebindServices()
    {
        Time = new TimeService(State);
        Relationships = new RelationshipService(State);
        NarrativeCommands = new NarrativeCommandService(State);
        Outcomes = new EventOutcomeService(State);
        DreamEscape = new DreamEscapeService(State);
        VillageOpening = new VillageOpeningService(State);
    }

    private DreamActionResult ApplyDreamAction(Func<DreamActionResult> action)
    {
        var result = action();
        if (result.Changed)
        {
            NotifyChanged();
            SaveCheckpoint();
        }
        return result;
    }

    private sealed class TestSavePathLease(GameSession session) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            session._testSavePath = null;
        }
    }

    private sealed class PersistenceSuppressionLease(GameSession session) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            session._persistenceSuppressionCount = Math.Max(
                0,
                session._persistenceSuppressionCount - 1);
        }
    }
}
