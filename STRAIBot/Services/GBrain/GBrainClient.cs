using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STRAIBot.Services.GBrain;

/// <summary>
/// HTTP client for the local GBrain MCP server (v0.37+).
///
/// Protocol notes (confirmed via live testing):
///
///   Endpoint:   POST {baseUrl}/mcp
///   Auth:       Authorization: Bearer {Memory:GBrainAccessToken}
///   Accept:     application/json, text/event-stream   ← REQUIRED by GBrain
///   Tool name:  "query"  (hybrid vector + keyword search)
///   Arguments:  { "query": "{propertyKey} {guestMessage}", "limit": N }
///
///   There is NO namespace/scope argument on the query tool.
///   Property scoping is achieved by prefixing the propertyKey into the query string.
///   Example: "BlueHorizon late checkout cleaners 10am"
///
///   Response format: SSE envelope  (even though we request application/json)
///     "event: message\ndata: {jsonrpc-payload}\n\n"
///   The jsonrpc result shape:
///     result.content[0].text  →  JSON array of chunk objects
///   Each chunk:
///     { slug, title, chunk_text, score, ... }
///
/// Configured via appsettings.json:
///   Memory:GBrainBaseUrl        — server address (e.g. http://172.23.150.171:3131)
///   Memory:GBrainAccessToken    — bearer token from client_credentials OAuth flow
///   Memory:FallbackToMarkdown   — fall back to Markdown on empty/error
/// </summary>
public class GBrainClient : IGBrainClient
{
    private readonly HttpClient _http;
    private readonly ILogger<GBrainClient> _logger;
    private readonly string _baseUrl;
    private readonly string _accessToken;

    private const int DefaultTopK = 3;

    public GBrainClient(
        HttpClient http,
        IConfiguration config,
        ILogger<GBrainClient> logger)
    {
        _http = http;
        _logger = logger;
        _baseUrl = (config["Memory:GBrainBaseUrl"] ?? "http://localhost:3131").TrimEnd('/');
        _accessToken = config["Memory:GBrainAccessToken"] ?? string.Empty;
    }

