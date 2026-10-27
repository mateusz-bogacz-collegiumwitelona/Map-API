using Api.Controllers.Base;
using Api.Request.Auth;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces.Auth;
using Services.Response.Auth;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : BaseController
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(Result<AuthResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _authService.LoginAsync(request.Email, request.Password, cancellationToken);
            return HandleResult(result);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(Result<AuthResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Refresh(
            [FromBody] RefreshTokenRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _authService.RefreshAsync(request.RefreshToken, cancellationToken);
            return HandleResult(result);
        }

        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        public async Task<IActionResult> Logout(
            [FromBody] LogoutRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _authService.LogoutAsync(request.RefreshToken, cancellationToken);
            return HandleResult(result);
        }
    }
}
