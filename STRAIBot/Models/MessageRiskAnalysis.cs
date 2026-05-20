namespace STRAIBot.Models;

public class MessageRiskAnalysis
{
    public RiskLevel RiskLevel { get; set; }
    public PolicyRuleType PolicyRuleType { get; set; }
    public MessageCategory Category { get; set; }
    public bool ShouldAutoSend { get; set; }
    public bool RequiresHostReview { get; set; }
    public bool ShouldNotifyHost { get; set; }
    public string? EscalationReason { get; set; }
}
