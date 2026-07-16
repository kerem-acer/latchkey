using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Latchkey.Backends.Files;

namespace Latchkey.Backends.Dpapi;

/// <summary>
/// Stores secrets as DPAPI-encrypted files on disk. Windows only; the key is managed by the OS and
/// tied to the user (<see cref="DpapiScope.CurrentUser" />) or the machine
/// (<see cref="DpapiScope.LocalMachine" />). Opt-in only — <see cref="LatchkeyBackend.Auto" /> never
/// selects it. Same one-file-per-key layout as <see cref="FileBackend" />, but each value is sealed
/// with <c>CryptProtectData</c> before it touches the disk.
/// </summary>
sealed class DpapiBackend : ISecretBackend
{
    const string Extension = ".latchkey";

    readonly string _directory;
    readonly uint _flags;
    bool? _available;

    internal DpapiBackend(DpapiBackendOption option)
    {
        _directory = string.IsNullOrEmpty(option.Path) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Latchkey") : option.Path;
        _flags = Crypt32.CryptProtectUiForbidden |
            (option.Scope == DpapiScope.LocalMachine ? Crypt32.CryptProtectLocalMachine : 0);
    }

    public bool IsAvailable => _available ??= Probe();

    public void Store(string service,
        string key,
        ReadOnlySpan<byte> value,
        string label)
    {
        EnsureAvailable();
        var blob = Protect(value);
        try
        {
            WriteBlob(EntryId.Compute(service, key), blob);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(blob);
        }
    }

    public byte[]? Retrieve(string service, string key)
    {
        EnsureAvailable();
        var blob = ReadBlob(EntryId.Compute(service, key));
        return blob is null ? null : Unprotect(blob);
    }

    public bool Remove(string service, string key)
    {
        EnsureAvailable();
        return DeleteBlob(EntryId.Compute(service, key));
    }

    public async ValueTask StoreAsync(string service,
        string key,
        ReadOnlyMemory<byte> value,
        string label,
        CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        var blob = Protect(value.Span); // DPAPI has no async form; the file write below is the real I/O.
        try
        {
            await WriteBlobAsync(EntryId.Compute(service, key), blob, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(blob);
        }
    }

    public async ValueTask<byte[]?> RetrieveAsync(string service, string key, CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        var blob = await ReadBlobAsync(EntryId.Compute(service, key), cancellationToken).ConfigureAwait(false);
        return blob is null ? null : Unprotect(blob);
    }

    public ValueTask<bool> RemoveAsync(string service, string key, CancellationToken cancellationToken = default) =>
        new(Remove(service, key));

    // --- DPAPI transform ---

    byte[] Protect(ReadOnlySpan<byte> input) => Transform(input, true);

    byte[] Unprotect(ReadOnlySpan<byte> input) => Transform(input, false);

    unsafe byte[] Transform(ReadOnlySpan<byte> input, bool protect)
    {
        // DPAPI wants a non-null pointer even for zero-length input.
        byte dummy = 0;
        fixed (byte* pinned = input)
        {
            var p = input.IsEmpty ? &dummy : pinned;
            var inBlob = new DATA_BLOB
            {
                cbData = (uint)input.Length,
                pbData = (nint)p
            };

            var ok = protect ?
                Crypt32.CryptProtectData(
                    in inBlob,
                    nint.Zero,
                    nint.Zero,
                    nint.Zero,
                    nint.Zero,
                    _flags,
                    out var outBlob) :
                Crypt32.CryptUnprotectData(
                    in inBlob,
                    nint.Zero,
                    nint.Zero,
                    nint.Zero,
                    nint.Zero,
                    _flags,
                    out outBlob);

            if (!ok)
            {
                var err = Marshal.GetLastPInvokeError();
                throw new LatchkeyException(
                    $"{(protect ? "CryptProtectData" : "CryptUnprotectData")} failed (Win32 error {err}).");
            }

            try
            {
                var len = (int)outBlob.cbData;
                if (len == 0)
                {
                    return
                        [];
                }

                var result = new byte[len];
                Marshal.Copy(
                    outBlob.pbData,
                    result,
                    0,
                    len);

                return result;
            }
            finally
            {
                if (outBlob.pbData != nint.Zero)
                {
                    // Wipe the output buffer (plaintext when unprotecting) before releasing it.
                    new Span<byte>((void*)outBlob.pbData, (int)outBlob.cbData).Clear();
                    Crypt32.LocalFree(outBlob.pbData);
                }
            }
        }
    }

    // --- file mechanics ---

    void WriteBlob(string entryId, byte[] blob)
    {
        Directory.CreateDirectory(_directory);
        var temp = TempPath(entryId);
        File.WriteAllBytes(temp, blob);
        File.Move(temp, PathFor(entryId), true);
    }

    async ValueTask WriteBlobAsync(string entryId, byte[] blob, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);
        var temp = TempPath(entryId);
        await File.WriteAllBytesAsync(temp, blob, cancellationToken).ConfigureAwait(false);
        File.Move(temp, PathFor(entryId), true);
    }

    byte[]? ReadBlob(string entryId)
    {
        var path = PathFor(entryId);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    async ValueTask<byte[]?> ReadBlobAsync(string entryId, CancellationToken cancellationToken)
    {
        var path = PathFor(entryId);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false) : null;
    }

    bool DeleteBlob(string entryId)
    {
        var path = PathFor(entryId);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    string PathFor(string entryId) => Path.Combine(_directory, entryId + Extension);

    string TempPath(string entryId) =>
        Path.Combine(_directory, entryId + ".tmp-" + Guid.NewGuid().ToString("N"));

    void EnsureAvailable()
    {
        if (!IsAvailable)
        {
            throw new LatchkeyBackendUnavailableException(BackendSelector.UnavailableMessage(LatchkeyBackend.Dpapi));
        }
    }

    bool Probe()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            ReadOnlySpan<byte> sample =
            [
                1,
                2,
                3
            ];

            return Unprotect(Protect(sample)).AsSpan().SequenceEqual(sample);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (LatchkeyException)
        {
            return false;
        }
    }
}