    // ── Health check ──────────────────────────────────────────────────────────

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/health");
            AddAuthHeader(request);
            var response = await _http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GBrain health check failed at {BaseUrl}/health.", _baseUrl);
            return false;
        }
    }

    // ── Semantic search ───────────────────────────────────────────────────────

    public async Task<string> SearchAsync(
        string propertyKey,
        string gBrainMemoryKey,
        string guestMessage,
        CancellationToken cancellationToken = default)
    {
        // Prefix the propertyKey into the query so GBrain retrieves chunks from
        // the correct property. GBrain has no namespace filter on the query tool —
        // property scoping is semantic, driven by the propertyKey prefix.
        var scopedQuery = $"{propertyKey} {guestMessage}";

        _logger.LogInformation(
            "GBrain search | Property={PropertyKey} | Namespace={Namespace} | Query={Query}",
            propertyKey,
            gBrainMemoryKey,
            scopedQuery.Length > 100 ? scopedQuery[..100] + "…" : scopedQuery);

        try
        {
            var context = await SendMcpQueryAsync(scopedQuery, DefaultTopK, cancellationToken);

            if (string.IsNullOrWhiteSpace(context))
            {
                _logger.LogInformation(
                    "GBrain returned no context | Property={PropertyKey}", propertyKey);
                return string.Empty;
            }

            _logger.LogInformation(
                "GBrain context retrieved | Property={PropertyKey} | Length={Length}",
                propertyKey, context.Length);

            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GBrain search failed | Property={PropertyKey} | BaseUrl={BaseUrl}",
                propertyKey, _baseUrl);
            return string.Empty;
        }
    }

    // ── MCP protocol ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sends an MCP tool-call to POST /mcp using the "query" tool.
    ///
    /// Required headers (confirmed against GBrain v0.37):
    ///   Authorization: Bearer {token}
    ///   Accept: application/json, text/event-stream
    ///
    /// The response is SSE-wrapped even when Accept includes application/json:
    ///   "event: message\ndata: {jsonrpc-payload}\n\n"
    /// </summary>
    private async Task<string> SendMcpQueryAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        var mcpRequest = new McpToolCallRequest
        {
            Id = 1,
            Params = new McpToolCallParams
            {
                Name = "query",
                Arguments = new McpQueryArguments
                {
                    Query = query,
                    Limit = limit
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/mcp");
        AddAuthHeader(request);

        // GBrain requires both content types in Accept — without text/event-stream
        // it returns: "Not Acceptable: Client must accept both application/json and text/event-stream"
        request.Headers.Add("Accept", "application/json, text/event-stream");
        request.Content = JsonContent.Create(mcpRequest);

        var httpResponse = await _http.SendAsync(request, cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "GBrain /mcp returned {Status}. Query={Query}",
                (int)httpResponse.StatusCode, query);
            return string.Empty;
        }

        var raw = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        return ParseSseResponse(raw, query);
    }

    /// <summary>
    /// Parses the SSE-wrapped MCP response.
    ///
    /// GBrain response format:
    ///   "event: message\ndata: {jsonrpc-payload}\n\n"
    ///
    /// jsonrpc payload:
    ///   { "result": { "content": [ { "type": "text", "text": "[...chunk array JSON...]" } ] } }
    ///
    /// Each chunk in the array:
    ///   { "slug": "bluehorizon/policies/late-checkout-policy",
    ///     "chunk_text": "# Blue Horizon — Late Checkout Policy\n...",
    ///     "score": 0.9999 }
    ///
    /// We concatenate chunk_text values separated by double newlines.
    /// </summary>
    private string ParseSseResponse(string raw, string query)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        try
        {
            // Extract the JSON payload from the SSE "data: {...}" line
            var jsonPayload = ExtractSseData(raw);
            if (string.IsNullOrWhiteSpace(jsonPayload))
            {
                _logger.LogWarning("GBrain SSE response had no data line. Raw={Raw}",
                    raw.Length > 200 ? raw[..200] : raw);
                return string.Empty;
            }

            using var doc = JsonDocument.Parse(jsonPayload);
            var root = doc.RootElement;

            // result.content[0].text → JSON string containing the chunk array
            if (!root.TryGetProperty("result", out var result) ||
                !result.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("GBrain response missing result.content array. Query={Query}", query);
                return string.Empty;
            }

            // content[0].text is a JSON-encoded string of the chunk array
            var firstContent = content.EnumerateArray().FirstOrDefault();
            if (!firstContent.TryGetProperty("text", out var textProp))
                return string.Empty;

            var chunksJson = textProp.GetString();
            if (string.IsNullOrWhiteSpace(chunksJson))
                return string.Empty;

            return ExtractChunkTexts(chunksJson, query);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse GBrain response. Query={Query}", query);
            return string.Empty;
        }
    }

    private static string ExtractSseData(string raw)
    {
        // Find the "data: " line in the SSE stream
        foreach (var line in raw.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return trimmed["data:".Length..].Trim();
        }
        return string.Empty;
    }

    private string ExtractChunkTexts(string chunksJson, string query)
    {
        using var chunksDoc = JsonDocument.Parse(chunksJson);
        var chunks = chunksDoc.RootElement;

        if (chunks.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var texts = new List<string>();
        foreach (var chunk in chunks.EnumerateArray())
        {
            if (chunk.TryGetProperty("chunk_text", out var chunkText))
            {
                var text = chunkText.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    texts.Add(text.Trim());
            }
        }

        if (texts.Count == 0)
        {
            _logger.LogInformation("GBrain returned chunks but all had empty chunk_text. Query={Query}", query);
            return string.Empty;
        }

        _logger.LogDebug("GBrain returned {Count} chunk(s) for query={Query}", texts.Count, query);
        return string.Join("\n\n---\n\n", texts);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AddAuthHeader(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
            request.Headers.Add("Authorization", $"Bearer {_accessToken}");
        else
            _logger.LogWarning(
                "GBrain access token is not configured. Set Memory:GBrainAccessToken in appsettings.json.");
    }

    // ── MCP request models ────────────────────────────────────────────────────

    private sealed class McpToolCallRequest
    {
        [JsonPropertyName("jsonrpc")] public string JsonRpc { get; init; } = "2.0";
        [JsonPropertyName("id")]      public int Id { get; set; }
        [JsonPropertyName("method")]  public string Method { get; init; } = "tools/call";
        [JsonPropertyName("params")]  public McpToolCallParams Params { get; set; } = new();
    }

    private sealed class McpToolCallParams
    {
        [JsonPropertyName("name")]      public string Name { get; set; } = "query";
        [JsonPropertyName("arguments")] public McpQueryArguments Arguments { get; set; } = new();
    }

    private sealed class McpQueryArguments
    {
        /// <summary>
        /// Prefixed with PropertyKey for property scoping:
        /// e.g. "BlueHorizon late checkout cleaners 10am"
        /// </summary>
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 3;
    }
}


