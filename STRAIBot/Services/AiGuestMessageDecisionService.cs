using STRAIBot.Models;
using STRAIBot.Services.OpenAI;

namespace STRAIBot.Services;

/// <summary>
/// Local placeholder implementation of <see cref="IAiGuestMessageDecisionService"/>.
///
/// CURRENT BEHAVIOR:
/// Uses retrieved memory context and a lightweight keyword check to produce a
/// structured <see cref="AiGuestMessageDecision"/>. No external calls are made.
///
/// FUTURE BEHAVIOR (OpenAI integration):
/// Replace the body of <see cref="DecideAsync"/> with:
///   1. Build the system + user prompt using <see cref="BuildPrompt"/>.
///   2. Call OpenAI / Azure OpenAI chat completions API.
///   3. Deserialize the JSON response into <see cref="AiGuestMessageDecision"/>.
///   4. Return the deserialized decision (validator corrects any safety issues).
///
/// This is the ONLY place in the codebase that should ever call OpenAI.
/// </summary>
public class AiGuestMessageDecisionService : IAiGuestMessageDecisionService
{
    private readonly IOpenAiDecisionClient _openAi;
    private readonly ILogger<AiGuestMessageDecisionService> _logger;

    // ── OpenAI system prompt (ready for future integration) ──────────────────
    // When OpenAI is added, pass this as the system message.
    // The {propertyName}, {guestMessage}, and {retrievedContext} tokens
    // are replaced by BuildPrompt() before sending to the model.
    private const string SystemPrompt = """
        You are an AI assistant for a short-term rental guest communication system.
        Use only the provided property context and policies.
        Return only valid JSON matching the AiGuestMessageDecision schema.
        Do not invent amenities, exceptions, refunds, approvals, or policy changes.
        Never approve pets, parties, extra guests, early check-in, late checkout,
        refunds, cancellations, service animal/legal accommodation requests, or rule exceptions.
        If the message is about an emergency, safety, lockout, leak, flood, smoke, fire,
        gas smell, police, no heat, no AC, or immediate property issue, classify it as
        Emergency and require host notification.
        If the message mentions service animal, ADA, legal accommodation, lawsuit,
        discrimination, or similar legal language, require host review and host notification.
        For strict hard rules, generate a clear guest-facing response that politely
        states the policy.
        For informational questions, generate a concise helpful response.
        If confidence is low or the context does not clearly answer the question,
        require host review.
        """;

    private const string UserPromptTemplate = """
        PropertyName:
        {propertyName}

        GuestMessage:
        {guestMessage}

        RetrievedPropertyContext:
        {retrievedContext}

        Return JSON:
        {
          "propertyName": "...",
          "guestMessage": "...",
          "category": "...",
          "policyRuleType": "HardRule|Informational|HostDecision|Emergency",
          "riskLevel": "Low|Medium|High|Emergency",
          "confidence": "Low|Medium|High",
          "shouldAutoSend": true,
          "requiresHostReview": false,
          "shouldNotifyHost": false,
          "guestResponse": "...",
          "matchedPolicy": "...",
          "reasoningSummary": "...",
          "escalationReason": null
        }
        """;

    // ── Emergency keywords (safety net, also enforced by AiDecisionValidator) ─
    private static readonly string[] EmergencyTerms =
    [
        "fire", "smoke", "gas smell", "smell gas", "gas leak",
        "leak", "leaking", "flood", "flooding", "water coming",
        "police", "911", "unsafe", "not safe", "danger",
        "lockout", "locked out", "cannot get in", "can't get in",
        "no heat", "heat not working", "no ac", "ac not working",
        "air conditioning not working", "carbon monoxide", "emergency"
    ];

    // ── Service animal / legal escalation terms ───────────────────────────────
    private static readonly string[] LegalEscalationTerms =
    [
        "service animal", "service dog", "emotional support animal", "esa",
        "ada", "legal accommodation", "accommodation request",
        "lawsuit", "discrimination", "legal",
        "already brought", "already have my pet", "brought my dog", "brought my cat"
    ];

