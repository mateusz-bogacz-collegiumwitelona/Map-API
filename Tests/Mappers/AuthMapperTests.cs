using Api.Mappers;
using Api.Request.Auth;

namespace Tests.Mappers;

public class AuthMapperTests
{
    #region ToLoginCommand
    [Test]
    public async Task ToLoginCommand_MapsEmailAndPassword()
    {
        // Arrange
        var mapper = new AuthMapper();
        var request = new LoginRequest
        {
            Email = "user@example.com",
            Password = "Password123!"
        };

        // Act
        var command = mapper.ToLoginCommand(request);

        // Assert
        await Assert.That(command.Email).IsEqualTo(request.Email);
        await Assert.That(command.Password).IsEqualTo(request.Password);
    }
    #endregion

    #region ToRegisterCommand
    [Test]
    public async Task ToRegisterCommand_MapsAllPropertiesAndFormatsNames()
    {
        // Arrange
        var mapper = new AuthMapper();
        var request = new RegisterRequest
        {
            FirstName = "  jan  ",
            LastName = "  kowalski  ",
            Email = "  jan.kowalski@example.com  ",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Bio = "Software Developer"
        };

        // Act
        var command = mapper.ToRegisterCommand(request);

        // Assert
        await Assert.That(command.FirstName).IsEqualTo("Jan");
        await Assert.That(command.LastName).IsEqualTo("Kowalski");
        await Assert.That(command.Email).IsEqualTo("jan.kowalski@example.com");
        await Assert.That(command.Password).IsEqualTo("Password123!");
        await Assert.That(command.Bio).IsEqualTo("Software Developer");
    }

    [Test]
    public async Task ToRegisterCommand_WhenNamesAreWhitespace_ReturnsEmptyStrings()
    {
        // Arrange
        var mapper = new AuthMapper();
        var request = new RegisterRequest
        {
            FirstName = "   ",
            LastName = "",
            Email = "user@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Bio = null
        };

        // Act
        var command = mapper.ToRegisterCommand(request);

        // Assert
        await Assert.That(command.FirstName).IsEqualTo(string.Empty);
        await Assert.That(command.LastName).IsEqualTo(string.Empty);
        await Assert.That(command.Email).IsEqualTo("user@example.com");
        await Assert.That(command.Bio).IsNull();
    }
    #endregion
}