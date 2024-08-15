using System.Buffers;
using BenchmarkDotNet.Attributes;
using Microsoft.IO;
using Microsoft.Toolkit.HighPerformance;

namespace MiniGameRouter.Benchmark.Benchmarks;

[RPlotExporter]
[MemoryDiagnoser]
[Config(typeof(DefaultConfig))]
public class StreamCopyBenchmark
{
    private static readonly RecyclableMemoryStreamManager Manager1 = new();
    private static readonly RecyclableMemoryStreamManager Manager2 = new();
    private static readonly RecyclableMemoryStreamManager Manager3 = new();
    private byte[] _data = null!;
    private Stream _existStream = null!;
    private ReadOnlyMemory<byte> _mem = null!;

    [Params(50, 100, 1000, 10000, 100000)] public int N;

    [GlobalSetup]
    public void Setup()
    {
        // Setup
        _data = new byte[N];
        Random.Shared.NextBytes(_data);

        _existStream = new MemoryStream(_data);
        _mem = new ReadOnlyMemory<byte>(_data);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Cleanup
        _existStream.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void BytesCopyAsync()
    {
        var to = ArrayPool<byte>.Shared.Rent(N);

        Array.Copy(_data, to, N);
    }

    [Benchmark]
    public async Task BytesToMsCopyAsync()
    {
        await using var source = Manager1.GetStream(_data);
        await using var to = Manager1.GetStream();

        await source.CopyToAsync(to);
    }

    [Benchmark]
    public async Task MsCopyAsync()
    {
        _existStream.Seek(0, SeekOrigin.Begin);

        await using var to = Manager2.GetStream();

        await _existStream.CopyToAsync(to);
    }

    [Benchmark]
    public async Task ReadOnlyMemoryCopyToMsAsync()
    {
        var underlyingStream = _mem.AsStream();
        await using var to = Manager3.GetStream();

        await underlyingStream.CopyToAsync(to);
    }

    [Benchmark]
    public unsafe void UnsafeMsConstructionAsync()
    {
        using var p = _mem.Pin();
        using var s = new UnmanagedMemoryStream(
            (byte*)p.Pointer, _mem.Length, _mem.Length, FileAccess.Read);
    }
}