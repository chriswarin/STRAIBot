using STRAIBot.Models;

namespace STRAIBot.Services;

public class MessageRiskClassifier : IMessageRiskClassifier
{
    // ── Emergency ────────────────────────────────────────────────────────────
    // Structural/safety issues that need immediate host notification.
    private static readonly string[] EmergencyKeywords =
    [
        "lockout", "locked out", "can't get in", "cannot get in", "lock doesn't work",
        "flood", "flooding",
        "leak", "leaking", "water coming", "water coming from", "water dripping",
        "no heat", "heat not working", "heater not working", "heat is out", "heat stopped",
        "no ac", "ac not working", "air conditioning not working", "ac is out", "no air conditioning",
        "ac stopped", "not cooling", "air conditioner broke",
        "smoke", "fire", "flames",
        "gas smell", "smell gas", "gas leak",
        "police", "911",
        "unsafe", "not safe", "safety concern", "danger",
        "emergency", "urgent help",
        "carbon monoxide", "co alarm", "co detector"
    ];

    // ── Service animal — HostDecision (escalate regardless of pet hard rule) ─
    private static readonly string[] ServiceAnimalKeywords =
    [
        "service animal", "service dog", "emotional support animal", "esa",
        "ada", "disability accommodation", "accommodation request",
        "already brought", "already have my pet", "already have my dog", "already have my cat",
        "brought my dog", "brought my cat", "my pet is here",
        "legal", "lawsuit", "discrimination", "complaint about pet policy"
    ];

    // ── Normal pet keywords (HardRule — auto-respond, no host escalation) ───
    // Broad coverage: bare animal words + phrased questions.
    // ServiceAnimalKeywords is checked FIRST so escalation always wins.
    private static readonly string[] PetHardRuleKeywords =
    [
        "pet", "pets", "dog", "dogs", "cat", "cats", "puppy", "puppies",
        "kitten", "kittens", "bring an animal", "with my animal", "with our animal",
        "bring my dog", "bring our dog", "bring my cat", "bring our cat",
        "bring my pet", "bring our pet", "bring a dog", "bring a cat",
        "can i bring", "can we bring",
        "are pets allowed", "is the property pet friendly", "pet friendly",
        "do you allow pets", "do you allow dogs", "do you allow cats",
        "pet policy", "dog policy",
        "traveling with a dog", "traveling with a pet",
        "coming with my dog", "coming with our dog"
    ];

    // ── Late checkout — HardRule auto-respond (firm no, no host review) ─────
    private static readonly string[] LateCheckoutKeywords =
    [
        "late checkout", "late check-out", "check out late", "late check out",
        "checkout at noon", "checkout at 11", "checkout at 12", "check out at noon",
        "check out at 11", "check out at 12", "stay until noon", "stay until 11",
        "stay a little later", "stay later", "leave later", "extended checkout",
        "extend our checkout", "extend checkout"
    ];

    // ── Early check-in — HostDecision (needs host review) ───────────────────
    private static readonly string[] EarlyCheckInKeywords =
    [
        "early check-in", "early checkin", "check in early", "arrive early",
        "check in before", "get in before", "early arrival", "arrive before 4",
        "arrive before check-in"
    ];

    // ── Smoking — HardRule auto-respond ─────────────────────────────────────
    private static readonly string[] SmokingKeywords =
    [
        "smoke", "smoking", "vape", "vaping", "cigarette", "cigar",
        "can i smoke", "can we smoke", "smoke outside", "smoke on the deck",
        "smoke on the porch", "smoking policy", "is smoking allowed"
    ];

    // ── Parties / gatherings / occupancy — HardRule auto-respond ────────────
    private static readonly string[] PartyKeywords =
    [
        "party", "parties", "event", "birthday party", "gathering",
        "host a", "host an event", "have people over", "invite people",
        "extra people", "extra guests", "more than", "16 people", "15 people",
        "20 people", "over the limit", "additional guests",
        "unauthorized gathering"
    ];

    // ── Refund / cancellation — HostDecision ────────────────────────────────
    private static readonly string[] RefundKeywords =
    [
        "refund", "refunds", "money back", "charge back", "chargeback",
        "dispute", "overcharged", "cancel", "cancellation", "cancelling"
    ];

    // ── Complaint — HostDecision ─────────────────────────────────────────────
    private static readonly string[] ComplaintKeywords =
    [
        "complaint", "complain", "not acceptable", "unacceptable",
        "terrible", "disgusting", "worst", "filing a complaint", "report you"
    ];

    // ── Damage — HostDecision ────────────────────────────────────────────────
    private static readonly string[] DamageKeywords =
    [
        "damage", "damages", "damaged", "something broke", "broke something",
        "broke the", "cracked", "stained", "torn", "shattered"
    ];

