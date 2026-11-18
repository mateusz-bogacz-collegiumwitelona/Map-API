namespace Services.Command.List
{
    public sealed record UserListCommand : BasicListCommand
    {
        public bool IncludeBanned { get; init; } = false;
        public bool IncludeAdmins { get; init; } = false;
    }
}
