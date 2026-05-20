using STRAIBot.Models.Guesty;

namespace STRAIBot.Services.Guesty;

/// <summary>
/// Fetches reservation details from the Guesty API.
/// Used by PropertyResolver to extract the listingId for property mapping.
/// </summary>
public interface IGuestyReservationClient
{
    /// <summary>
    /// Fetches a Guesty reservation by ID.
    /// Returns null if the reservation cannot be retrieved (missing token, network error, 404).
    /// </summary>
    Task<GuestyReservationDto?> GetReservationAsync(
        string reservationId,
        CancellationToken cancellationToken = default);
}
