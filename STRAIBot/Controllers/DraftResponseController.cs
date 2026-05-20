using Microsoft.AspNetCore.Mvc;
using STRAIBot.Models;
using STRAIBot.Services;

namespace STRAIBot.Controllers;

[ApiController]
[Route("api/draft-response")]
[Produces("application/json")]
public class DraftResponseController : ControllerBase
{
    private readonly IDraftResponseService _draftResponseService;

    public DraftResponseController(IDraftResponseService draftResponseService)
    {
        _draftResponseService = draftResponseService;
    }

    /// <summary>
    /// Generate a draft guest response using property memory and risk classification.
    /// </summary>
    /// <remarks>
    /// Classifies the guest message by risk level, retrieves relevant property memory context,
    /// and returns a ready-to-review guest response with routing flags.
    ///
    /// **Low-risk amenity question example:**
    ///
    ///     POST /api/draft-response
    ///     {
    ///         "propertyName": "CozyCrab",
    ///         "guestMessage": "Do you allow pets?"
    ///     }
    ///
    /// Expected result: `policyRuleType = HardRule`, `shouldAutoSend = true`,
    /// `requiresHostReview = false`, `shouldNotifyHost = false`,
    /// firm no-pets response drawn from property memory.
    ///
    /// **Emergency example:**
    ///
    ///     POST /api/draft-response
    ///     {
    ///         "propertyName": "TurquoiseBay",
    ///         "guestMessage": "There is water leaking from the ceiling"
    ///     }
    ///
    /// Expected result: `riskLevel = Emergency`, `shouldAutoSend = true`,
    /// `requiresHostReview = true`, `shouldNotifyHost = true`.
    ///
    /// **Available property names:** CozyCrab, BlueHorizon, TurquoiseBay
    /// </remarks>
    /// <param name="request">Property name and guest message.</param>
    /// <returns>Draft response with risk classification and routing flags.</returns>
    /// <response code="200">Draft response generated successfully.</response>
    /// <response code="400">propertyName or guestMessage is missing.</response>
    [HttpPost]
    [ProducesResponseType(typeof(DraftResponseResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DraftResponseResult>> Post([FromBody] DraftResponseRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _draftResponseService.GenerateDraftAsync(
            request.PropertyName,
            request.GuestMessage);

        return Ok(result);
    }
}
