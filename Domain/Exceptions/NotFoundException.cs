using Domain.Const;

namespace Domain.Exceptions
{
    public class NotFoundException : AppException
    {
        public NotFoundException(string message)
            : base(message, ErrorCodes.NotFound, 404)
        {
        }
    }
}
