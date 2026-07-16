using System.Collections.Concurrent;

using Latchkey.Backends;

namespace Latchkey.Tests.Support;

/// <summary>
/// A backend that overrides the async <see cref="ISecretBackend" /> methods with real (if trivial)
/// async work and counts async vs. sync dispatch. Lets tests prove the client reaches the async
/// path rather than the sync-bridging defaults.
/// </summary>
sealed class AsyncRecordingBackend : ISecretBackend
{
    readonly ConcurrentDictionary<(string, string), byte[]> _store = new();
    public int AsyncRemoveCalls;
    public int AsyncRetrieveCalls;

    public int AsyncStoreCalls;
    public int SyncCalls;

    public bool IsAvailable => true;

    public void Store(string service,
        string key,
        ReadOnlySpan<byte> value,
        string label)
    {
        Interlocked.Increment(ref SyncCalls);
        _store[(service, key)] = value.ToArray();
    }

    public byte[]? Retrieve(string service, string key)
    {
        Interlocked.Increment(ref SyncCalls);
        return _store.TryGetValue((service, key), out var v) ? (byte[])v.Clone() : null;
    }

    public bool Remove(string service, string key)
    {
        Interlocked.Increment(ref SyncCalls);
        return _store.TryRemove((service, key), out _);
    }

    public async ValueTask StoreAsync(string service,
        string key,
        ReadOnlyMemory<byte> value,
        string label,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref AsyncStoreCalls);
        await Task.Yield();
        _store[(service, key)] = value.ToArray();
    }

    public async ValueTask<byte[]?> RetrieveAsync(string service, string key, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref AsyncRetrieveCalls);
        await Task.Yield();
        return _store.TryGetValue((service, key), out var v) ? (byte[])v.Clone() : null;
    }

    public async ValueTask<bool> RemoveAsync(string service, string key, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref AsyncRemoveCalls);
        await Task.Yield();
        return _store.TryRemove((service, key), out _);
    }
}
