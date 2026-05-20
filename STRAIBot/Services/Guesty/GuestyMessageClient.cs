using System.Net.Http.Headers;
using System.Net.Http.Json;
using STRAIBot.Models.Messaging;

namespace STRAIBot.Services.Guesty;

/// <summary>
/// Sends outbound messages via the Guesty Open API.
/// Configured via the "Guesty" section in appsettings.json.
///
/// Safe to run without a token — logs a warning and skips the HTTP call.
/// Never throws fatal exceptions so webhook processing continues on send failure.
/// </summary>
public class GuestyMessageClient : IGuestyMessageClient
{
    private readonly HttpClient _http;
    private readonly ILogger<GuestyMessageClient> _logger;
    private readonly string _baseUrl;
    private readonly string? _accessToken;

    public GuestyMessageClient(
        HttpClient http,
        IConfiguration config,
        ILogger<GuestyMessageClient> logger)
    {
        _http = http;
        _logger = logger;
        _baseUrl = (config["Guesty:BaseUrl"] ?? "https://open-api.guesty.com/v1").TrimEnd('/');
        _accessToken = config["Guesty:AccessToken"];

        if (string.IsNullOrWhiteSpace(_accessToken))
            _logger.LogWarning(
                "Guesty:AccessToken is not configured. " +
                "GuestyMessageClient will skip all outbound send requests.");
    }

    public async Task SendMessageAsync(
        string conversationId,
        string messageBody,
        string module = "email",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            _logger.LogWarning("SendMessageAsync called with empty conversationId — skipped.");
            return;
        }

        if (string.IsNullOrWhiteSpace(messageBody))
        {
            _logger.LogWarning(
                "SendMessageAsync called with empty messageBody for conversation {ConversationId} — skipped.",
                conversationId);
            return;
        }

        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            _logger.LogWarning(
                "Skipping Guesty send for conversation {ConversationId} — AccessToken not configured.",
                conversationId);
            return;
        }

        var url = $"{_baseUrl}/communication/conversations/{conversationId}/send-message";
        var payload = new GuestySendMessageRequest { Body = messageBody, Module = module };

        _logger.LogInformation(
            "Sending Guesty message | ConversationId={ConversationId} | Module={Module} | " +
            "BodyPreview={Preview}",
            conversationId,
            module,
            messageBody.Length > 100 ? messageBody[..100] + "…" : messageBody);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Content = JsonContent.Create(payload);

            var response = await _http.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Guesty message sent successfully | ConversationId={ConversationId} | Status={Status}",
                    conversationId, (int)response.StatusCode);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Guesty send failed | ConversationId={ConversationId} | Status={Status} | Response={Body}",
                    conversationId, (int)response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception sending Guesty message for conversation {ConversationId}.",
                conversationId);
        }
    }
}
