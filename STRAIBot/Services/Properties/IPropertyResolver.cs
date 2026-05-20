using STRAIBot.Models;
using STRAIBot.Models.Guesty;

namespace STRAIBot.Services.Properties;

/// <summary>
/// Resolves the full PropertyMapping for the property associated with a
/// Guesty webhook payload.
///
/// Flow:
///   webhook.reservationId
///     → GET /reservations/{id}          (GuestyReservationClient)
///     → extract GuestyListingId
///     → GetByGuestyListingId            (IPropertyMappingService)
///     → return PropertyMapping
///
/// Returns null if the reservation cannot be fetched or the listing ID has
/// no active mapping. Callers must not auto-send when null is returned.
/// </summary>
public interface IPropertyResolver
{
    /// <summary>
    /// Resolves the full PropertyMapping for the property associated with the webhook.
    /// Returns null if resolution fails at any step.
    /// </summary>
    Task<PropertyMapping?> ResolveAsync(
        GuestyWebhookEnvelope webhook,
        CancellationToken cancellationToken = default);
}
