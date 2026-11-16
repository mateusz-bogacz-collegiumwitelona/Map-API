using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Ban;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/ban")]
    public class BanController : BaseController
    {
        [EndpointSummary("Report a user")]
        [EndpointDescription("Reports a user for inappropriate behavior. " +
            "The report reason must be between 5 and 500 characters. " +
            "Returns 400 when the userId or reason is invalid and 404 when the user does not exist.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPost("report")]
        [Authorize]
        public async Task<IActionResult> ReportUserAsync(
            [FromBody] ReportUserRequest request,
            [FromServices] IBanServices ban,
            [FromServices] BanMapper mapper
            )
        {
            var result = await ban.ReportUserAsync(mapper.ToReport(request), CurrentUserId);
            return HandleResult(result);
        }
    }
}
