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
