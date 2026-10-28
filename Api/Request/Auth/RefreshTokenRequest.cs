namespace Api.Request.Auth
{
    public sealed record RefreshTokenRequest
    {
        public required string RefreshToken { get; init; }
    }
}
