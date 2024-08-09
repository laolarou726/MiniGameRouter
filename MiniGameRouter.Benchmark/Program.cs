using BenchmarkDotNet.Running;
using MiniGameRouter.Benchmark.Benchmarks;

namespace MiniGameRouter.Benchmark;

class Program
{
    static void Main(string[] args)
    {
        BenchmarkRunner.Run<StreamCopyBenchmark>();
    }
}