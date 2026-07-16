using System.Collections.Concurrent;

using Latchkey.Backends;

namespace Latchkey.Tests.Support;

/// <summary>
/// Hand-written recording <see cref="ISecretBackend" />. TUnit.Mocks (like every mocking
/// library) cannot mock <see cref="ISecretBackend.Store" /> because its value parameter is a
/// <see cref="ReadOnlySpan{T}" /> ref struct, so this fake stands in for span-involving tests:
/// it copies the bytes it receives so assertions can inspect exactly what the client passed.
/// </summary>
sealed class RecordingBackend : ISecretBackend
{
    readonly ConcurrentDictionary<(string, string), byte[]> _store = new();
    public string? LastKey;
    public string? LastLabel;

    public string? LastService;
    public byte[]? LastStoredValue;
    public int RemoveCalls;
    public int RetrieveCalls;

    public int StoreCalls;

    public int Count => _store.Count;

    public bool IsAvailable { get; set; } = true;

    public void Store(string service,
        string key,
        ReadOnlySpan<byte> value,
        string label)
    {
        Interlocked.Increment(ref StoreCalls);
        LastService = service;
        LastKey = key;
        LastLabel = label;
        LastStoredValue = value.ToArray();
        _store[(service, key)] = value.ToArray();
    }

    public byte[]? Retrieve(string service, string key)
    {
        Interlocked.Increment(ref RetrieveCalls);
        return _store.TryGetValue((service, key), out var v) ? (byte[])v.Clone() : null;
    }

    public bool Remove(string service, string key)
    {
        Interlocked.Increment(ref RemoveCalls);
        return _store.TryRemove((service, key), out _);
    }
}
