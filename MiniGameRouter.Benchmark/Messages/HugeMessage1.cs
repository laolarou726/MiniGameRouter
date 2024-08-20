using MemoryPack;

namespace MiniGameRouter.Benchmark.Messages;

[MemoryPackable]
public partial class HugeMessage1
{
    public int Property1 { get; set; }
    public int[] Property2 { get; set; }
    public string Property3 { get; set; }
    public byte[] Property4 { get; set; }
    public string[] Property5 { get; set; }

    public static HugeMessage1 GetRandom()
    {
        return new HugeMessage1
        {
            Property1 = Random.Shared.Next(),
            Property2 = Enumerable.Range(0, 100).Select(_ => Random.Shared.Next()).ToArray(),
            Property3 = string.Join(',', Enumerable.Range(0, 10_000)),
            Property4 = Enumerable.Range(0, 10_000).Select(_ => (byte)Random.Shared.Next(byte.MinValue, byte.MaxValue)).ToArray(),
            Property5 = Enumerable.Range(0, 1_000).Select(_ => string.Join(',', Enumerable.Range(0, 1_000))).ToArray()
        };
    }
}