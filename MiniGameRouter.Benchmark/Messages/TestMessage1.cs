using MemoryPack;

namespace MiniGameRouter.Benchmark.Messages;

[MemoryPackable]
public partial class TestMessage1
{
    public int Field1 { get; set; }

    public double Field2 { get; set; }

    public string? Field3 { get; set; }
}