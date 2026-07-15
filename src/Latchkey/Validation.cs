namespace Latchkey;

/// <summary>Input validation shared by the client and factory. Keeps rules in one place.</summary>
internal static class Validation
{
    /// <summary>Upper bound on key length (characters). Generous; guards against pathological input.</summary>
    internal const int MaxKeyLength = 4096;

    /// <summary>Upper bound on service-name length (characters).</summary>
    internal const int MaxServiceNameLength = 1024;

    /// <summary>
    /// Validates a service name. Unicode is allowed; empty, whitespace-only, embedded
    /// NUL, and over-long names are not.
    /// </summary>
    internal static void ValidateServiceName(string serviceName)
    {
        if (serviceName is null)
            throw new ArgumentNullException(nameof(serviceName));
        if (serviceName.Length == 0)
            throw new ArgumentException("ServiceName must not be empty.", nameof(serviceName));
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("ServiceName must not be whitespace only.", nameof(serviceName));
        if (serviceName.Length > MaxServiceNameLength)
            throw new ArgumentException($"ServiceName must be at most {MaxServiceNameLength} characters.", nameof(serviceName));
        if (serviceName.Contains('\0'))
            throw new ArgumentException("ServiceName must not contain a NUL character.", nameof(serviceName));
    }

    /// <summary>
    /// Validates a key. Unicode is allowed; empty, whitespace-only, embedded NUL, and
    /// over-long keys are not.
    /// </summary>
    internal static void ValidateKey(string key)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (key.Length == 0)
            throw new ArgumentException("Key must not be empty.", nameof(key));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key must not be whitespace only.", nameof(key));
        if (key.Length > MaxKeyLength)
            throw new ArgumentException($"Key must be at most {MaxKeyLength} characters.", nameof(key));
        if (key.Contains('\0'))
            throw new ArgumentException("Key must not contain a NUL character.", nameof(key));
    }
}
