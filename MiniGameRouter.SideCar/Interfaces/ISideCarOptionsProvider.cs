using MiniGameRouter.SideCar.Models;

namespace MiniGameRouter.SideCar.Interfaces;

public interface ISideCarOptionsProvider
{
    SideCarOptions Options { get; }
}