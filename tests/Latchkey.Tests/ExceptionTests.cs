namespace Latchkey.Tests;

public class ExceptionTests
{
    [Test]
    public async Task LatchkeyExceptionPreservesMessageAndInner()
    {
        var inner = new InvalidOperationException("boom");
        var ex = new LatchkeyException("outer", inner);
        await Assert.That(ex.Message).IsEqualTo("outer");
        await Assert.That(ex.InnerException).IsEqualTo(inner);
    }

    [Test]
    public async Task BackendUnavailableExceptionPreservesMessageAndInner()
    {
        var inner = new DllNotFoundException("libsecret");
        var ex = new LatchkeyBackendUnavailableException("no store", inner);
        await Assert.That(ex.Message).IsEqualTo("no store");
        await Assert.That(ex.InnerException).IsEqualTo(inner);
        await Assert.That(ex is not null).IsTrue();
    }

    [Test]
    public async Task ValueTooLargeExceptionIsALatchkeyException()
    {
        var ex = new LatchkeyValueTooLargeException("too big");
        await Assert.That(ex.Message).IsEqualTo("too big");
        await Assert.That(ex is not null).IsTrue();
    }
}
