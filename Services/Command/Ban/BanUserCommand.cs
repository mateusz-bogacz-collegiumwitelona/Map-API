namespace Services.Command.Ban
{
    public sealed record BanUserCommand
    {
        public required Guid UserId { get; init; }
        public required string Reason { get; init; }
        public TimeSpan? Duration { get; set; }
    }
}
