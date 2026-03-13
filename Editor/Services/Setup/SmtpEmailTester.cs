// <copyright file="SmtpEmailTester.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using System.Net.Mail;
    using System.Threading.Tasks;

    /// <summary>
    /// Default runtime implementation for SMTP configuration testing.
    /// </summary>
    public class SmtpEmailTester : ISmtpEmailTester
    {
        /// <inheritdoc/>
        public async Task<TestResult> TestAsync(string host, string port, string username, string password, string senderEmail, string recipient)
        {
            using var client = new SmtpClient(host, int.Parse(port));
            client.EnableSsl = port == "587" || port == "465";
            client.UseDefaultCredentials = false;
            client.Credentials = new System.Net.NetworkCredential(username, password);

            var message = new MailMessage(senderEmail, recipient)
            {
                Subject = "SkyCMS Setup Test Email",
                Body = "This is a test email from SkyCMS setup wizard.",
                IsBodyHtml = false
            };

            await client.SendMailAsync(message);

            return new TestResult
            {
                Success = true,
                Message = $"Test email sent successfully to {recipient}"
            };
        }
    }
}
