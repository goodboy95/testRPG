namespace TownRpg.Core.Domain;

public sealed class PromiseState
{
    public required string Id { get; init; }
    public required string PromisorId { get; init; }
    public required string PromiseeId { get; init; }
    public required string Description { get; init; }
    public PromiseStatus Status { get; set; } = PromiseStatus.Pending;
    public int CreatedDay { get; init; }

    public void TransitionTo(PromiseStatus next)
    {
        if (Status != PromiseStatus.Pending)
        {
            throw new InvalidOperationException("A resolved promise cannot be changed.");
        }

        if (next == PromiseStatus.Pending)
        {
            throw new InvalidOperationException("A promise is already pending.");
        }

        Status = next;
    }
}
