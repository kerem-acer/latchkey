using Latchkey;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers Latchkey with a dependency-injection container.</summary>
public static class LatchkeyServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ILatchkey"/> as a singleton for the given service name.</summary>
    public static IServiceCollection AddLatchkey(this IServiceCollection services, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddLatchkey(options => options.ServiceName = serviceName);
    }

    /// <summary>Registers <see cref="ILatchkey"/> as a singleton, configuring options inline.</summary>
    public static IServiceCollection AddLatchkey(this IServiceCollection services, Action<LatchkeyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddOptions<LatchkeyOptions>().Configure(configure);
        return AddCore(services);
    }

    /// <summary>
    /// Registers <see cref="ILatchkey"/> as a singleton, expecting <see cref="LatchkeyOptions"/> to be
    /// configured elsewhere — e.g. <c>services.Configure&lt;LatchkeyOptions&gt;(config.GetSection("Latchkey"))</c>.
    /// </summary>
    public static IServiceCollection AddLatchkey(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<LatchkeyOptions>();
        return AddCore(services);
    }

    /// <summary>
    /// Adds a startup check that round-trips a throwaway value to confirm persistence works, failing
    /// host startup if it does not. Opt-in because it can block on a keychain unlock prompt.
    /// </summary>
    public static IServiceCollection AddLatchkeyPersistenceCheck(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHostedService<LatchkeyPersistenceCheckService>();
        return services;
    }

    private static IServiceCollection AddCore(IServiceCollection services)
    {
        // Validate ServiceName at startup rather than at first Get.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<LatchkeyOptions>, LatchkeyOptionsValidator>());
        services.AddOptions<LatchkeyOptions>().ValidateOnStart();

        // Thread-safe and stateless beyond config, so a singleton is correct.
        services.TryAddSingleton<ILatchkey>(static sp =>
            LatchkeyFactory.Create(sp.GetRequiredService<IOptions<LatchkeyOptions>>().Value));

        return services;
    }
}
