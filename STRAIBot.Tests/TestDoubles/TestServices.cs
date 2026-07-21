using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using STRAIBot.Models;
using STRAIBot.Services;
using STRAIBot.Services.GBrain;
using STRAIBot.Services.OpenAI;
using STRAIBot.Services.Properties;

namespace STRAIBot.Tests.TestDoubles;

internal static class TestServices
{
    public static ILogger<T> Logger<T>() => NullLogger<T>.Instance;

    public static IConfiguration Config(params (string Key, string? Value)[] entries)
    {
        var dict = entries.ToDictionary(x => x.Key, x => x.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }
}

internal sealed class FakeOpenAiDecisionClient : IOpenAiDecisionClient
{
    public Func<string, string, CancellationToken, Task<AiGuestMessageDecision?>> Handler { get; set; } =
        (_, _, _) => Task.FromResult<AiGuestMessageDecision?>(null);

    public Task<AiGuestMessageDecision?> GetDecisionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) =>
        Handler(systemPrompt, userPrompt, cancellationToken);
}

internal sealed class FakeHostNotificationService : IHostNotificationService
{
    public List<(string PropertyName, string GuestMessage, MessageRiskAnalysis Risk)> Notifications { get; } = [];

    public Task NotifyAsync(string propertyName, string guestMessage, MessageRiskAnalysis risk)
    {
        Notifications.Add((propertyName, guestMessage, risk));
        return Task.CompletedTask;
    }
}

internal sealed class FakePropertyMemoryService : IPropertyMemoryService
{
    public Func<string, bool> PropertyExistsHandler { get; set; } = _ => true;
    public Func<string, string, Task<string>> ContextHandler { get; set; } = (_, _) => Task.FromResult(string.Empty);

    public bool PropertyExists(string propertyName) => PropertyExistsHandler(propertyName);

    public Task<string> GetRelevantContextAsync(string propertyName, string guestMessage) =>
        ContextHandler(propertyName, guestMessage);
}

internal sealed class FakeGBrainClient : IGBrainClient
{
    public List<(string PropertyKey, string MemoryKey, string GuestMessage)> SearchCalls { get; } = [];
    public string SearchResult { get; set; } = string.Empty;
    public bool Healthy { get; set; } = true;

    public Task<string> SearchAsync(string propertyKey, string gBrainMemoryKey, string guestMessage, CancellationToken cancellationToken = default)
    {
        SearchCalls.Add((propertyKey, gBrainMemoryKey, guestMessage));
        return Task.FromResult(SearchResult);
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(Healthy);
}

internal sealed class FakePropertyMappingService : IPropertyMappingService
{
    public List<PropertyMapping> Mappings { get; } = [];

    public PropertyMapping? GetByPropertyKey(string propertyKey) =>
        Mappings.FirstOrDefault(m => string.Equals(m.PropertyKey, propertyKey, StringComparison.OrdinalIgnoreCase));

    public PropertyMapping? GetByGBrainMemoryKey(string gBrainMemoryKey) =>
        Mappings.FirstOrDefault(m => string.Equals(m.GBrainMemoryKey, gBrainMemoryKey, StringComparison.OrdinalIgnoreCase));

    public PropertyMapping? GetByGuestyListingId(string guestyListingId) =>
        Mappings.FirstOrDefault(m => string.Equals(m.ExternalIds.GuestyListingId, guestyListingId, StringComparison.OrdinalIgnoreCase));

    public PropertyMapping? GetByAirbnbListingId(string airbnbListingId) =>
        Mappings.FirstOrDefault(m => string.Equals(m.ExternalIds.AirbnbListingId, airbnbListingId, StringComparison.OrdinalIgnoreCase));

    public PropertyMapping? GetByVrboListingId(string vrboListingId) =>
        Mappings.FirstOrDefault(m => string.Equals(m.ExternalIds.VrboListingId, vrboListingId, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<PropertyMapping> GetActiveMappings() => Mappings.Where(m => m.IsActive).ToList();
}

internal sealed class FakeWebHostEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "STRAIBot.Tests";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public string EnvironmentName { get; set; } = "Development";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = AppContext.BaseDirectory;
}
