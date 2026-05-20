namespace STRAIBot.Services;

/// <summary>
/// Unified memory context interface. Abstracts over Markdown and GBrain providers.
/// DraftResponseService depends on this — swap the underlying provider in Program.cs.
/// </summary>
public interface IMemoryContextService
{
    /// <summary>
    /// Retrieves the most relevant context snippets for a given property and guest message.
    /// Returns an empty string if no relevant context is found.
    /// </summary>
    Task<string> GetRelevantContextAsync(string propertyName, string guestMessage);

    /// <summary>
    /// Returns true if the requested property has memory available in the active provider.
    /// </summary>
    bool PropertyExists(string propertyName);
}
