using Api.Configuration;
using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Account;
using Api.Request.Auth;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;
using Services.Interfaces.Auth;
using Services.Response.Auth;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [EnableRateLimiting(RateLimitingConfiguration.AuthPolicy)]
    public class AuthController : BaseController
    {
        [HttpPost("login")]
        [AllowAnonymous]
        [EndpointSummary("Log in using email and password")]
        [EndpointDescription("Validates the supplied credentials and returns a JWT access token with its expiration time plus a persisted refresh token." +
            " Invalid credentials and unconfirmed accounts return the same generic 401 response, " +
            "banned accounts are rejected with 403, and accounts are temporarily locked after repeated failures.")]
        [ProducesResponseType(typeof(Result<AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status423Locked)]
        public async Task<IActionResult> Login(
            [FromServices] IAuthService auth,
            [FromServices] AuthMapper mapper,
            [FromBody] LoginRequest request)
        {
            var result = await auth.LoginAsync(mapper.ToLoginCommand(request));
            return HandleResult(result);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [EndpointSummary("Refresh an access token")]
        [EndpointDescription("Exchanges a valid, unexpired, and non-revoked refresh token for a new JWT access token and a rotated refresh token. " +
            "Missing, revoked, or expired refresh tokens are rejected with 401. " +
            "Banned accounts are rejected with 403 and all of their refresh tokens are revoked.")]
        [ProducesResponseType(typeof(Result<AuthResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Refresh(
            [FromServices] IAuthService auth,
            [FromBody] RefreshTokenRequest request)
        {
            var result = await auth.RefreshAsync(request.RefreshToken);
            return HandleResult(result);
        }

        [HttpPost("logout")]
        [Authorize]
        [EndpointSummary("Log out and revoke a refresh token")]
        [EndpointDescription("Requires a valid bearer access token and revokes the supplied refresh token on the server so it " +
            "can no longer be used to refresh the session. " +
            "The endpoint is idempotent and returns 200 when the token is already revoked or unknown.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        public async Task<IActionResult> Logout(
            [FromServices] IAuthService auth,
            [FromBody] LogoutRequest request
            )
        {
            var result = await auth.LogoutAsync(request.RefreshToken);
            return HandleResult(result);
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [EndpointSummary("Register a new user account")]
        [EndpointDescription("Creates a new user account with the supplied email, password, and user details. " +
                    "The account is created in an unconfirmed state and an email verification code is sent to the provided email address. " +
                    "The endpoint always returns 201 on valid input (without revealing whether the email is already registered) and 400 if validation fails.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RegisterAsync(
                    [FromServices] IAccountServices account,
                    [FromServices] AuthMapper mapper,
                    [FromBody] RegisterRequest request)
        {
            var result = await account.RegisterAsync(mapper.ToRegisterCommand(request));
            return HandleResult(result);
        }

        [HttpPost("confirm-email")]
        [AllowAnonymous]
        [EndpointSummary("Confirm a user's email address")]
        [EndpointDescription("Validates the supplied email verification code and marks the user's email address as confirmed. " +
                    "The endpoint returns 200 on success, 400 if the request data or code is invalid/expired, and 429 if maximum verification attempts are exceeded.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ConfirmRegisterAsync(
                    [FromServices] IAccountServices account,
                    [FromServices] AuthMapper mapper,
                    [FromBody] ConfirmEmailRequest request)
        {
            var result = await account.ConfirmRegisterAsync(mapper.ToConfirmEmail(request));
            return HandleResult(result);
        }

        [HttpPost("confirm-email/resend")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitingConfiguration.EmailResendPolicy)]
        [EndpointSummary("Resend email verification code")]
        [EndpointDescription("Resends the email verification code to the user's email address if the account exists and is unverified. " +
             "The endpoint returns a generic 200 response on valid input, 400 if validation fails, and 429 if the resend rate limit is exceeded.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ResendEmailVerificationCodeAsync(
             [FromServices] IAccountServices account,
             [FromServices] AuthMapper mapper,
             [FromBody] ResendVerificationCodeRequest request)
        {
            var result = await account.ResendEmailVerificationCodeAsync(mapper.ToResendVerificationCode(request));
            return HandleResult(result);
        }
    }
}
