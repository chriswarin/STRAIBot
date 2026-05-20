using STRAIBot.Models;

namespace STRAIBot.Services;

/// <summary>
/// Produces an <see cref="AiGuestMessageDecision"/> for a given property, guest message,
/// and retrieved memory context.
///
/// Current implementation: local rule-based placeholder (no external calls).
/// Future implementation: sends a structured prompt to OpenAI / Azure OpenAI and
/// deserializes the JSON response into <see cref="AiGuestMessageDecision"/>.
///
/// This is the only service that should ever call OpenAI or Azure OpenAI.
/// </summary>
public interface IAiGuestMessageDecisionService
{
    /// <summary>
    /// Produce a guest message decision from property context.
    /// </summary>
    /// <param name="propertyName">The property identifier (e.g. "CozyCrab").</param>
    /// <param name="guestMessage">The raw guest message text.</param>
    /// <param name="retrievedContext">
    /// Property memory context retrieved from Markdown or GBrain.
    /// Will be included verbatim in the OpenAI prompt when integrated.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AiGuestMessageDecision> DecideAsync(
        string propertyName,
        string guestMessage,
        string retrievedContext,
        CancellationToken cancellationToken = default);
}
