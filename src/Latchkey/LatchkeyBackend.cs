using Latchkey.Backends.Dpapi;
using Latchkey.Backends.Files;
using Latchkey.Backends.Pass;
using Latchkey.Backends.SystemdCreds;

namespace Latchkey;

/// <summary>Selects which credential store backs a <see cref="ILatchkey" /> instance.</summary>
public enum LatchkeyBackend
{
    /// <summary>Detect the native backend for the current OS. The default.</summary>
    Auto,

    /// <summary>Windows Credential Manager (advapi32).</summary>
    WindowsCredentialManager,

    /// <summary>macOS Keychain Services (Security.framework).</summary>
    MacOSKeychain,

    /// <summary>Linux Secret Service via libsecret.</summary>
    SecretService,

    /// <summary>
    /// In-process, non-persistent store. Testing only; never survives the process.
    /// Must be opted into explicitly — <see cref="Auto" /> never selects it.
    /// </summary>
    InMemory,

    /// <summary>
    /// Plaintext files on disk (all platforms). <b>Unencrypted</b> — its at-rest safety is
    /// only the file permissions and any full-disk encryption around it. Opt-in only;
    /// <see cref="Auto" /> never selects it. Configure with <see cref="FileBackendOption" />.
    /// </summary>
    File,

    /// <summary>
    /// Encrypted files on disk via Windows DPAPI (<c>CryptProtectData</c>). Windows only.
    /// Opt-in only; <see cref="Auto" /> never selects it. Configure with
    /// <see cref="DpapiBackendOption" />.
    /// </summary>
    Dpapi,

    /// <summary>
    /// The <c>pass</c> Unix password manager (GPG-encrypted files). Requires the <c>pass</c>
    /// binary and a configured GPG key. Opt-in only; <see cref="Auto" /> never selects it.
    /// Configure with <see cref="PassBackendOption" />.
    /// </summary>
    Pass,

    /// <summary>
    /// Encrypted files on disk via <c>systemd-creds encrypt|decrypt</c> (TPM- or host-key
    /// sealed). Requires systemd ≥ 250. Opt-in only; <see cref="Auto" /> never selects it.
    /// Configure with <see cref="SystemdCredsBackendOption" />.
    /// </summary>
    SystemdCreds
}
