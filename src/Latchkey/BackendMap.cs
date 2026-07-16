using System.Runtime.InteropServices;

namespace Latchkey;

/// <summary>
/// Maps each operating system to an ordered list of backends to try — first is preferred, the
/// rest are fallbacks (the first one that is actually available wins). A fresh map is seeded with
/// the native default (Windows → Credential Manager, macOS → Keychain, Linux → Secret Service);
/// customize it fluently. The all-OS list (<see cref="ForAll" />) is appended after the current
/// OS's list as a universal fallback.
/// </summary>
/// <remarks>
/// Used only when <see cref="LatchkeyOptions.Backend" /> is <see cref="LatchkeyBackend.Auto" />.
/// The map values must not contain <see cref="LatchkeyBackend.Auto" />.
/// </remarks>
public sealed class BackendMap
{
    readonly Dictionary<OSPlatform, IReadOnlyList<LatchkeyBackend>> _byOs =
        [];

    IReadOnlyList<LatchkeyBackend> _all =
        [];

    /// <summary>Creates a map seeded with the native default for each operating system.</summary>
    public BackendMap()
    {
        _byOs[OSPlatform.Windows] =
        [
            LatchkeyBackend.WindowsCredentialManager
        ];

        _byOs[OSPlatform.OSX] =
        [
            LatchkeyBackend.MacOSKeychain
        ];

        _byOs[OSPlatform.Linux] =
        [
            LatchkeyBackend.SecretService
        ];
    }

    /// <summary>Sets the ordered backends to try on <paramref name="os" />, replacing any existing entry.</summary>
    public BackendMap For(OSPlatform os, params LatchkeyBackend[] backends)
    {
        ArgumentNullException.ThrowIfNull(backends);
        _byOs[os] =
        [
            .. backends
        ];

        return this;
    }

    /// <summary>
    /// Sets the ordered backends tried on <b>every</b> OS (the <c>null</c> entry), appended after
    /// whatever the current OS's own list resolves to.
    /// </summary>
    public BackendMap ForAll(params LatchkeyBackend[] backends)
    {
        ArgumentNullException.ThrowIfNull(backends);
        _all =
        [
            .. backends
        ];

        return this;
    }

    /// <summary>Removes the native seed and every configured entry, leaving an empty map.</summary>
    public BackendMap Clear()
    {
        _byOs.Clear();
        _all =
            [];

        return this;
    }

    /// <summary>
    /// The ordered, de-duplicated backends to try for <paramref name="currentOs" />: that OS's list
    /// followed by the all-OS list.
    /// </summary>
    internal IReadOnlyList<LatchkeyBackend> Resolve(OSPlatform? currentOs)
    {
        var result = new List<LatchkeyBackend>();
        var seen = new HashSet<LatchkeyBackend>();

        if (currentOs is { } os && _byOs.TryGetValue(os, out var specific))
        {
            Append(specific, result, seen);
        }

        Append(_all, result, seen);
        return result;
    }

    /// <summary>Every backend referenced by any entry — used for validation.</summary>
    internal IEnumerable<LatchkeyBackend> AllBackends() => _byOs.Values.SelectMany(static list => list).Concat(_all);

    static void Append(IReadOnlyList<LatchkeyBackend> source, List<LatchkeyBackend> result, HashSet<LatchkeyBackend> seen)
    {
        foreach (var backend in source)
        {
            if (seen.Add(backend))
            {
                result.Add(backend);
            }
        }
    }
}
