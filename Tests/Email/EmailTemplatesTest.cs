using Email.Templates;

namespace Tests.Email
{
    public class EmailTemplatesTest
    {
        [Test]
        public async Task EmailChangeCode_ShouldContainCodeAndValidityInBothBodies()
        {
            // Act
            var content = EmailTemplates.EmailChangeCode("Jan", "482913", 15);

            // Assert
            await Assert.That(content.Subject).IsEqualTo("Confirm your new email address");
            await Assert.That(content.HtmlBody).Contains("482913");
            await Assert.That(content.HtmlBody).Contains("15 minutes");
            await Assert.That(content.TextBody).Contains("482913");
            await Assert.That(content.TextBody).Contains("15 minutes");
        }

        [Test]
        public async Task EmailChangedNotification_ShouldContainNewEmailInBothBodies()
        {
            // Act
            var content = EmailTemplates.EmailChangedNotification("Jan", "new@example.com");

            // Assert
            await Assert.That(content.Subject).IsEqualTo("Your email address has been changed");
            await Assert.That(content.HtmlBody).Contains("new@example.com");
            await Assert.That(content.TextBody).Contains("new@example.com");
        }

        [Test]
        public async Task Templates_ShouldHtmlEncodeUserProvidedValues()
        {
            // Arrange
            const string maliciousName = "<script>alert('x')</script>";

            // Act
            var codeEmail = EmailTemplates.EmailChangeCode(maliciousName, "123456", 15);
            var notification = EmailTemplates.EmailChangedNotification(maliciousName, "a<b>@example.com");

            // Assert
            await Assert.That(codeEmail.HtmlBody).DoesNotContain("<script>");
            await Assert.That(codeEmail.HtmlBody).Contains("&lt;script&gt;");
            await Assert.That(notification.HtmlBody).DoesNotContain("<script>");
            await Assert.That(notification.HtmlBody).DoesNotContain("a<b>@example.com");
        }
    }
}
