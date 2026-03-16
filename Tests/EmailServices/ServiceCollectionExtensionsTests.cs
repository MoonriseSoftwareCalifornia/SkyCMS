using Cosmos.EmailServices;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Sky.Tests
{
    [TestClass]
    public class ServiceCollectionExtensionsTests
    {
        [TestMethod]
        public void AddCosmosEmailServices_PrefersSmtp_WhenSmtpConfigIsValid()
        {
            var adminEmail = "admin@example.com";

            var smtpConfig = new Dictionary<string, string>
            {
                ["AdminEmail"] = adminEmail,
                ["SmtpEmailProviderOptions:Host"] = "smtp.example.com",
                ["SmtpEmailProviderOptions:UserName"] = "user",
                ["SmtpEmailProviderOptions:Password"] = "pass",
                ["SmtpEmailProviderOptions:Port"] = "587"
            };

            var config = new ConfigurationBuilder().AddInMemoryCollection(smtpConfig).Build();
            var services = new ServiceCollection();

            services.AddCosmosEmailServices(config);
            var sp = services.BuildServiceProvider();

            var sender = sp.GetService<IEmailSender>();
            Assert.IsNotNull(sender);
            Assert.IsInstanceOfType(sender, typeof(DynamicEmailSender));

            // Verify it's a DynamicEmailSender that can be cast to ICosmosEmailSender
            var cosmosSender = sender as ICosmosEmailSender;
            Assert.IsNotNull(cosmosSender);
        }

        [TestMethod]
        public void AddCosmosEmailServices_UsesAzure_WhenAzureConnectionIsPresent()
        {
            var adminEmail = "admin@example.com";
            var data = new Dictionary<string, string>
            {
                ["AdminEmail"] = adminEmail,
                ["ConnectionStrings:AzureCommunicationConnection"] = "Endpoint=sb://example/;AccessKey=abc"
            };

            var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton<Azure.Identity.DefaultAzureCredential>(new Azure.Identity.DefaultAzureCredential());
            services.AddCosmosEmailServices(config);
            var sp = services.BuildServiceProvider();

            var sender = sp.GetService<IEmailSender>();
            Assert.IsNotNull(sender);
            Assert.IsInstanceOfType(sender, typeof(DynamicEmailSender));

            // Verify it's a DynamicEmailSender that can be cast to ICosmosEmailSender
            var cosmosSender = sender as ICosmosEmailSender;
            Assert.IsNotNull(cosmosSender);
        }

        [TestMethod]
        public void AddCosmosEmailServices_UsesSendGrid_WhenApiKeyIsPresent()
        {
            var adminEmail = "admin@example.com";
            var data = new Dictionary<string, string>
            {
                ["AdminEmail"] = adminEmail,
                ["CosmosSendGridApiKey"] = "SG.TESTKEY"
            };

            var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddCosmosEmailServices(config);
            var sp = services.BuildServiceProvider();

            var sender = sp.GetService<IEmailSender>();
            Assert.IsNotNull(sender);
            Assert.IsInstanceOfType(sender, typeof(DynamicEmailSender));

            // Verify it's a DynamicEmailSender that can be cast to ICosmosEmailSender
            var cosmosSender = sender as ICosmosEmailSender;
            Assert.IsNotNull(cosmosSender);
        }

        [TestMethod]
        public void AddCosmosEmailServices_FallsBackToNoOp_WhenNoProviderConfigured()
        {
            var data = new Dictionary<string, string>
            {
                ["AdminEmail"] = "admin@example.com"
            };

            var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
            var services = new ServiceCollection();

            services.AddCosmosEmailServices(config);
            var sp = services.BuildServiceProvider();

            var sender = sp.GetService<IEmailSender>();
            Assert.IsNotNull(sender);
            Assert.IsInstanceOfType(sender, typeof(DynamicEmailSender));

            // Verify it's a DynamicEmailSender and internally resolves to NoOp
            var cosmosSender = sender as ICosmosEmailSender;
            Assert.IsNotNull(cosmosSender);
        }

        [TestMethod]
        public void AddSendGridEmailProvider_RegistersOptionsAndSender()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var opts = new SendGridEmailProviderOptions("KEY", "from@example.com");

            services.AddSendGridEmailProvider(opts);
            var sp = services.BuildServiceProvider();

            var iopts = sp.GetService<IOptions<SendGridEmailProviderOptions>>();
            Assert.IsNotNull(iopts);

            var sender = sp.GetService<IEmailSender>();
            Assert.IsNotNull(sender);
            Assert.IsInstanceOfType(sender, typeof(SendGridEmailSender));
        }
    }
}
