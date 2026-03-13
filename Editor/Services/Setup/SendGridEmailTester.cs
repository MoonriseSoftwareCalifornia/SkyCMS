// <copyright file="SendGridEmailTester.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using System.Threading.Tasks;

    /// <summary>
    /// Default runtime implementation for SendGrid configuration testing.
    /// </summary>
    public class SendGridEmailTester : ISendGridEmailTester
    {
        /// <inheritdoc/>
        public async Task<TestResult> TestAsync(string apiKey, string senderEmail, string recipient)
        {
            var client = new SendGrid.SendGridClient(apiKey);
            var from = new SendGrid.Helpers.Mail.EmailAddress(senderEmail, "SkyCMS Setup");
            var to = new SendGrid.Helpers.Mail.EmailAddress(recipient);
            var msg = SendGrid.Helpers.Mail.MailHelper.CreateSingleEmail(
                from,
                to,
                "SkyCMS Setup Test Email",
                "This is a test email from SkyCMS setup wizard.",
                "<p>This is a test email from SkyCMS setup wizard.</p>");

            var response = await client.SendEmailAsync(msg);

            if (response.IsSuccessStatusCode)
            {
                return new TestResult
                {
                    Success = true,
                    Message = $"Test email sent successfully to {recipient}"
                };
            }

            var body = await response.Body.ReadAsStringAsync();
            return new TestResult
            {
                Success = false,
                Message = $"SendGrid returned status {response.StatusCode}: {body}"
            };
        }
    }
}
