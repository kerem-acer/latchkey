using Latchkey.Backends.Pass;

namespace Latchkey.IntegrationTests;

/// <summary>
/// The pass backend against a real <c>pass</c> installation. Skipped when pass is not installed or
/// the store is not initialised. Entries go under a unique service namespace and are removed after.
/// </summary>
[Category("Integration")]
public class PassIntegrationTests
{
    static LatchkeyOptions Options() => new()
    {
        ServiceName = Integration.UniqueService(),
        Backend = LatchkeyBackend.Pass,
        BackendOptions =
        [
            PassBackendOption.Default with
            {
                Prefix = "latchkey-test"
            }
        ]
    };

    [Test]
    public async Task PassRoundTripsTextAndBinary()
    {
        var options = Options();
        if (!Latchkey.VerifyPersistence(options))
        {
            Skip.Test("pass is not installed/initialised here.");
        }

        var store = LatchkeyFactory.Create(options);
        try
        {
            store.Set("token", "s3cr3t");
            await Assert.That(store.Get("token")).IsEqualTo("s3cr3t");

            byte[] data =
            [
                0x00,
                0x01,
                0xFF,
                0x00
            ];

            store.Set("bin", data);
            await Assert.That(store.GetBytes("bin")!.SequenceEqual(data)).IsTrue();

            await Assert.That(store.Delete("token")).IsTrue();
            await Assert.That(store.Get("token")).IsNull();
            await Assert.That(store.Delete("token")).IsFalse();
        }
        finally
        {
            store.Delete("token");
            store.Delete("bin");
        }
    }

    [Test]
    public async Task PassAsyncRoundTrips()
    {
        var options = Options();
        if (!Latchkey.VerifyPersistence(options))
        {
            Skip.Test("pass is not installed/initialised here.");
        }

        var store = LatchkeyFactory.Create(options);
        try
        {
            await store.SetAsync("token", "async-secret");
            await Assert.That(await store.GetAsync("token")).IsEqualTo("async-secret");
        }
        finally
        {
            await store.DeleteAsync("token");
        }
    }
}
