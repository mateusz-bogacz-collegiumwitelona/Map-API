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

        #region Resolve Mapping Tests

        [Test]
        public async Task ToResolve_WithTemporaryBan_MapsAllProperties()
        {
            // Arrange
            var duration = TimeSpan.FromDays(7);
            var request = new ResolveReportRequest
            {
                Id = Guid.NewGuid(),
                IsBanned = true,
                Duration = duration
            };

            // Act
            var command = _mapper.ToResolve(request);

            // Assert
            await Assert.That(command.Id).IsEqualTo(request.Id);
            await Assert.That(command.IsBanned).IsTrue();
            await Assert.That(command.Duration).IsEqualTo(duration);
        }

        [Test]
        public async Task ToResolve_WithPermanentBan_MapsNullDuration()
        {
            // Arrange
            var request = new ResolveReportRequest
            {
                Id = Guid.NewGuid(),
                IsBanned = true,
                Duration = null
            };

            // Act
            var command = _mapper.ToResolve(request);

            // Assert
            await Assert.That(command.Id).IsEqualTo(request.Id);
            await Assert.That(command.IsBanned).IsTrue();
            await Assert.That(command.Duration).IsNull();
        }

        [Test]
        public async Task ToResolve_WhenRejected_MapsIsBannedFalseAndNullDuration()
        {
            // Arrange
            var request = new ResolveReportRequest
            {
                Id = Guid.NewGuid(),
                IsBanned = false,
                Duration = null
            };

            // Act
            var command = _mapper.ToResolve(request);

            // Assert
            await Assert.That(command.Id).IsEqualTo(request.Id);
            await Assert.That(command.IsBanned).IsFalse();
            await Assert.That(command.Duration).IsNull();
        }

        [Test]
        public async Task ToResolve_DoesNotModifyEmptyId()
        {
            // Arrange
            var request = new ResolveReportRequest
            {
                Id = Guid.Empty,
                IsBanned = false,
                Duration = null
            };

            // Act
            var command = _mapper.ToResolve(request);

            // Assert
            await Assert.That(command.Id).IsEqualTo(Guid.Empty);
        }

        #endregion

        #region Ban Mapping Tests

        [Test]
        public async Task ToBan_WithTemporaryDuration_MapsAllProperties()
        {
            // Arrange
            var duration = TimeSpan.FromDays(7);
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "Spam",
                Duration = duration
            };

            // Act
            var command = _mapper.ToBan(request);

            // Assert
            await Assert.That(command.UserId).IsEqualTo(request.UserId);
            await Assert.That(command.Reason).IsEqualTo("Spam");
            await Assert.That(command.Duration).IsEqualTo(duration);
        }

        [Test]
        public async Task ToBan_WithPermanentBan_MapsNullDuration()
        {
            // Arrange
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "Severe violation",
                Duration = null
            };

            // Act
            var command = _mapper.ToBan(request);

            // Assert
            await Assert.That(command.UserId).IsEqualTo(request.UserId);
            await Assert.That(command.Reason).IsEqualTo("Severe violation");
            await Assert.That(command.Duration).IsNull();
        }

        [Test]
        public async Task ToBan_TrimsWhitespaceFromReason()
        {
            // Arrange
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "   Harassment in comments   ",
                Duration = TimeSpan.FromDays(1)
            };

            // Act
            var command = _mapper.ToBan(request);

            // Assert
            await Assert.That(command.Reason).IsEqualTo("Harassment in comments");
        }

        [Test]
        public async Task ToBan_TrimsTabsAndNewLinesFromReason()
        {
            // Arrange
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "\t\n Fake profile \r\n",
                Duration = null
            };

            // Act
            var command = _mapper.ToBan(request);

            // Assert
            await Assert.That(command.Reason).IsEqualTo("Fake profile");
        }

        [Test]
        public async Task ToBan_PreservesWhitespaceInsideReason()
        {
            // Arrange
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "  Too   many   spaces  ",
                Duration = null
            };

            // Act
            var command = _mapper.ToBan(request);

            // Assert
            await Assert.That(command.Reason).IsEqualTo("Too   many   spaces");
        }

        [Test]
        public async Task ToBan_WhenReasonIsWhitespace_ReturnsEmptyString()
        {
            // Arrange
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "     ",
                Duration = null
            };

            // Act
            var command = _mapper.ToBan(request);

            // Assert
            await Assert.That(command.Reason).IsEqualTo(string.Empty);
        }

        [Test]
        public async Task ToBan_DoesNotModifyEmptyUserId()
        {
            // Arrange
            var request = new BanUserRequest
            {
                UserId = Guid.Empty,
                Reason = "Spam",
                Duration = null
            };

            // Act
            var command = _mapper.ToBan(request);

            // Assert
            await Assert.That(command.UserId).IsEqualTo(Guid.Empty);
        }

        #endregion
    }
}
