using BenchmarkDotNet.Attributes;
using Latchkey;

namespace Latchkey.Benchmarks;

/// <summary>
/// Measures the Latchkey managed layer in isolation, against the in-memory backend. These are the
/// numbers with an allocation budget: Set should add no managed allocations of its own (see the
/// AllocationTests in the unit suite, which assert this).
/// </summary>
[MemoryDiagnoser]
public class InMemoryBenchmarks
{
    private ILatchkey _store = null!;
    private byte[] _value = null!;

    [GlobalSetup]
    public void Setup()
    {
        _store = LatchkeyFactory.Create(new LatchkeyOptions
        {
            ServiceName = "dev.latchkey.bench",
            Backend = LatchkeyBackend.InMemory,
        });
        _value = new byte[64];
        _store.Set("key", "seeded-value");
    }

    [Benchmark]
    public void Set_String() => _store.Set("key", "a benchmark secret value");

    [Benchmark]
    public void Set_Bytes() => _store.Set("key", _value);

    [Benchmark]
    public string? Get_String() => _store.Get("key");

    [Benchmark]
    public byte[]? Get_Bytes() => _store.GetBytes("key");

    [Benchmark]
    public bool Contains() => _store.Contains("key");

    [Benchmark]
    public bool Delete_Missing() => _store.Delete("does-not-exist");
}
