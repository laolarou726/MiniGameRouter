using MiniGameRouter.Models.DB;
using MiniGameRouter.Shared.Models;
using MiniGameRouter.Models.Trie;

namespace MiniGameRouter.Services;

public class DynamicRoutingPrefixMatchService : IHostedService
{
    private int _maxLength = 0;

    private readonly object _locker = new ();
    private readonly TrieDictionary<string> _trie = new ();
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DynamicRoutingPrefixMatchService(
        IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public void AddMatch(string key, string value)
    {
        lock (_locker)
        {
            _trie.Add(key, value);
            _maxLength = Math.Max(_maxLength, key.Length);
        }
    }

    public DynamicRoutingMappingRequestModel? TryGetMatch(string rawStr)
    {
        lock (_locker)
        {
            var chs = rawStr.Select(ch => new Character(ch)).ToList();
            var matches = _trie
                .Matches(chs)
                .OrderBy(m => m.Key.Length)
                .LastOrDefault();

            if (string.IsNullOrEmpty(matches.Key) ||
                string.IsNullOrEmpty(matches.Value)) return null;

            return new DynamicRoutingMappingRequestModel
            {
                MatchPrefix = matches.Key,
                TargetEndPoint = matches.Value
            };
        }
    }

    public bool TryRemoveMatch(string key)
    {
        lock (_locker)
        {
            var removed = _trie.Remove(key);

            if (removed && key.Length == _maxLength)
            {
                _maxLength = _trie.Keys.AsParallel().Max(k => k.Length);
            }

            return removed;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DynamicRoutingMappingContext>();

        await foreach (var record in context.DynamicRoutingMappings.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            // ReSharper disable once InconsistentlySynchronizedField
            _trie.Add(record.MatchPrefix, record.TargetEndPoint);
            _maxLength = Math.Max(_maxLength, record.MatchPrefix.Length);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}