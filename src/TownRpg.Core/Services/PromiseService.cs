using TownRpg.Core.Domain;

namespace TownRpg.Core.Services;

public sealed class PromiseService(GameState state)
{
    public PromiseState Create(
        string id,
        string promisorId,
        string promiseeId,
        string description)
    {
        if (state.Promises.Any(item => item.Id == id))
        {
            return state.Promises.Single(item => item.Id == id);
        }

        var promise = new PromiseState
        {
            Id = id,
            PromisorId = promisorId,
            PromiseeId = promiseeId,
            Description = description,
            CreatedDay = state.World.Day
        };
        state.Promises.Add(promise);
        return promise;
    }

    public PromiseState Resolve(string id, PromiseStatus status)
    {
        var promise = state.Promises.FirstOrDefault(item => item.Id == id)
            ?? throw new KeyNotFoundException($"Promise '{id}' does not exist.");
        promise.TransitionTo(status);
        return promise;
    }
}
