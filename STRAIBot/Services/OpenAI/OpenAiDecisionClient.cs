using System.ClientModel;
using System.Text.Json;
using global::OpenAI;
using global::OpenAI.Chat;
using STRAIBot.Models;

namespace STRAIBot.Services.OpenAI;

/// <summary>
/// Calls the OpenAI Chat Completions API using the official OpenAI .NET SDK v2.
///
/// Uses response_format: json_object to instruct GPT-4o to return pure JSON.
/// The JSON is deserialized directly into <see cref="AiGuestMessageDecision"/>.
///
/// Configured via:
///   OpenAI:ApiKey  — your API key (set in appsettings.Development.json, never committed)
///   OpenAI:Model   — model name (default: gpt-4o)
///
/// On any failure (network, parse, empty response) returns null so
/// AiGuestMessageDecisionService can fall back to the local placeholder.
/// </summary>
public class OpenAiDecisionClient : IOpenAiDecisionClient
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<OpenAiDecisionClient> _logger;
    private readonly string _model;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OpenAiDecisionClient(IConfiguration config, ILogger<OpenAiDecisionClient> logger)
    {
        _logger = logger;
        _model = config["OpenAI:Model"] ?? "gpt-4o";

        var apiKey = config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning(
                "OpenAI:ApiKey is not configured. " +
                "Set it in appsettings.Development.json. " +
                "AI decisions will fall back to local placeholder.");
        }

        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey ?? "not-configured"));
        _chatClient = openAiClient.GetChatClient(_model);
    }

    public async Task<AiGuestMessageDecision?> GetDecisionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_chatClient.ToString()))
        {
            _logger.LogWarning("OpenAI client not properly configured — skipping API call.");
            return null;
        }

        _logger.LogInformation("Calling OpenAI {Model} for guest message decision.", _model);

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
            var content = response.Value.Content[0].Text;

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("OpenAI returned empty content.");
                return null;
            }

            _logger.LogDebug("OpenAI raw response: {Content}",
                content.Length > 300 ? content[..300] + "…" : content);

            var decision = JsonSerializer.Deserialize<AiGuestMessageDecision>(content, _jsonOptions);

            if (decision is null)
            {
                _logger.LogWarning("OpenAI response deserialized to null.");
                return null;
            }

            _logger.LogInformation(
                "OpenAI decision received | Category={Category} | RiskLevel={RiskLevel} | " +
                "PolicyRuleType={PolicyRuleType} | Confidence={Confidence} | AutoSend={AutoSend}",
                decision.Category, decision.RiskLevel, decision.PolicyRuleType,
                decision.Confidence, decision.ShouldAutoSend);

            return decision;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI API call failed for model {Model}.", _model);
            return null;
        }
    }
}
