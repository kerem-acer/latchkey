using Latchkey.Backends;

namespace Latchkey;

/// <summary>Configuration for a <see cref="ILatchkey" /> instance.</summary>
public sealed class LatchkeyOptions
{
    /// <summary>Namespace for all keys. Reverse-DNS recommended, e.g. "dev.example.myapp".</summary>
    public required string ServiceName { get; set; }

    /// <summary>Human-readable label shown in Keychain Access / Seahorse. Defaults to ServiceName.</summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Force a specific backend. When <see cref="LatchkeyBackend.Auto" /> (the default), selection
    /// runs through <see cref="Backends" />; any other value forces that single backend and ignores
    /// the map. <see cref="CustomBackend" /> wins over both.
    /// </summary>
    public LatchkeyBackend Backend { get; init; } = LatchkeyBackend.Auto;

    /// <summary>
    /// The per-OS backend priority map consulted when <see cref="Backend" /> is
    /// <see cref="LatchkeyBackend.Auto" />. Seeded with the native default; customize it fluently,
    /// e.g. <c>Backends.For(OSPlatform.Windows, LatchkeyBackend.Dpapi).ForAll(LatchkeyBackend.File)</c>.
    /// </summary>
    public BackendMap Backends { get; init; } = new();

    /// <summary>
    /// Typed configuration for the configurable backends (<see cref="LatchkeyBackend.File" />,
    /// <see cref="LatchkeyBackend.Dpapi" />, <see cref="LatchkeyBackend.Pass" />,
    /// <see cref="LatchkeyBackend.SystemdCreds" />). These entries do <em>not</em> select a
    /// backend — <see cref="Backend" /> does — they only parameterize the one that is selected.
    /// At most one <see cref="BackendOption" /> per backend may be supplied. Ignored entirely
    /// when <see cref="CustomBackend" /> is set.
    /// </summary>
    public IReadOnlyList<BackendOption> BackendOptions { get; init; } =
        [];

    /// <summary>
    /// Supply your own store. When set, <see cref="Backend" /> is ignored and no detection runs.
    /// This is the supported answer for headless Linux, containers, and anywhere
    /// the caller has a real secret source we can't pick for them (systemd
    /// credentials, TPM, KMS-derived key, pass/GPG).
    /// </summary>
    public ISecretBackend? CustomBackend { get; init; }
}
