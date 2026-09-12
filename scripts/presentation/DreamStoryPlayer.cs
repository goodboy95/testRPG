using Godot;
using DialogueManagerRuntime;
using TownRpg.Application;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

/// <summary>Dialogue Manager owns traversal; the session commits the one validated outcome after END.</summary>
public partial class DreamStoryPlayer : Node
{
    private DialoguePanel _panel = null!;
    private GameSession _session = null!;
    public bool Running { get; private set; }

    public void Configure(Control parent)
    {
        _session = GetNode<GameSession>("/root/GameSession");
        _panel = new DialoguePanel { Name = "PrologueDialogue" };
        parent.AddChild(_panel);
    }

    public async Task PlayPendingAsync(CancellationToken cancellationToken)
    {
        if (Running || _session.State.DreamEscape is not { } dream) return;
        Running = true;
        using var lease = _session.Modal.Acquire("dream-story");
        try
        {
            var ids = new[] { DreamPrologueCatalog.EventId("intro") }
                .Concat(DreamPrologueCatalog.Groups.Select(group => DreamPrologueCatalog.EventId(group.Id)));
            foreach (var eventId in ids)
            {
                if (_session.State.CompletedEvents.Contains(eventId) || !DreamPrologueCatalog.StoryReady(eventId, dream)) continue;
                var definition = _session.Content.Events.Events.Single(entry => entry.Id == eventId);
                using var resource = ResourceLoader.Load<Resource>(definition.DialoguePath)
                    ?? throw new InvalidDataException($"Missing compiled dialogue: {definition.DialoguePath}");
                var next = definition.StartTitle;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var line = await DialogueManager.GetNextDialogueLine(resource, next,
                        mutation_behaviour: MutationBehaviour.Skip).WaitAsync(cancellationToken);
                    if (line is null) break;
                    if (line.Responses.Count > 0) throw new InvalidDataException("Prologue scenes must not contain unhandled choices.");
                    var speaker = line.Character == "主角" ? _session.PlayerName : line.Character;
                    await _panel.ShowLineAsync(line.TranslationKey, speaker,
                        line.Text.Replace("<player_name>", _session.PlayerName, StringComparison.Ordinal))
                        .WaitAsync(cancellationToken);
                    next = line.NextId;
                }
                cancellationToken.ThrowIfCancellationRequested();
                _session.CompleteDreamStory(eventId);
            }
        }
        finally
        {
            if (GodotObject.IsInstanceValid(_panel) && _panel.IsInsideTree()) _panel.CloseDialogue();
            Running = false;
        }
    }
}
