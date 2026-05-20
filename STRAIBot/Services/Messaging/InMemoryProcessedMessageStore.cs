using System.Collections.Concurrent;

namespace STRAIBot.Services.Messaging;

/// <summary>
/// In-memory implementation of <see cref="IProcessedMessageStore"/>.
/// Thread-safe via ConcurrentDictionary. Registered as singleton.
///
/// Limitation: deduplication state is lost on process restart.
/// If Guesty retries a message after a restart it will be processed again.
///
/// FUTURE: replace with database-backed store (e.g. Redis, SQL) for
/// persistent deduplication across restarts and multiple instances.
/// </summary>
public sealed class InMemoryProcessedMessageStore : IProcessedMessageStore
{
    // Value is the UTC time the message was first processed — useful for debugging.
    private readonly ConcurrentDictionary<string, DateTime> _processed = new();

    public bool HasProcessed(string messageId) =>
        !string.IsNullOrWhiteSpace(messageId) && _processed.ContainsKey(messageId);

    public void MarkProcessed(string messageId)
    {
        if (!string.IsNullOrWhiteSpace(messageId))
            _processed.TryAdd(messageId, DateTime.UtcNow);
    }
}
