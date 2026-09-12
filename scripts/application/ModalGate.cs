namespace TownRpg.Application;

public sealed class ModalGate
{
    private readonly Dictionary<string, int> _owners = new(StringComparer.Ordinal);

    public bool IsBlocked => _owners.Count > 0;
    public bool HasOwner(string owner) => _owners.ContainsKey(owner);

    public IDisposable Acquire(string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        _owners[owner] = _owners.GetValueOrDefault(owner) + 1;
        return new Lease(this, owner);
    }

    private void Release(string owner)
    {
        if (!_owners.TryGetValue(owner, out var count))
        {
            return;
        }

        if (count <= 1)
        {
            _owners.Remove(owner);
        }
        else
        {
            _owners[owner] = count - 1;
        }
    }

    private sealed class Lease(ModalGate gate, string owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            gate.Release(owner);
        }
    }
}
