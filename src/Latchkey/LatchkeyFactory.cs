namespace Latchkey;

/// <summary>Creates <see cref="ILatchkey" /> instances.</summary>
public static class LatchkeyFactory
{
    /// <summary>
    /// Creates a <see cref="ILatchkey" /> for the given service using the auto-detected
    /// native backend. Throws <see cref="LatchkeyBackendUnavailableException" /> if no
    /// usable store is available.
    /// </summary>
    public static ILatchkey Create(string serviceName)
    {
        Validation.ValidateServiceName(serviceName);
        return Create(
            new LatchkeyOptions
            {
                ServiceName = serviceName
            });
    }

    /// <summary>
    /// Creates a <see cref="ILatchkey" /> from explicit options. When
    /// <see cref="LatchkeyOptions.CustomBackend" /> is set it is used directly and no
    /// detection runs.
    /// </summary>
    public static ILatchkey Create(LatchkeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validation.ValidateServiceName(options.ServiceName);
        Validation.ValidateBackendOptions(options.BackendOptions);
        Validation.ValidateBackendMap(options.Backends);

        var backend = BackendSelector.Resolve(options);
        var label = string.IsNullOrEmpty(options.DisplayName) ? options.ServiceName : options.DisplayName;
        return new LatchkeyClient(backend, options.ServiceName, label);
    }
}
