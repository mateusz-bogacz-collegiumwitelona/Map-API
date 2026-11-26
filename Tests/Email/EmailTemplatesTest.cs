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
            var registrationEmail = EmailTemplates.RegistrationVerificationCode(maliciousName, "654321", 240);
            var codeEmail = EmailTemplates.EmailChangeCode(maliciousName, "123456", 15);
            var notification = EmailTemplates.EmailChangedNotification(maliciousName, "a<b>@example.com");

            // Assert
            await Assert.That(registrationEmail.HtmlBody).DoesNotContain("<script>");
            await Assert.That(registrationEmail.HtmlBody).Contains("&lt;script&gt;");

            await Assert.That(codeEmail.HtmlBody).DoesNotContain("<script>");
            await Assert.That(codeEmail.HtmlBody).Contains("&lt;script&gt;");

            await Assert.That(notification.HtmlBody).DoesNotContain("<script>");
            await Assert.That(notification.HtmlBody).DoesNotContain("a<b>@example.com");
        }

        [Test]
        public async Task RegistrationVerificationCode_ShouldContainCodeAndValidityInBothBodies()
        {
            // Act
            var content = EmailTemplates.RegistrationVerificationCode("Jan", "654321", 240);

            // Assert
            await Assert.That(content.Subject).IsEqualTo("Verify your account");
            await Assert.That(content.HtmlBody).Contains("Jan");
            await Assert.That(content.HtmlBody).Contains("654321");
            await Assert.That(content.HtmlBody).Contains("240 minutes");
            await Assert.That(content.TextBody).Contains("Jan");
            await Assert.That(content.TextBody).Contains("654321");
            await Assert.That(content.TextBody).Contains("240 minutes");
        }

        [Test]
        public async Task AccountBannedNotification_WhenPermanent_ShouldContainReasonAndPermanentInfoInBothBodies()
        {
            // Act
            var content = EmailTemplates.AccountBannedNotification("Jan", "Severe harassment", null);

            // Assert
            await Assert.That(content.Subject).IsEqualTo("Your account has been suspended");
            await Assert.That(content.HtmlBody).Contains("Jan");
            await Assert.That(content.HtmlBody).Contains("Severe harassment");
            await Assert.That(content.HtmlBody).Contains("<strong>permanently</strong>");
            await Assert.That(content.TextBody).Contains("Jan");
            await Assert.That(content.TextBody).Contains("Severe harassment");
            await Assert.That(content.TextBody).Contains("permanently");
        }

        [Test]
        public async Task AccountBannedNotification_WhenTemporary_ShouldContainFormattedUtcDateAndReasonInBothBodies()
        {
            // Arrange
            var bannedUntil = new DateTime(2026, 12, 24, 15, 30, 0, DateTimeKind.Utc);

            // Act
            var content = EmailTemplates.AccountBannedNotification("Anna", "Spam links", bannedUntil);

            // Assert
            await Assert.That(content.Subject).IsEqualTo("Your account has been suspended");
            await Assert.That(content.HtmlBody).Contains("Anna");
            await Assert.That(content.HtmlBody).Contains("Spam links");
            await Assert.That(content.HtmlBody).Contains("2026-12-24 15:30 UTC");
            await Assert.That(content.HtmlBody).DoesNotContain("permanently");
            await Assert.That(content.TextBody).Contains("Anna");
            await Assert.That(content.TextBody).Contains("Spam links");
            await Assert.That(content.TextBody).Contains("2026-12-24 15:30 UTC");
            await Assert.That(content.TextBody).DoesNotContain("permanently");
        }
    }
}
