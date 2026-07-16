namespace Latchkey.Backends.Files;

/// <summary>
/// Stores secrets as <b>plaintext</b> files on disk (all platforms). It invents no encryption:
/// at-rest safety is only the file permissions and any full-disk encryption around the store
/// directory. Opt-in only — <see cref="LatchkeyBackend.Auto" /> never selects it — and it does not
/// pretend to be a keyring. For at-rest protection use <see cref="LatchkeyBackend.Dpapi" />
/// (Windows) or <see cref="LatchkeyBackend.SystemdCreds" /> / <see cref="LatchkeyBackend.Pass" />
/// (Linux).
/// </summary>
/// <remarks>
/// Each key is one file named <c>Base64Url(SHA-256(service "\0" key))</c> — fixed length, so long
/// keys never blow the filesystem's name limit, and binary-safe. Writes are atomic (temp file +
/// rename); there is no cross-process lock, so concurrent writers to the same key are
/// last-writer-wins.
/// </remarks>
sealed class FileBackend : ISecretBackend
{
    const string Extension = ".latchkey";

    readonly string _directory;
    bool? _available;

    internal FileBackend(FileBackendOption option)
    {
        _directory = string.IsNullOrEmpty(option.Path) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Latchkey") : option.Path;
    }

    public bool IsAvailable => _available ??= ProbeWritable();

    public void Store(string service,
        string key,
        ReadOnlySpan<byte> value,
        string label)
    {
        EnsureAvailable();
        Directory.CreateDirectory(_directory);
        var id = EntryId.Compute(service, key);
        var temp = TempPath(id);
        using (var stream = new FileStream(
            temp,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None))
        {
            stream.Write(value);
        }

        File.Move(temp, PathFor(id), true);
    }

    public byte[]? Retrieve(string service, string key)
    {
        EnsureAvailable();
        var path = PathFor(EntryId.Compute(service, key));
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public bool Remove(string service, string key)
    {
        EnsureAvailable();
        var path = PathFor(EntryId.Compute(service, key));
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public async ValueTask StoreAsync(string service,
        string key,
        ReadOnlyMemory<byte> value,
        string label,
        CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        Directory.CreateDirectory(_directory);
        var id = EntryId.Compute(service, key);
        var temp = TempPath(id);
        await using (var stream = new FileStream(
            temp,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            true))
        {
            await stream.WriteAsync(value, cancellationToken).ConfigureAwait(false);
        }

        File.Move(temp, PathFor(id), true);
    }

    public async ValueTask<byte[]?> RetrieveAsync(string service, string key, CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        var path = PathFor(EntryId.Compute(service, key));
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false) : null;
    }

    public ValueTask<bool> RemoveAsync(string service, string key, CancellationToken cancellationToken = default) =>
        new(Remove(service, key));

    void EnsureAvailable()
    {
        if (!IsAvailable)
        {
            throw new LatchkeyBackendUnavailableException(BackendSelector.UnavailableMessage(LatchkeyBackend.File));
        }
    }

    bool ProbeWritable()
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var probe = Path.Combine(_directory, ".latchkey-probe-" + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    string PathFor(string entryId) => Path.Combine(_directory, entryId + Extension);

    string TempPath(string entryId) =>
        Path.Combine(_directory, entryId + ".tmp-" + Guid.NewGuid().ToString("N"));
}
