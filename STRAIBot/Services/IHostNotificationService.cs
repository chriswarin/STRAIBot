using STRAIBot.Models;

namespace STRAIBot.Services;

public interface IHostNotificationService
{
    /// <summary>
    /// Sends a notification to the host about a high-risk or emergency guest message.
    /// No-op in the current stub implementation; replace with real delivery later.
    /// </summary>
    Task NotifyAsync(string propertyName, string guestMessage, MessageRiskAnalysis risk);
}
