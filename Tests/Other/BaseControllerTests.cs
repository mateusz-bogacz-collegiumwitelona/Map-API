using Api.Controllers.Base;
using Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace Tests.Other
{
    public class BaseControllerTests
    {
        private TestController _sut = null!;


        [Before(Test)]
        public void Setup()
        {
            _sut = new TestController
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        private static JsonElement ToJsonElement(object? value)
        {
            var json = JsonSerializer.Serialize(value);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        // ─── CurrentUserId ─────────────────────────────

        [Test]
        public async Task CurrentUserId_WhenClaimExists_ReturnsParsedGuid()
        {
            // Arrange
            var expectedUserId = Guid.NewGuid();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, expectedUserId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            _sut.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);

            // Act
            var actualUserId = _sut.ExposedCurrentUserId;

            // Assert
            await Assert.That(actualUserId).IsEqualTo(expectedUserId);
        }

        [Test]
        public async Task CurrentUserId_WhenClaimIsMissing_ThrowsArgumentNullException()
        {
            // Arrange
            _sut.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            // Act & Assert
            await Assert.That(() => _sut.ExposedCurrentUserId).Throws<ArgumentNullException>();
        }

        // ─── HandleResult(Result) ──────────────

        [Test]
        public async Task HandleResult_WhenResultIsNull_Returns500WithMessage()
        {
            // Act
            var actionResult = _sut.ExecuteHandleResult((Result)null!);

            // Assert
            var objectResult = actionResult as ObjectResult;
            await Assert.That(objectResult).IsNotNull();
            await Assert.That(objectResult!.StatusCode).IsEqualTo(500);

            var json = ToJsonElement(objectResult.Value);
            await Assert.That(json.GetProperty("success").GetBoolean()).IsFalse();
            await Assert.That(json.GetProperty("message").GetString()).IsEqualTo("Result cannot be null.");
        }

        [Test]
        public async Task HandleResult_WhenSuccess_ReturnsStatusCodeAndPayload()
        {
            // Arrange
            var result = Result.Success("Operation successful", StatusCodes.Status200OK);

            // Act
            var actionResult = _sut.ExecuteHandleResult(result);

            // Assert
            var objectResult = actionResult as ObjectResult;
            await Assert.That(objectResult).IsNotNull();
            await Assert.That(objectResult!.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var json = ToJsonElement(objectResult.Value);
            await Assert.That(json.GetProperty("success").GetBoolean()).IsTrue();
            await Assert.That(json.GetProperty("message").GetString()).IsEqualTo("Operation successful");
        }

        [Test]
        public async Task HandleResult_WhenFailure_ReturnsErrorStatusCodeErrorCodeAndErrors()
        {
            // Arrange
            var errors = new List<string> { "Field is required", "Invalid format" };
            var result = Result.Failure("Validation failed", "VAL_001", StatusCodes.Status400BadRequest, errors);

            // Act
            var actionResult = _sut.ExecuteHandleResult(result);

            // Assert
            var objectResult = actionResult as ObjectResult;
            await Assert.That(objectResult).IsNotNull();
            await Assert.That(objectResult!.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);

            var json = ToJsonElement(objectResult.Value);
            await Assert.That(json.GetProperty("success").GetBoolean()).IsFalse();
            await Assert.That(json.GetProperty("message").GetString()).IsEqualTo("Validation failed");
            await Assert.That(json.GetProperty("errorCode").GetString()).IsEqualTo("VAL_001");

            var errorElements = json.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();
            await Assert.That(errorElements.Count).IsEqualTo(2);
            await Assert.That(errorElements).Contains("Field is required");
        }

        // ─── HandleResult<T>(Result<T>) ───

        [Test]
        public async Task HandleResultGeneric_WhenSuccess_ReturnsDataPayload()
        {
            // Arrange
            var payload = new { OrderId = 123, Total = 99.99m };
            var result = Result<object>.Success("Data retrieved", StatusCodes.Status200OK, payload);

            // Act
            var actionResult = _sut.ExecuteHandleResult(result);

            // Assert
            var objectResult = actionResult as ObjectResult;
            await Assert.That(objectResult).IsNotNull();
            await Assert.That(objectResult!.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var json = ToJsonElement(objectResult.Value);
            await Assert.That(json.GetProperty("success").GetBoolean()).IsTrue();
            await Assert.That(json.GetProperty("data").GetProperty("OrderId").GetInt32()).IsEqualTo(123);
        }

        [Test]
        public async Task HandleResultGeneric_WhenFailure_ReturnsErrorsAndOmitsData()
        {
            // Arrange
            var result = Result<string>.Failure("Resource not found", "NOT_FOUND", StatusCodes.Status404NotFound);

            // Act
            var actionResult = _sut.ExecuteHandleResult(result);

            // Assert
            var objectResult = actionResult as ObjectResult;
            await Assert.That(objectResult).IsNotNull();
            await Assert.That(objectResult!.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);

            var json = ToJsonElement(objectResult.Value);
            await Assert.That(json.GetProperty("success").GetBoolean()).IsFalse();
            await Assert.That(json.GetProperty("errorCode").GetString()).IsEqualTo("NOT_FOUND");

            await Assert.That(json.TryGetProperty("data", out _)).IsFalse();
        }

        private class TestController : BaseControlle
        {
            public Guid ExposedCurrentUserId => CurrentUserId;

            public IActionResult ExecuteHandleResult<T>(Result<T> result) => HandleResult(result);

            public IActionResult ExecuteHandleResult(Result result) => HandleResult(result);
        }
    }
}
