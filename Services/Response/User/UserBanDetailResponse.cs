namespace Services.Response.User
{
    public sealed record UserBanDetailResponse
    {
        public bool IsBanned { get; init; }
        public DateTime? BannedUntil { get; init; }
        public string? BannedReason { get; init; }
    }
}
