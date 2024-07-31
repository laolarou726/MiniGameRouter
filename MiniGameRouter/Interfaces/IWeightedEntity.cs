using System.Numerics;

namespace MiniGameRouter.Interfaces;

public interface IWeightedEntity
{
    uint Weight { get; }
}