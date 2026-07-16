using System.ComponentModel;
using System.Runtime.InteropServices;

using Latchkey.Backends.Files;

namespace Latchkey.Backends.SystemdCreds;

/// <summary>
/// Stores secrets as <c>systemd-creds</c>-encrypted files on disk. Linux only; requires the
/// <c>systemd-creds</c> tool and access to a TPM or the host key. Opt-in only —
/// <see cref="LatchkeyBackend.Auto" /> never selects it. Same one-file-per-key layout as
/// <see cref="FileBackend" />, but each value is sealed by <c>systemd-creds encrypt</c> first; the
/// ciphertext is treated as opaque and fed back verbatim to <c>systemd-creds decrypt</c>.
/// </summary>
sealed class SystemdCredsBackend : ISecretBackend
{
    const string Executable = "systemd-creds";
    const string Extension = ".latchkey";

    readonly string _directory;
    readonly string? _namePrefix;
    bool? _available;

    internal SystemdCredsBackend(SystemdCredsBackendOption option)
    {
        _directory = string.IsNullOrEmpty(option.Path) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Latchkey") : option.Path;
        _namePrefix = string.IsNullOrEmpty(option.Name) ? null : option.Name;
    }

    public bool IsAvailable => _available ??= Probe();

    public void Store(string service,
        string key,
        ReadOnlySpan<byte> value,
        string label)
    {
        EnsureAvailable();
        var id = EntryId.Compute(service, key);
        WriteBlob(id, Encrypt(value, id));
    }

    public byte[]? Retrieve(string service, string key)
    {
        EnsureAvailable();
        var id = EntryId.Compute(service, key);
        var blob = ReadBlob(id);
        return blob is null ? null : Decrypt(blob, id);
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
        var id = EntryId.Compute(service, key);
        var blob = await EncryptAsync(value, id, cancellationToken).ConfigureAwait(false);
        await WriteBlobAsync(id, blob, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<byte[]?> RetrieveAsync(string service, string key, CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        var id = EntryId.Compute(service, key);
        var blob = await ReadBlobAsync(id, cancellationToken).ConfigureAwait(false);
        return blob is null ? null : await DecryptAsync(blob, id, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<bool> RemoveAsync(string service, string key, CancellationToken cancellationToken = default) =>
        new(Remove(service, key));

    // --- systemd-creds transform (ciphertext is opaque; stored and replayed verbatim) ---

    byte[] Encrypt(ReadOnlySpan<byte> plaintext, string id) =>
        Run(
            [
                "encrypt",
                "--name=" + EffectiveName(id),
                "-",
                "-"
            ],
            plaintext,
            "encrypt");

    byte[] Decrypt(ReadOnlySpan<byte> ciphertext, string id) =>
        Run(
            [
                "decrypt",
                "--name=" + EffectiveName(id),
                "-",
                "-"
            ],
            ciphertext,
            "decrypt");

    ValueTask<byte[]> EncryptAsync(ReadOnlyMemory<byte> plaintext, string id, CancellationToken cancellationToken) =>
        RunAsync(
            [
                "encrypt",
                "--name=" + EffectiveName(id),
                "-",
                "-"
            ],
            plaintext,
            "encrypt",
            cancellationToken);

    ValueTask<byte[]> DecryptAsync(ReadOnlyMemory<byte> ciphertext, string id, CancellationToken cancellationToken) =>
        RunAsync(
            [
                "decrypt",
                "--name=" + EffectiveName(id),
                "-",
                "-"
            ],
            ciphertext,
            "decrypt",
            cancellationToken);

    string EffectiveName(string id) => _namePrefix is null ? id : $"{_namePrefix}.{id}";

    static byte[] Run(IReadOnlyList<string> arguments, ReadOnlySpan<byte> stdin, string operation)
    {
        var result = ProcessRunner.Run(Executable, arguments, stdin);
        ThrowIfFailed(result, operation);
        return result.StandardOutput;
    }

    static async ValueTask<byte[]> RunAsync(IReadOnlyList<string> arguments,
        ReadOnlyMemory<byte> stdin,
        string operation,
        CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(
            Executable,
            arguments,
            stdin,
            null,
            cancellationToken).ConfigureAwait(false);

        ThrowIfFailed(result, operation);
        return result.StandardOutput;
    }

    static void ThrowIfFailed(ProcessResult result, string operation)
    {
        if (result.ExitCode != 0)
        {
            throw new LatchkeyException($"systemd-creds {operation} failed (exit {result.ExitCode}): {result.StandardError.Trim()}");
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
            throw new LatchkeyBackendUnavailableException(BackendSelector.UnavailableMessage(LatchkeyBackend.SystemdCreds));
        }
    }

    bool Probe()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
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

            return Decrypt(Encrypt(sample, "latchkey-probe"), "latchkey-probe").AsSpan().SequenceEqual(sample);
        }
        catch (Win32Exception)
        {
            return false; // systemd-creds not found
        }
        catch (LatchkeyException)
        {
            return false; // no TPM / host-key access
        }
        catch (IOException)
        {
            return false;
        }
    }
}
