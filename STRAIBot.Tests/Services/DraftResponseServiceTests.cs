using STRAIBot.Models;
using STRAIBot.Services;
using STRAIBot.Services.Memory;
using STRAIBot.Tests.TestDoubles;

namespace STRAIBot.Tests.Services;

public class DraftResponseServiceTests
{
    [Fact]
    public async Task GenerateDraftAsync_WhenDecisionNotifiesHost_CallsNotificationAndMapsEnums()
    {
        var memory = new FakeMemoryContextService
        {
            PropertyExistsResult = true,
            ContextResult = "Policy line"
        };

        var decisionService = new FakeAiGuestMessageDecisionService
        {
            Decision = new AiGuestMessageDecision
            {
                PropertyName = "CozyCrab",
                GuestMessage = "Help",
                Category = "Emergency",
                PolicyRuleType = "Emergency",
                RiskLevel = "Emergency",
                Confidence = "High",
                ShouldAutoSend = true,
                RequiresHostReview = true,
                ShouldNotifyHost = true,
                GuestResponse = "Emergency response",
                EscalationReason = "Emergency"
            }
        };

        var validator = new PassThroughValidator();
        var notifications = new FakeHostNotificationService();

        var sut = new DraftResponseService(
            memory,
            decisionService,
            validator,
            notifications,
            TestServices.Logger<DraftResponseService>());

        var result = await sut.GenerateDraftAsync("CozyCrab", "There is a fire");

        Assert.Single(notifications.Notifications);
        Assert.Equal(RiskLevel.Emergency, result.RiskLevel);
        Assert.Equal(PolicyRuleType.Emergency, result.PolicyRuleType);
        Assert.Equal(MessageCategory.Emergency, result.Category);
        Assert.Equal(ConfidenceLevel.High, result.Confidence);
        Assert.Equal("Policy line", result.RetrievedContext);
    }

    [Fact]
    public async Task GenerateDraftAsync_WhenPropertyDoesNotExist_DoesNotRetrieveContext()
    {
        var memory = new FakeMemoryContextService
        {
            PropertyExistsResult = false,
            ContextResult = "should not be used"
        };

        var decisionService = new FakeAiGuestMessageDecisionService
        {
            Decision = new AiGuestMessageDecision
            {
                PropertyName = "Unknown",
                GuestMessage = "Hello",
                Category = "General",
                PolicyRuleType = "Informational",
                RiskLevel = "Low",
                Confidence = "Medium",
                ShouldAutoSend = true,
                RequiresHostReview = false,
                ShouldNotifyHost = false,
                GuestResponse = "Hello"
            }
        };

        var sut = new DraftResponseService(
            memory,
            decisionService,
            new PassThroughValidator(),
            new FakeHostNotificationService(),
            TestServices.Logger<DraftResponseService>());

        var result = await sut.GenerateDraftAsync("Unknown", "Hello");

        Assert.Equal(0, memory.GetContextCallCount);
        Assert.Equal(string.Empty, result.RetrievedContext);
    }

    [Fact]
    public async Task GenerateDraftAsync_WhenDecisionHasInvalidEnumStrings_UsesFallbackEnums()
    {
        var memory = new FakeMemoryContextService
        {
            PropertyExistsResult = true,
            ContextResult = "context"
        };

        var decisionService = new FakeAiGuestMessageDecisionService
        {
            Decision = new AiGuestMessageDecision
            {
                PropertyName = "CozyCrab",
                GuestMessage = "Question",
                Category = "NotARealCategory",
                PolicyRuleType = "Nope",
                RiskLevel = "Nope",
                Confidence = "Nope",
                ShouldAutoSend = true,
                RequiresHostReview = false,
                ShouldNotifyHost = false,
                GuestResponse = "Response"
            }
        };

        var sut = new DraftResponseService(
            memory,
            decisionService,
            new PassThroughValidator(),
            new FakeHostNotificationService(),
            TestServices.Logger<DraftResponseService>());

        var result = await sut.GenerateDraftAsync("CozyCrab", "Question");

        Assert.Equal(RiskLevel.Low, result.RiskLevel);
        Assert.Equal(PolicyRuleType.Informational, result.PolicyRuleType);
        Assert.Equal(MessageCategory.General, result.Category);
        Assert.Equal(ConfidenceLevel.Low, result.Confidence);
    }

    private sealed class FakeMemoryContextService : IMemoryContextService
    {
        public bool PropertyExistsResult { get; set; }
        public string ContextResult { get; set; } = string.Empty;
        public int GetContextCallCount { get; private set; }

        public bool PropertyExists(string propertyKey) => PropertyExistsResult;

        public Task<string> GetContextAsync(string propertyKey, string guestMessage, CancellationToken cancellationToken = default)
        {
            GetContextCallCount++;
            return Task.FromResult(ContextResult);
        }
    }

    private sealed class FakeAiGuestMessageDecisionService : IAiGuestMessageDecisionService
    {
        public AiGuestMessageDecision Decision { get; set; } = new();

        public Task<AiGuestMessageDecision> DecideAsync(string propertyName, string guestMessage, string retrievedContext, CancellationToken cancellationToken = default)
            => Task.FromResult(Decision);
    }

    private sealed class PassThroughValidator : IAiDecisionValidator
    {
        public AiGuestMessageDecision ValidateAndCorrect(AiGuestMessageDecision decision, string propertyName, string guestMessage, string retrievedContext)
            => decision;
    }
}
