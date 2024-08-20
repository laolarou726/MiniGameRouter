using BenchmarkDotNet.Attributes;
using Hive.Codec.Abstractions;
using Hive.Codec.MemoryPack;
using Hive.Codec.Shared;
using Microsoft.Extensions.Logging;
using MiniGameRouter.Benchmark.Messages;

namespace MiniGameRouter.Benchmark.Benchmarks;

[RPlotExporter]
[MemoryDiagnoser]
[Config(typeof(DefaultConfig))]
public class CodecBenchmark
{
    private ICustomCodecProvider _provider = null!;
    private LoggerFactory _loggerFactory = null!;
    private DefaultPacketIdMapper _mapper = null!;
    private MemoryPackPacketCodec _codec = null!;

    private TestMessage1 _testMessage1 = null!;
    private MemoryStream _stream1 = null!;
    private MemoryStream _decodeStream = null!;

    [GlobalSetup]
    public void Setup()
    {
        _loggerFactory = new LoggerFactory();

        var logger = _loggerFactory.CreateLogger<DefaultPacketIdMapper>();

        _mapper = new DefaultPacketIdMapper(logger);

        _mapper.Register<TestMessage1>();

        _provider = new DefaultCustomCodecProvider();
        _codec = new MemoryPackPacketCodec(_mapper, _provider);

        _stream1 = new MemoryStream();
        _testMessage1 = new TestMessage1
        {
            Field1 = Random.Shared.Next(),
            Field2 = Random.Shared.NextDouble(),
            Field3 = Guid.NewGuid().ToString("N")
        };

        _decodeStream = new MemoryStream();
        _decodeStream.Seek(0, SeekOrigin.Begin);
        _codec.Encode(_testMessage1, _decodeStream);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _loggerFactory.Dispose();
        _stream1.Dispose();
        _decodeStream.Dispose();
    }

    [Benchmark]
    public void Encode()
    {
        _stream1.Seek(0, SeekOrigin.Begin);
        _codec.Encode(_testMessage1, _stream1);
    }

    [Benchmark]
    public void Decode()
    {
        _decodeStream.Seek(0, SeekOrigin.Begin);
        _codec.Decode(_decodeStream);
    }
}