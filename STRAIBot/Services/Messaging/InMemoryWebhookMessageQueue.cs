using System.Threading.Channels;
using STRAIBot.Models.Guesty;

namespace STRAIBot.Services.Messaging;

/// <summary>
/// In-process webhook queue backed by a bounded <see cref="Channel{T}"/>.
/// Registered as a singleton so the controller and background service
/// share the same channel instance.
///
/// Capacity is intentionally small — webhooks should be processed quickly.
/// If the channel is full, the oldest item is not dropped; the enqueue
/// returns false and the caller logs a warning.
/// </summary>
public sealed class InMemoryWebhookMessageQueue : IWebhookMessageQueue
{
    private const int Capacity = 512;

    private readonly Channel<GuestyWebhookEnvelope> _channel =
        Channel.CreateBounded<GuestyWebhookEnvelope>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public bool Enqueue(GuestyWebhookEnvelope envelope) =>
        _channel.Writer.TryWrite(envelope);

    public ValueTask<GuestyWebhookEnvelope> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
