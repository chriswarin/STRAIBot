using STRAIBot.Models;

namespace STRAIBot.Services;

/// <summary>
/// Routes memory retrieval to either MarkdownPropertyMemoryService or GBrainClient
/// based on the Memory:Provider configuration value.
/// Falls back to Markdown if GBrain is unavailable or returns empty context.
/// </summary>
public class MemoryContextService : IMemoryContextService
{
    private readonly IPropertyMemoryService _markdownService;
    private readonly IGBrainClient _gBrainClient;
    private readonly IConfiguration _config;
    private readonly ILogger<MemoryContextService> _logger;

    public MemoryContextService(
        IPropertyMemoryService markdownService,
        IGBrainClient gBrainClient,
        IConfiguration config,
        ILogger<MemoryContextService> logger)
    {
        _markdownService = markdownService;
        _gBrainClient = gBrainClient;
        _config = config;
        _logger = logger;
    }

    private MemoryProviderType ActiveProvider =>
        Enum.TryParse<MemoryProviderType>(
            _config["Memory:Provider"], ignoreCase: true, out var p) ? p : MemoryProviderType.Markdown;

    public bool PropertyExists(string propertyName)
    {
        if (ActiveProvider == MemoryProviderType.GBrain)
        {
            // GBrain existence check is async; for the sync path we fall through to Markdown.
            // A fully async pipeline would await PropertyExistsAsync here.
            _logger.LogDebug("GBrain provider active — falling back to Markdown for synchronous PropertyExists check.");
        }
        return _markdownService.PropertyExists(propertyName);
    }

    public async Task<string> GetRelevantContextAsync(string propertyName, string guestMessage)
    {
        if (ActiveProvider == MemoryProviderType.GBrain)
        {
            _logger.LogDebug("Memory provider = GBrain. Querying GBrain for property {Property}.", propertyName);
            try
            {
                var gBrainContext = await _gBrainClient.QueryAsync(propertyName, guestMessage);
                if (!string.IsNullOrWhiteSpace(gBrainContext))
                {
                    _logger.LogInformation("GBrain returned context for property {Property}.", propertyName);
                    return gBrainContext;
                }

                _logger.LogWarning(
                    "GBrain returned empty context for property {Property}. Falling back to Markdown.", propertyName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "GBrain query failed for property {Property}. Falling back to Markdown.", propertyName);
            }
        }

        _logger.LogDebug("Memory provider = Markdown for property {Property}.", propertyName);
        return await _markdownService.GetRelevantContextAsync(propertyName, guestMessage);
    }
}
