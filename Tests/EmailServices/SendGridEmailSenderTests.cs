using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cosmos.EmailServices;

namespace Sky.Tests
{
    [TestClass]
    public class SendGridEmailSenderTests : SkyCmsTestBase
    {
        [TestMethod]
        public void SandboxMode_PropertyReflectsOptions()
        {
            var options = Options.Create(new SendGridEmailProviderOptions
            {
                SandboxMode = true,
                DefaultFromEmailAddress = "noreply@example.com",
                LogErrors = false
            });

            var sender = new SendGridEmailSender(options, new NullLogger<SendGridEmailSender>());

            Assert.IsTrue(sender.SandboxMode);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task SendEmailAsync_WhenClientThrows_SetsBadRequestResult()
        {
            // Arrange: give obviously invalid configuration so the internal SendGrid client fails.
            var options = Options.Create(new SendGridEmailProviderOptions
            {
                ApiKey = "INVALID_API_KEY",
                DefaultFromEmailAddress = "noreply@example.com",
                SandboxMode = false,
                LogErrors = false
            });

            var sender = new SendGridEmailSender(options, new NullLogger<SendGridEmailSender>());

            // Act
            await sender.SendEmailAsync("to@example.com", "test", "<p>hi</p>");

            // Assert: on exception the implementation sets BadRequest
            Assert.AreEqual(HttpStatusCode.Unauthorized, sender.SendResult.StatusCode);
            Assert.IsFalse(string.IsNullOrEmpty(sender.SendResult.Message));
        }
    }
}
