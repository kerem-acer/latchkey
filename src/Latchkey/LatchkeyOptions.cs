namespace Latchkey;

/// <summary>Configuration for a <see cref="ILatchkey"/> instance.</summary>
public sealed class LatchkeyOptions
{
    /// <summary>Namespace for all keys. Reverse-DNS recommended, e.g. "dev.example.myapp".</summary>
    public required string ServiceName { get; set; }

    /// <summary>Human-readable label shown in Keychain Access / Seahorse. Defaults to ServiceName.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Force a specific backend instead of auto-detecting. Mainly for tests.</summary>
    public LatchkeyBackend Backend { get; set; } = LatchkeyBackend.Auto;

    /// <summary>
    /// Supply your own store. When set, <see cref="Backend"/> is ignored and no detection runs.
    /// This is the supported answer for headless Linux, containers, and anywhere
    /// the caller has a real secret source we can't pick for them (systemd
    /// credentials, TPM, KMS-derived key, pass/GPG).
    /// </summary>
    public ISecretBackend? CustomBackend { get; set; }
}
