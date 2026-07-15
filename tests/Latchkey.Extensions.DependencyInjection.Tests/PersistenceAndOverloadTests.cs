using Latchkey;
using Latchkey.Extensions.DependencyInjection.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Latchkey.Extensions.DependencyInjection.Tests;

public class PersistenceAndOverloadTests
{
    [Test]
    public async Task AddLatchkey_Parameterless_Uses_Externally_Configured_Options()
    {
        var services = new ServiceCollection();
        services.Configure<LatchkeyOptions>(o =>
        {
            o.ServiceName = "dev.latchkey.test";
            o.Backend = LatchkeyBackend.InMemory;
        });
        services.AddLatchkey();

        using var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<ILatchkey>();
        store.Set("k", "v");
        await Assert.That(store.Get("k")).IsEqualTo("v");
    }

    [Test]
    public async Task AddLatchkey_String_Overload_Sets_ServiceName_On_Options()
    {
        var services = new ServiceCollection();
        services.AddLatchkey("dev.example.viaString");

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<LatchkeyOptions>>().Value; // materializes the config lambda
        await Assert.That(options.ServiceName).IsEqualTo("dev.example.viaString");
    }

    [Test]
    public async Task AddLatchkeyPersistenceCheck_Registers_A_HostedService()
    {
        var services = new ServiceCollection();
        services.AddLatchkey("dev.latchkey.test");
        services.AddLatchkeyPersistenceCheck();

        bool hasHostedService = services.Any(d => d.ServiceType == typeof(IHostedService));
        await Assert.That(hasHostedService).IsTrue();
    }

    [Test]
    public async Task PersistenceCheck_Passes_Startup_When_The_Configured_Store_Round_Trips()
    {
        using var host = BuildHost(o => o.Backend = LatchkeyBackend.InMemory);
        await host.StartAsync();
        await host.StopAsync();
    }

    [Test]
    public async Task PersistenceCheck_Fails_Startup_When_The_Store_Does_Not_Persist()
    {
        using var host = BuildHost(o => o.CustomBackend = new NullReadBackend());
        await Assert.That(async () => await host.StartAsync()).Throws<LatchkeyBackendUnavailableException>();
    }

    [Test]
    public async Task PersistenceCheck_Wraps_Backend_Errors_As_Unavailable()
    {
        using var host = BuildHost(o => o.CustomBackend = new ThrowingBackend());
        await Assert.That(async () => await host.StartAsync()).Throws<LatchkeyBackendUnavailableException>();
    }

    private static IHost BuildHost(Action<LatchkeyOptions> configure)
    {
        return new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLatchkey(options =>
                {
                    options.ServiceName = $"dev.latchkey.test.{Guid.NewGuid():N}";
                    configure(options);
                });
                services.AddLatchkeyPersistenceCheck();
            })
            .Build();
    }
}
