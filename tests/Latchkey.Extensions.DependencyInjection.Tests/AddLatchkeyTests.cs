using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Latchkey.Extensions.DependencyInjection.Tests;

public class AddLatchkeyTests
{
    static ServiceProvider BuildInMemory(string serviceName = "dev.latchkey.test")
    {
        var services = new ServiceCollection();
        services.AddLatchkey(_ => new LatchkeyOptions
        {
            ServiceName = serviceName,
            Backend = LatchkeyBackend.InMemory
        });

        return services.BuildServiceProvider();
    }

    [Test]
    public async Task RegistersILatchkeyAsSingleton()
    {
        await using var sp = BuildInMemory();
        var a = sp.GetRequiredService<ILatchkey>();
        var b = sp.GetRequiredService<ILatchkey>();
        await Assert.That(ReferenceEquals(a, b)).IsTrue();
    }

    [Test]
    public async Task ResolvedILatchkeyRoundTrips()
    {
        await using var sp = BuildInMemory();
        var store = sp.GetRequiredService<ILatchkey>();
        store.Set("k", "v");
        await Assert.That(store.Get("k")).IsEqualTo("v");
    }

    [Test]
    public async Task AddLatchkeyTwiceIsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddLatchkey(_ => new LatchkeyOptions
        {
            ServiceName = "dev.latchkey.test",
            Backend = LatchkeyBackend.InMemory
        });

        services.AddLatchkey(_ => new LatchkeyOptions
        {
            ServiceName = "dev.latchkey.test",
            Backend = LatchkeyBackend.InMemory
        });

        var registrations = services.Count(d => d.ServiceType == typeof(ILatchkey));
        await Assert.That(registrations).IsEqualTo(1);
    }

    [Test]
    public async Task InvalidServiceNameFailsValidationOnResolve()
    {
        var services = new ServiceCollection();
        services.AddLatchkey(_ => new LatchkeyOptions
        {
            ServiceName = "   ",
            Backend = LatchkeyBackend.InMemory
        });

        using var sp = services.BuildServiceProvider();

        await Assert.That(() => sp.GetRequiredService<ILatchkey>()).Throws<OptionsValidationException>();
    }

    [Test]
    public async Task InvalidServiceNameFailsHostStartUpViaValidateOnStart()
    {
        using var host = new HostBuilder()
            .ConfigureServices(services =>
                services.AddLatchkey(_ => new LatchkeyOptions
                {
                    ServiceName = "",
                    Backend = LatchkeyBackend.InMemory
                }))
            .Build();

        await Assert.That(async () => await host.StartAsync()).Throws<OptionsValidationException>();
    }

    [Test]
    public async Task ValidServiceNamePassesValidation()
    {
        using var sp = BuildInMemory("dev.example.myapp");
        var options = sp.GetRequiredService<IOptions<LatchkeyOptions>>().Value;
        await Assert.That(options.ServiceName).IsEqualTo("dev.example.myapp");
    }
}
