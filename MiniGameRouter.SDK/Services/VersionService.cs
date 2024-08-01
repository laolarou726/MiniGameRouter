using Microsoft.Extensions.Logging;
using MiniGameRouter.SDK.Interfaces;

namespace MiniGameRouter.SDK.Services;

public class VersionService : IVersionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    
    public VersionService(
        HttpClient httpClient,
        ILogger<VersionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    
    public async Task<string> GetApiVersionAsync()
    {
        const string url = "/Version";
        
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var res = await _httpClient.SendAsync(req);
        
        res.EnsureSuccessStatusCode();
        
        return await res.Content.ReadAsStringAsync();
    }
}