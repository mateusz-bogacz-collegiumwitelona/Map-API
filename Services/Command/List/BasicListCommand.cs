namespace Services.Command.List
{
    public record BasicListCommand : SimpleListCommand
    {
        public string? SearchTerm { get; init; }
    }
}
