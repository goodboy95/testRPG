using Godot;
using TownRpg.Core.Domain;
using TownRpg.Core.Services;
using GodotFileAccess = Godot.FileAccess;

namespace TownRpg.Application;

public partial class GameSession : Node
{
    public const string SavePath = "user://save-slot-1.json";

    public GameState State { get; private set; } = GameState.CreateVerticalSlice();
    public ContentRepository Content { get; } = new();
    public ModalGate Modal { get; } = new();
    public TimeService Time { get; private set; } = null!;
    public RelationshipService Relationships { get; private set; } = null!;
    public NarrativeCommandService NarrativeCommands { get; private set; } = null!;
    public EventOutcomeService Outcomes { get; private set; } = null!;
    public ResonanceBattle? ActiveBattle { get; set; }
    public string PlayerName => State.Player.Name;
    public string PlayerTitle => State.Player.Title;
    public string InitializationError { get; private set; } = string.Empty;
    public bool HasRecoverableSave
    {
        get
        {
            var saveAbsolute = ProjectSettings.GlobalizePath(SavePath);
            return GodotFileAccess.FileExists(SavePath) ||
                   GodotFileAccess.FileExists(saveAbsolute + ".bak");
        }
    }

    public event Action? StateChanged;

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
        var retainedSettings = State.Settings;
        State = GameState.CreateVerticalSlice();
        State.Settings = retainedSettings;
        State.Player.Name = string.IsNullOrWhiteSpace(playerName) ? "小岚" : playerName.Trim();
        State.Player.Title = string.IsNullOrWhiteSpace(playerTitle) ? "同学" : playerTitle.Trim();
        RebindServices();
        NotifyChanged();
    }

    public void AdvanceMovingTime(double seconds)
    {
        var before = State.World.MinuteOfDay;
        Time.AdvanceMovementSeconds(seconds, Modal.IsBlocked);
        if (before != State.World.MinuteOfDay)
        {
            NotifyChanged();
        }
    }

    public void Sleep()
    {
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

    private void WriteSave()
    {
        var json = SaveGameSerializer.Serialize(State);
        var temporaryPath = SavePath + ".tmp";
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

        var saveAbsolute = ProjectSettings.GlobalizePath(SavePath);
        var temporaryAbsolute = ProjectSettings.GlobalizePath(temporaryPath);
        var backupAbsolute = saveAbsolute + ".bak";
        if (GodotFileAccess.FileExists(backupAbsolute))
        {
            if (DirAccess.RemoveAbsolute(backupAbsolute) != Error.Ok)
            {
                throw new IOException("Could not remove a stale save backup.");
            }
        }

        var hadSave = GodotFileAccess.FileExists(SavePath);
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
        var saveAbsolute = ProjectSettings.GlobalizePath(SavePath);
        var backupAbsolute = saveAbsolute + ".bak";
        if (!GodotFileAccess.FileExists(SavePath) && GodotFileAccess.FileExists(backupAbsolute))
        {
            if (DirAccess.RenameAbsolute(backupAbsolute, saveAbsolute) != Error.Ok)
            {
                error = "找到了备份存档，但无法恢复。";
                return false;
            }
        }

        if (!GodotFileAccess.FileExists(SavePath))
        {
            error = "没有找到存档。";
            return false;
        }

        try
        {
            using var file = GodotFileAccess.Open(SavePath, GodotFileAccess.ModeFlags.Read);
            if (file is null)
            {
                error = "存档文件存在，但无法打开。";
                return false;
            }

            State = SaveGameSerializer.Deserialize(file.GetAsText());
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
    }
}
