namespace Latchkey.Backends.Pass;

/// <summary>
/// Configuration for <see cref="LatchkeyBackend.Pass" />, the <c>pass</c> Unix password
/// manager backend.
/// </summary>
public sealed record PassBackendOption : BackendOption
{
    PassBackendOption() { }

    /// <summary>The default configuration. Customize with <c>PassBackendOption.Default with { ... }</c>.</summary>
    public static PassBackendOption Default { get; } = new();

    internal override LatchkeyBackend Backend => LatchkeyBackend.Pass;

    /// <summary>
    /// The password-store directory (exported to <c>pass</c> as <c>PASSWORD_STORE_DIR</c>).
    /// When <c>null</c>, <c>pass</c> uses its own default (<c>~/.password-store</c> or an
    /// existing <c>PASSWORD_STORE_DIR</c>).
    /// </summary>
    public string? StoreDir { get; init; }

    /// <summary>
    /// Prefix under which entries are created, e.g. <c>"latchkey"</c> gives
    /// <c>latchkey/{service}/{key}</c>. When <c>null</c>, entries are created directly under
    /// <c>{service}/{key}</c>.
    /// </summary>
    public string? Prefix { get; init; }
}
