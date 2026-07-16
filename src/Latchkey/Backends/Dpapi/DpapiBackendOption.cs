namespace Latchkey.Backends.Dpapi;

/// <summary>Which DPAPI protection scope encrypts the stored blobs.</summary>
public enum DpapiScope
{
    /// <summary>
    /// Tie the key to the current Windows user (the default and the safer choice). Only
    /// processes running as this user can decrypt.
    /// </summary>
    CurrentUser,

    /// <summary>
    /// Tie the key to the machine. <b>Any</b> user on the machine can decrypt — weaker.
    /// Use only when a value must survive across user accounts on the same box.
    /// </summary>
    LocalMachine
}

/// <summary>
/// Configuration for <see cref="LatchkeyBackend.Dpapi" />, the Windows DPAPI-encrypted
/// on-disk backend.
/// </summary>
public sealed record DpapiBackendOption : BackendOption
{
    DpapiBackendOption() { }

    /// <summary>The default configuration. Customize with <c>DpapiBackendOption.Default with { ... }</c>.</summary>
    public static DpapiBackendOption Default { get; } = new();

    internal override LatchkeyBackend Backend => LatchkeyBackend.Dpapi;

    /// <summary>
    /// Directory the encrypted files live in. When <c>null</c>, a per-user
    /// application-data directory is used (<c>%LOCALAPPDATA%\Latchkey</c>).
    /// </summary>
    public string? Path { get; init; }

    /// <summary>The DPAPI protection scope. Defaults to <see cref="DpapiScope.CurrentUser" />.</summary>
    public DpapiScope Scope { get; init; } = DpapiScope.CurrentUser;
}
