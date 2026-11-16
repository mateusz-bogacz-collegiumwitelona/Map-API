namespace Services.Command.Auth
{
    public sealed record LoginCommand(string Email, string Password);
}
