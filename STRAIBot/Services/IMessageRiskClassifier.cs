using STRAIBot.Models;

namespace STRAIBot.Services;

public interface IMessageRiskClassifier
{
    /// <summary>
    /// Classifies a guest message by risk level, category, and auto-send eligibility.
    /// </summary>
    MessageRiskAnalysis Classify(string guestMessage);
}
