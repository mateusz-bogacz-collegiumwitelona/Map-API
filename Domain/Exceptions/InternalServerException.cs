using Domain.Const;

namespace Domain.Exceptions
{
    public class InternalServerException : AppException
    {
        public InternalServerException(string message, string errorCode = ErrorCodes.InternalError)
            : base(message, errorCode, 500)
        {
        }
    }
}
