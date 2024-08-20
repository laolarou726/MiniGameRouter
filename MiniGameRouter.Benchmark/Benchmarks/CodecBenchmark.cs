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
    private MemoryStream _decodeStream1 = null!;

    private EmbeddedMessage1 _embeddedMessage1 = null!;
    private MemoryStream _stream2 = null!;
    private MemoryStream _decodeStream2 = null!;

    private HugeMessage1 _hugeMessage1 = null!;
    private MemoryStream _stream3 = null!;
    private MemoryStream _decodeStream3 = null!;

    private static TestMessage1 GetRandomTestMessage1()
    {
        return new TestMessage1
        {
            Field1 = Random.Shared.Next(),
            Field2 = Random.Shared.NextDouble(),
            Field3 = Guid.NewGuid().ToString("N")
        };
    }


    [GlobalSetup]
    public void Setup()
    {
        _loggerFactory = new LoggerFactory();

        var logger = _loggerFactory.CreateLogger<DefaultPacketIdMapper>();

        _mapper = new DefaultPacketIdMapper(logger);

        _mapper.Register<TestMessage1>();
        _mapper.Register<EmbeddedMessage1>();
        _mapper.Register<HugeMessage1>();

        _provider = new DefaultCustomCodecProvider();
        _codec = new MemoryPackPacketCodec(_mapper, _provider);

        _stream1 = new MemoryStream();
        _testMessage1 = GetRandomTestMessage1();

        _decodeStream1 = new MemoryStream();
        _decodeStream1.Seek(0, SeekOrigin.Begin);
        _codec.Encode(_testMessage1, _decodeStream1);

        _stream2 = new MemoryStream();
        _embeddedMessage1 = new EmbeddedMessage1
        {
            TestMessage1 = GetRandomTestMessage1(),
            TestMessage2 = GetRandomTestMessage1(),
            TestMessage3 = GetRandomTestMessage1()
        };

        _decodeStream2 = new MemoryStream();
        _decodeStream2.Seek(0, SeekOrigin.Begin);
        _codec.Encode(_embeddedMessage1, _decodeStream2);

        _stream3 = new MemoryStream();
        _hugeMessage1 = HugeMessage1.GetRandom();

        _decodeStream3 = new MemoryStream();
        _decodeStream3.Seek(0, SeekOrigin.Begin);
        _codec.Encode(_hugeMessage1, _decodeStream3);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _loggerFactory.Dispose();

        _stream1.Dispose();
        _decodeStream1.Dispose();

        _stream2.Dispose();
        _decodeStream2.Dispose();

        _stream3.Dispose();
        _decodeStream3.Dispose();
    }

    [Benchmark]
    public void SmallPacketEncode()
    {
        _stream1.Seek(0, SeekOrigin.Begin);
        _codec.Encode(_testMessage1, _stream1);
    }

    [Benchmark]
    public void SmallPacketDecode()
    {
        _decodeStream1.Seek(0, SeekOrigin.Begin);
        _codec.Decode(_decodeStream1);
    }

    [Benchmark]
    public void EmbeddedPacketEncode()
    {
        _stream2.Seek(0, SeekOrigin.Begin);
        _codec.Encode(_embeddedMessage1, _stream2);
    }

    [Benchmark]
    public void EmbeddedPacketDecode()
    {
        _decodeStream2.Seek(0, SeekOrigin.Begin);
        _codec.Decode(_decodeStream2);
    }

    [Benchmark]
    public void HugePacketEncode()
    {
        _stream3.Seek(0, SeekOrigin.Begin);
        _codec.Encode(_hugeMessage1, _stream3);
    }

    [Benchmark]
    public void HugePacketDecode()
    {
        _decodeStream3.Seek(0, SeekOrigin.Begin);
        _codec.Decode(_decodeStream3);
    }
}