    // ── Maintenance (non-emergency) — HostDecision ──────────────────────────
    private static readonly string[] MaintenanceKeywords =
    [
        "not working", "broken down", "repair", "maintenance",
        "broken", "doesn't work", "won't turn on", "stopped working",
        "out of order", "needs fixing"
    ];

    // ── Low-risk informational mappings ──────────────────────────────────────
    private static readonly (string[] Keywords, MessageCategory Category)[] InformationalMappings =
    [
        (["bike", "bikes", "bicycle", "beach towel", "beach towels", "beach chair", "beach equipment", "umbrella", "boogie board", "kayak", "fishing", "fish from"], MessageCategory.AmenityQuestion),
        (["parking", "park my car", "how many cars", "car limit", "driveway", "ev charger", "electric vehicle"], MessageCategory.ParkingQuestion),
        (["wifi", "wi-fi", "internet", "password", "wireless", "network"], MessageCategory.WiFiQuestion),
        (["pool", "swimming", "swim", "saltwater pool", "pool heated", "pool open", "pool season"], MessageCategory.PoolQuestion),
        (["hot tub", "jacuzzi", "spa", "hot tub heated", "hot tub open"], MessageCategory.AmenityQuestion),
        (["coffee", "keurig", "drip coffee", "french press"], MessageCategory.AmenityQuestion),
        (["kitchen", "cookware", "dishes", "utensils", "stove", "microwave", "refrigerator", "fridge", "oven"], MessageCategory.AmenityQuestion),
        (["amenity", "amenities", "what is included", "what's included", "what do you have"], MessageCategory.AmenityQuestion),
        (["check-in time", "checkin time", "when can i arrive", "what time is check-in", "check in time", "arrival time"], MessageCategory.AmenityQuestion),
        (["ski", "skiing", "ski-in", "ski out", "slopes", "lifts", "ski resort", "mountain", "massanutten"], MessageCategory.AmenityQuestion),
        (["beach", "ocean", "sand", "water access", "beach access"], MessageCategory.AmenityQuestion),
        (["view", "ocean view", "oceanfront", "waterfront", "canal view"], MessageCategory.AmenityQuestion),
        (["quiet hours", "noise", "quiet time", "noise policy"], MessageCategory.AmenityQuestion),
        (["grill", "bbq", "barbecue", "outdoor grill"], MessageCategory.AmenityQuestion),
        (["checkout time", "what time is checkout", "when do i need to leave", "when is checkout"], MessageCategory.AmenityQuestion),
        (["fireplace", "fire pit"], MessageCategory.AmenityQuestion),
        (["ev charger", "electric car", "tesla charger", "electric vehicle charger"], MessageCategory.ParkingQuestion),
    ];

