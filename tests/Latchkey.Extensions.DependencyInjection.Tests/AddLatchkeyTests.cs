using Latchkey;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Latchkey.Extensions.DependencyInjection.Tests;

public class AddLatchkeyTests
{
    private static ServiceProvider BuildInMemory(string serviceName = "dev.latchkey.test")
    {
        var services = new ServiceCollection();
        services.AddLatchkey(o =>
        {
            o.ServiceName = serviceName;
            o.Backend = LatchkeyBackend.InMemory;
        });
        return services.BuildServiceProvider();
    }

    [Test]
    public async Task Registers_ILatchkey_As_Singleton()
    {
        using var sp = BuildInMemory();
        var a = sp.GetRequiredService<ILatchkey>();
        var b = sp.GetRequiredService<ILatchkey>();
        await Assert.That(ReferenceEquals(a, b)).IsTrue();
    }

    [Test]
    public async Task Resolved_ILatchkey_RoundTrips()
    {
        using var sp = BuildInMemory();
        var store = sp.GetRequiredService<ILatchkey>();
        store.Set("k", "v");
        await Assert.That(store.Get("k")).IsEqualTo("v");
    }

    [Test]
    public async Task AddLatchkey_Twice_Is_Idempotent()
    {
        var services = new ServiceCollection();
        services.AddLatchkey(o => { o.ServiceName = "dev.latchkey.test"; o.Backend = LatchkeyBackend.InMemory; });
        services.AddLatchkey(o => { o.ServiceName = "dev.latchkey.test"; o.Backend = LatchkeyBackend.InMemory; });

        int registrations = services.Count(d => d.ServiceType == typeof(ILatchkey));
        await Assert.That(registrations).IsEqualTo(1);
    }

    [Test]
    public async Task Invalid_ServiceName_Fails_Validation_On_Resolve()
    {
        var services = new ServiceCollection();
        services.AddLatchkey(o => { o.ServiceName = "   "; o.Backend = LatchkeyBackend.InMemory; });
        using var sp = services.BuildServiceProvider();

        await Assert.That(() => sp.GetRequiredService<ILatchkey>()).Throws<OptionsValidationException>();
    }

    [Test]
    public async Task Invalid_ServiceName_Fails_Host_StartUp_Via_ValidateOnStart()
    {
        using var host = new HostBuilder()
            .ConfigureServices(services =>
                services.AddLatchkey(o => { o.ServiceName = ""; o.Backend = LatchkeyBackend.InMemory; }))
            .Build();

        await Assert.That(async () => await host.StartAsync()).Throws<OptionsValidationException>();
    }

    [Test]
    public async Task Valid_ServiceName_Passes_Validation()
    {
        using var sp = BuildInMemory("dev.example.myapp");
        var options = sp.GetRequiredService<IOptions<LatchkeyOptions>>().Value;
        await Assert.That(options.ServiceName).IsEqualTo("dev.example.myapp");
    }
}
