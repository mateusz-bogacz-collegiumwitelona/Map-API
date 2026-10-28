using Api.Configuration;
using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Auth;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces.Auth;
using Services.Response.Auth;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [EnableRateLimiting(RateLimitingConfiguration.AuthPolicy)]
    public class AuthController : BaseController
    {
        private readonly IAuthService _authService;
        private readonly AuthMapper _authMapper;

        public AuthController(IAuthService authService, AuthMapper authMapper)
        {
            _authService = authService;
            _authMapper = authMapper;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [EndpointSummary("Log in using email and password")]
        [EndpointDescription("Validates the supplied credentials and returns a JWT access token with its expiration time plus a persisted refresh token. Invalid credentials and unconfirmed accounts return the same generic 401 response, banned accounts are rejected with 403, and accounts are temporarily locked after repeated failures.")]
        [ProducesResponseType(typeof(Result<AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status423Locked)]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            var command = _authMapper.ToLoginCommand(request);
            var result = await _authService.LoginAsync(command, cancellationToken);
            return HandleResult(result);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [EndpointSummary("Refresh an access token")]
        [EndpointDescription("Exchanges a valid, unexpired, and non-revoked refresh token for a new JWT access token and a rotated refresh token. Missing, revoked, or expired refresh tokens are rejected with 401. Banned accounts are rejected with 403 and all of their refresh tokens are revoked.")]
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
        [EndpointSummary("Log out and revoke a refresh token")]
        [EndpointDescription("Requires a valid bearer access token and revokes the supplied refresh token on the server so it can no longer be used to refresh the session. The endpoint is idempotent and returns 200 when the token is already revoked or unknown.")]
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
