using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STRAIBot.Services.GBrain;

/// <summary>
/// HTTP client for the local GBrain MCP server at http://localhost:3131.
///
/// GBrain exposes an MCP (Model Context Protocol) endpoint at /mcp.
/// This client sends an MCP tool-call request to invoke the "search" tool,
/// which performs semantic vector search over the imported memory files.
///
/// The MCP call is isolated entirely in this class — if the payload shape changes
/// (e.g. tool name, argument keys, response structure), only this file needs updating.
///
/// IMPORTANT — MCP payload notes:
///   Tool name:      "search" (verify against GBrain admin at /admin)
///   Argument keys:  "query", "namespace" (verify against /admin or GBrain docs)
///   Response path:  result.content[0].text (standard MCP tool response shape)
///
///   If GBrain exposes a simpler REST endpoint in future, replace SendMcpSearchAsync
///   with a direct POST/GET call — the interface and fallback logic stay unchanged.
///
/// Configured via:
///   Memory:GBrainBaseUrl      — base URL (default: http://localhost:3131)
///   Memory:FallbackToMarkdown — whether to fall back to Markdown on failure
/// </summary>
public class GBrainClient : IGBrainClient
{
    private readonly HttpClient _http;
    private readonly ILogger<GBrainClient> _logger;
    private readonly string _baseUrl;

    public GBrainClient(
        HttpClient http,
        IConfiguration config,
        ILogger<GBrainClient> logger)
    {
        _http = http;
        _logger = logger;
        _baseUrl = (config["Memory:GBrainBaseUrl"] ?? "http://localhost:3131").TrimEnd('/');
    }

    // ── Health check ──────────────────────────────────────────────────────────

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/health", cancellationToken);
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
        if (string.IsNullOrWhiteSpace(gBrainMemoryKey))
        {
            _logger.LogWarning(
                "GBrain search called without gBrainMemoryKey for property {PropertyKey}. " +
                "Results would not be scoped — skipping GBrain call.",
                propertyKey);
            return string.Empty;
        }

        _logger.LogInformation(
            "GBrain search | Property={PropertyKey} | Namespace={Namespace} | " +
            "Query={Query}",
            propertyKey,
            gBrainMemoryKey,
            guestMessage.Length > 80 ? guestMessage[..80] + "…" : guestMessage);

        try
        {
            var context = await SendMcpSearchAsync(propertyKey, gBrainMemoryKey, guestMessage, cancellationToken);

            if (string.IsNullOrWhiteSpace(context))
            {
                _logger.LogInformation(
                    "GBrain returned no context | Property={PropertyKey} | Namespace={Namespace}",
                    propertyKey, gBrainMemoryKey);
                return string.Empty;
            }

            _logger.LogInformation(
                "GBrain context retrieved | Property={PropertyKey} | Namespace={Namespace} | " +
                "Length={Length}",
                propertyKey, gBrainMemoryKey, context.Length);

            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GBrain search failed | Property={PropertyKey} | Namespace={Namespace} | " +
                "BaseUrl={BaseUrl}",
                propertyKey, gBrainMemoryKey, _baseUrl);
            return string.Empty;
        }
    }

    // ── MCP protocol ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sends an MCP tool-call request to POST /mcp.
    ///
    /// MCP standard tool-call shape:
    /// {
    ///   "jsonrpc": "2.0",
    ///   "id":      1,
    ///   "method":  "tools/call",
    ///   "params": {
    ///     "name":      "search",
    ///     "arguments": {
    ///       "query":     "{guestMessage}",
    ///       "namespace": "{gBrainMemoryKey}"
    ///     }
    ///   }
    /// }
    ///
    /// TODO: Verify exact tool name and argument keys at http://localhost:3131/admin.
    ///       If GBrain uses different argument names (e.g. "q", "scope", "collection"),
    ///       update McpSearchArguments and re-test with gbrain search commands.
    ///       If GBrain exposes a simpler REST search endpoint, replace this method
    ///       with a direct HTTP call — the interface contract stays unchanged.
    /// </summary>
    private async Task<string> SendMcpSearchAsync(
        string propertyKey,
        string gBrainMemoryKey,
        string guestMessage,
        CancellationToken cancellationToken)
    {
        var request = new McpToolCallRequest
        {
            Id = 1,
            Params = new McpToolCallParams
            {
                Name = "search",
                Arguments = new McpSearchArguments
                {
                    Query = guestMessage,
                    Namespace = gBrainMemoryKey
                }
            }
        };

        var httpResponse = await _http.PostAsJsonAsync(
            $"{_baseUrl}/mcp",
            request,
            cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "GBrain /mcp returned {Status} for property {PropertyKey}.",
                (int)httpResponse.StatusCode, propertyKey);
            return string.Empty;
        }

        var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        return ExtractContextFromMcpResponse(json, propertyKey);
    }

    /// <summary>
    /// Extracts plain-text context from the MCP tool response.
    ///
    /// Standard MCP tool response shape:
    /// {
    ///   "jsonrpc": "2.0",
    ///   "id": 1,
    ///   "result": {
    ///     "content": [
    ///       { "type": "text", "text": "...retrieved context..." }
    ///     ]
    ///   }
    /// }
    ///
    /// TODO: If GBrain returns a different structure (e.g. result.context, result.chunks,
    ///       or a top-level "text" field), update the JsonPath extraction below.
    ///       Inspect the raw response by setting LogLevel:Default = Debug and checking logs.
    /// </summary>
    private string ExtractContextFromMcpResponse(string json, string propertyKey)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Standard MCP path: result.content[0].text
            if (root.TryGetProperty("result", out var result) &&
                result.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.Array)
            {
                var texts = new List<string>();

                foreach (var item in content.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var textProp))
                    {
                        var text = textProp.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                            texts.Add(text.Trim());
                    }
                }

                if (texts.Count > 0)
                    return string.Join("\n\n", texts);
            }

            // Fallback: result.context (non-standard but common in custom MCP servers)
            if (root.TryGetProperty("result", out var resultAlt) &&
                resultAlt.TryGetProperty("context", out var contextProp))
            {
                var ctx = contextProp.GetString();
                if (!string.IsNullOrWhiteSpace(ctx))
                    return ctx.Trim();
            }

            _logger.LogWarning(
                "GBrain /mcp response did not match expected shape for property {PropertyKey}. " +
                "Raw response: {Json}",
                propertyKey,
                json.Length > 500 ? json[..500] + "…" : json);

            return string.Empty;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Failed to parse GBrain /mcp response for property {PropertyKey}.",
                propertyKey);
            return string.Empty;
        }
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
        [JsonPropertyName("name")]      public string Name { get; set; } = "search";
        [JsonPropertyName("arguments")] public McpSearchArguments Arguments { get; set; } = new();
    }

    private sealed class McpSearchArguments
    {
        /// <summary>
        /// The guest message used as the semantic search query.
        /// TODO: verify argument key name against GBrain admin at /admin.
        /// Common alternatives: "q", "query", "message", "text".
        /// </summary>
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// The GBrain memory namespace (e.g. "property:blue-horizon").
        /// Scopes retrieval to a single property's memory.
        /// TODO: verify argument key name against GBrain admin at /admin.
        /// Common alternatives: "namespace", "scope", "collection", "filter".
        /// </summary>
        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = string.Empty;
    }
}
