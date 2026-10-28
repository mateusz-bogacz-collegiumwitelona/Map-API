namespace Services.Options
{
    public static class JwtOptionsValidation
    {
        public const string ErrorMessage =
            "JWT configuration is incomplete. Configure JWT:Issuer, JWT:Audience, and JWT:SecretKey " +
            "(JWT__SECRETKEY in environment variables) with a signing key of at least 32 bytes, " +
            "and positive token expiration values.";

        public static bool IsValid(JwtOptions options)
            => options.IsValid();
    }
}
