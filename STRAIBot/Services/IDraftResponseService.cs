using STRAIBot.Models;

namespace STRAIBot.Services;

public interface IDraftResponseService
{
    /// <summary>
    /// Classifies the guest message, retrieves property memory context,
    /// and generates an appropriate guest response with routing flags.
    /// </summary>
    Task<DraftResponseResult> GenerateDraftAsync(string propertyName, string guestMessage);
}

