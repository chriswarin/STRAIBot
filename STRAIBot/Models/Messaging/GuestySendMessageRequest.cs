namespace STRAIBot.Models.Messaging;

/// <summary>
/// Request body for POST /communication/conversations/{conversationId}/send-message
/// in the Guesty Open API.
/// </summary>
public class GuestySendMessageRequest
{
    /// <summary>The message body to send to the guest.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// The messaging module/channel. Defaults to "email".
    /// Other known values: "sms", "airbnb", "booking_com".
    /// Use the same module the guest used when possible.
    /// </summary>
    public string Module { get; set; } = "email";
}
