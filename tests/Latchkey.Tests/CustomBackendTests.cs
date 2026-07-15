using Latchkey.Tests.Support;

namespace Latchkey.Tests;

public class CustomBackendTests
{
    [Test]
    public async Task CustomBackend_Overrides_Backend_Enum_And_All_Calls_Land_On_It()
    {
        var backend = new RecordingBackend();
        // Backend is explicitly a native one, but CustomBackend must win with no probe.
        var c = LatchkeyFactory.Create(new LatchkeyOptions
        {
            ServiceName = "dev.latchkey.test",
            Backend = LatchkeyBackend.WindowsCredentialManager,
            CustomBackend = backend,
        });

        c.Set("k", "v");
        var value = c.Get("k");
        var deleted = c.Delete("k");

        await Assert.That(value).IsEqualTo("v");
        await Assert.That(deleted).IsTrue();
        await Assert.That(backend.StoreCalls).IsEqualTo(1);
        await Assert.That(backend.RetrieveCalls >= 1).IsTrue();
        await Assert.That(backend.RemoveCalls).IsEqualTo(1);
    }

    [Test]
    public async Task CustomBackend_Used_Even_When_Native_Would_Be_Unavailable()
    {
        // A native backend foreign to this OS would throw during detection; CustomBackend skips that.
        var backend = new RecordingBackend();
        var c = LatchkeyFactory.Create(new LatchkeyOptions
        {
            ServiceName = "dev.latchkey.test",
            Backend = LatchkeyBackend.SecretService,
            CustomBackend = backend,
        });

        c.Set("k", "v");
        await Assert.That(c.Get("k")).IsEqualTo("v");
    }
}
