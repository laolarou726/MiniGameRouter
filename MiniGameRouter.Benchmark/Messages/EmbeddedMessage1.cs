using MemoryPack;

namespace MiniGameRouter.Benchmark.Messages;

[MemoryPackable]
public partial class EmbeddedMessage1
{
    public TestMessage1 TestMessage1 { get; set; }

    public TestMessage1 TestMessage2 { get; set; }

    public TestMessage1 TestMessage3 { get; set; }
}