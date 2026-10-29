namespace Api.Request.Auth
{
    public sealed record LogoutRequest
    {
        public required string RefreshToken { get; init; }
    }
}
