using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.User;
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
    }
}
