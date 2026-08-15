using Godot;

namespace TownRpg.Application;

public partial class NarrativeBridge : Node
{
    private GameSession Session => GetNode<GameSession>("/root/GameSession");

    public bool Evaluate(string condition, string argument) =>
        Session.NarrativeCommands.Evaluate(condition, argument);

    public bool Evaluate(string condition, string first, string second, string third) =>
        Session.NarrativeCommands.Evaluate(condition, first, second, third);

    public void Execute(string command, string first) => ExecuteCore(command, first);

    public void Execute(string command, string first, string second) =>
        ExecuteCore(command, first, second);

    public void Execute(string command, string first, string second, string third) =>
        ExecuteCore(command, first, second, third);

    public void Execute(string command, string first, string second, string third, string fourth) =>
        ExecuteCore(command, first, second, third, fourth);

    private void ExecuteCore(string command, params string[] arguments)
    {
        Session.NarrativeCommands.Execute(command, arguments);
        Session.NotifyChanged();
    }

    public string PlayerName() => Session.PlayerName;

    public string PlayerTitle() => Session.PlayerTitle;
}
