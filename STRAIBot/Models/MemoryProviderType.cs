namespace STRAIBot.Models;

/// <summary>
/// Identifies which memory backend is used to retrieve property context.
/// </summary>
public enum MemoryProviderType
{
    /// <summary>Local markdown files under memory/{PropertyName}/</summary>
    Markdown,

    /// <summary>GBrain vector memory service (requires GBrain to be running).</summary>
    GBrain
}
