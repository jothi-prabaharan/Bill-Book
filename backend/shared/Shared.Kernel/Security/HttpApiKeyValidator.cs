using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Security;

public class HttpApiKeyValidator : IApiKeyValidator
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HttpApiKeyValidator> _logger;

    public HttpApiKeyValidator(HttpClient httpClient, IMemoryCache cache, ILogger<HttpApiKeyValidator> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiKeyValidationResult> ValidateAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        // Simple hash-based caching to avoid sending raw API key across network unnecessarily, 
        // and caching prevents constant DB queries per request.
        var cacheKey = $"apikey_{apiKey}";
        if (_cache.TryGetValue(cacheKey, out ApiKeyValidationResult? cachedResult) && cachedResult != null)
        {
            return cachedResult;
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/api-keys/validate")
            {
                Content = JsonContent.Create(new { ApiKey = apiKey })
            };

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiKeyValidationResult>(cancellationToken: cancellationToken);
                if (result != null && result.IsValid)
                {
                    _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating API key with Master service.");
        }

        return new ApiKeyValidationResult { IsValid = false };
    }
}
