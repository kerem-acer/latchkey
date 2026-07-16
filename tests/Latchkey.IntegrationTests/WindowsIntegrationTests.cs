namespace Latchkey.IntegrationTests;

/// <summary>
/// Windows Credential Manager specifics, against the real store. Runs by default on Windows (the
/// windows-latest CI runner); skipped elsewhere. Credential Manager needs an interactive Windows
/// session and cannot be reached from a container.
/// </summary>
[Category("Integration")]
public class WindowsIntegrationTests
{
    [Test]
    public async Task ValueExactlyAtBlobLimitRoundTripsThroughRealStore()
    {
        Integration.RequireWindows();
        Integration.RequireBackend();

        var service = Integration.UniqueService();
        var store = LatchkeyFactory.Create(service);
        var data = new byte[2560]; // CRED_MAX_CREDENTIAL_BLOB_SIZE
        Random.Shared.NextBytes(data);
        try
        {
            store.Set("atlimit", data);
            var read = store.GetBytes("atlimit");
            await Assert.That(read).IsNotNull();
            await Assert.That(read!.SequenceEqual(data)).IsTrue();
        }
        finally
        {
            store.Delete("atlimit");
        }
    }

    [Test]
    public async Task ValueOneByteOverLimitThrowsBeforeTouchingTheStore()
    {
        Integration.RequireWindows();
        Integration.RequireBackend();

        var service = Integration.UniqueService();
        var store = LatchkeyFactory.Create(service);
        var data = new byte[2561];
        await Assert.That(() => store.Set("over", data)).Throws<LatchkeyValueTooLargeException>();
    }

    [Test]
    public async Task BinaryWithNullBytesRoundTripsAsRawBlob()
    {
        Integration.RequireWindows();
        Integration.RequireBackend();

        var service = Integration.UniqueService();
        var store = LatchkeyFactory.Create(service);
        byte[] data =
        [
            0x00,
            0xFF,
            0x00,
            0x10,
            0x00,
            0x7F,
            0x00
        ];

        try
        {
            store.Set("bin", data);
            var read = store.GetBytes("bin");
            await Assert.That(read).IsNotNull();
            await Assert.That(read!.SequenceEqual(data)).IsTrue();
        }
        finally
        {
            store.Delete("bin");
        }
    }
}
