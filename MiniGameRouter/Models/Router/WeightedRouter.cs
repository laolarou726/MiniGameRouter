using System.Numerics;
using MiniGameRouter.Interfaces;
using MiniGameRouter.Shared.Interfaces;

namespace MiniGameRouter.Models.Router;

public class WeightedRouter<T> : IDisposable where T : class, IValidate, IWeightedEntity
{
    private readonly List<T> _endPoints = [];
    private readonly object _lock = new();
    private ulong _totalWeight;

    public void Dispose()
    {
        _endPoints.Clear();
        GC.SuppressFinalize(this);
    }

    public void Add(T endPoint)
    {
        lock (_lock)
        {
            _endPoints.Add(endPoint);
            _totalWeight += endPoint.Weight;
        }
    }
    
    public void Add(IEnumerable<T> endPoints)
    {
        foreach (var endPoint in endPoints)
            Add(endPoint);
    }
    
    public bool Remove(T endPoint)
    {
        lock (_lock)
        {
            if (_endPoints.Remove(endPoint))
            {
                _totalWeight -= endPoint.Weight;
                return true;
            }
        }

        return false;
    }

    public T? GetRandom()
    {
        lock (_lock)
        {
            if (_endPoints.Count == 0)
                return null;
        }
        
        return _endPoints[Random.Shared.Next(0, _endPoints.Count)];
    }
    
    public T? Get()
    {
        lock (_lock)
        {
            if (_endPoints.Count == 0)
                return null;
        }
        
        // Generate a random number within total weight
        var randomNumber = (uint)Random.Shared.NextInt64(0, (long)_totalWeight);

        // Select endpoint based on weighted random selection
        uint cumulativeWeight = 0;

        lock (_lock)
        {
            foreach (var endpoint in _endPoints.Where(endpoint => endpoint.IsValid))
            {
                cumulativeWeight += endpoint.Weight;
                if (randomNumber < cumulativeWeight)
                {
                    return endpoint;
                }
            }
        }

        return null;
    }
}