using Cosmos.EmailServices;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Sky.Tests
{
    [TestClass]
    public class EmailHandlerTests
    {
        [TestMethod]
        public async Task SendCallbackTemplateEmail_CallsEmailSender_WithExpectedSubject()
        {
            var mockSender = new Mock<ICosmosEmailSender>();
            var handler = new EmailHandler(mockSender.Object, new NullLogger<EmailHandler>());

            var to = "user@example.com";
            await handler.SendCallbackTemplateEmail(EmailHandler.CallbackTemplate.ResetPasswordTemplate, "https://example.com/reset", "example.com", to, "ExampleSite", "from@example.com");

            mockSender.Verify(s => s.SendEmailAsync(
                It.Is<string>(t => t == to),
                It.Is<string>(sub => sub == "Password reset request"),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(f => f == "from@example.com")
            ), Times.Once);
        }

        [TestMethod]
        public async Task SendGeneralInfoTemplateEmail_SendsToSingleRecipient()
        {
            var mockSender = new Mock<ICosmosEmailSender>();
            var handler = new EmailHandler(mockSender.Object, new NullLogger<EmailHandler>());

            var to = "recipient@example.com";
            await handler.SendGeneralInfoTemplateEmail("Subject", "Subtitle", "SiteName", "example.com", "<p>Body</p>", to, "from@example.com");

            mockSender.Verify(s => s.SendEmailAsync(
                It.Is<string>(t => t == to),
                It.Is<string>(sub => sub == "Subject"),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(f => f == "from@example.com")
            ), Times.Once);
        }

        [TestMethod]
        public async Task SendGeneralInfoTemplateEmail_SendsToMultipleRecipients()
        {
            var mockSender = new Mock<ICosmosEmailSender>();
            var handler = new EmailHandler(mockSender.Object, new NullLogger<EmailHandler>());

            var recipients = new[] { "a@example.com", "b@example.com" };
            await handler.SendGeneralInfoTemplateEmail("Subject", "Subtitle", "SiteName", "example.com", "<p>Body</p>", recipients, "from@example.com");

            foreach (var r in recipients)
            {
                mockSender.Verify(s => s.SendEmailAsync(
                    It.Is<string>(t => t == r),
                    It.Is<string>(sub => sub == "Subject"),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.Is<string>(f => f == "from@example.com")
                ), Times.Once);
            }
        }
    }
}
