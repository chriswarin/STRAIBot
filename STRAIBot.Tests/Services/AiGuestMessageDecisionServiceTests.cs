using STRAIBot.Services;
using STRAIBot.Tests.TestDoubles;

namespace STRAIBot.Tests.Services;

public class AiGuestMessageDecisionServiceTests
{
    [Fact]
    public async Task DecideAsync_WhenOpenAiReturnsDecision_UsesItAndSetsPropertyAndMessage()
    {
        var openAi = new FakeOpenAiDecisionClient
        {
            Handler = (_, _, _) => Task.FromResult<STRAIBot.Models.AiGuestMessageDecision?>(new()
            {
                Category = "AmenityQuestion",
                PolicyRuleType = "Informational",
                RiskLevel = "Low",
                Confidence = "High",
                ShouldAutoSend = true,
                GuestResponse = "OpenAI response"
            })
        };

        var sut = new AiGuestMessageDecisionService(openAi, TestServices.Logger<AiGuestMessageDecisionService>());

        var result = await sut.DecideAsync("CozyCrab", "Do you have a pool?", "pool context", TestContext.Current.CancellationToken);

        Assert.Equal("CozyCrab", result.PropertyName);
        Assert.Equal("Do you have a pool?", result.GuestMessage);
        Assert.Equal("OpenAI response", result.GuestResponse);
        Assert.Equal("Informational", result.PolicyRuleType);
    }

    [Fact]
    public async Task DecideAsync_WhenOpenAiReturnsNull_EmergencyMessageReturnsEmergencyDecision()
    {
        var openAi = new FakeOpenAiDecisionClient();
        var sut = new AiGuestMessageDecisionService(openAi, TestServices.Logger<AiGuestMessageDecisionService>());

        var result = await sut.DecideAsync("CozyCrab", "There is a gas leak in the kitchen", "", TestContext.Current.CancellationToken);

        Assert.Equal("Emergency", result.Category);
        Assert.Equal("Emergency", result.PolicyRuleType);
        Assert.Equal("Emergency", result.RiskLevel);
        Assert.True(result.RequiresHostReview);
        Assert.True(result.ShouldNotifyHost);
    }

    [Fact]
    public async Task DecideAsync_WhenOpenAiReturnsNull_SmokingMessageIsHardRuleNotEmergency()
    {
        var openAi = new FakeOpenAiDecisionClient();
        var sut = new AiGuestMessageDecisionService(openAi, TestServices.Logger<AiGuestMessageDecisionService>());

        var result = await sut.DecideAsync("CozyCrab", "Can I smoke outside?", "", TestContext.Current.CancellationToken);

        Assert.Equal("SmokingQuestion", result.Category);
        Assert.Equal("HardRule", result.PolicyRuleType);
        Assert.Equal("Low", result.RiskLevel);
    }

    [Fact]
    public async Task DecideAsync_WhenHardRuleAndContextPresent_UsesContextLinesWithHighConfidence()
    {
        var openAi = new FakeOpenAiDecisionClient();
        var sut = new AiGuestMessageDecisionService(openAi, TestServices.Logger<AiGuestMessageDecisionService>());
        var context = "# title\n- Pets are not allowed.\n- A fine may apply.";

        var result = await sut.DecideAsync("CozyCrab", "Are pets allowed?", context, TestContext.Current.CancellationToken);

        Assert.Equal("PetRequest", result.Category);
        Assert.Equal("HardRule", result.PolicyRuleType);
        Assert.Equal("High", result.Confidence);
        Assert.Contains("Pets are not allowed", result.GuestResponse, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DecideAsync_WhenInformationalWithoutContext_RequiresHostReview()
    {
        var openAi = new FakeOpenAiDecisionClient();
        var sut = new AiGuestMessageDecisionService(openAi, TestServices.Logger<AiGuestMessageDecisionService>());

        var result = await sut.DecideAsync("CozyCrab", "Do you have board games?", "", TestContext.Current.CancellationToken);

        Assert.Equal("Informational", result.PolicyRuleType);
        Assert.Equal("Low", result.Confidence);
        Assert.False(result.ShouldAutoSend);
        Assert.True(result.RequiresHostReview);
        Assert.Contains("double-check", result.GuestResponse, StringComparison.OrdinalIgnoreCase);
    }
}
