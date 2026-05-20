using STRAIBot.Models;
using STRAIBot.Services.Memory;

namespace STRAIBot.Services;

/// <summary>
/// Orchestrates the full guest message pipeline:
///   1. Retrieve property memory context
///   2. Call IAiGuestMessageDecisionService to produce a decision
///   3. Call IAiDecisionValidator to enforce safety invariants
///   4. Notify host if required
///   5. Map the decision to DraftResponseResult and log
///
/// This service contains NO response-construction logic and NO keyword lists.
/// All decision intelligence lives in AiGuestMessageDecisionService.
/// All safety guardrails live in AiDecisionValidator.
///
/// NOTE: MessageRiskClassifier is retained as an optional safety fallback but is
/// no longer the primary decision engine. It may be removed once OpenAI integration
/// is validated in production.
/// </summary>
public class DraftResponseService : IDraftResponseService
{
    private readonly IMemoryContextService _memoryService;
    private readonly IAiGuestMessageDecisionService _decisionService;
    private readonly IAiDecisionValidator _validator;
    private readonly IHostNotificationService _hostNotification;
    private readonly ILogger<DraftResponseService> _logger;

    public DraftResponseService(
        IMemoryContextService memoryService,
        IAiGuestMessageDecisionService decisionService,
        IAiDecisionValidator validator,
        IHostNotificationService hostNotification,
        ILogger<DraftResponseService> logger)
    {
        _memoryService = memoryService;
        _decisionService = decisionService;
        _validator = validator;
        _hostNotification = hostNotification;
        _logger = logger;
    }

    public async Task<DraftResponseResult> GenerateDraftAsync(string propertyName, string guestMessage)
    {
        // ── Step 1: Retrieve property memory context ──────────────────────────
        var retrievedContext = _memoryService.PropertyExists(propertyName)
            ? await _memoryService.GetContextAsync(propertyName, guestMessage)
            : string.Empty;

        // ── Step 2: Produce an AI decision ────────────────────────────────────
        var decision = await _decisionService.DecideAsync(
            propertyName, guestMessage, retrievedContext);

        // ── Step 3: Validate and correct the decision ─────────────────────────
        decision = _validator.ValidateAndCorrect(decision, propertyName, guestMessage, retrievedContext);

        // ── Step 4: Notify host if required ───────────────────────────────────
        if (decision.ShouldNotifyHost)
        {
            // Build a lightweight MessageRiskAnalysis just for the notification signature.
            // MessageRiskClassifier is intentionally NOT called here; this shim exists so
            // IHostNotificationService keeps its current interface without breaking changes.
            var notificationRisk = BuildNotificationRisk(decision);
            await _hostNotification.NotifyAsync(propertyName, guestMessage, notificationRisk);
        }

        // ── Step 5: Map to DraftResponseResult and return ─────────────────────
        var result = MapToResult(decision, retrievedContext);
        LogResult(result);
        return result;
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static DraftResponseResult MapToResult(AiGuestMessageDecision d, string retrievedContext) =>
        new()
        {
            PropertyName = d.PropertyName,
            GuestMessage = d.GuestMessage,
            RetrievedContext = retrievedContext,
            GuestResponse = d.GuestResponse,
            ShouldAutoSend = d.ShouldAutoSend,
            RequiresHostReview = d.RequiresHostReview,
            ShouldNotifyHost = d.ShouldNotifyHost,
            RiskLevel = ParseEnum(d.RiskLevel, Models.RiskLevel.Low),
            PolicyRuleType = ParseEnum(d.PolicyRuleType, Models.PolicyRuleType.Informational),
            Category = ParseEnum(d.Category, MessageCategory.General),
            Confidence = ParseEnum(d.Confidence, ConfidenceLevel.Low),
            EscalationReason = d.EscalationReason,
            MatchedPolicy = d.MatchedPolicy,
            ReasoningSummary = d.ReasoningSummary
        };

    private static MessageRiskAnalysis BuildNotificationRisk(AiGuestMessageDecision d) =>
        new()
        {
            RiskLevel = ParseEnum(d.RiskLevel, Models.RiskLevel.High),
            PolicyRuleType = ParseEnum(d.PolicyRuleType, Models.PolicyRuleType.HostDecision),
            Category = ParseEnum(d.Category, MessageCategory.General),
            ShouldAutoSend = d.ShouldAutoSend,
            RequiresHostReview = d.RequiresHostReview,
            ShouldNotifyHost = d.ShouldNotifyHost,
            EscalationReason = d.EscalationReason
        };

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) ? result : fallback;

    // ── Logging ───────────────────────────────────────────────────────────────

    private void LogResult(DraftResponseResult r)
    {
        _logger.LogInformation(
            "Response generated | Property={Property} | Risk={Risk} | Policy={Policy} | " +
            "Category={Category} | AutoSend={AutoSend} | HostNotified={Notify} | Confidence={Confidence}",
            r.PropertyName, r.RiskLevel, r.PolicyRuleType,
            r.Category, r.ShouldAutoSend, r.ShouldNotifyHost, r.Confidence);
    }
}
