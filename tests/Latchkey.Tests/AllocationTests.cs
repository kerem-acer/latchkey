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
    static long BytesPerOp(Action op, int iterations = 10_000)
    {
        // Warm up so tiered JIT and any first-time ArrayPool rent settle before measuring.
        for (var i = 0; i < 1_000; i++)
        {
            op();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            op();
        }

        var after = GC.GetAllocatedBytesForCurrentThread();

        return (after - before) / iterations;
    }

    static LatchkeyClient NewSinkClient(byte[]? cached = null) =>
        new(new SinkBackend(cached), "dev.latchkey.test", "dev.latchkey.test");

    [Test]
    public async Task SetBytesAllocatesNothingInTheLayer()
    {
        var client = NewSinkClient();
        var value = new byte[64];
        var perOp = BytesPerOp(() => client.Set("k", value));
        await Assert.That(perOp).IsEqualTo(0L);
    }

    [Test]
    public async Task SetSmallStringAllocatesNothingInTheLayer()
    {
        // Under the 256-byte stackalloc threshold: no heap allocation at all.
        var client = NewSinkClient();
        var perOp = BytesPerOp(() => client.Set("k", "a short secret value"));
        await Assert.That(perOp).IsEqualTo(0L);
    }

    [Test]
    public async Task SetLargeStringAllocatesNothingInTheLayer()
    {
        // Over the threshold: the pooled path must net to zero once the pool is warm.
        var client = NewSinkClient();
        var large = new string('x', 4000);
        var perOp = BytesPerOp(() => client.Set("k", large));
        await Assert.That(perOp).IsEqualTo(0L);
    }

    [Test]
    public async Task DeleteAllocatesNothingInTheLayer()
    {
        var client = NewSinkClient();
        var perOp = BytesPerOp(() => client.Delete("k"));
        await Assert.That(perOp).IsEqualTo(0L);
    }

    [Test]
    public async Task GetBytesAddsNothingBeyondTheBackendArray()
    {
        // The backend hands back a cached array; GetBytes returns it as-is, adding no allocation.
        var client = NewSinkClient();
        var perOp = BytesPerOp(() => client.GetBytes("k"));
        await Assert.That(perOp).IsEqualTo(0L);
    }
}
