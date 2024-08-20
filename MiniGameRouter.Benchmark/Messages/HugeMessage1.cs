using MemoryPack;

namespace MiniGameRouter.Benchmark.Messages;

[MemoryPackable]
public partial class HugeMessage1
{
    [MemoryPackOrder(0)]
    public int Property1 { get; set; }

    [MemoryPackOrder(1)]
    public string? Property2 { get; set; }

    [MemoryPackOrder(2)]
    public List<int>? Property3 { get; set; }

    [MemoryPackOrder(3)]
    public List<byte>? Property4 { get; set; }

    [MemoryPackOrder(4)]
    public List<string>? Property5 { get; set; }

    public static HugeMessage1 GetRandom()
    {
        return new HugeMessage1
        {
            Property1 = Random.Shared.Next(),
            Property2 = string.Join(',', Enumerable.Range(0, 100)),
            Property3 = Enumerable.Range(0, 100).Select(_ => Random.Shared.Next()).ToList(),
            Property4 = Enumerable.Range(0, 100).Select(_ => (byte)Random.Shared.Next(byte.MinValue + 1, byte.MaxValue - 1)).ToList(),
            Property5 = Enumerable.Range(0, 100).Select(_ => string.Join(',', Enumerable.Range(0, 100))).ToList()
        };
    }
}