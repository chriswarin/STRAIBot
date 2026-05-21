using STRAIBot.Models;

namespace STRAIBot.Services.OpenAI;

/// <summary>
/// Sends structured prompts to OpenAI and deserializes the response into
/// an <see cref="AiGuestMessageDecision"/>.
///
/// This is the single place in the codebase that calls the OpenAI API.
/// Configured via OpenAI:ApiKey and OpenAI:Model in appsettings.json.
/// </summary>
public interface IOpenAiDecisionClient
{
    /// <summary>
    /// Sends the system prompt + context prompt to OpenAI and returns a
    /// parsed <see cref="AiGuestMessageDecision"/>.
    ///
    /// Returns null if the model response cannot be parsed — callers should
    /// fall back to the local placeholder in that case.
    /// </summary>
    Task<AiGuestMessageDecision?> GetDecisionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}
