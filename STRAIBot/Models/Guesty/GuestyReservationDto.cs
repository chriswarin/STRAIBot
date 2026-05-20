using System.Text.Json.Serialization;

namespace STRAIBot.Models.Guesty;

/// <summary>
/// Subset of the Guesty GET /reservations/{id} response used by STRAIBot.
/// Only the fields needed for property resolution are mapped.
/// </summary>
public class GuestyReservationDto
{
    /// <summary>Guesty reservation ID.</summary>
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The Guesty listing ID associated with this reservation.
    /// Primary field used to resolve the STRAIBot PropertyKey.
    /// </summary>
    [JsonPropertyName("listingId")]
    public string? ListingId { get; set; }

    /// <summary>
    /// Nested listing object. Some Guesty API versions embed the listing inline.
    /// ListingId from this object is used as a fallback if the top-level listingId is absent.
    /// </summary>
    [JsonPropertyName("listing")]
    public GuestyListingSummary? Listing { get; set; }

    /// <summary>Reservation status, e.g. "confirmed", "inquiry", "cancelled".</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>Guest name for logging.</summary>
    [JsonPropertyName("guestName")]
    public string? GuestName { get; set; }

    /// <summary>
    /// Resolves the listing ID from either the top-level field or the nested listing object.
    /// Returns null if neither is present.
    /// </summary>
    [JsonIgnore]
    public string? ResolvedListingId =>
        !string.IsNullOrWhiteSpace(ListingId) ? ListingId : Listing?.Id;
}

/// <summary>Minimal inline listing summary embedded in a reservation response.</summary>
public class GuestyListingSummary
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }
}
