namespace Latchkey.Backends.SystemdCreds;

/// <summary>
/// Configuration for <see cref="LatchkeyBackend.SystemdCreds" />, the
/// <c>systemd-creds</c>-encrypted on-disk backend.
/// </summary>
public sealed record SystemdCredsBackendOption : BackendOption
{
    SystemdCredsBackendOption() { }

    /// <summary>The default configuration. Customize with <c>SystemdCredsBackendOption.Default with { ... }</c>.</summary>
    public static SystemdCredsBackendOption Default { get; } = new();

    internal override LatchkeyBackend Backend => LatchkeyBackend.SystemdCreds;

    /// <summary>
    /// Directory the encrypted blobs live in. When <c>null</c>, a per-user
    /// application-data directory is used (<c>~/.local/share/Latchkey</c>).
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Credential name passed to <c>systemd-creds --name=</c> during encryption. The name is
    /// authenticated into the ciphertext, so decryption must use the same value. When
    /// <c>null</c>, a name is derived deterministically from the service and key.
    /// </summary>
    public string? Name { get; init; }
}
