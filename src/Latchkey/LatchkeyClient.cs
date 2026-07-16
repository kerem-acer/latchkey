using System.Buffers;
using System.Security.Cryptography;
using System.Text;

using Latchkey.Backends;

namespace Latchkey;

/// <summary>
/// The <see cref="ILatchkey" /> implementation: validation, UTF-8 encoding, and buffer
/// hygiene. Contains no platform conditionals — it delegates storage to an
/// <see cref="ISecretBackend" /> chosen by <see cref="BackendSelector" />.
/// </summary>
sealed class LatchkeyClient : ILatchkey
{
    /// <summary>Encode values up to this many bytes on the stack; rent from the pool above it.</summary>
    const int StackAllocThreshold = 256;

    readonly ISecretBackend _backend;
    readonly string _label;
    readonly ArrayPool<byte> _pool;
    readonly string _service;

    internal LatchkeyClient(ISecretBackend backend,
        string service,
        string label,
        ArrayPool<byte>? pool = null)
    {
        _backend = backend;
        _service = service;
        _label = label;
        _pool = pool ?? ArrayPool<byte>.Shared;
    }

    public void Set(string key, string value)
    {
        Validation.ValidateKey(key);
        ArgumentNullException.ThrowIfNull(value);

        var maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);

        byte[]? rented = null;
        var buffer = maxBytes <= StackAllocThreshold ? stackalloc byte[StackAllocThreshold] : rented = _pool.Rent(maxBytes);
        try
        {
            var written = Encoding.UTF8.GetBytes(value, buffer);
            _backend.Store(
                _service,
                key,
                buffer[..written],
                _label);
        }
        finally
        {
            // Wipe the secret bytes whether we used the stack or the pool.
            if (rented is not null)
            {
                CryptographicOperations.ZeroMemory(rented.AsSpan(0, maxBytes));
                _pool.Return(rented);
            }
            else
            {
                CryptographicOperations.ZeroMemory(buffer);
            }
        }
    }

    public void Set(string key, ReadOnlySpan<byte> value)
    {
        Validation.ValidateKey(key);
        _backend.Store(
            _service,
            key,
            value,
            _label);
    }

    public string? Get(string key)
    {
        Validation.ValidateKey(key);

        var bytes = _backend.Retrieve(_service, key);
        if (bytes is null)
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            // The intermediate array held secret material; wipe it. (The returned
            // string cannot be zeroed — GetBytes is the honest primitive for that.)
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public byte[]? GetBytes(string key)
    {
        Validation.ValidateKey(key);
        return _backend.Retrieve(_service, key);
    }

    public bool Delete(string key)
    {
        Validation.ValidateKey(key);
        return _backend.Remove(_service, key);
    }

    public bool Contains(string key)
    {
        Validation.ValidateKey(key);

        var bytes = _backend.Retrieve(_service, key);
        if (bytes is null)
        {
            return false;
        }

        CryptographicOperations.ZeroMemory(bytes);
        return true;
    }

    public async ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        Validation.ValidateKey(key);
        ArgumentNullException.ThrowIfNull(value);

        var maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);

        // Unlike Set, we cannot stackalloc across the await, so always rent from the pool.
        var rented = _pool.Rent(maxBytes);
        try
        {
            var written = Encoding.UTF8.GetBytes(value, rented);
            await _backend.StoreAsync(
                    _service,
                    key,
                    rented.AsMemory(0, written),
                    _label,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rented.AsSpan(0, maxBytes));
            _pool.Return(rented);
        }
    }

    public ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        Validation.ValidateKey(key);
        return _backend.StoreAsync(
            _service,
            key,
            value,
            _label,
            cancellationToken);
    }

    public async ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        Validation.ValidateKey(key);

        var bytes = await _backend.RetrieveAsync(_service, key, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            // The intermediate array held secret material; wipe it. (The returned
            // string cannot be zeroed — GetBytesAsync is the honest primitive for that.)
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public ValueTask<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default)
    {
        Validation.ValidateKey(key);
        return _backend.RetrieveAsync(_service, key, cancellationToken);
    }

    public ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Validation.ValidateKey(key);
        return _backend.RemoveAsync(_service, key, cancellationToken);
    }

    public async ValueTask<bool> ContainsAsync(string key, CancellationToken cancellationToken = default)
    {
        Validation.ValidateKey(key);

        var bytes = await _backend.RetrieveAsync(_service, key, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return false;
        }

        CryptographicOperations.ZeroMemory(bytes);
        return true;
    }
}
