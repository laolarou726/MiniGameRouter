using System.Collections.Concurrent;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;

namespace MiniGameRouter.Benchmark.Benchmarks;

[RPlotExporter]
[MemoryDiagnoser]
[ThreadingDiagnoser]
[Config(typeof(DefaultConfig))]
public class JobQueueBenchmark
{
    private object _lock = null!;

    private Queue<int> _queue = null!;
    private ConcurrentQueue<int> _concurrentQueue = null!;

    private Channel<int> _boundedChannel = null!;
    private Channel<int> _unboundedChannel = null!;

    [Params(1, 5, 10)] public int MaxConcurrency;
    [Params(100000)] public int N;

    [GlobalSetup]
    public void Setup()
    {
        _lock = new object();
        _queue = [];
        _concurrentQueue = [];
        _boundedChannel = Channel.CreateBounded<int>(N);
        _unboundedChannel = Channel.CreateUnbounded<int>();

        for (var i = 0; i < N; i++)
        {
            _queue.Enqueue(Random.Shared.Next());
            _concurrentQueue.Enqueue(Random.Shared.Next());
            _boundedChannel.Writer.TryWrite(Random.Shared.Next());
            _unboundedChannel.Writer.TryWrite(Random.Shared.Next());
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _queue.Clear();
        _concurrentQueue.Clear();
        _boundedChannel.Writer.Complete();
        _unboundedChannel.Writer.Complete();
    }

    [Benchmark(Baseline = true)]
    public async Task LockQueueReadOnly()
    {
        var tasks = new Task[MaxConcurrency];

        for (var i = 0; i < MaxConcurrency; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                while (_queue.Count > 0)
                {
                    lock (_lock)
                    {
                        if (_queue.Count > 0)
                        {
                            _queue.Dequeue();
                        }
                    }
                }
            });
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task ConcurrentQueueReadOnly()
    {
        var tasks = new Task[MaxConcurrency];

        for (var i = 0; i < MaxConcurrency; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                while (!_concurrentQueue.IsEmpty)
                {
                    _concurrentQueue.TryDequeue(out _);
                }
            });
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task BoundedChannelReadOnly()
    {
        var tasks = new Task[MaxConcurrency];

        for (var i = 0; i < MaxConcurrency; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                while (_boundedChannel.Reader.Count > 0)
                {
                    if (!_boundedChannel.Reader.TryRead(out var _)) break;
                }
            });
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task UnboundedChannelReadOnly()
    {
        var tasks = new Task[MaxConcurrency];

        for (var i = 0; i < MaxConcurrency; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                while (_unboundedChannel.Reader.Count > 0)
                {
                    if (!_unboundedChannel.Reader.TryRead(out var _)) break;
                }
            });
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task LockQueueReadWrite()
    {
        var count = 0L;
        var tasks = new Task[MaxConcurrency];

        for (var i = 0; i < MaxConcurrency; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < N; j++)
                {
                    lock (_lock)
                    {
                        var dequeued = _queue.Dequeue();
                        _queue.Enqueue(dequeued);

                        var current = Interlocked.Increment(ref count);

                        if (current >= N) break;
                    }
                }
            });
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task ConcurrentQueueReadWrite()
    {
        var count = 0L;
        var tasks = new Task[MaxConcurrency];

        for (var i = 0; i < MaxConcurrency; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < N; j++)
                {
                    _concurrentQueue.TryDequeue(out var dequeued);
                    _concurrentQueue.Enqueue(dequeued);

                    var current = Interlocked.Increment(ref count);

                    if (current >= N) break;
                }
            });
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task BoundedChannelReadWrite()
    {
        var count = 0L;
        var tasks = new Task[MaxConcurrency];

        for (var i = 0; i < MaxConcurrency; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                while (_boundedChannel.Reader.Count > 0 && await _boundedChannel.Reader.WaitToReadAsync())
                {
                    var dequeued = await _boundedChannel.Reader.ReadAsync();
                    
                    await _boundedChannel.Writer.WriteAsync(dequeued);

                    var current = Interlocked.Increment(ref count);

                    if (current >= N) break;
                }
            });
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task UnboundedChannelReadWrite()
    {
        var count = 0L;
        var tasks = new Task[MaxConcurrency];

        for (var i = 0; i < MaxConcurrency; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                while (_unboundedChannel.Reader.Count > 0 && await _unboundedChannel.Reader.WaitToReadAsync())
                {
                    var dequeued = await _unboundedChannel.Reader.ReadAsync();
                    
                    await _unboundedChannel.Writer.WriteAsync(dequeued);

                    var current = Interlocked.Increment(ref count);

                    if (current >= N) break;
                }
            });
        }

        await Task.WhenAll(tasks);
    }
}