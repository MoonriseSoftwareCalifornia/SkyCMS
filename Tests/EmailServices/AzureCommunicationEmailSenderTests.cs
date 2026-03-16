using Cosmos.EmailServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace Sky.Tests
{
    [TestClass]
    public class AzureCommunicationEmailSenderTests
    {
        [TestMethod]
        public void Constructor_Initializes_SendResult()
        {
            var options = Options.Create(new AzureCommunicationEmailProviderOptions
            {
                ConnectionString = "endpoint=https://example/;AccessKey=abc",
                DefaultFromEmailAddress = "noreply@example.com"
            });

            var sender = new AzureCommunicationEmailSender(options, new NullLogger<AzureCommunicationEmailSender>(), null!);

            Assert.IsNotNull(sender.SendResult);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task SendEmailAsync_WhenAccessKeyMissing_SetsInternalServerError()
        {
            var options = Options.Create(new AzureCommunicationEmailProviderOptions
            {
                // Connection string without AccessKey
                ConnectionString = "endpoint=https://example/;",
                DefaultFromEmailAddress = "noreply@example.com"
            });

            var sender = new AzureCommunicationEmailSender(options, new NullLogger<AzureCommunicationEmailSender>(), null!);

            await sender.SendEmailAsync("to@example.com", "subj", "<p>hi</p>");

            Assert.AreEqual(HttpStatusCode.InternalServerError, sender.SendResult.StatusCode);
            StringAssert.Contains(sender.SendResult.Message, "AccessKey not found");
        }
    }
}
