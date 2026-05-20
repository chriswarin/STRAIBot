using Microsoft.AspNetCore.Mvc;
using STRAIBot.Services.GBrain;
using STRAIBot.Services.Memory;
using STRAIBot.Services.Properties;

namespace STRAIBot.Controllers;

/// <summary>
/// Diagnostic endpoint for testing GBrain memory retrieval locally via Scalar.
///
/// GET /api/memory/test?propertyKey=BlueHorizon&message=late%20checkout%20cleaners%2010am
///
/// Use this to verify:
///   - Which memory provider is active (GBrain vs Markdown)
///   - What context GBrain retrieves for a given property + message
///   - Whether fallback to Markdown occurred
///   - That GBrainMemoryKey scoping is working correctly
///
/// Test cases that should return exact granular policy files:
///   propertyKey=BlueHorizon  + message=late checkout cleaners 10am
///     → expected top chunk from: bluehorizon/policies/late-checkout-policy
///
///   propertyKey=CozyCrab     + message=no pets fine
///     → expected top chunk from: cozycrab/policies/pet-policy
///
///   propertyKey=TurquoiseBay + message=pool heated April October
///     → expected top chunk from: turquoisebay/policies/pool-policy
///
///   propertyKey=TurquoiseBay + message=16 people party
///     → expected top chunk from: turquoisebay/policies/party-occupancy-policy
/// </summary>
[ApiController]
[Route("api/memory")]
public class MemoryTestController : ControllerBase
{
    private readonly IMemoryContextService _memory;
    private readonly IPropertyMappingService _mappings;
    private readonly IGBrainClient _gBrain;
    private readonly IConfiguration _config;
    private readonly ILogger<MemoryTestController> _logger;

    public MemoryTestController(
        IMemoryContextService memory,
        IPropertyMappingService mappings,
        IGBrainClient gBrain,
        IConfiguration config,
        ILogger<MemoryTestController> logger)
    {
        _memory = memory;
        _mappings = mappings;
        _gBrain = gBrain;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Tests memory retrieval for a given property and message.
    /// Returns retrieved context, active provider, and whether GBrain fallback occurred.
    /// </summary>
    /// <param name="propertyKey">STRAIBot internal property key (e.g. "BlueHorizon")</param>
    /// <param name="message">Guest message to use as the retrieval query</param>
    [HttpGet("test")]
    public async Task<IActionResult> Test(
        [FromQuery] string propertyKey,
        [FromQuery] string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(propertyKey))
            return BadRequest(new { error = "propertyKey is required" });

        if (string.IsNullOrWhiteSpace(message))
            return BadRequest(new { error = "message is required" });

        var provider = _config["Memory:Provider"] ?? "Markdown";
        var fallbackEnabled = _config.GetValue<bool>("Memory:FallbackToMarkdown", true);
        var mapping = _mappings.GetByPropertyKey(propertyKey);
        var gBrainMemoryKey = mapping?.GBrainMemoryKey ?? "(no mapping found)";

        // Check GBrain health if provider is GBrain
        bool? gBrainHealthy = null;
        if (string.Equals(provider, "GBrain", StringComparison.OrdinalIgnoreCase))
            gBrainHealthy = await _gBrain.IsHealthyAsync(cancellationToken);

        _logger.LogInformation(
            "Memory test | Property={PropertyKey} | Provider={Provider} | " +
            "GBrainMemoryKey={GBrainMemoryKey} | Message={Message}",
            propertyKey, provider, gBrainMemoryKey, message);

        // Run retrieval — MemoryContextService handles GBrain → Markdown fallback
        var retrievedContext = await _memory.GetContextAsync(propertyKey, message, cancellationToken);

        // Determine if fallback occurred by checking whether GBrain was tried and
        // whether the result came from Markdown (heuristic: GBrain healthy but context
        // came back — we log it; if healthy=false and context not empty, fallback happened)
        var fallbackUsed = string.Equals(provider, "GBrain", StringComparison.OrdinalIgnoreCase)
                           && gBrainHealthy == false
                           && !string.IsNullOrWhiteSpace(retrievedContext);

        return Ok(new
        {
            provider,
            propertyKey,
            gBrainMemoryKey,
            gBrainHealthy,
            fallbackToMarkdownEnabled = fallbackEnabled,
            fallbackUsed,
            message,
            contextLength = retrievedContext.Length,
            retrievedContext = string.IsNullOrWhiteSpace(retrievedContext)
                ? "(no context retrieved)"
                : retrievedContext
        });
    }
}
