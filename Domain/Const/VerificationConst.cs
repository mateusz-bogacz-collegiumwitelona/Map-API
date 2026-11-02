namespace Domain.Const
{
    public static class VerificationConst
    {
        public const int CodeLength = 6;
        public const int CodeExpirationMinutes = 15;
        public const int MaxAttempts = 5;
        public const int ResendCooldownSeconds = 60;
    }
}
