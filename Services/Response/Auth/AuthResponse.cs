namespace Services.Response.Auth
{
    public sealed record AuthResponse(
        Guid UserId,
        IReadOnlyList<string> Roles,
        string AccessToken,
        DateTime AccessTokenExpiration,
        string RefreshToken);
}
