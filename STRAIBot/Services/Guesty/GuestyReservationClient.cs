using System.Net.Http.Headers;
using System.Net.Http.Json;
using STRAIBot.Models.Guesty;

namespace STRAIBot.Services.Guesty;

/// <summary>
/// HTTP client for the Guesty Open API v1.
/// Configured via the "Guesty" section in appsettings.json.
///
/// If AccessToken is empty the client logs a warning and returns null
/// so the app starts and runs locally without a live Guesty connection.
/// </summary>
public class GuestyReservationClient : IGuestyReservationClient
{
    private readonly HttpClient _http;
    private readonly ILogger<GuestyReservationClient> _logger;
    private readonly string _baseUrl;
    private readonly string? _accessToken;

    public GuestyReservationClient(
        HttpClient http,
        IConfiguration config,
        ILogger<GuestyReservationClient> logger)
    {
        _http = http;
        _logger = logger;
        _baseUrl = (config["Guesty:BaseUrl"] ?? "https://open-api.guesty.com/v1").TrimEnd('/');
        _accessToken = config["Guesty:AccessToken"];

        if (string.IsNullOrWhiteSpace(_accessToken))
            _logger.LogWarning(
                "Guesty:AccessToken is not configured. " +
                "GuestyReservationClient will return null for all requests. " +
                "Set the token in appsettings or environment variables before going live.");
    }

    public async Task<GuestyReservationDto?> GetReservationAsync(
        string reservationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            _logger.LogWarning(
                "Skipping Guesty reservation lookup for {ReservationId} — AccessToken not configured.",
                reservationId);
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"{_baseUrl}/reservations/{reservationId}");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Guesty GET /reservations/{ReservationId} returned {StatusCode}.",
                    reservationId, response.StatusCode);
                return null;
            }

            var reservation = await response.Content
                .ReadFromJsonAsync<GuestyReservationDto>(cancellationToken: cancellationToken);

            if (reservation is null)
                _logger.LogWarning(
                    "Guesty reservation {ReservationId} returned a null body.", reservationId);

            return reservation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error fetching Guesty reservation {ReservationId} from {BaseUrl}.",
                reservationId, _baseUrl);
            return null;
        }
    }
}
