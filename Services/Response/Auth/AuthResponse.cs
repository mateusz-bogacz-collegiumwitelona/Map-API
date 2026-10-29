namespace Services.Response.Auth
{
    public sealed record AuthResponse
    {
        public required Guid UserId { get; init; }
        public required IReadOnlyList<string> Roles { get; init; }
        public required string AccessToken { get; init; }
        public required DateTime AccessTokenExpiration { get; init; }
        public required string RefreshToken { get; init; }
    }
}
