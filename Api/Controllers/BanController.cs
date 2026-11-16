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

        [EndpointSummary("Resolve user report")]
        [EndpointDescription("Resolves a pending user report by either rejecting it or approving it and banning the reported user. " +
            "Set 'isBanned' to false to reject the report, or true to approve it and apply a ban (leave 'duration' null for a permanent ban, or specify a TimeSpan for a temporary ban). " +
            "Returns 200 when the report is successfully resolved, or 400 if the report does not exist, has already been resolved, the reported user is already banned, or validation fails.")]
        [HttpPost("report/resolve")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        public async Task<IActionResult> ResolveReportAsync(
            [FromServices] IBanServices ban,
            [FromServices] BanMapper mapper,
            [FromBody] ResolveReportRequest request
            )
        {
            var result = await ban.ResolveReportAsync(mapper.ToResolve(request), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Unban user")]
        [EndpointDescription("Immediately lifts an active temporary or permanent ban from a specific user and restores their account access. " +
            "Returns 200 when the user is successfully unbanned, 400 if the user does not have an active ban, or 404 if the user does not exist.")]
        [HttpPost("unban/{userId:guid}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        public async Task<IActionResult> UnbanUserAsync(
            [FromServices] IBanServices ban,
            [FromRoute] Guid userId
            )
        {
            var result = await ban.UnbanUserAsync(userId, CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Ban user directly")]
        [EndpointDescription("Directly applies a temporary or permanent ban to a specific user without requiring a prior user report, " +
            "and automatically approves any pending reports against that user. Leave 'duration' null for a permanent ban or specify a TimeSpan for a temporary ban. " +
            "Returns 200 when the user is banned, 400 if the admin tries to ban themselves, the user is already banned, or validation fails, or 404 if the user does not exist.")]
        [HttpPost("ban")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        public async Task<IActionResult> BanUserAsync(
            [FromServices] IBanServices ban,
            [FromServices] BanMapper mapper,
            [FromBody] BanUserRequest request
            )
        {
            var result = await ban.BanUserAsync(mapper.ToBan(request), CurrentUserId);
            return HandleResult(result);
        }
    }
}
