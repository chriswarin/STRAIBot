namespace STRAIBot.Services.Guesty;

/// <summary>
/// Sends outbound messages into a Guesty conversation thread.
/// Used by GuestyWebhookProcessor when AutoSendMessaging:Enabled = true
/// and DryRunMode = false.
/// </summary>
public interface IGuestyMessageClient
{
    /// <summary>
    /// Sends a reply message to a Guesty conversation.
    /// POST /communication/conversations/{conversationId}/send-message
    ///
    /// Does not throw on failure — errors are logged and the pipeline continues.
    /// </summary>
    /// <param name="conversationId">The Guesty conversation/thread ID.</param>
    /// <param name="messageBody">The plain-text reply to send.</param>
    /// <param name="module">
    /// Messaging module/channel (default "email").
    /// Use the same module the guest used when possible.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendMessageAsync(
        string conversationId,
        string messageBody,
        string module = "email",
        CancellationToken cancellationToken = default);
}
