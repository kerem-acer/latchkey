using System.Buffers;
using System.Security.Cryptography;
using System.Text;

using Latchkey.Backends.Dpapi;
using Latchkey.Backends.Files;
using Latchkey.Backends.SystemdCreds;

namespace Latchkey.Backends;

/// <summary>
/// Computes the stable, filesystem-safe file-name stem shared by the blob-store backends
/// (<see cref="FileBackend" />, <see cref="SystemdCredsBackend" />, <see cref="DpapiBackend" />):
/// <c>Base64Url(SHA-256(service "\0" key))</c> — fixed length regardless of key size, and
/// binary-safe. The null separator keeps <c>("ab","c")</c> and <c>("a","bc")</c> from colliding.
/// </summary>
static class EntryId
{
    // service/key fit on the stack in the common case; longer identifiers rent from the pool.
    const int StackThreshold = 256;

    internal static string Compute(string service, string key)
    {
        var max = Encoding.UTF8.GetMaxByteCount(service.Length) + 1 + Encoding.UTF8.GetMaxByteCount(key.Length);

        byte[]? rented = null;
        var buffer = max <= StackThreshold ? stackalloc byte[StackThreshold] : rented = ArrayPool<byte>.Shared.Rent(max);
        try
        {
            var n = Encoding.UTF8.GetBytes(service, buffer);
            buffer[n++] = 0;
            n += Encoding.UTF8.GetBytes(key, buffer[n..]);

            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(buffer[..n], hash);
            return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
        finally
        {
            // The buffer holds non-secret identifiers, so return it without wiping — only the
            // secret value is subject to buffer hygiene (see LatchkeyClient).
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
