namespace STRAIBot.Models;

/// <summary>
/// External platform identifiers for a single STRAIBot property.
/// Each field holds the listing/property ID as that platform knows it.
///
/// Null or empty means the property is not listed on that platform,
/// or the ID has not been configured yet.
///
/// FUTURE: extend with BookingComListingId, DirectBookingId, etc.
/// </summary>
public class PropertyExternalIds
{
    /// <summary>
    /// The Guesty listing ID for this property.
    /// Found in Guesty under Listings → select listing → URL or API.
    /// Used to match incoming Guesty webhook reservations to the correct property.
    /// </summary>
    public string? GuestyListingId { get; set; }

    /// <summary>
    /// Airbnb listing ID for this property.
    /// FUTURE: used when an Airbnb webhook or channel manager integration is added.
    /// </summary>
    public string? AirbnbListingId { get; set; }

    /// <summary>
    /// VRBO/HomeAway listing ID for this property.
    /// FUTURE: used when a VRBO channel integration is added.
    /// </summary>
    public string? VrboListingId { get; set; }

    /// <summary>
    /// Catch-all for any other PMS or channel not listed above.
    /// </summary>
    public string? OtherPmsListingId { get; set; }
}
