namespace Latchkey;

/// <summary>Selects which credential store backs a <see cref="ILatchkey"/> instance.</summary>
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
    /// Must be opted into explicitly — <see cref="Auto"/> never selects it.
    /// </summary>
    InMemory,
}
