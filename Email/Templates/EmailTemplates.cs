using System.Net;

namespace Email.Templates
{
    public sealed record EmailContent(string Subject, string HtmlBody, string TextBody);

    public static class EmailTemplates
    {
        public static EmailContent EmailChangeCode(string firstName, string code, int validForMinutes)
        {
            const string subject = "Confirm your new email address";

            var html = Layout(subject, $"""
                <p>Hi {Encode(firstName)},</p>
                <p>Use the code below in the Map app to confirm your new email address:</p>
                <p style="font-size:28px;font-weight:bold;letter-spacing:6px;">{Encode(code)}</p>
                <p>The code is valid for {validForMinutes} minutes. Until you confirm it, you can still sign in with your current email address.</p>
                <p>If you did not request this change, you can ignore this message.</p>
                """);

            var text = $"""
                Hi {firstName},

                Use this code in the Map app to confirm your new email address: {code}

                The code is valid for {validForMinutes} minutes. Until you confirm it, you can still sign in with your current email address.

                If you did not request this change, you can ignore this message.
                """;

            return new EmailContent(subject, html, text);
        }

        public static EmailContent EmailChangedNotification(string firstName, string newEmail)
        {
            const string subject = "Your email address has been changed";

            var html = Layout(subject, $"""
                <p>Hi {Encode(firstName)},</p>
                <p>The email address of your Map account has been changed to <strong>{Encode(newEmail)}</strong>.</p>
                <p>From now on, use the new address to sign in.</p>
                <p>If you did not make this change, contact support immediately.</p>
                """);

            var text = $"""
                Hi {firstName},

                The email address of your Map account has been changed to {newEmail}.

                From now on, use the new address to sign in.

                If you did not make this change, contact support immediately.
                """;

            return new EmailContent(subject, html, text);
        }

        private static string Layout(string title, string content)
            => $"""
                <!DOCTYPE html>
                <html>
                <head><meta charset="utf-8"><title>{Encode(title)}</title></head>
                <body style="font-family:Arial,sans-serif;color:#222;line-height:1.5;">
                {content}
                <p style="color:#888;font-size:12px;">Map - My adventures &amp; places</p>
                </body>
                </html>
                """;

        private static string Encode(string value)
            => WebUtility.HtmlEncode(value);
    }
}
