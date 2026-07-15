namespace Latchkey.Tests.Support;

/// <summary>
/// A zero-allocation backend: Store discards, Retrieve returns a cached array, Remove is a no-op.
/// Used to isolate and measure the Latchkey *layer's* own allocations, with no backend noise.
/// </summary>
internal sealed class SinkBackend : ISecretBackend
{
    private readonly byte[] _cached;

    public SinkBackend(byte[]? cached = null) => _cached = cached ?? new byte[16];

    public bool IsAvailable => true;

    public void Store(string service, string key, ReadOnlySpan<byte> value, string label)
    {
        // Discard: we are measuring the caller's allocations, not storage.
    }

    public byte[]? Retrieve(string service, string key) => _cached;

    public bool Remove(string service, string key) => true;
}
