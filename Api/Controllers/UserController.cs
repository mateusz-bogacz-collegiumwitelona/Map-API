using Api.Controllers.Base;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Response.User;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UserController : BaseController
    {
        [EndpointSummary("Get basic profile information")]
        [EndpointDescription("Return the basic profile information sutch a user id, first name, last name and bio if exist.")]
        [ProducesResponseType(typeof(Result<PagedResult<UserResponse>>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetUserDataAsync([FromServices] IUserServices user)
        {
            var result = await user.GetUserDataAsync(CurrentUserId);
            return HandleResult(result);
        }
    }
}
