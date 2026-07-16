using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;

namespace Latchkey.Backends.Pass;

/// <summary>
/// Stores secrets in the <c>pass</c> Unix password manager (GPG-encrypted files). Shells out to
/// the <c>pass</c> binary; secrets are piped on stdin, never on the command line. Values are
/// base64-encoded so binary content round-trips through pass's line-oriented storage.
/// </summary>
sealed class PassBackend : ISecretBackend
{
    const string Executable = "pass";
    const string NotFoundMarker = "is not in the password store";
    readonly IReadOnlyDictionary<string, string?>? _environment;

    readonly string? _prefix;
    bool? _available;

    internal PassBackend(PassBackendOption option)
    {
        _prefix = string.IsNullOrEmpty(option.Prefix) ? null : option.Prefix;
        _environment = string.IsNullOrEmpty(option.StoreDir) ?
            null :
            new Dictionary<string, string?>
            {
                ["PASSWORD_STORE_DIR"] = option.StoreDir
            };
    }

    public bool IsAvailable => _available ??= Probe();

    public void Store(string service,
        string key,
        ReadOnlySpan<byte> value,
        string label)
    {
        EnsureAvailable();
        var stdin = Encoding.ASCII.GetBytes(Convert.ToBase64String(value));
        try
        {
            var result = ProcessRunner.Run(
                Executable,
                [
                    "insert",
                    "-m",
                    "-f",
                    EntryName(service, key)
                ],
                stdin,
                _environment);

            ThrowIfStoreFailed(result);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(stdin);
        }
    }

    public byte[]? Retrieve(string service, string key)
    {
        EnsureAvailable();
        var result = ProcessRunner.Run(
            Executable,
            [
                "show",
                EntryName(service, key)
            ],
            default,
            _environment);

        return DecodeRetrieve(result);
    }

    public bool Remove(string service, string key)
    {
        EnsureAvailable();
        var result = ProcessRunner.Run(
            Executable,
            [
                "rm",
                "-f",
                EntryName(service, key)
            ],
            default,
            _environment);

        return InterpretRemove(result);
    }

    public async ValueTask StoreAsync(string service,
        string key,
        ReadOnlyMemory<byte> value,
        string label,
        CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        var stdin = Encoding.ASCII.GetBytes(Convert.ToBase64String(value.Span));
        try
        {
            var result = await ProcessRunner
                .RunAsync(
                    Executable,
                    [
                        "insert",
                        "-m",
                        "-f",
                        EntryName(service, key)
                    ],
                    stdin,
                    _environment,
                    cancellationToken)
                .ConfigureAwait(false);

            ThrowIfStoreFailed(result);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(stdin);
        }
    }

    public async ValueTask<byte[]?> RetrieveAsync(string service, string key, CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        var result = await ProcessRunner
            .RunAsync(
                Executable,
                [
                    "show",
                    EntryName(service, key)
                ],
                default,
                _environment,
                cancellationToken)
            .ConfigureAwait(false);

        return DecodeRetrieve(result);
    }

    public async ValueTask<bool> RemoveAsync(string service, string key, CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        var result = await ProcessRunner
            .RunAsync(
                Executable,
                [
                    "rm",
                    "-f",
                    EntryName(service, key)
                ],
                default,
                _environment,
                cancellationToken)
            .ConfigureAwait(false);

        return InterpretRemove(result);
    }

    static void ThrowIfStoreFailed(ProcessResult result)
    {
        if (result.ExitCode != 0)
        {
            throw new LatchkeyException($"pass insert failed (exit {result.ExitCode}): {result.StandardError.Trim()}");
        }
    }

    static byte[]? DecodeRetrieve(ProcessResult result)
    {
        if (result.ExitCode != 0)
        {
            if (IsNotFound(result.StandardError))
            {
                return null;
            }

            throw new LatchkeyException($"pass show failed (exit {result.ExitCode}): {result.StandardError.Trim()}");
        }

        var encoded = Encoding.ASCII.GetString(result.StandardOutput).Trim();
        try
        {
            return Convert.FromBase64String(encoded);
        }
        catch (FormatException ex)
        {
            throw new LatchkeyException(
                "A pass entry under this key is not valid Latchkey data (expected base64). It was most " +
                "likely created by another tool; Latchkey refuses to return possibly-corrupted bytes.",
                ex);
        }
    }

    static bool InterpretRemove(ProcessResult result)
    {
        if (result.ExitCode == 0)
        {
            return true;
        }

        if (IsNotFound(result.StandardError))
        {
            return false;
        }

        throw new LatchkeyException($"pass rm failed (exit {result.ExitCode}): {result.StandardError.Trim()}");
    }

    string EntryName(string service, string key)
    {
        var body = $"{Uri.EscapeDataString(service)}/{Uri.EscapeDataString(key)}";
        return _prefix is null ? body : $"{_prefix}/{body}";
    }

    static bool IsNotFound(string stderr) => stderr.Contains(NotFoundMarker, StringComparison.Ordinal);

    void EnsureAvailable()
    {
        if (!IsAvailable)
        {
            throw new LatchkeyBackendUnavailableException(BackendSelector.UnavailableMessage(LatchkeyBackend.Pass));
        }
    }

    bool Probe()
    {
        try
        {
            return ProcessRunner.Run(
                    Executable,
                    [
                        "ls"
                    ],
                    default,
                    _environment).ExitCode ==
                0;
        }
        catch (Win32Exception)
        {
            return false; // pass binary not found
        }
        catch (IOException)
        {
            return false;
        }
    }
}
