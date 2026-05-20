namespace STRAIBot.Services.Memory;

/// <summary>
/// Retrieves property-scoped memory context for the AI reasoning pipeline.
///
/// Two providers are supported:
///   Markdown — reads local memory/{PropertyKey}/**/*.md files (offline, always available)
///   GBrain   — semantic vector search via local GBrain MCP server (precise, preferred)
///
/// Active provider is controlled by Memory:Provider in appsettings.json.
/// Fallback behavior is controlled by Memory:FallbackToMarkdown.
/// </summary>
public interface IMemoryContextService
{
    /// <summary>
    /// Retrieves the most relevant memory context for a given property and guest message.
    ///
    /// If provider is GBrain, uses GBrainMemoryKey to scope the search.
    /// If GBrain returns empty and FallbackToMarkdown = true, falls back to Markdown.
    ///
    /// Returns empty string if no context is found — callers must handle this gracefully.
    /// </summary>
    Task<string> GetContextAsync(
        string propertyKey,
        string guestMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the requested property has memory available in the active provider.
    /// Used to skip retrieval entirely when no memory exists.
    /// </summary>
    bool PropertyExists(string propertyKey);
}
