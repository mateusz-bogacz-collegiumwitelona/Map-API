namespace Services.Commands.Auth
{
    public sealed record LoginCommand(string Email, string Password);
}
