namespace STRAIBot.Models.Messaging;

/// <summary>
/// A fully-resolved, cleaned inbound guest message ready for AI processing.
/// Created by GuestyWebhookProcessor after property resolution and HTML cleaning.
///
/// FUTURE: persist to database for audit trail, replay, and analytics.
/// </summary>
public class ProcessedGuestMessage
{
    /// <summary>Guesty conversation/thread ID — used to send the reply.</summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>Guesty reservation ID — used to look up property mapping.</summary>
    public string ReservationId { get; set; } = string.Empty;

    /// <summary>
    /// Guesty message ID — used for duplicate detection.
    /// FUTURE: store in database to survive restarts.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Resolved internal STRAIBot property key (e.g. "CozyCrab").</summary>
    public string PropertyKey { get; set; } = string.Empty;

    /// <summary>Guest name from the reservation, if available.</summary>
    public string GuestName { get; set; } = string.Empty;

    /// <summary>Cleaned plain-text guest message body.</summary>
    public string GuestMessage { get; set; } = string.Empty;

    /// <summary>
    /// Raw webhook payload for debugging and audit purposes.
    /// FUTURE: store in database for replay and debugging.
    /// </summary>
    public string RawPayload { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the webhook was received by STRAIBot.</summary>
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
}
