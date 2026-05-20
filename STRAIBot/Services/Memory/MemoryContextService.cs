using STRAIBot.Models;
using STRAIBot.Services.GBrain;
using STRAIBot.Services.Properties;

namespace STRAIBot.Services.Memory;

/// <summary>
/// Routes memory retrieval to GBrain or Markdown based on Memory:Provider config.
///
/// GBrain path:
///   1. Resolve PropertyMapping by propertyKey to get GBrainMemoryKey
///   2. Call IGBrainClient.SearchAsync(propertyKey, gBrainMemoryKey, guestMessage)
///   3. If empty and FallbackToMarkdown = true → use Markdown
///
/// Markdown path:
///   Direct call to IPropertyMemoryService (reads local memory/**/*.md files)
///
/// This service is the single point of truth for which memory provider is active.
/// DraftResponseService depends on IMemoryContextService only — it never knows
/// which provider is underneath.
/// </summary>
public class MemoryContextService : IMemoryContextService
{
    private readonly IPropertyMemoryService _markdown;
    private readonly IGBrainClient _gBrain;
    private readonly IPropertyMappingService _mappings;
    private readonly IConfiguration _config;
    private readonly ILogger<MemoryContextService> _logger;

    public MemoryContextService(
        IPropertyMemoryService markdown,
        IGBrainClient gBrain,
        IPropertyMappingService mappings,
        IConfiguration config,
        ILogger<MemoryContextService> logger)
    {
        _markdown = markdown;
        _gBrain = gBrain;
        _mappings = mappings;
        _config = config;
        _logger = logger;
    }

    // ── Config helpers ────────────────────────────────────────────────────────

    private bool IsGBrainProvider =>
        string.Equals(_config["Memory:Provider"], "GBrain", StringComparison.OrdinalIgnoreCase);

    private bool FallbackToMarkdown =>
        _config.GetValue<bool>("Memory:FallbackToMarkdown", true);

    // ── IMemoryContextService ─────────────────────────────────────────────────

    public bool PropertyExists(string propertyKey)
    {
        // GBrain existence is async — sync path always checks Markdown folder presence.
        // This is used as a quick pre-flight check before calling GetContextAsync.
        return _markdown.PropertyExists(propertyKey);
    }

    public async Task<string> GetContextAsync(
        string propertyKey,
        string guestMessage,
        CancellationToken cancellationToken = default)
    {
        if (IsGBrainProvider)
        {
            var context = await TryGBrainAsync(propertyKey, guestMessage, cancellationToken);

            if (!string.IsNullOrWhiteSpace(context))
                return context;

            if (!FallbackToMarkdown)
            {
                _logger.LogWarning(
                    "GBrain returned no context for {PropertyKey} and FallbackToMarkdown is false. " +
                    "AI will proceed with no retrieved context.",
                    propertyKey);
                return string.Empty;
            }

            _logger.LogInformation(
                "GBrain returned no context for {PropertyKey} — falling back to Markdown.",
                propertyKey);
        }

        return await GetMarkdownContextAsync(propertyKey, guestMessage);
    }

    // ── GBrain retrieval ──────────────────────────────────────────────────────

    private async Task<string> TryGBrainAsync(
        string propertyKey,
        string guestMessage,
        CancellationToken cancellationToken)
    {
        // Resolve the GBrainMemoryKey so every query is scoped to one property namespace.
        var mapping = _mappings.GetByPropertyKey(propertyKey);

        if (mapping is null)
        {
            _logger.LogWarning(
                "No PropertyMapping found for {PropertyKey} — cannot scope GBrain query. " +
                "Add a mapping with GBrainMemoryKey in appsettings.json.",
                propertyKey);
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(mapping.GBrainMemoryKey))
        {
            _logger.LogWarning(
                "PropertyMapping for {PropertyKey} has no GBrainMemoryKey set. " +
                "Update appsettings.json to include GBrainMemoryKey.",
                propertyKey);
            return string.Empty;
        }

        return await _gBrain.SearchAsync(
            propertyKey,
            mapping.GBrainMemoryKey,
            guestMessage,
            cancellationToken);
    }

    // ── Markdown retrieval ────────────────────────────────────────────────────

    private async Task<string> GetMarkdownContextAsync(string propertyKey, string guestMessage)
    {
        _logger.LogDebug("Memory provider = Markdown | Property={PropertyKey}", propertyKey);
        return await _markdown.GetRelevantContextAsync(propertyKey, guestMessage);
    }
}
