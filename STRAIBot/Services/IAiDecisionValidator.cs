using STRAIBot.Models;

namespace STRAIBot.Services;

/// <summary>
/// Safety guardrail that validates and corrects an <see cref="AiGuestMessageDecision"/>
/// before it is used to send a guest response or notify the host.
///
/// This layer exists to catch any mistakes from the AI (or local placeholder) that
/// would violate hard business rules — e.g. approving pets, missing emergency flags,
/// or sending a response without any text.
/// </summary>
public interface IAiDecisionValidator
{
    /// <summary>
    /// Validates and corrects the decision in-place.
    /// Returns the corrected decision (same instance, mutated).
    /// </summary>
    AiGuestMessageDecision ValidateAndCorrect(
        AiGuestMessageDecision decision,
        string propertyName,
        string guestMessage,
        string retrievedContext);
}
