using Api.Mappers;
using Api.Request.User;

namespace Tests.Mappers
{
    internal class UserMapperTest
    {
        private readonly UserMapper _mapper = new();

        [Test]
        public async Task MapEdit_WhenNamesHaveMixedCasesAndSpaces_ShouldTrimAndFormatToTitleCase()
        {
            // Arrange
            var request = new EditUserRequest
            {
                FirstName = "  jAN  ",
                LastName = "kOWALSKI",
                Bio = "Proste bio"
            };

            // Act
            var result = _mapper.MapEdit(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.FirstName).IsEqualTo("Jan");
            await Assert.That(result.LastName).IsEqualTo("Kowalski");
            await Assert.That(result.Bio).IsEqualTo("Proste bio");
        }

        [Test]
        public async Task MapEdit_WhenNamesAreWhitespaceOrEmpty_ShouldMapToNull()
        {
            // Arrange
            var request = new EditUserRequest
            {
                FirstName = "   ",
                LastName = string.Empty,
                Bio = null
            };

            // Act
            var result = _mapper.MapEdit(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.FirstName).IsNull();
            await Assert.That(result.LastName).IsNull();
            await Assert.That(result.Bio).IsNull();
        }
    }
}
