namespace STRAIBot.Services.Messaging;

/// <summary>
/// Tracks which Guesty message IDs have already been processed
/// to prevent duplicate AI decisions and duplicate auto-replies on webhook retries.
///
/// FUTURE: replace with a database-backed implementation so deduplication
/// survives process restarts and scales across multiple instances.
/// </summary>
public interface IProcessedMessageStore
{
    /// <summary>Returns true if this messageId has already been processed.</summary>
    bool HasProcessed(string messageId);

    /// <summary>Records that this messageId has been processed.</summary>
    void MarkProcessed(string messageId);
}
