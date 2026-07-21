using STRAIBot.Models;
using STRAIBot.Services;
using STRAIBot.Tests.TestDoubles;

namespace STRAIBot.Tests.Services;

public class AiDecisionValidatorTests
{
    private static AiDecisionValidator CreateSut() =>
        new(new FakePropertyMemoryService(), TestServices.Logger<AiDecisionValidator>());

    private static AiGuestMessageDecision BaselineDecision() =>
        new()
        {
            PropertyName = "CozyCrab",
            GuestMessage = "hello",
            Category = "AmenityQuestion",
            PolicyRuleType = "Informational",
            RiskLevel = "Low",
            Confidence = "High",
            ShouldAutoSend = true,
            RequiresHostReview = false,
            ShouldNotifyHost = false,
            GuestResponse = "Thanks for your message."
        };

    [Fact]
    public void ValidateAndCorrect_MissingProperty_BlocksAutoSendAndForcesHostReview()
    {
        var sut = CreateSut();
        var decision = BaselineDecision();
        decision.PropertyName = string.Empty;

        var result = sut.ValidateAndCorrect(decision, "", "Hello", "");

        Assert.False(result.ShouldAutoSend);
        Assert.True(result.RequiresHostReview);
        Assert.True(result.ShouldNotifyHost);
    }

    [Fact]
    public void ValidateAndCorrect_EmptyResponse_AddsFallbackAndBlocksAutoSend()
    {
        var sut = CreateSut();
        var decision = BaselineDecision();
        decision.GuestResponse = "";

        var result = sut.ValidateAndCorrect(decision, "CozyCrab", "Hello", "");

        Assert.False(result.ShouldAutoSend);
        Assert.True(result.RequiresHostReview);
        Assert.Contains("host will review", result.GuestResponse, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndCorrect_LowConfidence_RequiresHostReview()
    {
        var sut = CreateSut();
        var decision = BaselineDecision();
        decision.Confidence = "Low";

        var result = sut.ValidateAndCorrect(decision, "CozyCrab", "Hello", "");

        Assert.True(result.RequiresHostReview);
    }

    [Fact]
    public void ValidateAndCorrect_EmergencyMessage_ForcesEmergencyFlags()
    {
        var sut = CreateSut();
        var decision = BaselineDecision();
        decision.RiskLevel = "Low";
        decision.PolicyRuleType = "Informational";

        var result = sut.ValidateAndCorrect(decision, "CozyCrab", "There is a gas leak", "");

        Assert.Equal("Emergency", result.RiskLevel);
        Assert.Equal("Emergency", result.PolicyRuleType);
        Assert.Equal("Emergency", result.Category);
        Assert.True(result.RequiresHostReview);
        Assert.True(result.ShouldNotifyHost);
    }

    [Fact]
    public void ValidateAndCorrect_SmokingOnlyMessage_DoesNotForceEmergency()
    {
        var sut = CreateSut();
        var decision = BaselineDecision();
        decision.RiskLevel = "Low";

        var result = sut.ValidateAndCorrect(decision, "CozyCrab", "Can I smoke outside?", "");

        Assert.NotEqual("Emergency", result.RiskLevel);
    }

    [Fact]
    public void ValidateAndCorrect_ServiceAnimalLanguage_ForcesHostReviewAndNotify()
    {
        var sut = CreateSut();
        var decision = BaselineDecision();
        decision.RequiresHostReview = false;
        decision.ShouldNotifyHost = false;

        var result = sut.ValidateAndCorrect(decision, "CozyCrab", "I need ADA accommodation for my service dog", "");

        Assert.True(result.RequiresHostReview);
        Assert.True(result.ShouldNotifyHost);
        Assert.Contains("accommodation", result.EscalationReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndCorrect_ApprovesForbiddenTopic_BlocksAutoSend()
    {
        var sut = CreateSut();
        var decision = BaselineDecision();
        decision.GuestResponse = "Yes, you can bring your dog no problem.";

        var result = sut.ValidateAndCorrect(decision, "CozyCrab", "Can I bring my dog?", "");

        Assert.False(result.ShouldAutoSend);
        Assert.True(result.RequiresHostReview);
        Assert.True(result.ShouldNotifyHost);
        Assert.Contains("forbidden topic", result.ReasoningSummary ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndCorrect_AutoSendWithoutResponse_BlocksAutoSend()
    {
        var sut = CreateSut();
        var decision = BaselineDecision();
        decision.ShouldAutoSend = true;
        decision.GuestResponse = "";

        var result = sut.ValidateAndCorrect(decision, "CozyCrab", "Hello", "");

        Assert.False(result.ShouldAutoSend);
        Assert.True(result.RequiresHostReview);
    }
}
