using Cosmos.EmailServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Sky.Tests
{
    [TestClass]
    public class SendGridEmailSenderIntegrationTests : SkyCmsTestBase
    {
        [TestMethod]
        public async System.Threading.Tasks.Task SendEmailAsync_HappyPath_WithRealSendGridCredentials()
        {
            var apiKey = Environment.GetEnvironmentVariable("SENDGRID_TEST_API_KEY");
            var toEmail = Environment.GetEnvironmentVariable("SENDGRID_TEST_EMAIL_TO");

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(toEmail))
            {
                Assert.Inconclusive("Set SENDGRID_TEST_API_KEY and SENDGRID_TEST_EMAIL_TO to run this integration test.");
                return;
            }

            var options = Options.Create(new SendGridEmailProviderOptions(apiKey, "integration-test@example.com")
            {
                SandboxMode = false,
                LogErrors = true
            });

            var sender = new SendGridEmailSender(options, new NullLogger<SendGridEmailSender>());

            await sender.SendEmailAsync(toEmail, "Integration test", "<p>integration test</p>");

            // Expect either Accepted (202) or another 2xx depending on provider.
            Assert.IsTrue(((int)sender.SendResult.StatusCode) >= 200 && ((int)sender.SendResult.StatusCode) < 300,
                $"Unexpected status code: {sender.SendResult.StatusCode}. Message: {sender.SendResult.Message}");
        }
    }
}
