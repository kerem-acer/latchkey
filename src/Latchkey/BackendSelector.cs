using System.Runtime.InteropServices;

using Latchkey.Backends;
using Latchkey.Backends.Dpapi;
using Latchkey.Backends.Files;
using Latchkey.Backends.InMemory;
using Latchkey.Backends.MacKeychain;
using Latchkey.Backends.Pass;
using Latchkey.Backends.SecretService;
using Latchkey.Backends.SystemdCreds;
using Latchkey.Backends.WindowsCredential;

namespace Latchkey;

/// <summary>
/// The one and only place platform detection lives. The <see cref="LatchkeyClient" /> and
/// every other type stay platform-agnostic; all <see cref="RuntimeInformation.IsOSPlatform" />
/// checks are here, resolved once and cached.
/// </summary>
static class BackendSelector
{
    /// <summary>The current OS as an <see cref="OSPlatform" />, or null on an unrecognized platform.</summary>
    static OSPlatform? CurrentOs()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return OSPlatform.Windows;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return OSPlatform.OSX;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return OSPlatform.Linux;
        }

        return null;
    }

    /// <summary>Resolves a usable backend for the given options, or throws.</summary>
    internal static ISecretBackend Resolve(LatchkeyOptions options)
    {
        if (options.CustomBackend is not null)
        {
            return options.CustomBackend;
        }

        // A specific (non-Auto) Backend forces that single backend, ignoring the map.
        if (options.Backend != LatchkeyBackend.Auto)
        {
            var forced = TryCreate(options.Backend, options.BackendOptions);
            if (forced is null || !forced.IsAvailable)
            {
                throw new LatchkeyBackendUnavailableException(UnavailableMessage(options.Backend));
            }

            return forced;
        }

        // Auto: walk the OS-resolved priority list; the first available backend wins, pinned here.
        var candidates = options.Backends.Resolve(CurrentOs());
        foreach (var backend in candidates)
        {
            if (TryCreate(backend, options.BackendOptions) is { IsAvailable: true } instance)
            {
                return instance;
            }
        }

        throw new LatchkeyBackendUnavailableException(NoCandidateMessage(candidates));
    }

    static string NoCandidateMessage(IReadOnlyList<LatchkeyBackend> tried) =>
        tried.Count == 0 ?
            "No backend is configured for this operating system. Configure LatchkeyOptions.Backends " +
            "(or set LatchkeyOptions.Backend / CustomBackend)." :
            $"None of the configured backends are available here (tried: {string.Join(", ", tried)}). " +
            "Supply your own store via LatchkeyOptions.CustomBackend.";

    /// <summary>
    /// Constructs a backend instance by kind, or null for a kind with no implementation on
    /// this platform. The configurable backends pull their matching <see cref="BackendOption" />
    /// (or a default) from <paramref name="backendOptions" />. Direct construction and an
    /// <c>OfType</c> match only — no reflection, so this stays AOT-clean.
    /// </summary>
    static ISecretBackend? TryCreate(LatchkeyBackend backend, IReadOnlyList<BackendOption> backendOptions) => backend switch
    {
        LatchkeyBackend.InMemory => new InMemoryBackend(),
        LatchkeyBackend.WindowsCredentialManager => new WindowsCredentialBackend(),
        LatchkeyBackend.SecretService => new SecretServiceBackend(),
        LatchkeyBackend.MacOSKeychain => new MacKeychainBackend(),
        LatchkeyBackend.File => new FileBackend(Opt<FileBackendOption>(backendOptions) ?? FileBackendOption.Default),
        LatchkeyBackend.Dpapi => new DpapiBackend(Opt<DpapiBackendOption>(backendOptions) ?? DpapiBackendOption.Default),
        LatchkeyBackend.Pass => new PassBackend(Opt<PassBackendOption>(backendOptions) ?? PassBackendOption.Default),
        LatchkeyBackend.SystemdCreds => new SystemdCredsBackend(Opt<SystemdCredsBackendOption>(backendOptions) ?? SystemdCredsBackendOption.Default),
        _ => null
    };

    /// <summary>The first supplied option of type <typeparamref name="T" />, or null.</summary>
    static T? Opt<T>(IReadOnlyList<BackendOption> backendOptions)
        where T : BackendOption =>
        backendOptions.OfType<T>().FirstOrDefault();

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
        LatchkeyBackend.File =>
            "The file store directory is not writable. Point FileBackendOption.Path at a " +
            "writable directory. Note the File backend stores secrets in plaintext.",
        LatchkeyBackend.Dpapi =>
            "Windows DPAPI is not available. The Dpapi backend runs on Windows only (it calls " +
            "crypt32 CryptProtectData). On other platforms use SystemdCreds or Pass, or supply " +
            "your own store via LatchkeyOptions.CustomBackend.",
        LatchkeyBackend.Pass =>
            "The pass password manager is not available. Install pass, initialise a store " +
            "(pass init <gpg-id>), and configure a GPG key, or supply your own store via " +
            "LatchkeyOptions.CustomBackend.",
        LatchkeyBackend.SystemdCreds =>
            "systemd-creds is not available. It needs systemd (>= 250) and access to a TPM or " +
            "the host key (often root). On other platforms use Dpapi (Windows) or supply your " +
            "own store via LatchkeyOptions.CustomBackend.",
        _ =>
            "No native credential store is available on this platform. Supply your own store via " +
            "LatchkeyOptions.CustomBackend."
    };
}
