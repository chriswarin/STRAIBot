using System.Text.Json.Serialization;

namespace STRAIBot.Models.Guesty;

/// <summary>
/// Top-level envelope for a Guesty webhook POST.
/// Guesty sends reservation.messageReceived (and other event types) to this endpoint.
///
/// Guesty docs: https://open-api.guesty.com/docs/webhooks
///
/// Only the fields STRAIBot needs are mapped here. Unknown fields are ignored
/// because JsonSerializer is configured with default (permissive) options.
/// </summary>
public class GuestyWebhookEnvelope
{
    /// <summary>Guesty event type, e.g. "reservation.messageReceived".</summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    /// <summary>Top-level reservation ID. Use this to call GET /reservations/{id}.</summary>
    [JsonPropertyName("reservationId")]
    public string ReservationId { get; set; } = string.Empty;

    /// <summary>
    /// Nested data payload. Structure varies by event type.
    /// For reservation.messageReceived this contains the message body.
    /// </summary>
    [JsonPropertyName("data")]
    public GuestyWebhookData? Data { get; set; }
}

/// <summary>
/// The "data" block from a Guesty reservation.messageReceived webhook.
/// </summary>
public class GuestyWebhookData
{
    /// <summary>
    /// Guesty message ID. Used for duplicate detection and idempotency.
    /// FUTURE: persist to database so deduplication survives process restarts.
    /// </summary>
    [JsonPropertyName("_id")]
    public string? MessageId { get; set; }

    /// <summary>
    /// The message body as sent by Guesty. May contain HTML.
    /// Use IHtmlMessageCleaner to convert to plain text before processing.
    /// </summary>
    [JsonPropertyName("body")]
    public string? Body { get; set; }

    /// <summary>Plain text version of the message, if Guesty provides it.</summary>
    [JsonPropertyName("plainText")]
    public string? PlainText { get; set; }

    /// <summary>Direction: "guest_to_host" or "host_to_guest".</summary>
    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    /// <summary>
    /// Guesty conversation/thread ID.
    /// Required to send a reply via POST /communication/conversations/{id}/send-message.
    /// </summary>
    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; set; }

    /// <summary>
    /// The messaging platform/channel module, e.g. "email", "sms", "airbnb", "booking_com".
    /// Passed through to the outbound reply so STRAIBot responds on the same channel.
    /// </summary>
    [JsonPropertyName("module")]
    public string? Module { get; set; }

    /// <summary>The platform origin, e.g. "guesty", "airbnb", "booking_com".</summary>
    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    /// <summary>Guest name, if provided in the message payload.</summary>
    [JsonPropertyName("guestName")]
    public string? GuestName { get; set; }
}

