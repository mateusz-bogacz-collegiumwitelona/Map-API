using Domain.Const;

namespace Domain.Exceptions
{
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message, string errorCode = ErrorCodes.Common.AccessDenied)
            : base(message, errorCode, 403)
        {
        }
    }
}
