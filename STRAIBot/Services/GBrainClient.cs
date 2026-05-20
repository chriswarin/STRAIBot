using System.Net.Http.Json;

namespace STRAIBot.Services;

/// <summary>
/// HTTP client for the GBrain memory service.
/// Configured via Memory:GBrainBaseUrl in appsettings.json.
/// All methods catch errors and return empty/false so the Markdown fallback
/// in MemoryContextService always keeps the app running when GBrain is offline.
/// </summary>
public class GBrainClient : IGBrainClient
{
    private readonly HttpClient _http;
    private readonly ILogger<GBrainClient> _logger;
    private readonly string _baseUrl;

    public GBrainClient(HttpClient http, IConfiguration config, ILogger<GBrainClient> logger)
    {
        _http = http;
        _logger = logger;
        _baseUrl = (config["Memory:GBrainBaseUrl"] ?? "http://localhost:8088").TrimEnd('/');
    }

    /// <summary>
    /// POST {GBrainBaseUrl}/query
    /// Body: { "propertyName": "...", "guestMessage": "..." }
    /// Expected response: { "context": "..." }
    /// </summary>
    public async Task<string> QueryAsync(string propertyName, string guestMessage)
    {
        try
        {
            var payload = new { propertyName, guestMessage };
            var response = await _http.PostAsJsonAsync($"{_baseUrl}/query", payload);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GBrain /query returned {StatusCode} for property {Property}.",
                    response.StatusCode, propertyName);
                return string.Empty;
            }

            var result = await response.Content.ReadFromJsonAsync<GBrainQueryResponse>();
            return result?.Context ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GBrain /query request failed for property {Property}. Is GBrain running at {BaseUrl}?",
                propertyName, _baseUrl);
            return string.Empty;
        }
    }

    /// <summary>
    /// GET {GBrainBaseUrl}/properties/{propertyName}/exists
    /// Expected response: { "exists": true/false }
    /// </summary>
    public async Task<bool> PropertyExistsAsync(string propertyName)
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/properties/{propertyName}/exists");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GBrain /properties/{Property}/exists returned {StatusCode}.",
                    propertyName, response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<GBrainExistsResponse>();
            return result?.Exists ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GBrain /properties/{Property}/exists failed. Is GBrain running at {BaseUrl}?",
                propertyName, _baseUrl);
            return false;
        }
    }

    // ── Private response DTOs ─────────────────────────────────────────────────

    private sealed record GBrainQueryResponse(string? Context);
    private sealed record GBrainExistsResponse(bool Exists);
}

