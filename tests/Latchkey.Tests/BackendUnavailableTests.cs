using System.Runtime.InteropServices;

namespace Latchkey.Tests;

public class BackendUnavailableTests
{
    /// <summary>A native backend that is guaranteed unavailable on the current OS.</summary>
    private static LatchkeyBackend ForeignBackend() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? LatchkeyBackend.MacOSKeychain
            : LatchkeyBackend.WindowsCredentialManager;

    [Test]
    public async Task Requesting_Foreign_Backend_Throws_Unavailable_Naming_CustomBackend()
    {
        var foreign = ForeignBackend();
        await Assert.That(() => LatchkeyFactory.Create(new LatchkeyOptions
            {
                ServiceName = "dev.latchkey.test",
                Backend = foreign,
            }))
            .Throws<LatchkeyBackendUnavailableException>()
            .WithMessageContaining("CustomBackend");
    }

    // The message-generation itself is asserted directly so it stays deterministic on every OS,
    // regardless of which native backends happen to be available on the test machine.

    [Test]
    public async Task SecretService_Message_Names_libsecret_And_CustomBackend_And_Headless()
    {
        var message = BackendSelector.UnavailableMessage(LatchkeyBackend.SecretService);
        await Assert.That(message.Contains("libsecret-1-0")).IsTrue();
        await Assert.That(message.Contains("CustomBackend")).IsTrue();
        await Assert.That(message.Contains("container")).IsTrue();
    }

    [Test]
    [Arguments(LatchkeyBackend.SecretService)]
    [Arguments(LatchkeyBackend.WindowsCredentialManager)]
    [Arguments(LatchkeyBackend.MacOSKeychain)]
    [Arguments(LatchkeyBackend.Auto)]
    public async Task Every_Unavailable_Message_Is_NonEmpty_And_Names_CustomBackend(LatchkeyBackend backend)
    {
        var message = BackendSelector.UnavailableMessage(backend);
        await Assert.That(string.IsNullOrWhiteSpace(message)).IsFalse();
        await Assert.That(message.Contains("CustomBackend")).IsTrue();
    }
}
