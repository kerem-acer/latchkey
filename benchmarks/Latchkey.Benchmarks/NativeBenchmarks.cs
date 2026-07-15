using BenchmarkDotNet.Attributes;
using Latchkey;

namespace Latchkey.Benchmarks;

/// <summary>
/// Measures the real OS credential store (auto-detected backend). This documents reality — the OS
/// call dominates and is orders of magnitude slower than the managed layer — so no allocation budget
/// is asserted here. Run it explicitly on a machine with a working store, e.g.:
/// <c>dotnet run -c Release -- --filter *NativeBenchmarks*</c>.
/// </summary>
[MemoryDiagnoser]
public class NativeBenchmarks
{
    private ILatchkey _store = null!;

    [GlobalSetup]
    public void Setup()
    {
        _store = LatchkeyFactory.Create($"dev.latchkey.bench.{Guid.NewGuid():N}");
        _store.Set("key", "seeded-value");
    }

    [GlobalCleanup]
    public void Cleanup() => _store.Delete("key");

    [Benchmark]
    public void Set() => _store.Set("key", "a benchmark secret value");

    [Benchmark]
    public string? Get() => _store.Get("key");

    [Benchmark]
    public bool Delete_Missing() => _store.Delete("does-not-exist");
}
