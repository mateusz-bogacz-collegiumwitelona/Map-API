namespace Domain.Const
{
    public static class ErrorCodes
    {
        // Basic
        public const string InvalidOperation = "INVALID_OPERATION";
        public const string NotFound = "NOT_FOUND";
        public const string BadRequest = "BAD_REQUEST";
        public const string InternalError = "INTERNAL_ERROR";
        public const string ValidationError = "VALIDATION_ERROR";

        // User
        public const string UserFirstNameInvalid = "USR_001";
        public const string UserLastNameInvalid = "USR_002";
        public const string UserBioInvalid = "USR_003";
    }
}
