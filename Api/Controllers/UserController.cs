using Api.Configuration;
using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Account;
using Api.Request.List;
using Api.Request.User;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;
using Services.Response.User;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UserController : BaseController
    {
        [EndpointSummary("Get basic profile information")]
        [EndpointDescription("Return the basic profile information such as user id, first name, last name and bio if exist.")]
        [ProducesResponseType(typeof(Result<UserResponse>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUserProfileAsync(
            [FromQuery] Guid? userId,
            [FromServices] IUserServices user)
        {
            var result = await user.GetUserProfileAsync(CurrentUserId, userId);
            return HandleResult(result);
        }

        [EndpointSummary("Edit user profile information")]
        [EndpointDescription("Edit the user profile information such as first name, last name and bio.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPatch]
        [Authorize]
        public async Task<IActionResult> EditUserDataAsync(
            [FromServices] IUserServices user,
            [FromServices] UserMapper mapper,
            [FromBody] EditUserRequest request)
        {
            var result = await user.EditUserAsync(CurrentUserId, mapper.MapEdit(request));
            return HandleResult(result);
        }

        [EndpointSummary("Set profile visibility")]
        [EndpointDescription("Set the profile visibility to public or private.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPatch("visibility/{isPublic:bool}")]
        [Authorize]
        public async Task<IActionResult> SetProfileVisibilityAsync(
            [FromServices] IUserServices user,
            [FromRoute] bool isPublic)
        {
            var result = await user.SetProfileVisibilityAsync(CurrentUserId, isPublic);
            return HandleResult(result);
        }

        [EndpointSummary("Request an email address change")]
        [EndpointDescription("Sends a 6-digit verification code to the new email address. The current email address stays active for signing in until the change is confirmed. Returns 409 when the address is already in use and 429 when a code was requested less than a minute ago. Available only for accounts with the User role.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status409Conflict)]
        [HttpPost("email")]
        [Authorize(Policy = AuthorizationConfiguration.UserPolicy)]
        [EnableRateLimiting(RateLimitingConfiguration.EmailResendPolicy)]
        public async Task<IActionResult> RequestEmailChangeAsync(
            [FromServices] IAccountServices account,
            [FromServices] UserMapper mapper,
            [FromBody] ChangeEmailRequest request)
        {
            var result = await account.RequestEmailChangeAsync(CurrentUserId, mapper.MapChangeEmail(request));
            return HandleResult(result);
        }

        [EndpointSummary("Confirm an email address change")]
        [EndpointDescription("Confirms the pending email change with the verification code sent to the new address, then makes it the verified primary email and notifies the old address. Codes expire after 15 minutes and allow 5 attempts. Available only for accounts with the User role.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status409Conflict)]
        [HttpPost("email/confirm")]
        [Authorize(Policy = AuthorizationConfiguration.UserPolicy)]
        [EnableRateLimiting(RateLimitingConfiguration.AuthPolicy)]
        public async Task<IActionResult> ConfirmEmailChangeAsync(
            [FromServices] IAccountServices account,
            [FromServices] UserMapper mapper,
            [FromBody] ConfirmEmailChangeRequest request)
        {
            var result = await account.ConfirmEmailChangeAsync(CurrentUserId, mapper.MapConfirmEmailChange(request));
            return HandleResult(result);
        }

        [EndpointSummary("Delete own account")]
        [EndpointDescription("Soft-deletes the current account and anonymizes its personal data while keeping relational history. All refresh tokens are revoked and the current access token stops working immediately. Available only for accounts with the User role.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpDelete]
        [Authorize(Policy = AuthorizationConfiguration.UserPolicy)]
        public async Task<IActionResult> DeleteAccountAsync(
            [FromServices] IAccountServices account)
        {
            var result = await account.DeleteAccountAsync(CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Retrieve a paginated list of users")]
        [EndpointDescription("Returns a paginated list of users with optional accent-insensitive search by first name," +
            " last name, email, or role. By default, banned users and administrators are excluded unless explicitly " +
            "included via query flags. Available only for accounts with the Admin role.")]
        [ProducesResponseType(typeof(Result<PagedResult<UserListResponse>>), StatusCodes.Status200OK)]
        [HttpGet("list")]
        [Authorize(Policy = AuthorizationConfiguration.AdminPolicy)]
        public async Task<IActionResult> GetUserListAsnyc(
            [FromServices] IUserServices user,
            [FromServices] ListMapper mapper,
            [FromQuery] UserListRequest request
            )
        {
            var result = await user.GetUserListAsnyc(mapper.MapUserList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Retrieve detailed information about a specific user")]
        [EndpointDescription("Returns detailed information about a specific user, " +
            "including their ID, first name, last name, email, role, and account status. " +
            "Available only for accounts with the Admin role.")]
        [HttpGet("detail/{userId:guid}")]
        [ProducesResponseType(typeof(Result<UserDetailResponse>), StatusCodes.Status200OK)]
        [Authorize(Policy = AuthorizationConfiguration.AdminPolicy)]
        public async Task<IActionResult> GetUserDetailAsync(
            [FromServices] IUserServices user,
            [FromRoute] Guid userId)
        {
            var result = await user.GetUserDetailAsync(userId);
            return HandleResult(result);
        }

        [EndpointSummary("Get user ban detail")]
        [EndpointDescription("Returns detailed information about a specific user's ban status, " +
            "including whether the user is currently banned, the reason for the ban, and the duration of the ban. " +
            "Available only for accounts with the Admin role.")]
        [HttpGet("detail/{userId:guid}/ban")]
        [ProducesResponseType(typeof(Result<UserBanDetailResponse>), StatusCodes.Status200OK)]
        [Authorize(Policy = AuthorizationConfiguration.AdminPolicy)]
        public async Task<IActionResult> GetUserBanDetailAsync(
           [FromServices] IUserServices user,
           [FromRoute] Guid userId)
        {
            var result = await user.GetUserBanDetailAsync(userId);
            return HandleResult(result);
        }

        [EndpointSummary("Retrieve a paginated list of reports associated with a specific user")]
        [EndpointDescription("Returns a paginated and sorting list of reports associated with a specific user, " +
            "including details such as report ID, type, status, and creation date. " +
            "Available only for accounts with the Admin role.")]
        [HttpGet("detail/{userId:guid}/report")]
        [ProducesResponseType(typeof(Result<PagedResult<UserReportListRespone>>), StatusCodes.Status200OK)]
        [Authorize(Policy = AuthorizationConfiguration.AdminPolicy)]
        public async Task<IActionResult> GetUserReportListAsync(
            [FromServices] IUserServices user,
            [FromServices] ListMapper mapper,
            [FromRoute] Guid userId,
            [FromQuery] SimpleListRequest request
            )
        {
            var result = await user.GetUserReportListAsync(userId, mapper.ToSimpleList(request));
            return HandleResult(result);
        }
    }
}
