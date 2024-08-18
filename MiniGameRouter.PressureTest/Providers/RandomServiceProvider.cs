using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.PressureTest.Providers;

public class RandomServiceProvider
{
    public static EndPointMappingRequestModel GetRandomEndPoint()
    {
        return new EndPointMappingRequestModel
        {
            ServiceName = $"Random_Service_{Guid.NewGuid():N}",
            TargetEndPoint = $"Random_Instance_{Guid.NewGuid():N}",
            TimeoutInMilliseconds = 3000,
            Weight = 1
        };
    }
}