using STRAIBot.Models;

namespace STRAIBot.Services;

/// <summary>
/// Enforces hard business rules that the AI must never violate.
/// Corrections are logged so any AI drift is visible in application logs.
/// </summary>
public class AiDecisionValidator : IAiDecisionValidator
{
    private readonly IPropertyMemoryService _markdownMemory;
    private readonly ILogger<AiDecisionValidator> _logger;

    // ── Emergency terms ───────────────────────────────────────────────────────
    private static readonly string[] EmergencyTerms =
    [
        "fire", "smoke", "gas smell", "smell gas", "gas leak",
        "leak", "leaking", "flood", "flooding", "water coming",
        "police", "911", "unsafe", "not safe", "danger",
        "lockout", "locked out", "cannot get in", "can't get in",
        "no heat", "heat not working", "no ac", "ac not working",
        "air conditioning not working", "carbon monoxide", "emergency"
    ];

    // ── Legal / service animal terms ──────────────────────────────────────────
    private static readonly string[] LegalTerms =
    [
        "service animal", "service dog", "emotional support animal", "esa",
        "ada", "legal accommodation", "accommodation request",
        "lawsuit", "discrimination",
        "already brought", "already have my pet", "brought my dog", "brought my cat"
    ];

    // ── Terms that must never be approved by the AI ───────────────────────────
    // If GuestResponse contains any approval language alongside these topics,
    // the validator blocks auto-send and forces host review.
    private static readonly string[] NeverApproveTopics =
    [
        "pet", "dog", "cat", "party", "parties", "event", "gathering",
        "extra guest", "early check-in", "late checkout", "late check-out",
        "refund", "cancellation", "service animal"
    ];

    private static readonly string[] ApprovalPhrases =
    [
        "yes, you can", "sure, that", "of course", "no problem", "absolutely",
        "that's fine", "that is fine", "you're welcome to", "we allow", "allowed",
        "approved", "exception", "we can accommodate"
    ];

    public AiDecisionValidator(
        IPropertyMemoryService markdownMemory,
        ILogger<AiDecisionValidator> logger)
    {
        _markdownMemory = markdownMemory;
        _logger = logger;
    }

    public AiGuestMessageDecision ValidateAndCorrect(
        AiGuestMessageDecision decision,
        string propertyName,
        string guestMessage,
        string retrievedContext)
    {
        var msg = guestMessage.ToLowerInvariant();
        var response = (decision.GuestResponse ?? string.Empty).ToLowerInvariant();

        // ── Rule 1: Unknown or missing property → block auto-send ─────────────
        if (string.IsNullOrWhiteSpace(decision.PropertyName) ||
            !_markdownMemory.PropertyExists(decision.PropertyName))
        {
            Correct(decision, "Unknown or missing property — blocked auto-send.",
                autoSend: false, requiresHostReview: true, shouldNotifyHost: true);
        }

        // ── Rule 2: Empty guest response → cannot auto-send ───────────────────
        if (string.IsNullOrWhiteSpace(decision.GuestResponse))
        {
            Correct(decision, "GuestResponse is empty — blocked auto-send.",
                autoSend: false, requiresHostReview: true);
            decision.GuestResponse =
                "Thanks for your message. The host will review this and follow up with you shortly.";
        }

        // ── Rule 3: Low confidence → require host review ──────────────────────
        if (string.Equals(decision.Confidence, "Low", StringComparison.OrdinalIgnoreCase))
        {
            if (!decision.RequiresHostReview)
            {
                Correct(decision, "Confidence is Low — host review required.",
                    requiresHostReview: true);
            }
        }

        // ── Rule 4: Emergency terms in guest message → force Emergency flags ──
        if (ContainsAny(msg, EmergencyTerms) && !IsSmokingOnly(msg))
        {
            if (decision.RiskLevel != "Emergency" || !decision.RequiresHostReview || !decision.ShouldNotifyHost)
            {
                Correct(decision, "Emergency keyword in message — forced Emergency classification.",
                    autoSend: true, requiresHostReview: true, shouldNotifyHost: true);
                decision.RiskLevel = "Emergency";
                decision.PolicyRuleType = "Emergency";
                decision.Category = "Emergency";
                decision.EscalationReason = "Emergency keyword detected. Immediate host notification required.";
            }
        }

        // ── Rule 5: Legal / service animal language → force host review ───────
        if (ContainsAny(msg, LegalTerms))
        {
            if (!decision.RequiresHostReview || !decision.ShouldNotifyHost)
            {
                Correct(decision, "Legal/service animal language detected — forced host review.",
                    autoSend: true, requiresHostReview: true, shouldNotifyHost: true);
                decision.EscalationReason ??= "Service animal or legal accommodation language detected.";
            }
        }

        // ── Rule 6: AI must not approve forbidden topics ───────────────────────
        if (ContainsAny(msg, NeverApproveTopics) && ContainsAny(response, ApprovalPhrases))
        {
            Correct(decision, "AI response appears to approve a forbidden topic — blocked auto-send.",
                autoSend: false, requiresHostReview: true, shouldNotifyHost: true);
            decision.ReasoningSummary =
                (decision.ReasoningSummary ?? string.Empty) +
                " [VALIDATOR: Possible AI approval of forbidden topic — host review required.]";
        }

        // ── Rule 7: Cannot auto-send without a response ───────────────────────
        if (decision.ShouldAutoSend && string.IsNullOrWhiteSpace(decision.GuestResponse))
        {
            Correct(decision, "ShouldAutoSend=true but GuestResponse is empty — blocked.",
                autoSend: false, requiresHostReview: true);
        }

        return decision;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void Correct(
        AiGuestMessageDecision decision,
        string reason,
        bool? autoSend = null,
        bool? requiresHostReview = null,
        bool? shouldNotifyHost = null)
    {
        _logger.LogWarning("AiDecisionValidator correction | Property={Property} | Reason={Reason}",
            decision.PropertyName, reason);

        if (autoSend.HasValue) decision.ShouldAutoSend = autoSend.Value;
        if (requiresHostReview.HasValue) decision.RequiresHostReview = requiresHostReview.Value;
        if (shouldNotifyHost.HasValue) decision.ShouldNotifyHost = shouldNotifyHost.Value;

        decision.ReasoningSummary =
            (decision.ReasoningSummary ?? string.Empty) + $" [VALIDATOR: {reason}]";
    }

    private static bool ContainsAny(string message, string[] terms) =>
        terms.Any(t => message.Contains(t, StringComparison.OrdinalIgnoreCase));

    private static bool IsSmokingOnly(string msg) =>
        (msg.Contains("smok") || msg.Contains("vap") || msg.Contains("cigarette") || msg.Contains("cigar"))
        && !msg.Contains("fire") && !msg.Contains("flood") && !msg.Contains("leak")
        && !msg.Contains("lockout") && !msg.Contains("police") && !msg.Contains("gas")
        && !msg.Contains("carbon monoxide") && !msg.Contains("emergency");
}
