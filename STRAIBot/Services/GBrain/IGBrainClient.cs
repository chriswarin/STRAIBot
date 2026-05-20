namespace STRAIBot.Services.GBrain;

/// <summary>
/// Retrieves property-scoped operational memory from the local GBrain MCP server.
///
/// GBrain identity model:
///   propertyKey     = STRAIBot's internal key (e.g. "BlueHorizon")
///   gBrainMemoryKey = GBrain memory namespace (e.g. "property:blue-horizon")
///   guestMessage    = the semantic query used to retrieve relevant chunks
///
/// The gBrainMemoryKey is the primary scoping mechanism — every query MUST include it
/// so GBrain only returns chunks from the correct property's memory namespace.
/// </summary>
public interface IGBrainClient
{
    /// <summary>
    /// Searches GBrain for the most semantically relevant context chunks for a
    /// given property and guest message.
    ///
    /// Returns retrieved context as a plain-text string for the AI reasoning layer.
    /// Returns empty string if GBrain is unavailable or returns no results —
    /// callers should fall back to Markdown in that case.
    /// </summary>
    Task<string> SearchAsync(
        string propertyKey,
        string gBrainMemoryKey,
        string guestMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the GBrain server is reachable via GET /health.
    /// Returns false on any network or HTTP error.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
