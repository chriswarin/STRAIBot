using STRAIBot.Models;
using STRAIBot.Models.Guesty;
using STRAIBot.Services.Guesty;

namespace STRAIBot.Services.Properties;

/// <summary>
/// Resolves the full PropertyMapping for the property associated with a Guesty
/// webhook payload by fetching the reservation, extracting the Guesty listing ID,
/// and looking it up in IPropertyMappingService.
/// </summary>
public class PropertyResolver : IPropertyResolver
{
    private readonly IGuestyReservationClient _guestyClient;
    private readonly IPropertyMappingService _mappingService;
    private readonly ILogger<PropertyResolver> _logger;

    public PropertyResolver(
        IGuestyReservationClient guestyClient,
        IPropertyMappingService mappingService,
        ILogger<PropertyResolver> logger)
    {
        _guestyClient = guestyClient;
        _mappingService = mappingService;
        _logger = logger;
    }

    public async Task<PropertyMapping?> ResolveAsync(
        GuestyWebhookEnvelope webhook,
        CancellationToken cancellationToken = default)
    {
        // ── Step 1: Extract reservationId ─────────────────────────────────────
        var reservationId = webhook.ReservationId;

        if (string.IsNullOrWhiteSpace(reservationId))
        {
            _logger.LogWarning(
                "Guesty webhook event '{Event}' has no reservationId — cannot resolve property.",
                webhook.Event);
            return null;
        }

        // ── Step 2: Fetch reservation from Guesty ─────────────────────────────
        var reservation = await _guestyClient.GetReservationAsync(reservationId, cancellationToken);

        if (reservation is null)
        {
            _logger.LogWarning(
                "Could not fetch reservation {ReservationId} from Guesty — property unresolved.",
                reservationId);
            return null;
        }

        // ── Step 3: Extract GuestyListingId ───────────────────────────────────
        var guestyListingId = reservation.ResolvedListingId;

        if (string.IsNullOrWhiteSpace(guestyListingId))
        {
            _logger.LogWarning(
                "Reservation {ReservationId} has no listingId. " +
                "Add a PropertyMapping with a GuestyListingId to enable auto-processing.",
                reservationId);
            return null;
        }

        // ── Step 4: Match GuestyListingId → PropertyMapping ───────────────────
        var mapping = _mappingService.GetByGuestyListingId(guestyListingId);

        if (mapping is null)
        {
            _logger.LogWarning(
                "No active PropertyMapping for GuestyListingId '{GuestyListingId}' " +
                "(ReservationId={ReservationId}). " +
                "Add an entry to PropertyMappings in appsettings.json for this listing.",
                guestyListingId, reservationId);
            return null;
        }

        _logger.LogInformation(
            "Property resolved | ReservationId={ReservationId} | " +
            "GuestyListingId={GuestyListingId} | PropertyKey={PropertyKey} | " +
            "GBrainMemoryKey={GBrainMemoryKey}",
            reservationId, guestyListingId, mapping.PropertyKey, mapping.GBrainMemoryKey);

        return mapping;
    }
}
