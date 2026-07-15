using BenchmarkDotNet.Running;
using Latchkey.Benchmarks;

// Pick benchmarks with a filter, e.g.:
//   dotnet run -c Release -- --filter *InMemoryBenchmarks*
//   dotnet run -c Release -- --filter *NativeBenchmarks*
BenchmarkSwitcher.FromTypes([typeof(InMemoryBenchmarks), typeof(NativeBenchmarks)]).Run(args);
