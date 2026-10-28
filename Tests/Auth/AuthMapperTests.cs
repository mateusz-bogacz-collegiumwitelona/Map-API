using Api.Mappers;
using Api.Request.Auth;

namespace Tests.Auth;

public class AuthMapperTests
{
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
}
