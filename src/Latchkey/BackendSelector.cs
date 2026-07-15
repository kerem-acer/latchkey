using System.Runtime.InteropServices;

namespace Latchkey;

/// <summary>
/// The one and only place platform detection lives. The <see cref="LatchkeyClient"/> and
/// every other type stay platform-agnostic; all <see cref="RuntimeInformation.IsOSPlatform"/>
/// checks are here, resolved once and cached.
/// </summary>
internal static class BackendSelector
{
    /// <summary>Which backend <see cref="LatchkeyBackend.Auto"/> maps to on this OS. Computed once.</summary>
    private static readonly LatchkeyBackend PlatformBackend = DetectPlatformBackend();

    private static LatchkeyBackend DetectPlatformBackend()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return LatchkeyBackend.WindowsCredentialManager;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return LatchkeyBackend.MacOSKeychain;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return LatchkeyBackend.SecretService;
        return LatchkeyBackend.Auto; // Unknown platform; treated as "no native backend".
    }

    /// <summary>Backs <see cref="Latchkey.DetectBackend"/>: the backend Auto would use, or null.</summary>
    internal static LatchkeyBackend? Detect()
    {
        var backend = PlatformBackend;
        var instance = TryCreate(backend);
        return instance is { IsAvailable: true } ? backend : null;
    }

    /// <summary>Resolves a usable backend for the given options, or throws.</summary>
    internal static ISecretBackend Resolve(LatchkeyOptions options)
    {
        if (options.CustomBackend is not null)
            return options.CustomBackend;

        var requested = options.Backend == LatchkeyBackend.Auto ? PlatformBackend : options.Backend;

        var instance = TryCreate(requested);
        if (instance is null || !instance.IsAvailable)
            throw new LatchkeyBackendUnavailableException(UnavailableMessage(requested));

        return instance;
    }

    /// <summary>
    /// Constructs a backend instance by kind, or null for a kind with no implementation on
    /// this platform. Direct construction only — no reflection, so this stays AOT-clean.
    /// </summary>
    private static ISecretBackend? TryCreate(LatchkeyBackend backend) => backend switch
    {
        LatchkeyBackend.InMemory => new InMemoryBackend(),
        LatchkeyBackend.WindowsCredentialManager => new WindowsCredentialBackend(),
        LatchkeyBackend.SecretService => new SecretServiceBackend(),
        LatchkeyBackend.MacOSKeychain => new MacKeychainBackend(),
        _ => null,
    };

    internal static string UnavailableMessage(LatchkeyBackend backend) => backend switch
    {
        LatchkeyBackend.SecretService =>
            "No Secret Service is available. Latchkey needs the libsecret-1-0 library and a " +
            "running Secret Service provider (e.g. gnome-keyring) reachable over a D-Bus session " +
            "bus. Headless servers, containers, and bare SSH sessions typically have none. " +
            "Latchkey ships no file fallback by design. Supply your own store via " +
            "LatchkeyOptions.CustomBackend (systemd credentials, a TPM, a KMS-derived key, or a " +
            "pass/GPG store).",
        LatchkeyBackend.WindowsCredentialManager =>
            "Windows Credential Manager is not available. Supply your own store via " +
            "LatchkeyOptions.CustomBackend.",
        LatchkeyBackend.MacOSKeychain =>
            "The macOS Keychain is not available. Supply your own store via " +
            "LatchkeyOptions.CustomBackend.",
        _ =>
            "No native credential store is available on this platform. Supply your own store via " +
            "LatchkeyOptions.CustomBackend.",
    };
}
