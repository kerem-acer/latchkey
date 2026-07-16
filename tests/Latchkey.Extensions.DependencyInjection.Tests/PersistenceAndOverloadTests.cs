using Latchkey.Extensions.DependencyInjection.Tests.Support;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Latchkey.Extensions.DependencyInjection.Tests;

public class PersistenceAndOverloadTests
{
    [Test]
    public async Task AddLatchkeyParameterlessUsesExternallyConfiguredOptions()
    {
        // Init-only options can't be set by a mutating Configure delegate; the configuration binder can.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ServiceName"] = "dev.latchkey.test",
                    ["Backend"] = nameof(LatchkeyBackend.InMemory)
                })
            .Build();

        var services = new ServiceCollection();
        services.Configure<LatchkeyOptions>(config);
        services.AddLatchkey();

        using var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<ILatchkey>();
        store.Set("k", "v");
        await Assert.That(store.Get("k")).IsEqualTo("v");
    }

    [Test]
    public async Task AddLatchkeyStringOverloadSetsServiceNameOnOptions()
    {
        var services = new ServiceCollection();
        services.AddLatchkey("dev.example.viaString");

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<LatchkeyOptions>>().Value; // materializes the config lambda
        await Assert.That(options.ServiceName).IsEqualTo("dev.example.viaString");
    }

    [Test]
    public async Task AddLatchkeyPersistenceCheckRegistersAHostedService()
    {
        var services = new ServiceCollection();
        services.AddLatchkey("dev.latchkey.test");
        services.AddLatchkeyPersistenceCheck();

        var hasHostedService = services.Any(d => d.ServiceType == typeof(IHostedService));
        await Assert.That(hasHostedService).IsTrue();
    }

    [Test]
    public async Task PersistenceCheckPassesStartupWhenTheConfiguredStoreRoundTrips()
    {
        using var host = BuildHost(svc => new LatchkeyOptions
        {
            ServiceName = svc,
            Backend = LatchkeyBackend.InMemory
        });

        await host.StartAsync();
        await host.StopAsync();
    }

    [Test]
    public async Task PersistenceCheckFailsStartupWhenTheStoreDoesNotPersist()
    {
        using var host = BuildHost(svc => new LatchkeyOptions
        {
            ServiceName = svc,
            CustomBackend = new NullReadBackend()
        });

        await Assert.That(async () => await host.StartAsync()).Throws<LatchkeyBackendUnavailableException>();
    }

    [Test]
    public async Task PersistenceCheckWrapsBackendErrorsAsUnavailable()
    {
        using var host = BuildHost(svc => new LatchkeyOptions
        {
            ServiceName = svc,
            CustomBackend = new ThrowingBackend()
        });

        await Assert.That(async () => await host.StartAsync()).Throws<LatchkeyBackendUnavailableException>();
    }

    static IHost BuildHost(Func<string, LatchkeyOptions> build) =>
        new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLatchkey(_ => build($"dev.latchkey.test.{Guid.NewGuid():N}"));
                services.AddLatchkeyPersistenceCheck();
            })
            .Build();
}
