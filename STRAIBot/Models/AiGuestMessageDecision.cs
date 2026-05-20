namespace STRAIBot.Models;

/// <summary>
/// Represents the decision contract returned by the AI guest message decision service.
/// In the current implementation this is produced by a local rule-based placeholder.
/// When OpenAI/Azure OpenAI is integrated, the JSON response from the model will be
/// deserialized directly into this type.
///
/// All string enum fields (RiskLevel, PolicyRuleType, Confidence) use string values so
/// JSON from OpenAI maps cleanly without a custom converter.
/// </summary>
public class AiGuestMessageDecision
{
    /// <summary>The property the guest is asking about.</summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>The original guest message.</summary>
    public string GuestMessage { get; set; } = string.Empty;

    /// <summary>
    /// Message category string, e.g. "PetRequest", "Emergency", "AmenityQuestion".
    /// Matches the <see cref="MessageCategory"/> enum names.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Policy rule type: HardRule | Informational | HostDecision | Emergency
    /// </summary>
    public string PolicyRuleType { get; set; } = string.Empty;

    /// <summary>
    /// Risk level: Low | Medium | High | Emergency
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Confidence in the response: Low | Medium | High
    /// </summary>
    public string Confidence { get; set; } = string.Empty;

    /// <summary>Whether the response can be sent to the guest automatically.</summary>
    public bool ShouldAutoSend { get; set; }

    /// <summary>Whether a human host must review before or after sending.</summary>
    public bool RequiresHostReview { get; set; }

    /// <summary>Whether the host should be notified about this message.</summary>
    public bool ShouldNotifyHost { get; set; }

    /// <summary>The guest-facing response text.</summary>
    public string GuestResponse { get; set; } = string.Empty;

    /// <summary>
    /// The specific policy section or rule that was matched, e.g. "Pet Policy".
    /// Null if no specific policy was identified.
    /// </summary>
    public string? MatchedPolicy { get; set; }

    /// <summary>
    /// A brief internal summary of why this decision was made.
    /// Used for debugging and audit logging. Not sent to the guest.
    /// </summary>
    public string? ReasoningSummary { get; set; }

    /// <summary>
    /// Reason the message was escalated to the host, if applicable.
    /// Null for low-risk auto-responses.
    /// </summary>
    public string? EscalationReason { get; set; }
}
