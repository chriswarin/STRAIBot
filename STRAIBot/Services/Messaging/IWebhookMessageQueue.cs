using STRAIBot.Models.Guesty;

namespace STRAIBot.Services.Messaging;

/// <summary>
/// A lightweight in-process queue for incoming Guesty webhook envelopes.
/// The webhook controller writes to it; GuestyWebhookProcessor reads from it.
///
/// Replaced with a durable queue (Azure Service Bus, RabbitMQ, etc.) later
/// by swapping this implementation without touching the controller or processor.
/// </summary>
public interface IWebhookMessageQueue
{
    /// <summary>
    /// Enqueue a webhook envelope for background processing.
    /// Returns false if the queue is full (should not happen under normal load).
    /// </summary>
    bool Enqueue(GuestyWebhookEnvelope envelope);

    /// <summary>
    /// Asynchronously dequeue the next envelope.
    /// Awaits until an item is available or the token is cancelled.
    /// </summary>
    ValueTask<GuestyWebhookEnvelope> DequeueAsync(CancellationToken cancellationToken);
}
