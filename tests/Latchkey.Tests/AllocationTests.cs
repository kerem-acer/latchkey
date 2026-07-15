using Latchkey.Tests.Support;

namespace Latchkey.Tests;

/// <summary>
/// Asserts the managed allocation budget of the Latchkey layer itself, measured with a
/// zero-allocation backend so nothing but the layer is counted. These back up the spec's
/// zero-allocation-on-Set guarantee with an actual regression test, not just a benchmark.
/// </summary>
public class AllocationTests
{
    /// <summary>Measures average bytes allocated per invocation on the current thread, after warmup.</summary>
    private static long BytesPerOp(Action op, int iterations = 10_000)
    {
        // Warm up so tiered JIT and any first-time ArrayPool rent settle before measuring.
        for (int i = 0; i < 1_000; i++)
            op();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
            op();
        long after = GC.GetAllocatedBytesForCurrentThread();

        return (after - before) / iterations;
    }

    private static ILatchkey NewSinkClient(byte[]? cached = null) =>
        new LatchkeyClient(new SinkBackend(cached), "dev.latchkey.test", "dev.latchkey.test");

    [Test]
    public async Task Set_Bytes_Allocates_Nothing_In_The_Layer()
    {
        var client = NewSinkClient();
        byte[] value = new byte[64];
        long perOp = BytesPerOp(() => client.Set("k", value));
        await Assert.That(perOp).IsEqualTo(0L);
    }

    [Test]
    public async Task Set_Small_String_Allocates_Nothing_In_The_Layer()
    {
        // Under the 256-byte stackalloc threshold: no heap allocation at all.
        var client = NewSinkClient();
        long perOp = BytesPerOp(() => client.Set("k", "a short secret value"));
        await Assert.That(perOp).IsEqualTo(0L);
    }

    [Test]
    public async Task Set_Large_String_Allocates_Nothing_In_The_Layer()
    {
        // Over the threshold: the pooled path must net to zero once the pool is warm.
        var client = NewSinkClient();
        string large = new string('x', 4000);
        long perOp = BytesPerOp(() => client.Set("k", large));
        await Assert.That(perOp).IsEqualTo(0L);
    }

    [Test]
    public async Task Delete_Allocates_Nothing_In_The_Layer()
    {
        var client = NewSinkClient();
        long perOp = BytesPerOp(() => client.Delete("k"));
        await Assert.That(perOp).IsEqualTo(0L);
    }

    [Test]
    public async Task GetBytes_Adds_Nothing_Beyond_The_Backend_Array()
    {
        // The backend hands back a cached array; GetBytes returns it as-is, adding no allocation.
        var client = NewSinkClient();
        long perOp = BytesPerOp(() => client.GetBytes("k"));
        await Assert.That(perOp).IsEqualTo(0L);
    }
}
