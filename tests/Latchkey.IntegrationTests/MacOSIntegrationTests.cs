namespace Latchkey.IntegrationTests;

/// <summary>
/// macOS Keychain specifics, against the real store. Runs by default on macOS (locally and on the
/// macos-* CI runners); skipped elsewhere. The Keychain cannot be reached from a container.
/// </summary>
[Category("Integration")]
public class MacOSIntegrationTests
{
    [Test]
    public async Task LargeValueRoundTripsThereIsNoWindowsStyleCap()
    {
        Integration.RequireMacOS();
        Integration.RequireBackend();

        var service = Integration.UniqueService();
        var store = LatchkeyFactory.Create(service);
        var data = new byte[16 * 1024]; // far past the Windows 2560-byte limit
        Random.Shared.NextBytes(data);
        try
        {
            store.Set("large", data);
            var read = store.GetBytes("large");
            await Assert.That(read).IsNotNull();
            await Assert.That(read!.SequenceEqual(data)).IsTrue();
        }
        finally
        {
            store.Delete("large");
        }
    }

    [Test]
    public async Task BinaryWithNullBytesRoundTripsViaCFData()
    {
        Integration.RequireMacOS();
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
