using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Latchkey.Extensions.DependencyInjection;

/// <summary>Registers Latchkey with a dependency-injection container.</summary>
public static class LatchkeyServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ILatchkey" /> as a singleton for the given service name.</summary>
    public static IServiceCollection AddLatchkey(this IServiceCollection services, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddLatchkey(_ => new LatchkeyOptions
        {
            ServiceName = serviceName
        });
    }

    /// <summary>Registers <see cref="ILatchkey" /> as a singleton from a prebuilt <see cref="LatchkeyOptions" />.</summary>
    public static IServiceCollection AddLatchkey(this IServiceCollection services, LatchkeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        return services.AddLatchkey(_ => options);
    }

    /// <summary>
    /// Registers <see cref="ILatchkey" /> as a singleton, building <see cref="LatchkeyOptions" /> from the
    /// service provider. Options are immutable, so they are <b>constructed</b> here (e.g.
    /// <c>sp =&gt; new LatchkeyOptions { ServiceName = "..." }</c>), not mutated.
    /// </summary>
    public static IServiceCollection AddLatchkey(this IServiceCollection services, Func<IServiceProvider, LatchkeyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Build the options through a custom OptionsFactory so IOptions<>, ValidateOnStart, and
        // OptionsValidationException all keep working without mutating the (init-only) options.
        services.TryAddSingleton<IOptionsFactory<LatchkeyOptions>>(sp => new LatchkeyOptionsFactory(
            () => configure(sp),
            sp.GetServices<IConfigureOptions<LatchkeyOptions>>(),
            sp.GetServices<IPostConfigureOptions<LatchkeyOptions>>(),
            sp.GetServices<IValidateOptions<LatchkeyOptions>>()));

        return AddCore(services);
    }

    /// <summary>
    /// Registers <see cref="ILatchkey" /> as a singleton, expecting <see cref="LatchkeyOptions" /> to be
    /// configured elsewhere by binding — e.g.
    /// <c>services.Configure&lt;LatchkeyOptions&gt;(config.GetSection("Latchkey"))</c> (the configuration
    /// binder assigns init-only properties fine).
    /// </summary>
    public static IServiceCollection AddLatchkey(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
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

    static IServiceCollection AddCore(IServiceCollection services)
    {
        // Validate at startup rather than at first Get.
        services.AddOptions<LatchkeyOptions>().ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<LatchkeyOptions>, LatchkeyOptionsValidator>());

        // Thread-safe and stateless beyond config, so a singleton is correct.
        services.TryAddSingleton(static sp =>
            LatchkeyFactory.Create(sp.GetRequiredService<IOptions<LatchkeyOptions>>().Value));

        return services;
    }
}

/// <summary>
/// An <see cref="OptionsFactory{TOptions}" /> that <b>constructs</b> the options via a delegate rather
/// than mutating a new instance — required because <see cref="LatchkeyOptions" /> is init-only. Configure
/// / post-configure / validate all still run, so the Options pipeline behaves normally.
/// </summary>
sealed class LatchkeyOptionsFactory(
    Func<LatchkeyOptions> create,
    IEnumerable<IConfigureOptions<LatchkeyOptions>> setups,
    IEnumerable<IPostConfigureOptions<LatchkeyOptions>> postConfigures,
    IEnumerable<IValidateOptions<LatchkeyOptions>> validations)
    : OptionsFactory<LatchkeyOptions>(setups, postConfigures, validations)
{
    protected override LatchkeyOptions CreateInstance(string name) => create();
}
