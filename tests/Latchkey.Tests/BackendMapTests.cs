using System.Runtime.InteropServices;

namespace Latchkey.Tests;

public class BackendMapTests
{
    [Test]
    public async Task DefaultSeedMapsEachOsToItsNativeBackend()
    {
        var map = new BackendMap();
        LatchkeyBackend[] windows =
        [
            LatchkeyBackend.WindowsCredentialManager
        ];

        LatchkeyBackend[] mac =
        [
            LatchkeyBackend.MacOSKeychain
        ];

        LatchkeyBackend[] linux =
        [
            LatchkeyBackend.SecretService
        ];

        await Assert.That(map.Resolve(OSPlatform.Windows).SequenceEqual(windows)).IsTrue();
        await Assert.That(map.Resolve(OSPlatform.OSX).SequenceEqual(mac)).IsTrue();
        await Assert.That(map.Resolve(OSPlatform.Linux).SequenceEqual(linux)).IsTrue();
    }

    [Test]
    public async Task ForAllAppendsAsUniversalFallbackAfterTheOsList()
    {
        var map = new BackendMap().For(OSPlatform.Windows, LatchkeyBackend.Dpapi).ForAll(LatchkeyBackend.File);
        LatchkeyBackend[] windows =
        [
            LatchkeyBackend.Dpapi,
            LatchkeyBackend.File
        ];

        LatchkeyBackend[] mac =
        [
            LatchkeyBackend.MacOSKeychain,
            LatchkeyBackend.File
        ];

        await Assert.That(map.Resolve(OSPlatform.Windows).SequenceEqual(windows)).IsTrue();
        await Assert.That(map.Resolve(OSPlatform.OSX).SequenceEqual(mac)).IsTrue();
    }

    [Test]
    public async Task ForOverridesOneOsAndKeepsTheOthers()
    {
        var map = new BackendMap().For(OSPlatform.Linux, LatchkeyBackend.SystemdCreds);

        await Assert.That(map.Resolve(OSPlatform.Windows).Single()).IsEqualTo(LatchkeyBackend.WindowsCredentialManager);
        await Assert.That(map.Resolve(OSPlatform.OSX).Single()).IsEqualTo(LatchkeyBackend.MacOSKeychain);
        await Assert.That(map.Resolve(OSPlatform.Linux).Single()).IsEqualTo(LatchkeyBackend.SystemdCreds);
    }

    [Test]
    public async Task ResolveDeduplicatesPreservingOrder()
    {
        var map = new BackendMap().Clear()
            .For(OSPlatform.Linux, LatchkeyBackend.InMemory)
            .ForAll(LatchkeyBackend.InMemory);

        await Assert.That(map.Resolve(OSPlatform.Linux).Count).IsEqualTo(1);
    }

    [Test]
    public async Task ClearEmptiesTheMap()
    {
        var map = new BackendMap().Clear();
        await Assert.That(map.Resolve(OSPlatform.Windows).Count).IsEqualTo(0);
    }

    [Test]
    public async Task UnknownOsUsesOnlyTheAllEntry()
    {
        var map = new BackendMap().ForAll(LatchkeyBackend.File);
        await Assert.That(map.Resolve(null).Single()).IsEqualTo(LatchkeyBackend.File);
    }

    [Test]
    public async Task AutoWalksToTheFirstAvailableBackend()
    {
        // The first entry is a native backend foreign to this OS (never available here),
        // so resolution must fall through to InMemory.
        var foreignNative = OperatingSystem.IsWindows() ? LatchkeyBackend.MacOSKeychain : LatchkeyBackend.WindowsCredentialManager;

        var store = LatchkeyFactory.Create(
            new LatchkeyOptions
            {
                ServiceName = "dev.latchkey.test",
                Backends = new BackendMap().Clear().ForAll(foreignNative, LatchkeyBackend.InMemory)
            });

        store.Set("k", "v");
        await Assert.That(store.Get("k")).IsEqualTo("v");
    }

    [Test]
    public async Task AutoWithAnEmptyMapThrows()
    {
        var options = new LatchkeyOptions
        {
            ServiceName = "dev.latchkey.test",
            Backends = new BackendMap().Clear()
        };

        await Assert.That(() => LatchkeyFactory.Create(options)).Throws<LatchkeyBackendUnavailableException>();
    }

    [Test]
    public async Task MapValuesContainingAutoAreRejected()
    {
        var options = new LatchkeyOptions
        {
            ServiceName = "dev.latchkey.test",
            Backends = new BackendMap().ForAll(LatchkeyBackend.Auto)
        };

        await Assert.That(() => LatchkeyFactory.Create(options)).Throws<ArgumentException>();
    }

    [Test]
    public async Task ForcedBackendIgnoresTheMap()
    {
        // An empty map would make Auto throw, but a specific Backend forces past it.
        var store = LatchkeyFactory.Create(
            new LatchkeyOptions
            {
                ServiceName = "dev.latchkey.test",
                Backend = LatchkeyBackend.InMemory,
                Backends = new BackendMap().Clear()
            });

        store.Set("k", "v");
        await Assert.That(store.Get("k")).IsEqualTo("v");
    }
}
