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

    public static EndPointMappingRequestModel GetRandomEndPoint(string extraPrefix)
    {
        return new EndPointMappingRequestModel
        {
            ServiceName = $"Random_Service_{extraPrefix}_{Guid.NewGuid():N}",
            TargetEndPoint = $"Random_Instance_{extraPrefix}_{Guid.NewGuid():N}",
            TimeoutInMilliseconds = 3000,
            Weight = 1
        };
    }
}