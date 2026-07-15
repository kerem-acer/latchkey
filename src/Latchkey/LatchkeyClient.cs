using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Latchkey;

/// <summary>
/// The <see cref="ILatchkey"/> implementation: validation, UTF-8 encoding, and buffer
/// hygiene. Contains no platform conditionals — it delegates storage to an
/// <see cref="ISecretBackend"/> chosen by <see cref="BackendSelector"/>.
/// </summary>
internal sealed class LatchkeyClient : ILatchkey
{
    /// <summary>Encode values up to this many bytes on the stack; rent from the pool above it.</summary>
    private const int StackAllocThreshold = 256;

    private readonly ISecretBackend _backend;
    private readonly string _service;
    private readonly string _label;
    private readonly ArrayPool<byte> _pool;

    internal LatchkeyClient(ISecretBackend backend, string service, string label, ArrayPool<byte>? pool = null)
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

        int maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);

        byte[]? rented = null;
        Span<byte> buffer = maxBytes <= StackAllocThreshold
            ? stackalloc byte[StackAllocThreshold]
            : (rented = _pool.Rent(maxBytes));
        try
        {
            int written = Encoding.UTF8.GetBytes(value, buffer);
            _backend.Store(_service, key, buffer[..written], _label);
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
        _backend.Store(_service, key, value, _label);
    }

    public string? Get(string key)
    {
        Validation.ValidateKey(key);

        byte[]? bytes = _backend.Retrieve(_service, key);
        if (bytes is null)
            return null;

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

        byte[]? bytes = _backend.Retrieve(_service, key);
        if (bytes is null)
            return false;

        CryptographicOperations.ZeroMemory(bytes);
        return true;
    }
}
