namespace Latchkey.IntegrationTests;

/// <summary>
/// Exercises the real OS credential store. Gated behind LATCHKEY_INTEGRATION=1 and every test uses
/// a unique service name that is cleaned up in a finally block, so nothing leaks into real credentials.
/// </summary>
[Category("Integration")]
public class RoundTripIntegrationTests
{
    [Test]
    public async Task Full_RoundTrip_Set_Get_Delete()
    {
        Integration.RequireBackend();

        string service = Integration.UniqueService();
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
    public async Task Overwrite_Upserts()
    {
        Integration.RequireBackend();

        string service = Integration.UniqueService();
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
    public async Task Delete_Nonexistent_ReturnsFalse()
    {
        Integration.RequireBackend();

        string service = Integration.UniqueService();
        var store = LatchkeyFactory.Create(service);
        await Assert.That(store.Delete("never-set")).IsFalse();
    }

    [Test]
    public async Task Unicode_And_Emoji_RoundTrip()
    {
        Integration.RequireBackend();

        string service = Integration.UniqueService();
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
    public async Task Binary_With_NullBytes_RoundTrips_Via_GetBytes()
    {
        Integration.RequireBackend();

        string service = Integration.UniqueService();
        var store = LatchkeyFactory.Create(service);
        byte[] data = { 0x00, 0x01, 0xFF, 0x00, 0x7F, 0x00, 0x80, 0xAB };
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
    public async Task DetectBackend_Returns_Expected_For_This_OS()
    {
        Integration.RequireBackend();
        await Assert.That(Latchkey.DetectBackend()).IsEqualTo(Integration.ExpectedBackend());
    }

    [Test]
    public async Task VerifyPersistence_Returns_True()
    {
        Integration.RequireBackend();
        await Assert.That(Latchkey.VerifyPersistence(Integration.UniqueService())).IsTrue();
    }
}
