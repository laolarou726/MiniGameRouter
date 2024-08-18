using BenchmarkDotNet.Running;
using MiniGameRouter.Benchmark.Benchmarks;

namespace MiniGameRouter.Benchmark;

internal class Program
{
    private static void Main(string[] args)
    {
        BenchmarkRunner.Run<JobQueueBenchmark>();
        // BenchmarkRunner.Run<StreamCopyBenchmark>();
    }
}