namespace Latchkey.Tests;

public class ExceptionTests
{
    [Test]
    public async Task LatchkeyException_Preserves_Message_And_Inner()
    {
        var inner = new InvalidOperationException("boom");
        var ex = new LatchkeyException("outer", inner);
        await Assert.That(ex.Message).IsEqualTo("outer");
        await Assert.That(ex.InnerException).IsEqualTo(inner);
    }

    [Test]
    public async Task BackendUnavailableException_Preserves_Message_And_Inner()
    {
        var inner = new DllNotFoundException("libsecret");
        var ex = new LatchkeyBackendUnavailableException("no store", inner);
        await Assert.That(ex.Message).IsEqualTo("no store");
        await Assert.That(ex.InnerException).IsEqualTo(inner);
        await Assert.That(ex is LatchkeyException).IsTrue();
    }

    [Test]
    public async Task ValueTooLargeException_Is_A_LatchkeyException()
    {
        var ex = new LatchkeyValueTooLargeException("too big");
        await Assert.That(ex.Message).IsEqualTo("too big");
        await Assert.That(ex is LatchkeyException).IsTrue();
    }
}
