namespace Latchkey.Backends.Files;

/// <summary>
/// Configuration for <see cref="LatchkeyBackend.File" />, the plaintext on-disk backend.
/// </summary>
public sealed record FileBackendOption : BackendOption
{
    FileBackendOption() { }

    /// <summary>The default configuration. Customize with <c>FileBackendOption.Default with { Path = ... }</c>.</summary>
    public static FileBackendOption Default { get; } = new();

    internal override LatchkeyBackend Backend => LatchkeyBackend.File;

    /// <summary>
    /// Directory the secret files live in. When <c>null</c>, a per-user application-data
    /// directory is used (<c>%LOCALAPPDATA%\Latchkey</c> on Windows,
    /// <c>~/.local/share/Latchkey</c> on Linux, <c>~/Library/Application Support/Latchkey</c>
    /// on macOS).
    /// </summary>
    public string? Path { get; init; }
}
