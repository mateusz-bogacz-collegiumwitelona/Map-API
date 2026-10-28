namespace Domain.Const
{
    /// <summary>
    /// Central catalogue of API error codes, grouped by domain.
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>
        /// Error codes related to authentication and session management.
        /// </summary>
        public static class Auth
        {
            /// <summary>
            /// Returned when the supplied email and password do not match a valid account.
            /// Used for both unknown accounts and incorrect passwords to avoid revealing whether an account exists.
            /// </summary>
            public const string InvalidCredentials = "AUTH_001";

            /// <summary>
            /// Returned when the supplied credentials are valid but the account's email address has not been confirmed.
            /// </summary>
            public const string EmailNotConfirmed = "AUTH_002";

            /// <summary>
            /// Returned when a refresh token is missing, expired, revoked, or otherwise invalid.
            /// </summary>
            public const string InvalidRefreshToken = "AUTH_003";

            /// <summary>
            /// Returned when the account is temporarily locked after too many failed login attempts.
            /// </summary>
            public const string AccountLocked = "AUTH_004";

            /// <summary>
            /// Returned when the account is banned and cannot authenticate.
            /// </summary>
            public const string AccountBanned = "AUTH_005";
        }

        /// <summary>
        /// Error codes for request validation failures.
        /// </summary>
        public static class Validation
        {
            /// <summary>
            /// Returned when request validation fails and no more specific validation error code is available.
            /// </summary>
            public const string ValidationError = "VALIDATION_001";
        }

        /// <summary>
        /// Error codes shared across application features.
        /// </summary>
        public static class Common
        {
            /// <summary>
            /// Returned when an operation cannot be completed because the resource is in a conflicting or invalid state,
            /// such as a database concurrency conflict.
            /// </summary>
            public const string InvalidOperation = "COMMON_001";

            /// <summary>
            /// Returned when a requested resource does not exist.
            /// </summary>
            public const string NotFound = "COMMON_002";

            /// <summary>
            /// Returned when the request is malformed or contains an invalid argument.
            /// </summary>
            public const string BadRequest = "COMMON_003";

            /// <summary>
            /// Returned when an unexpected server-side error occurs.
            /// </summary>
            public const string InternalError = "COMMON_004";

            /// <summary>
            /// Returned when the caller has exceeded the allowed request rate.
            /// </summary>
            public const string TooManyRequests = "COMMON_005";
        }
    }
}
