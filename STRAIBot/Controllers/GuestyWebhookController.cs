using Microsoft.AspNetCore.Mvc;
using STRAIBot.Models.Guesty;
using STRAIBot.Services.Messaging;

namespace STRAIBot.Controllers;

/// <summary>
/// Receives inbound Guesty webhook events at POST /api/webhooks/guesty.
///
/// Responsibilities of this controller:
///   - Accept the raw Guesty payload
///   - Log the event type and reservationId
///   - Enqueue the envelope into IWebhookMessageQueue
///   - Return 200 OK as fast as possible
///
/// All business logic (property resolution, HTML cleaning, draft generation,
/// host notification) runs in GuestyWebhookProcessor (BackgroundService).
/// This controller never calls DraftResponseService directly.
///
/// Guesty will retry if it does not receive a 2xx response within ~5 seconds.
/// </summary>
[ApiController]
[Route("api/webhooks")]
[Tags("Webhooks")]
public class GuestyWebhookController : ControllerBase
{
    private readonly IWebhookMessageQueue _queue;
    private readonly ILogger<GuestyWebhookController> _logger;

    public GuestyWebhookController(
        IWebhookMessageQueue queue,
        ILogger<GuestyWebhookController> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    /// <summary>
    /// Guesty webhook receiver. Acknowledges the event and enqueues it for async processing.
    /// </summary>
    /// <remarks>
    /// Configure this URL in your Guesty webhook settings:
    ///   POST https://your-host/api/webhooks/guesty
    ///
    /// Supported event: reservation.messageReceived
    ///
    /// Other event types are acknowledged (200) and silently ignored so Guesty
    /// does not retry them.
    /// </remarks>
    [HttpPost("guesty")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Receive([FromBody] GuestyWebhookEnvelope envelope)
    {
        _logger.LogInformation(
            "Guesty webhook received | Event={Event} | ReservationId={ReservationId}",
            envelope.Event, envelope.ReservationId);

        // Acknowledge and ignore events that are not guest messages
        if (!string.Equals(envelope.Event, "reservation.messageReceived",
                StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Guesty event '{Event}' acknowledged and ignored.", envelope.Event);
            return Ok(new { acknowledged = true, queued = false });
        }

        // Acknowledge and ignore outbound (host-to-guest) messages to avoid loops
        var direction = envelope.Data?.Direction;
        if (!string.IsNullOrWhiteSpace(direction) &&
            !string.Equals(direction, "guest_to_host", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "Guesty message direction '{Direction}' for reservation {ReservationId} ignored.",
                direction, envelope.ReservationId);
            return Ok(new { acknowledged = true, queued = false });
        }

        // Enqueue for GuestyWebhookProcessor — returns 200 immediately
        var queued = _queue.Enqueue(envelope);

        if (!queued)
            _logger.LogWarning(
                "Webhook queue is full — envelope for reservation {ReservationId} was dropped.",
                envelope.ReservationId);

        return Ok(new { acknowledged = true, queued });
    }
}