    // ── Hard rule triggers → firm policy response ─────────────────────────────
    private static readonly (string[] Keywords, string Category, string MatchedPolicy)[] HardRuleMappings =
    [
        (["pet", "pets", "dog", "dogs", "cat", "cats", "puppy", "bring my dog", "bring our dog",
          "bring a dog", "are pets allowed", "pet policy", "pet friendly"],
         "PetRequest", "Pet Policy"),

        (["smoke", "smoking", "vape", "vaping", "cigarette", "can i smoke", "smoke outside",
          "is smoking allowed"],
         "SmokingQuestion", "Smoking Policy"),

        (["late checkout", "late check-out", "check out late", "checkout at noon",
          "stay until noon", "stay later", "leave later", "extend checkout"],
         "LateCheckoutRequest", "Late Checkout Policy"),

        (["party", "parties", "birthday party", "event", "gathering", "host an event",
          "have people over", "16 people", "15 people", "20 people", "extra guests"],
         "PartyRisk", "Occupancy and Party Policy"),
    ];

    public AiGuestMessageDecisionService(
        IOpenAiDecisionClient openAi,
        ILogger<AiGuestMessageDecisionService> logger)
    {
        _openAi = openAi;
        _logger = logger;
    }

    public async Task<AiGuestMessageDecision> DecideAsync(
        string propertyName,
        string guestMessage,
        string retrievedContext,
        CancellationToken cancellationToken = default)
    {
        var userPrompt = BuildPrompt(propertyName, guestMessage, retrievedContext);

        // ── Try OpenAI first ─────────────────────────────────────────────────
        var decision = await _openAi.GetDecisionAsync(SystemPrompt, userPrompt, cancellationToken);

        if (decision is not null)
        {
            // Ensure propertyName and guestMessage are always set — the model
            // may omit them if context is short.
            decision.PropertyName = propertyName;
            decision.GuestMessage = guestMessage;
            return decision;
        }

        // ── Fallback to local placeholder ─────────────────────────────────────
        _logger.LogWarning(
            "OpenAI returned null for property {Property} — falling back to local placeholder.",
            propertyName);

        return BuildLocalDecision(propertyName, guestMessage, retrievedContext);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Local placeholder logic
    // Produces a structured decision without any external calls.
    // Intentionally minimal — the validator enforces all safety invariants.
    // ─────────────────────────────────────────────────────────────────────────

    private AiGuestMessageDecision BuildLocalDecision(
        string propertyName, string guestMessage, string retrievedContext)
    {
        var msg = guestMessage.ToLowerInvariant();

        // 1. Emergency — always highest priority
        if (ContainsAny(msg, EmergencyTerms) && !IsSmokingOnlyMessage(msg))
        {
            return new AiGuestMessageDecision
            {
                PropertyName = propertyName,
                GuestMessage = guestMessage,
                Category = "Emergency",
                PolicyRuleType = "Emergency",
                RiskLevel = "Emergency",
                Confidence = "High",
                ShouldAutoSend = true,
                RequiresHostReview = true,
                ShouldNotifyHost = true,
                GuestResponse =
                    "Thanks for letting us know. I've flagged this as urgent for the host team " +
                    "so they can review it right away. If there is an immediate safety concern, " +
                    "please contact local emergency services (911) first.",
                MatchedPolicy = "Emergency Protocol",
                ReasoningSummary = "Emergency keyword detected in guest message.",
                EscalationReason = "Emergency keyword detected. Immediate host notification required."
            };
        }

        // 2. Legal / service animal escalation — HostDecision regardless of pet rule
        if (ContainsAny(msg, LegalEscalationTerms))
        {
            return new AiGuestMessageDecision
            {
                PropertyName = propertyName,
                GuestMessage = guestMessage,
                Category = "ServiceAnimalRequest",
                PolicyRuleType = "HostDecision",
                RiskLevel = "High",
                Confidence = "High",
                ShouldAutoSend = true,
                RequiresHostReview = true,
                ShouldNotifyHost = true,
                GuestResponse =
                    "Thanks for reaching out. Requests of this nature need to be reviewed " +
                    "directly by the host. I've passed your message along and someone will " +
                    "follow up with you as soon as possible.",
                MatchedPolicy = "Service Animal / Legal Accommodation",
                ReasoningSummary = "Message contains service animal or legal accommodation language.",
                EscalationReason = "Service animal or legal accommodation request — host must review directly."
            };
        }

        // 3. Hard rule matches — use memory context to ground the response
        foreach (var (keywords, category, matchedPolicy) in HardRuleMappings)
        {
            if (!ContainsAny(msg, keywords)) continue;

            var policyLines = ExtractPolicyLines(retrievedContext);
            string guestResponse;
            string confidence;

            if (policyLines.Count > 0)
            {
                guestResponse = $"Hi there! Thanks for reaching out. {string.Join(" ", policyLines)} " +
                                "Please let us know if you have any other questions!";
                confidence = "High";
            }
            else
            {
                guestResponse = BuildHardRuleFallback(category);
                confidence = "Medium";
            }

            return new AiGuestMessageDecision
            {
                PropertyName = propertyName,
                GuestMessage = guestMessage,
                Category = category,
                PolicyRuleType = "HardRule",
                RiskLevel = category == "PartyRisk" ? "Medium" : "Low",
                Confidence = confidence,
                ShouldAutoSend = true,
                RequiresHostReview = false,
                ShouldNotifyHost = category == "PartyRisk",
                GuestResponse = guestResponse,
                MatchedPolicy = matchedPolicy,
                ReasoningSummary = $"Hard rule matched: {matchedPolicy}.",
                EscalationReason = category == "PartyRisk"
                    ? "Party or occupancy question — host notified as precaution."
                    : null
            };
        }

        // 4. Early check-in — HostDecision
        if (ContainsAny(msg, ["early check-in", "early checkin", "check in early",
                               "arrive early", "early arrival", "arrive before 4"]))
        {
            return new AiGuestMessageDecision
            {
                PropertyName = propertyName,
                GuestMessage = guestMessage,
                Category = "EarlyCheckInRequest",
                PolicyRuleType = "HostDecision",
                RiskLevel = "High",
                Confidence = "High",
                ShouldAutoSend = true,
                RequiresHostReview = true,
                ShouldNotifyHost = true,
                GuestResponse =
                    "Thanks for reaching out! Early check-in depends on availability and the " +
                    "cleaning schedule. I've flagged your request for the host to review and " +
                    "they'll get back to you as soon as possible.",
                MatchedPolicy = "Check-In Policy",
                ReasoningSummary = "Early check-in request requires host availability confirmation.",
                EscalationReason = "Early check-in request — host must confirm availability."
            };
        }

        // 5. Refund / cancellation / complaint / damage / maintenance — HostDecision
        if (ContainsAny(msg, ["refund", "cancel", "cancellation", "money back", "chargeback",
                               "complaint", "complain", "unacceptable", "damage", "damaged",
                               "broken", "maintenance", "repair", "not working"]))
        {
            var (cat, policy) = DetectHostDecisionCategory(msg);
            return new AiGuestMessageDecision
            {
                PropertyName = propertyName,
                GuestMessage = guestMessage,
                Category = cat,
                PolicyRuleType = "HostDecision",
                RiskLevel = "High",
                Confidence = "High",
                ShouldAutoSend = true,
                RequiresHostReview = true,
                ShouldNotifyHost = true,
                GuestResponse =
                    "Thanks for reaching out. This is something the host will need to review directly. " +
                    "I've flagged it for them and they'll get back to you as soon as possible.",
                MatchedPolicy = policy,
                ReasoningSummary = $"Host decision required: {cat}.",
                EscalationReason = $"{cat} requires host review."
            };
        }

        // 6. Informational — retrieve from memory and build a response
        return BuildInformationalDecision(propertyName, guestMessage, retrievedContext);
    }

    private AiGuestMessageDecision BuildInformationalDecision(
        string propertyName, string guestMessage, string retrievedContext)
    {
        var lines = ExtractContentLines(retrievedContext, maxLines: 5);
        string guestResponse;
        string confidence;

        if (lines.Count > 0)
        {
            var body = string.Join(" ", lines);
            guestResponse = $"Hi there! Thanks for reaching out. {body} " +
                            "Feel free to ask if you have any other questions — " +
                            "we're happy to help make your stay great!";
            confidence = lines.Count >= 3 ? "High" : "Medium";
        }
        else
        {
            guestResponse =
                "Hi there! Thanks for your question. " +
                "I want to double-check that for you and have the host confirm. " +
                "We'll get back to you as soon as possible!";
            confidence = "Low";
        }

        return new AiGuestMessageDecision
        {
            PropertyName = propertyName,
            GuestMessage = guestMessage,
            Category = "AmenityQuestion",
            PolicyRuleType = "Informational",
            RiskLevel = "Low",
            Confidence = confidence,
            ShouldAutoSend = confidence != "Low",
            RequiresHostReview = confidence == "Low",
            ShouldNotifyHost = false,
            GuestResponse = guestResponse,
            MatchedPolicy = null,
            ReasoningSummary = lines.Count > 0
                ? $"Informational response built from {lines.Count} memory line(s)."
                : "No relevant memory context found; deferred to host.",
            EscalationReason = confidence == "Low" ? "Insufficient context to answer confidently." : null
        };
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    /// <summary>Builds the user prompt string that will be sent to OpenAI when integrated.</summary>
    private static string BuildPrompt(string propertyName, string guestMessage, string retrievedContext) =>
        UserPromptTemplate
            .Replace("{propertyName}", propertyName)
            .Replace("{guestMessage}", guestMessage)
            .Replace("{retrievedContext}", retrievedContext);

    private static bool ContainsAny(string message, string[] terms) =>
        terms.Any(t => message.Contains(t, StringComparison.OrdinalIgnoreCase));

    // Smoking/vaping words overlap with emergency "smoke"; prevent false Emergency classification.
    private static bool IsSmokingOnlyMessage(string msg) =>
        (msg.Contains("smok") || msg.Contains("vap") || msg.Contains("cigarette") || msg.Contains("cigar"))
        && !msg.Contains("fire") && !msg.Contains("flood") && !msg.Contains("leak")
        && !msg.Contains("lockout") && !msg.Contains("police") && !msg.Contains("gas")
        && !msg.Contains("carbon monoxide") && !msg.Contains("emergency");

    private static readonly string[] MetadataLinePrefixes =
        ["#", "---", "[", "PolicyRuleType", "AutoRespond", "RequiresHostReview", "ShouldNotifyHost"];

    private static bool IsContentLine(string line) =>
        !string.IsNullOrWhiteSpace(line) &&
        !MetadataLinePrefixes.Any(p => line.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private static List<string> ExtractPolicyLines(string context, int maxLines = 4) =>
        ExtractContentLines(context, maxLines);

    private static List<string> ExtractContentLines(string context, int maxLines = 5)
    {
        if (string.IsNullOrWhiteSpace(context)) return [];
        return context
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(IsContentLine)
            .Take(maxLines)
            .Select(l => l.TrimStart('-', '*', ' ').Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }

    private static string BuildHardRuleFallback(string category) => category switch
    {
        "PetRequest" =>
            "Hi there! Unfortunately this property does not allow pets of any kind — no exceptions. " +
            "A $500 fine applies if evidence of a pet is found. We appreciate your understanding!",
        "SmokingQuestion" =>
            "Hi there! Smoking and vaping of any kind are not allowed indoors or anywhere on the premises. " +
            "A $500 fine applies for each violation. Thanks for understanding!",
        "LateCheckoutRequest" =>
            "Hi there! Late checkout is not available at this property. " +
            "Checkout is at 10:00 AM because the cleaning team arrives at that time. " +
            "A $200 fee applies for violations — this is not an approval to check out late.",
        "PartyRisk" =>
            "Hi there! Events, parties, and unauthorized gatherings are not permitted at this property. " +
            "Maximum occupancy must be observed at all times. A $500 fine applies for violations.",
        _ =>
            "Hi there! Thanks for your question. The host will follow up with you directly."
    };

    private static (string Category, string Policy) DetectHostDecisionCategory(string msg)
    {
        if (ContainsAny(msg, ["refund", "money back", "chargeback", "cancel", "cancellation"]))
            return ("RefundRequest", "Cancellation Policy");
        if (ContainsAny(msg, ["complaint", "complain", "unacceptable"]))
            return ("Complaint", "Guest Relations");
        if (ContainsAny(msg, ["damage", "damaged", "broken"]))
            return ("DamageIssue", "Damage Policy");
        return ("MaintenanceIssue", "Maintenance");
    }
}
