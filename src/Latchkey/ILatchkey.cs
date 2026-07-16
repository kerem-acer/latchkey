namespace Latchkey;

/// <summary>
/// Stores and retrieves secrets in the operating system's native credential store.
/// </summary>
/// <remarks>
///     <para>
///     The synchronous calls are <b>potentially blocking</b>: the OS may prompt the user to
///     unlock a keychain. The <c>*Async</c> overloads exist so file-, process-, and
///     network-backed backends can do real async I/O; the native OS stores have no async API
///     underneath, so their async form completes synchronously (it is not <c>Task.Run</c>
///     theatre). To keep a UI thread responsive against a native store, offload the
///     synchronous call with <c>Task.Run</c> at the call site — that is the honest way to
///     move blocking work off the thread.
///     </para>
///     <para>
///     Instances are thread-safe and may be shared and registered as singletons.
///     </para>
/// </remarks>
public interface ILatchkey
{
    /// <summary>Stores (upserts) a string value, encoded as UTF-8.</summary>
    void Set(string key, string value);

    /// <summary>Stores (upserts) raw bytes.</summary>
    void Set(string key, ReadOnlySpan<byte> value);

    /// <summary>
    /// Returns the value for <paramref name="key" /> decoded as UTF-8, or <c>null</c>
    /// if the key does not exist. A missing key is not exceptional.
    /// </summary>
    /// <remarks>
    /// The returned <see cref="string" /> holds secret material that cannot be zeroed.
    /// Prefer <see cref="GetBytes" /> when you need to wipe the value after use.
    /// </remarks>
    string? Get(string key);

    /// <summary>
    /// Returns the raw bytes for <paramref name="key" />, or <c>null</c> if the key does
    /// not exist. The caller owns the array and may zero it after use.
    /// </summary>
    byte[]? GetBytes(string key);

    /// <summary>Deletes a key. Returns <c>false</c> if it did not exist. Idempotent.</summary>
    bool Delete(string key);

    /// <summary>Returns whether a value is stored for <paramref name="key" />.</summary>
    bool Contains(string key);

    /// <summary>Async counterpart to <see cref="Set(string, string)" />.</summary>
    ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Async counterpart to <see cref="Set(string, ReadOnlySpan{byte})" />. Takes
    /// <see cref="ReadOnlyMemory{T}" /> because a span cannot cross an <c>await</c>.
    /// </summary>
    ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);

    /// <summary>Async counterpart to <see cref="Get" />.</summary>
    ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Async counterpart to <see cref="GetBytes" />.</summary>
    ValueTask<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Async counterpart to <see cref="Delete" />.</summary>
    ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Async counterpart to <see cref="Contains" />.</summary>
    ValueTask<bool> ContainsAsync(string key, CancellationToken cancellationToken = default);
}