    public MessageRiskAnalysis Classify(string guestMessage)
    {
        var msg = guestMessage.ToLowerInvariant();

        // 1. Emergency — highest priority
        if (MatchesAny(msg, EmergencyKeywords) && !IsSmokingOnly(msg))
        {
            return new MessageRiskAnalysis
            {
                RiskLevel = RiskLevel.Emergency,
                PolicyRuleType = PolicyRuleType.Emergency,
                Category = MessageCategory.Emergency,
                ShouldAutoSend = true,
                RequiresHostReview = true,
                ShouldNotifyHost = true,
                EscalationReason = "Emergency keyword detected. Immediate host notification required."
            };
        }

        // 2. Service animal / ADA / already brought animal — escalate regardless of pet hard rule
        if (MatchesAny(msg, ServiceAnimalKeywords))
        {
            return new MessageRiskAnalysis
            {
                RiskLevel = RiskLevel.High,
                PolicyRuleType = PolicyRuleType.HostDecision,
                Category = MessageCategory.ServiceAnimalRequest,
                ShouldAutoSend = true,
                RequiresHostReview = true,
                ShouldNotifyHost = true,
                EscalationReason = "Service animal or legal accommodation request — host must review directly."
            };
        }

        // 3. Normal pet request — HardRule, firm auto-response, no escalation
        if (MatchesAny(msg, PetHardRuleKeywords))
        {
            return new MessageRiskAnalysis
            {
                RiskLevel = RiskLevel.Low,
                PolicyRuleType = PolicyRuleType.HardRule,
                Category = MessageCategory.PetRequest,
                ShouldAutoSend = true,
                RequiresHostReview = false,
                ShouldNotifyHost = false,
                EscalationReason = null
            };
        }

        // 4. Smoking — HardRule auto-respond
        if (MatchesAny(msg, SmokingKeywords))
        {
            return new MessageRiskAnalysis
            {
                RiskLevel = RiskLevel.Low,
                PolicyRuleType = PolicyRuleType.HardRule,
                Category = MessageCategory.SmokingQuestion,
                ShouldAutoSend = true,
                RequiresHostReview = false,
                ShouldNotifyHost = false,
                EscalationReason = null
            };
        }

        // 5. Late checkout — HardRule, firm no
        if (MatchesAny(msg, LateCheckoutKeywords))
        {
            return new MessageRiskAnalysis
            {
                RiskLevel = RiskLevel.Low,
                PolicyRuleType = PolicyRuleType.HardRule,
                Category = MessageCategory.LateCheckoutRequest,
                ShouldAutoSend = true,
                RequiresHostReview = false,
                ShouldNotifyHost = false,
                EscalationReason = null
            };
        }

        // 6. Early check-in — HostDecision, notify host
        if (MatchesAny(msg, EarlyCheckInKeywords))
        {
            return new MessageRiskAnalysis
            {
                RiskLevel = RiskLevel.High,
                PolicyRuleType = PolicyRuleType.HostDecision,
                Category = MessageCategory.EarlyCheckInRequest,
                ShouldAutoSend = true,
                RequiresHostReview = true,
                ShouldNotifyHost = true,
                EscalationReason = "Early check-in request requires host review."
            };
        }

        // 7. Parties / occupancy — HardRule, firm no
        if (MatchesAny(msg, PartyKeywords))
        {
            return new MessageRiskAnalysis
            {
                RiskLevel = RiskLevel.Medium,
                PolicyRuleType = PolicyRuleType.HardRule,
                Category = MessageCategory.PartyRisk,
                ShouldAutoSend = true,
                RequiresHostReview = false,
                ShouldNotifyHost = true,
                EscalationReason = "Party or occupancy limit question — hard rule violation risk."
            };
        }

        // 8. Refund / cancellation — HostDecision
        if (MatchesAny(msg, RefundKeywords))
        {
            return new MessageRiskAnalysis
            {
                RiskLevel = RiskLevel.High,
                PolicyRuleType = PolicyRuleType.HostDecision,
                Category = MessageCategory.RefundRequest,
                ShouldAutoSend = true,
                RequiresHostReview = true,
                ShouldNotifyHost = true,
                EscalationReason = "Refund or cancellation request requires host review."
            };
        }

        // 9. Complaint — HostDecision
        if (MatchesAny(msg, ComplaintKeywords))
        {
            return new MessageRiskAnalysis
            {
                RiskLevel = RiskLevel.High,
                PolicyRuleType = PolicyRuleType.HostDecision,
                Category = MessageCategory.Complaint,
                ShouldAutoSend = true,
                RequiresHostReview = true,
                ShouldNotifyHost = true,
                EscalationReason = "Guest complaint requires host review."
            };
        }

        // 10. Damage — HostDecision
        if (MatchesAny(msg, DamageKeywords))
        {
            return new MessageRiskAnalysis
            {
                RiskLevel = RiskLevel.High,
                PolicyRuleType = PolicyRuleType.HostDecision,
                Category = MessageCategory.DamageIssue,
                ShouldAutoSend = true,
                RequiresHostReview = true,
                ShouldNotifyHost = true,
                EscalationReason = "Damage report requires host review."
            };
        }

        // 11. Maintenance (non-emergency) — HostDecision
        if (MatchesAny(msg, MaintenanceKeywords))
        {
            return new MessageRiskAnalysis
            {
                RiskLevel = RiskLevel.High,
                PolicyRuleType = PolicyRuleType.HostDecision,
                Category = MessageCategory.MaintenanceIssue,
                ShouldAutoSend = true,
                RequiresHostReview = true,
                ShouldNotifyHost = true,
                EscalationReason = "Maintenance issue requires host review."
            };
        }

        // 12. Low-risk informational
        var category = DetectInformationalCategory(msg);
        return new MessageRiskAnalysis
        {
            RiskLevel = RiskLevel.Low,
            PolicyRuleType = PolicyRuleType.Informational,
            Category = category,
            ShouldAutoSend = true,
            RequiresHostReview = false,
            ShouldNotifyHost = false,
            EscalationReason = null
        };
    }

    // Smoking keywords overlap with emergency "smoke" — guard so "smoke outside?"
    // doesn't trigger Emergency when only smoking-related terms are present.
    private static bool IsSmokingOnly(string message) =>
        (message.Contains("smok") || message.Contains("vap") || message.Contains("cigarette") || message.Contains("cigar"))
        && !message.Contains("fire") && !message.Contains("flood") && !message.Contains("leak")
        && !message.Contains("lockout") && !message.Contains("police") && !message.Contains("gas")
        && !message.Contains("carbon monoxide") && !message.Contains("emergency");

    private static MessageCategory DetectInformationalCategory(string message)
    {
        foreach (var (keywords, category) in InformationalMappings)
        {
            if (keywords.Any(k => message.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return category;
        }
        return MessageCategory.General;
    }

    private static bool MatchesAny(string message, string[] keywords) =>
        keywords.Any(k => message.Contains(k, StringComparison.OrdinalIgnoreCase));
}

