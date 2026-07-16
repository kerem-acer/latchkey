namespace Latchkey.Backends;

/// <summary>
/// Base type for the typed configuration objects supplied via
/// <see cref="LatchkeyOptions.BackendOptions" />. A <see cref="BackendOption" /> does not
/// <em>select</em> a backend — <see cref="LatchkeyOptions.Backend" /> does that — it only
/// parameterizes the backend once selected. At most one option per
/// <see cref="LatchkeyBackend" /> may be supplied.
/// </summary>
/// <remarks>
/// This hierarchy is closed: the four configurable backends each have exactly one option
/// type. Bringing your own store is still done through
/// <see cref="LatchkeyOptions.CustomBackend" />, not by subclassing this.
/// </remarks>
public abstract record BackendOption
{
    // Internal ctor closes the hierarchy to this assembly without sealing off the public
    // type from being referenced by callers.
    private protected BackendOption() { }

    /// <summary>Which backend this option configures. Used to match it to the selected backend.</summary>
    internal abstract LatchkeyBackend Backend { get; }
}
