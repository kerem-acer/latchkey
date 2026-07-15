using Latchkey;

namespace Latchkey.Extensions.DependencyInjection.Tests.Support;

/// <summary>Accepts writes but never returns a value — makes the persistence round-trip fail.</summary>
internal sealed class NullReadBackend : ISecretBackend
{
    public bool IsAvailable => true;
    public void Store(string service, string key, ReadOnlySpan<byte> value, string label) { }
    public byte[]? Retrieve(string service, string key) => null;
    public bool Remove(string service, string key) => true;
}

/// <summary>Throws on every operation — exercises the persistence check's exception-wrapping paths.</summary>
internal sealed class ThrowingBackend : ISecretBackend
{
    public bool IsAvailable => true;
    public void Store(string service, string key, ReadOnlySpan<byte> value, string label) => throw new LatchkeyException("store boom");
    public byte[]? Retrieve(string service, string key) => throw new LatchkeyException("retrieve boom");
    public bool Remove(string service, string key) => throw new LatchkeyException("remove boom");
}
