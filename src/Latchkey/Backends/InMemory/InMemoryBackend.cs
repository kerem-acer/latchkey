using System.Collections.Concurrent;

namespace Latchkey.Backends.InMemory;

/// <summary>
/// Thread-safe, in-process, non-persistent backend. For tests and benchmarks only —
/// it never survives the process. Selected only when explicitly requested via
/// <see cref="LatchkeyBackend.InMemory" /> or supplied as a custom backend.
/// </summary>
sealed class InMemoryBackend : ISecretBackend
{
    readonly ConcurrentDictionary<(string Service, string Key), byte[]> _store = new();

    public bool IsAvailable => true;

    public void Store(string service,
        string key,
        ReadOnlySpan<byte> value,
        string label) =>
        // Copy the span: the caller owns (and may zero) the source buffer after this returns.
        _store[(service, key)] = value.ToArray();

    public byte[]? Retrieve(string service, string key) =>
        // Return a copy so callers cannot mutate stored state, and can zero their copy.
        _store.TryGetValue((service, key), out var value) ? (byte[])value.Clone() : null;

    public bool Remove(string service, string key) => _store.TryRemove((service, key), out _);
}
