namespace STRAIBot.Services;

/// <summary>
/// Interface for GBrain memory retrieval.
/// Replace the current MarkdownPropertyMemoryService with a GBrainClient implementation
/// when GBrain integration is ready.
/// </summary>
public interface IGBrainClient
{
    /// <summary>
    /// Queries GBrain for property-specific context relevant to the guest message.
    /// </summary>
    Task<string> QueryAsync(string propertyName, string guestMessage);

    /// <summary>
    /// Returns true if GBrain has a memory store for the given property.
    /// </summary>
    Task<bool> PropertyExistsAsync(string propertyName);
}
