using Api.Mappers;
using Api.Request.Auth;

namespace Tests.Mappers;

public class AuthMapperTests
{
    private readonly AuthMapper _mapper = new();

    #region Login Mapping Tests

    [Test]
    public async Task ToLoginCommand_MapsEmailAndPassword()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "user@example.com",
            Password = "Password123!"
        };

        // Act
        var command = _mapper.ToLoginCommand(request);

        // Assert
        await Assert.That(command.Email).IsEqualTo(request.Email);
        await Assert.That(command.Password).IsEqualTo(request.Password);
    }

    #endregion

    #region Register Mapping Tests

    [Test]
    public async Task ToRegisterCommand_MapsAllPropertiesAndFormatsNames()
    {
        // Arrange
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
        var command = _mapper.ToRegisterCommand(request);

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
        var command = _mapper.ToRegisterCommand(request);

        // Assert
        await Assert.That(command.FirstName).IsEqualTo(string.Empty);
        await Assert.That(command.LastName).IsEqualTo(string.Empty);
        await Assert.That(command.Email).IsEqualTo("user@example.com");
        await Assert.That(command.Bio).IsNull();
    }

    #endregion

    #region ConfirmEmail Mapping Tests

    [Test]
    public async Task ToConfirmEmail_MapsPropertiesAndTrimsWhitespace()
    {
        // Arrange
        var request = new ConfirmEmailRequest
        {
            Email = "   jan.kowalski@example.com   ",
            Code = "  123456  "
        };

        // Act
        var command = _mapper.ToConfirmEmail(request);

        // Assert
        await Assert.That(command.Email).IsEqualTo("jan.kowalski@example.com");
        await Assert.That(command.Code).IsEqualTo("123456");
    }

    [Test]
    public async Task ToConfirmEmail_WhenAlreadyClean_PreservesExactValues()
    {
        // Arrange
        var request = new ConfirmEmailRequest
        {
            Email = "user@example.com",
            Code = "654321"
        };

        // Act
        var command = _mapper.ToConfirmEmail(request);

        // Assert
        await Assert.That(command.Email).IsEqualTo("user@example.com");
        await Assert.That(command.Code).IsEqualTo("654321");
    }

    #endregion
}