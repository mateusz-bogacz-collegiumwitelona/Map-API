using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Ban;
using Api.Request.List;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Response.Ban;

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
        [ProducesResponseType(typeof(Result), StatusCodes.Status201Created)]
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

        [EndpointSummary("Get a list of user reports")]
        [EndpointDescription("Retrieves a paginated list of user reports. " +
            "Supports filtering, sorting, and searching. " +
            "Returns 200 with the list of reports.")]
        [ProducesResponseType(typeof(Result<PagedResult<ReportListResponse>>), StatusCodes.Status200OK)]
        [HttpGet("reports")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetReportListAsync(
            [FromQuery] ReportListRequest request,
            [FromServices] IBanServices ban,
            [FromServices] ListMapper mapper
            )
        {
            var result = await ban.GetReportListAsync(mapper.ToReportList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Get report details")]
        [EndpointDescription("Retrieves detailed information about a specific user report. " +
            "Returns 200 with the report details or 404 if the report does not exist.")]
        [ProducesResponseType(typeof(Result<ReportDetailResponse>), StatusCodes.Status200OK)]
        [HttpGet("report/{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetReportDetailAsync(
            [FromRoute] Guid id,
            [FromServices] IBanServices ban)
        {
            var result = await ban.GetReportDetailAsync(id);
            return HandleResult(result);
        }
    }
}
