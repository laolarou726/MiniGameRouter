using Microsoft.Extensions.Configuration;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.Shared.Models;
using MiniGameRouter.Shared.Models.RoutingConfig;

namespace MiniGameRouter.PressureTest;

public class RandomServerConfigurationProvider : IServerConfigurationProvider
{
    private const int DefaultServiceCount = 100;
    private const int DefaultInstanceCount = 20;

    private readonly int _serviceCount;
    private readonly int _instanceCount;

    public MiniGameRouterOptions Options { get; }

    public RandomServerConfigurationProvider(
        IConfiguration configuration)
    {
        Options = new MiniGameRouterOptions
        {
            EndPointMappings = GenerateEndPoints(),
            ConnectionString = configuration.GetValue<string>("PressureTest:ConnectionString") ?? throw new NullReferenceException()
        };

        _serviceCount = configuration.GetValue("PressureTest:ServiceCount", DefaultServiceCount);
        _instanceCount = configuration.GetValue("PressureTest:InstanceCount", DefaultInstanceCount);
    }

    private IReadOnlyDictionary<string, EndPointMappingRequestModel[]> GenerateEndPoints()
    {
        var result = new Dictionary<string, EndPointMappingRequestModel[]>();

        foreach (var serviceIndex in Enumerable.Range(1, _serviceCount))
        {
            foreach (var instanceIndex in Enumerable.Range(1, _instanceCount))
            {
                var instances = new List<EndPointMappingRequestModel>();

                for (var i = 0; i < 10; i++)
                {
                    instances.Add(new EndPointMappingRequestModel
                    {
                        ServiceName = $"Service{serviceIndex}",
                        TargetEndPoint = Guid.NewGuid().ToString(),
                        TimeoutInMilliseconds = 3000,
                        Weight = 1
                    });
                }

                result.Add($"Service{serviceIndex}", instances.ToArray());
            }
        }

        return result;
    }
}