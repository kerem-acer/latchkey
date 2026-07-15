namespace Latchkey;

/// <summary>Base type for all Latchkey-specific failures.</summary>
public class LatchkeyException : Exception
{
    /// <summary>Creates a new <see cref="LatchkeyException"/>.</summary>
    public LatchkeyException(string message) : base(message) { }

    /// <summary>Creates a new <see cref="LatchkeyException"/> with an inner exception.</summary>
    public LatchkeyException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// No usable credential store is available on this machine. The message names what is
/// missing and points at <see cref="LatchkeyOptions.CustomBackend"/> as the supported
/// escape hatch for headless/container environments.
/// </summary>
public sealed class LatchkeyBackendUnavailableException : LatchkeyException
{
    /// <summary>Creates a new <see cref="LatchkeyBackendUnavailableException"/>.</summary>
    public LatchkeyBackendUnavailableException(string message) : base(message) { }

    /// <summary>Creates a new <see cref="LatchkeyBackendUnavailableException"/> with an inner exception.</summary>
    public LatchkeyBackendUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>A value exceeded a hard backend limit (e.g. the Windows credential blob size).</summary>
public sealed class LatchkeyValueTooLargeException : LatchkeyException
{
    /// <summary>Creates a new <see cref="LatchkeyValueTooLargeException"/>.</summary>
    public LatchkeyValueTooLargeException(string message) : base(message) { }
}
