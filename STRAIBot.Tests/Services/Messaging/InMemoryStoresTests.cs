using STRAIBot.Models.Guesty;
using STRAIBot.Services.Messaging;

namespace STRAIBot.Tests.Services.Messaging;

public class InMemoryStoresTests
{
    [Fact]
    public async Task WebhookQueue_EnqueueThenDequeue_ReturnsSameEnvelope()
    {
        var queue = new InMemoryWebhookMessageQueue();
        var envelope = new GuestyWebhookEnvelope
        {
            Event = "reservation.messageReceived",
            ReservationId = "res-123",
            Data = new GuestyWebhookData
            {
                MessageId = "msg-1",
                PlainText = "Hello"
            }
        };

        var queued = queue.Enqueue(envelope);
        var dequeued = await queue.DequeueAsync(CancellationToken.None);

        Assert.True(queued);
        Assert.Equal("res-123", dequeued.ReservationId);
        Assert.Equal("msg-1", dequeued.Data?.MessageId);
    }

    [Fact]
    public async Task WebhookQueue_PreservesOrder_ForSequentialMessages()
    {
        var queue = new InMemoryWebhookMessageQueue();

        queue.Enqueue(new GuestyWebhookEnvelope { ReservationId = "res-1" });
        queue.Enqueue(new GuestyWebhookEnvelope { ReservationId = "res-2" });

        var first = await queue.DequeueAsync(CancellationToken.None);
        var second = await queue.DequeueAsync(CancellationToken.None);

        Assert.Equal("res-1", first.ReservationId);
        Assert.Equal("res-2", second.ReservationId);
    }

    [Fact]
    public void ProcessedStore_HasProcessed_FalseForUnknownOrWhitespace()
    {
        var store = new InMemoryProcessedMessageStore();

        Assert.False(store.HasProcessed("unknown"));
        Assert.False(store.HasProcessed(""));
        Assert.False(store.HasProcessed("   "));
    }

    [Fact]
    public void ProcessedStore_MarkProcessed_TracksMessageId()
    {
        var store = new InMemoryProcessedMessageStore();

        store.MarkProcessed("msg-42");

        Assert.True(store.HasProcessed("msg-42"));
    }

    [Fact]
    public void ProcessedStore_MarkProcessed_IgnoresWhitespaceMessageId()
    {
        var store = new InMemoryProcessedMessageStore();

        store.MarkProcessed("  ");

        Assert.False(store.HasProcessed("  "));
    }
}
