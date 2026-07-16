using Latchkey.Backends;

namespace Latchkey;

/// <summary>Input validation shared by the client and factory. Keeps rules in one place.</summary>
static class Validation
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
        ArgumentNullException.ThrowIfNull(serviceName);

        if (serviceName.Length == 0)
        {
            throw new ArgumentException("ServiceName must not be empty.", nameof(serviceName));
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("ServiceName must not be whitespace only.", nameof(serviceName));
        }

        if (serviceName.Length > MaxServiceNameLength)
        {
            throw new ArgumentException($"ServiceName must be at most {MaxServiceNameLength} characters.", nameof(serviceName));
        }

        if (serviceName.Contains('\0'))
        {
            throw new ArgumentException("ServiceName must not contain a NUL character.", nameof(serviceName));
        }
    }

    /// <summary>
    /// Validates a key. Unicode is allowed; empty, whitespace-only, embedded NUL, and
    /// over-long keys are not.
    /// </summary>
    internal static void ValidateKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length == 0)
        {
            throw new ArgumentException("Key must not be empty.", nameof(key));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key must not be whitespace only.", nameof(key));
        }

        if (key.Length > MaxKeyLength)
        {
            throw new ArgumentException($"Key must be at most {MaxKeyLength} characters.", nameof(key));
        }

        if (key.Contains('\0'))
        {
            throw new ArgumentException("Key must not contain a NUL character.", nameof(key));
        }
    }

    /// <summary>
    /// Validates the <see cref="LatchkeyOptions.BackendOptions" /> list: no null entries and at
    /// most one option per <see cref="LatchkeyBackend" /> (a backend reads a single config object).
    /// </summary>
    internal static void ValidateBackendOptions(IReadOnlyList<BackendOption> backendOptions)
    {
        ArgumentNullException.ThrowIfNull(backendOptions);

        var seen = new HashSet<LatchkeyBackend>();
        for (var i = 0; i < backendOptions.Count; i++)
        {
            var option = backendOptions[i];
            if (option is null)
            {
                throw new ArgumentException("BackendOptions must not contain null entries.", nameof(backendOptions));
            }

            if (!seen.Add(option.Backend))
            {
                throw new ArgumentException(
                    $"BackendOptions contains more than one option for {option.Backend}; supply at most one per backend.",
                    nameof(backendOptions));
            }
        }
    }

    /// <summary>
    /// Validates a <see cref="BackendMap" />: its values must not contain
    /// <see cref="LatchkeyBackend.Auto" /> (which would be circular — Auto means "use the map").
    /// </summary>
    internal static void ValidateBackendMap(BackendMap backends)
    {
        ArgumentNullException.ThrowIfNull(backends);

        foreach (var backend in backends.AllBackends())
        {
            if (backend == LatchkeyBackend.Auto)
            {
                throw new ArgumentException(
                    "BackendMap values must not contain LatchkeyBackend.Auto; list concrete backends.",
                    nameof(backends));
            }
        }
    }
}
