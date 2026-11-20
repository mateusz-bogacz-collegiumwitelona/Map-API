using Domain.Const;

namespace Tests.Other
{
    public class ErrorCodesTests
    {
        [Test]
        public async Task ErrorCodes_UseStructuredDomainPrefixedValues()
        {
            #region Auth
            await Assert.That(ErrorCodes.InvalidCredentials).IsEqualTo("AUTH_001");
            await Assert.That(ErrorCodes.EmailNotConfirmed).IsEqualTo("AUTH_002");
            await Assert.That(ErrorCodes.InvalidRefreshToken).IsEqualTo("AUTH_003");
            await Assert.That(ErrorCodes.AccountLocked).IsEqualTo("AUTH_004");
            await Assert.That(ErrorCodes.AccountBanned).IsEqualTo("AUTH_005");
            #endregion

            #region Validation
            await Assert.That(ErrorCodes.ValidationError).IsEqualTo("VAL_001");
            await Assert.That(ErrorCodes.PageNumberInvalid).IsEqualTo("VAL_002");
            await Assert.That(ErrorCodes.PageSizeInvalid).IsEqualTo("VAL_003");
            #endregion

            #region Common
            await Assert.That(ErrorCodes.InvalidOperation).IsEqualTo("COMMON_001");
            await Assert.That(ErrorCodes.NotFound).IsEqualTo("COMMON_002");
            await Assert.That(ErrorCodes.BadRequest).IsEqualTo("COMMON_003");
            await Assert.That(ErrorCodes.InternalError).IsEqualTo("COMMON_004");
            await Assert.That(ErrorCodes.TooManyRequests).IsEqualTo("COMMON_005");
            await Assert.That(ErrorCodes.GuidInvalid).IsEqualTo("COMMON_006");
            await Assert.That(ErrorCodes.AccessDenied).IsEqualTo("ACCESS_DENIED");
            #endregion

            #region User
            await Assert.That(ErrorCodes.FirstNameInvalid).IsEqualTo("USR_001");
            await Assert.That(ErrorCodes.LastNameInvalid).IsEqualTo("USR_002");
            await Assert.That(ErrorCodes.BioInvalid).IsEqualTo("USR_003");
            await Assert.That(ErrorCodes.EmailInvalid).IsEqualTo("USR_004");
            await Assert.That(ErrorCodes.EmailTaken).IsEqualTo("USR_005");
            await Assert.That(ErrorCodes.EmailUnchanged).IsEqualTo("USR_006");
            await Assert.That(ErrorCodes.EmailAlreadyExist).IsEqualTo("USR_007");
            await Assert.That(ErrorCodes.UserNotFound).IsEqualTo("USR_008");
            await Assert.That(ErrorCodes.SortInvalid).IsEqualTo("USR_009");
            #endregion

            #region Verification
            await Assert.That(ErrorCodes.CodeInvalid).IsEqualTo("VER_001");
            await Assert.That(ErrorCodes.CodeExpired).IsEqualTo("VER_002");
            await Assert.That(ErrorCodes.AttemptsExceeded).IsEqualTo("VER_003");
            #endregion

            #region Trip
            await Assert.That(ErrorCodes.YearInvalid).IsEqualTo("TRIP_001");
            #endregion

            #region Ban / Report
            await Assert.That(ErrorCodes.ReportEmpty).IsEqualTo("BAN_001");
            await Assert.That(ErrorCodes.ReportTooLong).IsEqualTo("BAN_002");
            await Assert.That(ErrorCodes.ReportTooShort).IsEqualTo("BAN_003");
            await Assert.That(ErrorCodes.ReportNotFound).IsEqualTo("BAN_004");
            await Assert.That(ErrorCodes.ReportAlreadyResolved).IsEqualTo("BAN_005");
            await Assert.That(ErrorCodes.UserAlreadyBanned).IsEqualTo("BAN_006");
            await Assert.That(ErrorCodes.BanDurationInvalid).IsEqualTo("BAN_007");
            await Assert.That(ErrorCodes.BanDurationNotAllowed).IsEqualTo("BAN_008");
            await Assert.That(ErrorCodes.UserNotBanned).IsEqualTo("BAN_009");

            #endregion
        }

        [Test]
        public async Task ErrorCodes_AreUnique()
        {
            var codes = new[]
            {
                #region Auth
                ErrorCodes.InvalidCredentials,
                ErrorCodes.EmailNotConfirmed,
                ErrorCodes.InvalidRefreshToken,
                ErrorCodes.AccountLocked,
                ErrorCodes.AccountBanned,
                #endregion

                #region Validation
                ErrorCodes.ValidationError,
                ErrorCodes.PageNumberInvalid,
                ErrorCodes.PageSizeInvalid,
                #endregion

                #region Common

                ErrorCodes.InvalidOperation,
                ErrorCodes.NotFound,
                ErrorCodes.BadRequest,
                ErrorCodes.InternalError,
                ErrorCodes.TooManyRequests,
                ErrorCodes.GuidInvalid,
                ErrorCodes.AccessDenied,
                #endregion

                #region User
                ErrorCodes.FirstNameInvalid,
                ErrorCodes.LastNameInvalid,
                ErrorCodes.BioInvalid,
                ErrorCodes.EmailInvalid,
                ErrorCodes.EmailTaken,
                ErrorCodes.EmailUnchanged,
                ErrorCodes.EmailAlreadyExist,
                ErrorCodes.UserNotFound,
                ErrorCodes.SortInvalid,
                #endregion

                #region Trip
                ErrorCodes.YearInvalid,
                #endregion

                #region Verification
                ErrorCodes.CodeInvalid,
                ErrorCodes.CodeExpired,
                ErrorCodes.AttemptsExceeded,
                #endregion

                #region Ban / Report
                ErrorCodes.ReportEmpty,
                ErrorCodes.ReportTooLong,
                ErrorCodes.ReportTooShort,
                ErrorCodes.ReportNotFound,
                ErrorCodes.ReportAlreadyResolved,
                ErrorCodes.UserAlreadyBanned,
                ErrorCodes.BanDurationInvalid,
                ErrorCodes.BanDurationNotAllowed,
                ErrorCodes.UserNotBanned
                #endregion
            };

            await Assert.That(codes.Distinct().Count()).IsEqualTo(codes.Length);
        }
    }
}