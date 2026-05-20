using STRAIBot.Models;

namespace STRAIBot.Services;

/// <summary>
/// Stub implementation of host notification.
/// Replace with email, SMS, Guesty webhook, or push notification in a future iteration.
/// </summary>
public class HostNotificationService : IHostNotificationService
{
    private readonly ILogger<HostNotificationService> _logger;

    public HostNotificationService(ILogger<HostNotificationService> logger)
    {
        _logger = logger;
    }

    public Task NotifyAsync(string propertyName, string guestMessage, MessageRiskAnalysis risk)
    {
        // TODO: Replace with real notification delivery (email, SMS, Guesty, push)
        _logger.LogWarning(
            "[HOST NOTIFICATION] Property={Property} | RiskLevel={RiskLevel} | Category={Category} | Message=\"{Message}\" | Reason={Reason}",
            propertyName,
            risk.RiskLevel,
            risk.Category,
            guestMessage,
            risk.EscalationReason ?? "N/A");

        return Task.CompletedTask;
    }
}
