using Domain.Const;

namespace Tests.Other
{
    public class ErrorCodesTests
    {
        [Test]
        public async Task ErrorCodes_UseStructuredDomainPrefixedValues()
        {
            await Assert.That(ErrorCodes.Auth.InvalidCredentials).IsEqualTo("AUTH_001");
            await Assert.That(ErrorCodes.Auth.EmailNotConfirmed).IsEqualTo("AUTH_002");
            await Assert.That(ErrorCodes.Auth.InvalidRefreshToken).IsEqualTo("AUTH_003");
            await Assert.That(ErrorCodes.Auth.AccountLocked).IsEqualTo("AUTH_004");
            await Assert.That(ErrorCodes.Auth.AccountBanned).IsEqualTo("AUTH_005");

            await Assert.That(ErrorCodes.Validation.ValidationError).IsEqualTo("VALIDATION_001");

            await Assert.That(ErrorCodes.Common.InvalidOperation).IsEqualTo("COMMON_001");
            await Assert.That(ErrorCodes.Common.NotFound).IsEqualTo("COMMON_002");
            await Assert.That(ErrorCodes.Common.BadRequest).IsEqualTo("COMMON_003");
            await Assert.That(ErrorCodes.Common.InternalError).IsEqualTo("COMMON_004");
            await Assert.That(ErrorCodes.Common.TooManyRequests).IsEqualTo("COMMON_005");
            await Assert.That(ErrorCodes.Common.AccessDenied).IsEqualTo("ACCESS_DENIED");

            await Assert.That(ErrorCodes.User.FirstNameInvalid).IsEqualTo("USR_001");
            await Assert.That(ErrorCodes.User.LastNameInvalid).IsEqualTo("USR_002");
            await Assert.That(ErrorCodes.User.BioInvalid).IsEqualTo("USR_003");
        }

        [Test]
        public async Task ErrorCodes_AreUnique()
        {
            var codes = new[]
            {
                ErrorCodes.Auth.InvalidCredentials,
                ErrorCodes.Auth.EmailNotConfirmed,
                ErrorCodes.Auth.InvalidRefreshToken,
                ErrorCodes.Auth.AccountLocked,
                ErrorCodes.Auth.AccountBanned,
                ErrorCodes.Validation.ValidationError,
                ErrorCodes.Common.InvalidOperation,
                ErrorCodes.Common.NotFound,
                ErrorCodes.Common.BadRequest,
                ErrorCodes.Common.InternalError,
                ErrorCodes.Common.TooManyRequests,
                ErrorCodes.Common.AccessDenied,
                ErrorCodes.User.FirstNameInvalid,
                ErrorCodes.User.LastNameInvalid,
                ErrorCodes.User.BioInvalid
            };

            await Assert.That(codes.Distinct().Count()).IsEqualTo(codes.Length);
        }
    }
}
