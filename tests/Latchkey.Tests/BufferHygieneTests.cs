using Latchkey.Tests.Support;

namespace Latchkey.Tests;

public class BufferHygieneTests
{
    [Test]
    public async Task Set_String_Zeroes_Pooled_Buffer_Before_Return()
    {
        var pool = new TrackingArrayPool();
        var backend = new RecordingBackend();
        var client = new LatchkeyClient(backend, "dev.latchkey.test", "dev.latchkey.test", pool);

        // Value larger than the 256-byte stackalloc threshold forces the pooled path.
        var value = new string('s', 1000);
        client.Set("k", value);

        await Assert.That(pool.AnyReturned).IsTrue();                 // the pooled path actually ran
        await Assert.That(pool.AllReturnedBuffersWereZeroed).IsTrue(); // and the buffer was wiped
    }

    [Test]
    public async Task Set_Small_String_Uses_Stack_Not_Pool()
    {
        var pool = new TrackingArrayPool();
        var backend = new RecordingBackend();
        var client = new LatchkeyClient(backend, "dev.latchkey.test", "dev.latchkey.test", pool);

        client.Set("k", "small value");

        await Assert.That(pool.RentCount).IsEqualTo(0);
    }
}
