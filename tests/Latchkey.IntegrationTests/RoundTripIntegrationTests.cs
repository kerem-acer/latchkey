namespace Latchkey.IntegrationTests;

/// <summary>
/// Exercises the real OS credential store. Gated behind LATCHKEY_INTEGRATION=1 and every test uses
/// a unique service name that is cleaned up in a finally block, so nothing leaks into real credentials.
/// </summary>
[Category("Integration")]
public class RoundTripIntegrationTests
{
    [Test]
    public async Task FullRoundTripSetGetDelete()
    {
        Integration.RequireBackend();

        var service = Integration.UniqueService();
        var store = LatchkeyFactory.Create(service);
        try
        {
            store.Set("api-key", "value-1");
            await Assert.That(store.Get("api-key")).IsEqualTo("value-1");
            await Assert.That(store.Delete("api-key")).IsTrue();
            await Assert.That(store.Get("api-key")).IsNull();
        }
        finally
        {
            store.Delete("api-key");
        }
    }

    [Test]
    public async Task OverwriteUpserts()
    {
        Integration.RequireBackend();

        var service = Integration.UniqueService();
        var store = LatchkeyFactory.Create(service);
        try
        {
            store.Set("k", "first");
            store.Set("k", "second");
            await Assert.That(store.Get("k")).IsEqualTo("second");
        }
        finally
        {
            store.Delete("k");
        }
    }

    [Test]
    public async Task DeleteNonexistentReturnsFalse()
    {
        Integration.RequireBackend();

        var service = Integration.UniqueService();
        var store = LatchkeyFactory.Create(service);
        await Assert.That(store.Delete("never-set")).IsFalse();
    }

    [Test]
    public async Task UnicodeAndEmojiRoundTrip()
    {
        Integration.RequireBackend();

        var service = Integration.UniqueService();
        var store = LatchkeyFactory.Create(service);
        const string value = "üñïçödé — 日本語 — 🔐🗝️";
        try
        {
            store.Set("k", value);
            await Assert.That(store.Get("k")).IsEqualTo(value);
        }
        finally
        {
            store.Delete("k");
        }
    }

    [Test]
    public async Task BinaryWithNullBytesRoundTripsViaGetBytes()
    {
        Integration.RequireBackend();

        var service = Integration.UniqueService();
        var store = LatchkeyFactory.Create(service);
        byte[] data =
        [
            0x00,
            0x01,
            0xFF,
            0x00,
            0x7F,
            0x00,
            0x80,
            0xAB
        ];

        try
        {
            store.Set("k", data);
            var read = store.GetBytes("k");
            await Assert.That(read).IsNotNull();
            await Assert.That(read!.SequenceEqual(data)).IsTrue();
        }
        finally
        {
            store.Delete("k");
        }
    }

    [Test]
    public async Task EmptyValueRoundTrips()
    {
        Integration.RequireBackend();

        var service = Integration.UniqueService();
        var store = LatchkeyFactory.Create(service);
        try
        {
            store.Set("empty", []);
            var read = store.GetBytes("empty");
            await Assert.That(read).IsNotNull();
            await Assert.That(read!.Length).IsEqualTo(0);
        }
        finally
        {
            store.Delete("empty");
        }
    }

    [Test]
    public async Task VerifyPersistenceReturnsTrue()
    {
        Integration.RequireBackend();
        await Assert.That(Latchkey.VerifyPersistence(Integration.UniqueService())).IsTrue();
    }
}
