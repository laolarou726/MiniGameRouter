using System.Collections.Frozen;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.Shared.Models.RoutingConfig;

namespace MiniGameRouter.SDK.Providers;

public class SessionHashIdentityProvider : ISessionHashIdentityProvider
{
    public string GeneralSessionHash { get; }
    public FrozenDictionary<string, string>? ServiceHashes { get; }

    public SessionHashIdentityProvider(
        IConfiguration configuration,
        ILogger<SessionHashIdentityProvider> logger)
    {
        var sessionHashOptions = configuration
            .GetSection("MiniGameRouter")
            .GetSection("SessionHash")
            .Get<SessionHashOptions>();
        
        ArgumentNullException.ThrowIfNull(sessionHashOptions);
        ArgumentException.ThrowIfNullOrEmpty(sessionHashOptions.GeneralSessionHash);
        
        GeneralSessionHash = sessionHashOptions.GeneralSessionHash;
        ServiceHashes = sessionHashOptions.ServiceHashes?.ToFrozenDictionary();
        
        logger.LogInformation("Session hash identity provider initialized.");
        logger.LogInformation("General session hash: {hash}", GeneralSessionHash);
        logger.LogInformation("Service hashes: {@hashes}", ServiceHashes);
    }
}