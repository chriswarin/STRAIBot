using STRAIBot.Models;
using STRAIBot.Services;

namespace STRAIBot.Tests.Services;

public class MessageRiskClassifierTests
{
    private readonly MessageRiskClassifier _sut = new();

    [Fact]
    public void Classify_EmergencyLeak_ReturnsEmergency()
    {
        var result = _sut.Classify("There is water leaking from the ceiling now");

        Assert.Equal(RiskLevel.Emergency, result.RiskLevel);
        Assert.Equal(PolicyRuleType.Emergency, result.PolicyRuleType);
        Assert.Equal(MessageCategory.Emergency, result.Category);
        Assert.True(result.ShouldAutoSend);
        Assert.True(result.RequiresHostReview);
        Assert.True(result.ShouldNotifyHost);
    }

    [Fact]
    public void Classify_SmokingQuestion_DoesNotBecomeEmergency()
    {
        var result = _sut.Classify("Can I smoke outside on the deck?");

        Assert.Equal(RiskLevel.Low, result.RiskLevel);
        Assert.Equal(PolicyRuleType.HardRule, result.PolicyRuleType);
        Assert.Equal(MessageCategory.SmokingQuestion, result.Category);
    }

    [Fact]
    public void Classify_ServiceAnimalRequest_TakesPriorityOverPetRule()
    {
        var result = _sut.Classify("I have a service dog, is that okay?");

        Assert.Equal(RiskLevel.High, result.RiskLevel);
        Assert.Equal(PolicyRuleType.HostDecision, result.PolicyRuleType);
        Assert.Equal(MessageCategory.ServiceAnimalRequest, result.Category);
        Assert.True(result.RequiresHostReview);
        Assert.True(result.ShouldNotifyHost);
    }

    [Fact]
    public void Classify_LateCheckout_ReturnsHardRuleLowRisk()
    {
        var result = _sut.Classify("Can we do a late checkout at noon?");

        Assert.Equal(RiskLevel.Low, result.RiskLevel);
        Assert.Equal(PolicyRuleType.HardRule, result.PolicyRuleType);
        Assert.Equal(MessageCategory.LateCheckoutRequest, result.Category);
        Assert.True(result.ShouldAutoSend);
        Assert.False(result.RequiresHostReview);
    }

    [Fact]
    public void Classify_EarlyCheckIn_ReturnsHostDecision()
    {
        var result = _sut.Classify("Can we check in early around 1pm?");

        Assert.Equal(RiskLevel.High, result.RiskLevel);
        Assert.Equal(PolicyRuleType.HostDecision, result.PolicyRuleType);
        Assert.Equal(MessageCategory.EarlyCheckInRequest, result.Category);
        Assert.True(result.RequiresHostReview);
        Assert.True(result.ShouldNotifyHost);
    }

    [Fact]
    public void Classify_PartyMessage_ReturnsMediumRiskHardRule()
    {
        var result = _sut.Classify("Can we host a birthday party with 20 people?");

        Assert.Equal(RiskLevel.Medium, result.RiskLevel);
        Assert.Equal(PolicyRuleType.HardRule, result.PolicyRuleType);
        Assert.Equal(MessageCategory.PartyRisk, result.Category);
        Assert.True(result.ShouldNotifyHost);
    }

    [Fact]
    public void Classify_RefundRequest_ReturnsHostDecision()
    {
        var result = _sut.Classify("I want a refund and cancellation");

        Assert.Equal(RiskLevel.High, result.RiskLevel);
        Assert.Equal(PolicyRuleType.HostDecision, result.PolicyRuleType);
        Assert.Equal(MessageCategory.RefundRequest, result.Category);
    }

    [Fact]
    public void Classify_InformationalWifiQuestion_ReturnsInformationalCategory()
    {
        var result = _sut.Classify("What is the wifi password?");

        Assert.Equal(RiskLevel.Low, result.RiskLevel);
        Assert.Equal(PolicyRuleType.Informational, result.PolicyRuleType);
        Assert.Equal(MessageCategory.WiFiQuestion, result.Category);
        Assert.True(result.ShouldAutoSend);
        Assert.False(result.RequiresHostReview);
    }

    [Fact]
    public void Classify_UnknownQuestion_ReturnsGeneralInformational()
    {
        var result = _sut.Classify("What time does the moon come out there?");

        Assert.Equal(RiskLevel.Low, result.RiskLevel);
        Assert.Equal(PolicyRuleType.Informational, result.PolicyRuleType);
        Assert.Equal(MessageCategory.General, result.Category);
    }
}
