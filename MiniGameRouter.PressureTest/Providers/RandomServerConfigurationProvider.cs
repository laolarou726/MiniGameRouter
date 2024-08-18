using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.Shared.Models;
using MiniGameRouter.Shared.Models.RoutingConfig;

namespace MiniGameRouter.PressureTest.Providers;

public class RandomServerConfigurationProvider : IServerConfigurationProvider
{
    private const int DefaultServiceCount = 100;
    private const int DefaultInstanceCount = 20;

    private readonly int _serviceCount;
    private readonly int _instanceCount;

    public MiniGameRouterOptions Options { get; }

    public RandomServerConfigurationProvider(
        IConfiguration configuration,
        ILogger<RandomServerConfigurationProvider> logger)
    {
        _serviceCount = configuration.GetValue("PressureTest:ServiceCount", DefaultServiceCount);
        _instanceCount = configuration.GetValue("PressureTest:InstanceCount", DefaultInstanceCount);

        logger.LogInformation("ServiceCount={serverCount} InstanceCount={instanceCount}", _serviceCount, _instanceCount);

        Options = new MiniGameRouterOptions
        {
            EndPointMappings = GenerateEndPoints(),
            HealthCheckConcurrency = 10,
            ConnectionString = configuration.GetValue<string>("PressureTest:ConnectionString") ?? throw new NullReferenceException()
        };

        logger.LogInformation("Using connection string: {connectionStr}", Options.ConnectionString);
    }

    private Dictionary<string, EndPointMappingRequestModel[]> GenerateEndPoints()
    {
        var result = new Dictionary<string, EndPointMappingRequestModel[]>();

        foreach (var serviceIndex in Enumerable.Range(1, _serviceCount))
        {
            var instances = new List<EndPointMappingRequestModel>();

            foreach (var instanceIndex in Enumerable.Range(1, _instanceCount))
            {
                instances.Add(new EndPointMappingRequestModel
                {
                    ServiceName = $"Service{serviceIndex}",
                    TargetEndPoint = $"Instance{instanceIndex}_{Guid.NewGuid():N}",
                    TimeoutInMilliseconds = 3000,
                    Weight = 1
                });
            }

            result.Add($"Service{serviceIndex}", instances.ToArray());
        }

        return result;
    }
}