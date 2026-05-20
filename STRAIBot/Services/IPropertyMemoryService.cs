namespace STRAIBot.Services;

public interface IPropertyMemoryService
{
    /// <summary>
    /// Retrieves the most relevant markdown snippets for a given property and guest message.
    /// Returns an empty string if no relevant context is found.
    /// </summary>
    Task<string> GetRelevantContextAsync(string propertyName, string guestMessage);

    /// <summary>
    /// Returns true if the property memory directory exists and has at least one markdown file.
    /// </summary>
    bool PropertyExists(string propertyName);
}
