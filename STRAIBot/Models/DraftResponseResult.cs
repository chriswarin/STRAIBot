namespace STRAIBot.Models;

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Emergency
}

public enum PolicyRuleType
{
    HardRule,
    Informational,
    HostDecision,
    Emergency
}

public enum MessageCategory
{
    AmenityQuestion,
    ParkingQuestion,
    WiFiQuestion,
    PoolQuestion,
    EarlyCheckInRequest,
    LateCheckoutRequest,
    PetRequest,
    ServiceAnimalRequest,
    SmokingQuestion,
    OccupancyQuestion,
    AgeQuestion,
    CookingQuestion,
    Complaint,
    DamageIssue,
    MaintenanceIssue,
    Emergency,
    PartyRisk,
    RefundRequest,
    General
}

public enum ConfidenceLevel
{
    Low,
    Medium,
    High
}

public class DraftResponseResult
{
    public string PropertyName { get; set; } = string.Empty;
    public string GuestMessage { get; set; } = string.Empty;
    public string RetrievedContext { get; set; } = string.Empty;
    public string GuestResponse { get; set; } = string.Empty;
    public bool ShouldAutoSend { get; set; }
    public bool RequiresHostReview { get; set; }
    public bool ShouldNotifyHost { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public PolicyRuleType PolicyRuleType { get; set; }
    public MessageCategory Category { get; set; }
    public ConfidenceLevel Confidence { get; set; }
    public string? EscalationReason { get; set; }
    public string? MatchedPolicy { get; set; }
    public string? ReasoningSummary { get; set; }
}

