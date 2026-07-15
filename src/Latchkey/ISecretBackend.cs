namespace Latchkey;

/// <summary>
/// A place secrets live. Public so callers can plug in their own — see
/// <see cref="LatchkeyOptions.CustomBackend"/>. Implementations must be thread-safe.
/// Values are raw bytes; do not assume UTF-8 or any encoding.
/// </summary>
public interface ISecretBackend
{
    /// <summary>Stores (upserts) a secret value for the given service and key.</summary>
    /// <param name="service">Namespace for the key.</param>
    /// <param name="key">Key within the service namespace.</param>
    /// <param name="value">Raw secret bytes. May be empty. Never null.</param>
    /// <param name="label">Human-readable label for OS credential UIs.</param>
    void Store(string service, string key, ReadOnlySpan<byte> value, string label);

    /// <summary>Retrieves the raw bytes for a key, or <c>null</c> if it does not exist.</summary>
    byte[]? Retrieve(string service, string key);

    /// <summary>Removes a key. Returns <c>false</c> if it did not exist. Idempotent.</summary>
    bool Remove(string service, string key);

    /// <summary>
    /// Whether this backend can actually store and retrieve secrets on this machine
    /// right now. Implementations should probe (e.g. attempt a lookup), not merely
    /// check for the presence of a library or environment variable.
    /// </summary>
    bool IsAvailable { get; }
}
