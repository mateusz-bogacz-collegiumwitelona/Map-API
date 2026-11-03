using Domain.Const;

namespace Tests.Other
{
    public class ErrorCodesTests
    {
        [Test]
        public async Task ErrorCodes_UseStructuredDomainPrefixedValues()
        {
            await Assert.That(ErrorCodes.InvalidCredentials).IsEqualTo("AUTH_001");
            await Assert.That(ErrorCodes.EmailNotConfirmed).IsEqualTo("AUTH_002");
            await Assert.That(ErrorCodes.InvalidRefreshToken).IsEqualTo("AUTH_003");
            await Assert.That(ErrorCodes.AccountLocked).IsEqualTo("AUTH_004");
            await Assert.That(ErrorCodes.AccountBanned).IsEqualTo("AUTH_005");

            await Assert.That(ErrorCodes.ValidationError).IsEqualTo("VALIDATION_001");

            await Assert.That(ErrorCodes.InvalidOperation).IsEqualTo("COMMON_001");
            await Assert.That(ErrorCodes.NotFound).IsEqualTo("COMMON_002");
            await Assert.That(ErrorCodes.BadRequest).IsEqualTo("COMMON_003");
            await Assert.That(ErrorCodes.InternalError).IsEqualTo("COMMON_004");
            await Assert.That(ErrorCodes.TooManyRequests).IsEqualTo("COMMON_005");
            await Assert.That(ErrorCodes.AccessDenied).IsEqualTo("ACCESS_DENIED");

            await Assert.That(ErrorCodes.FirstNameInvalid).IsEqualTo("USR_001");
            await Assert.That(ErrorCodes.LastNameInvalid).IsEqualTo("USR_002");
            await Assert.That(ErrorCodes.BioInvalid).IsEqualTo("USR_003");
        }

        [Test]
        public async Task ErrorCodes_AreUnique()
        {
            var codes = new[]
            {
                ErrorCodes.InvalidCredentials,
                ErrorCodes.EmailNotConfirmed,
                ErrorCodes.InvalidRefreshToken,
                ErrorCodes.AccountLocked,
                ErrorCodes.AccountBanned,
                ErrorCodes.ValidationError,
                ErrorCodes.InvalidOperation,
                ErrorCodes.NotFound,
                ErrorCodes.BadRequest,
                ErrorCodes.InternalError,
                ErrorCodes.TooManyRequests,
                ErrorCodes.AccessDenied,
                ErrorCodes.FirstNameInvalid,
                ErrorCodes.LastNameInvalid,
                ErrorCodes.BioInvalid
            };

            await Assert.That(codes.Distinct().Count()).IsEqualTo(codes.Length);
        }
    }
}
