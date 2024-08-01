using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.SDK.Managers;

public class ServiceRegistrationManager : IHostedService
{
    private readonly List<Guid> _endPointIds = [];
    private readonly IEndPointService _endPointService;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    
    public ServiceRegistrationManager(
        IEndPointService endPointService,
        IConfiguration configuration,
        ILogger<ServiceRegistrationManager> logger)
    {
        _endPointService = endPointService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Service registration manager started.");
        
        var endPoints = _configuration
            .GetSection("MiniGameRouter")
            .GetSection("EndPoints")
            .Get<EndPointMappingRequestModel[]>();
        
        if (endPoints == null)
        {
            _logger.LogWarning("No end points found in configuration.");
            return;
        }

        var index = 0;
        
        foreach (var endPoint in endPoints)
        {
            if (string.IsNullOrEmpty(endPoint.ServiceName) ||
                string.IsNullOrEmpty(endPoint.TargetEndPoint))
            {
                _logger.LogWarning("Invalid end point configuration at collection index {index}.", index);
                index++;
                continue;
            }
            
            index++;
            
            var id = await _endPointService.CreateEndPointAsync(
                endPoint.ServiceName,
                endPoint.TargetEndPoint,
                endPoint.Weight ?? 1,
                endPoint.TimeoutInMilliseconds);

            if (id == null)
            {
                _logger.LogError("Failed to create end point for service {serviceName}.", endPoint.ServiceName);
                continue;
            }
            
            _endPointIds.Add(id.Value);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var endPointId in _endPointIds)
        {
            var result = await _endPointService.DeleteEndPointAsync(endPointId);
            
            if (!result)
            {
                _logger.LogError("Failed to delete end point {endPointId}.", endPointId);
                continue;
            }
            
            _logger.LogInformation("End point {endPointId} deleted.", endPointId);
        }
        
        _logger.LogInformation("Service registration manager stopped.");
    }
}