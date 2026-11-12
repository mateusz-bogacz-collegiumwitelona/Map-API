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
        public const string ValidationError = "VAL_001";
        public const string PageNumberInvalid = "VAL_002";
        public const string PageSizeInvalid = "VAL_003";
        #endregion

        #region Common
        public const string InvalidOperation = "COMMON_001";
        public const string NotFound = "COMMON_002";
        public const string BadRequest = "COMMON_003";
        public const string InternalError = "COMMON_004";
        public const string TooManyRequests = "COMMON_005";
        public const string GuidInvalid = "COMMON_006";
        public const string AccessDenied = "ACCESS_DENIED";
        #endregion

        #region User
        public const string FirstNameInvalid = "USR_001";
        public const string LastNameInvalid = "USR_002";
        public const string BioInvalid = "USR_003";
        public const string EmailInvalid = "USR_004";
        public const string EmailTaken = "USR_005";
        public const string EmailUnchanged = "USR_006";
        public const string EmailAlreadyExist = "USR_007";
        #endregion

        #region Verification
        public const string CodeInvalid = "VER_001";
        public const string CodeExpired = "VER_002";
        public const string AttemptsExceeded = "VER_003";
        #endregion

        #region Trip
        public const string YearInvalid = "TRIP_001";
        #endregion
    }
}
