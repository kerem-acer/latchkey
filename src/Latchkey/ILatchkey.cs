namespace Latchkey;

/// <summary>
/// Stores and retrieves secrets in the operating system's native credential store.
/// </summary>
/// <remarks>
/// <para>
/// All calls are <b>synchronous and potentially blocking</b>: the OS may prompt the
/// user to unlock a keychain. There is no async OS API underneath, so none is offered
/// here — do not wrap these in <c>Task.Run</c> expecting real asynchrony.
/// </para>
/// <para>
/// Instances are thread-safe and may be shared and registered as singletons.
/// </para>
/// </remarks>
public interface ILatchkey
{
    /// <summary>Stores (upserts) a string value, encoded as UTF-8.</summary>
    void Set(string key, string value);

    /// <summary>Stores (upserts) raw bytes.</summary>
    void Set(string key, ReadOnlySpan<byte> value);

    /// <summary>
    /// Returns the value for <paramref name="key"/> decoded as UTF-8, or <c>null</c>
    /// if the key does not exist. A missing key is not exceptional.
    /// </summary>
    /// <remarks>
    /// The returned <see cref="string"/> holds secret material that cannot be zeroed.
    /// Prefer <see cref="GetBytes"/> when you need to wipe the value after use.
    /// </remarks>
    string? Get(string key);

    /// <summary>
    /// Returns the raw bytes for <paramref name="key"/>, or <c>null</c> if the key does
    /// not exist. The caller owns the array and may zero it after use.
    /// </summary>
    byte[]? GetBytes(string key);

    /// <summary>Deletes a key. Returns <c>false</c> if it did not exist. Idempotent.</summary>
    bool Delete(string key);

    /// <summary>Returns whether a value is stored for <paramref name="key"/>.</summary>
    bool Contains(string key);
}
