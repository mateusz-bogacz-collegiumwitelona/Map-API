using Domain.Const;

namespace Domain.Exceptions
{
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message, string errorCode = ErrorCodes.InvalidOperation)
            : base(message, errorCode, 403)
        {
        }
    }
}
