using Api.Mappers;
using Api.Request.Ban;

namespace Tests.Mappers
{
    public class BanMapperTests
    {
        private readonly BanMapper _mapper = new();

        #region Report Mapping Tests

        [Test]
        public async Task ToReport_MapsUserIdAndReason()
        {
            // Arrange
            var request = new ReportUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "Spam"
            };

            // Act
            var command = _mapper.ToReport(request);

            // Assert
            await Assert.That(command.UserId).IsEqualTo(request.UserId);
            await Assert.That(command.Reason).IsEqualTo("Spam");
        }

        [Test]
        public async Task ToReport_TrimsWhitespaceFromReason()
        {
            // Arrange
            var request = new ReportUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "   Harassment in comments   "
            };

            // Act
            var command = _mapper.ToReport(request);

            // Assert
            await Assert.That(command.Reason).IsEqualTo("Harassment in comments");
        }

        [Test]
        public async Task ToReport_TrimsTabsAndNewLinesFromReason()
        {
            // Arrange
            var request = new ReportUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "\t\n Fake profile \r\n"
            };

            // Act
            var command = _mapper.ToReport(request);

            // Assert
            await Assert.That(command.Reason).IsEqualTo("Fake profile");
        }

        [Test]
        public async Task ToReport_WhenReasonIsAlreadyClean_PreservesExactValue()
        {
            // Arrange
            var request = new ReportUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "Inappropriate content"
            };

            // Act
            var command = _mapper.ToReport(request);

            // Assert
            await Assert.That(command.Reason).IsEqualTo("Inappropriate content");
        }

        [Test]
        public async Task ToReport_PreservesWhitespaceInsideReason()
        {
            // Arrange
            var request = new ReportUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "  Too   many   spaces  "
            };

            // Act
            var command = _mapper.ToReport(request);

            // Assert
            await Assert.That(command.Reason).IsEqualTo("Too   many   spaces");
        }

        [Test]
        public async Task ToReport_WhenReasonIsWhitespace_ReturnsEmptyString()
        {
            // Arrange
            var request = new ReportUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "     "
            };

            // Act
            var command = _mapper.ToReport(request);

            // Assert
            await Assert.That(command.Reason).IsEqualTo(string.Empty);
        }

        [Test]
        public async Task ToReport_WhenReasonIsEmpty_ReturnsEmptyString()
        {
            // Arrange
            var request = new ReportUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = string.Empty
            };

            // Act
            var command = _mapper.ToReport(request);

            // Assert
            await Assert.That(command.Reason).IsEqualTo(string.Empty);
        }

        [Test]
        public async Task ToReport_DoesNotModifyUserId()
        {
            // Arrange
            var request = new ReportUserRequest
            {
                UserId = Guid.Empty,
                Reason = "Spam"
            };

            // Act
            var command = _mapper.ToReport(request);

            // Assert
            await Assert.That(command.UserId).IsEqualTo(Guid.Empty);
        }

        #endregion
    }
}
