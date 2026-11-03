namespace Domain.Const
{
    /// <summary>
    /// Central catalogue of API error codes, grouped by domain.
    /// </summary>
    public static class ErrorCodes
    {
        #region Auth
        public const string InvalidCredentials = "AUTH_001";
        public const string EmailNotConfirmed = "AUTH_002";
        public const string InvalidRefreshToken = "AUTH_003";
        public const string AccountLocked = "AUTH_004";
        public const string AccountBanned = "AUTH_005";
        #endregion

        #region Validation
        public const string ValidationError = "VALIDATION_001";
        #endregion

        #region Common

        public const string InvalidOperation = "COMMON_001";
        public const string NotFound = "COMMON_002";
        public const string BadRequest = "COMMON_003";
        public const string InternalError = "COMMON_004";
        public const string TooManyRequests = "COMMON_005";
        public const string AccessDenied = "ACCESS_DENIED";
        #endregion

        #region User
        public const string FirstNameInvalid = "USR_001";
        public const string LastNameInvalid = "USR_002";
        public const string BioInvalid = "USR_003";
        public const string EmailAlreadyExist = "USR_004";
        #endregion
    }
}
