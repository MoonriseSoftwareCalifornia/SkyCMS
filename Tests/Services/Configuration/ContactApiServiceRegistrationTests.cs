// <copyright file="ContactApiServiceRegistrationTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using Cosmos.Common.Data;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Services.Email;
using Cosmos.EmailServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Sky.Cms.Api.Shared.Extensions;
using Sky.Cms.Api.Shared.Features.ContactForm.Submit;
using Sky.Cms.Api.Shared.Features.ContactForm.ValidateCaptcha;
using Sky.Cms.Api.Shared.Models;
using Sky.Cms.Api.Shared.Services;
using Sky.Cms.Api.Shared.Services.Captcha;

namespace Sky.Tests.Services.Configuration
{
    /// <summary>
    /// Priority 3 tests for AddContactApi service registration.
    /// Tests service registration, configuration binding, and dependency injection setup.
    /// </summary>
    [TestClass]
    public class ContactApiServiceRegistrationTests
    {
        [TestMethod]
        public void AddContactApi_RegistersContactApiConfiguration()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration();

            // Act
            services.AddContactApi(configuration);
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var options = serviceProvider.GetService<IOptions<ContactApiConfig>>();
            Assert.IsNotNull(options, "ContactApiConfig should be registered");
            Assert.IsNotNull(options.Value, "ContactApiConfig value should not be null");
        }

        [TestMethod]
        public void AddContactApi_BindsConfigurationFromContactApiSection()
        {
            // Arrange
            var services = new ServiceCollection();
            var configValues = new Dictionary<string, string>
            {
                { "ContactApi:CaptchaProvider", "turnstile" },
                { "ContactApi:CaptchaSiteKey", "test-site-key" },
                { "ContactApi:CaptchaSecretKey", "test-secret-key" },
                { "ContactApi:AdminEmail", "test@example.com" },
                { "ContactApi:MaxMessageLength", "5000" }
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            // Act
            services.AddContactApi(configuration);
            var serviceProvider = services.BuildServiceProvider();
            var config = serviceProvider.GetRequiredService<IOptions<ContactApiConfig>>().Value;

            // Assert
            Assert.AreEqual("turnstile", config.CaptchaProvider, "CaptchaProvider should be bound from configuration");
            Assert.AreEqual("test-site-key", config.CaptchaSiteKey, "CaptchaSiteKey should be bound from configuration");
            Assert.AreEqual("test-secret-key", config.CaptchaSecretKey, "CaptchaSecretKey should be bound from configuration");
            Assert.AreEqual("test@example.com", config.AdminEmail, "AdminEmail should be bound from configuration");
            Assert.AreEqual(5000, config.MaxMessageLength, "MaxMessageLength should be bound from configuration");
        }

        [TestMethod]
        public void AddContactApi_RegistersHttpClientFactory()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration();

            // Act
            services.AddContactApi(configuration);
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
            Assert.IsNotNull(httpClientFactory, "IHttpClientFactory should be registered for CAPTCHA validation");
        }

        [TestMethod]
        public void AddContactApi_RegistersMediator()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration();

            // Act
            services.AddContactApi(configuration);
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var mediator = serviceProvider.GetService<IMediator>();
            Assert.IsNotNull(mediator, "IMediator should be registered for CQRS pattern");
        }

        [TestMethod]
        public void AddContactApi_RegistersContactService()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration();

            // Act
            services.AddContactApi(configuration);
            RegisterMissingDependencies(services);
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var contactService = serviceProvider.GetService<IContactService>();
            Assert.IsNotNull(contactService, "IContactService should be registered");
            Assert.IsInstanceOfType(contactService, typeof(ContactService), "IContactService should resolve to ContactService");
        }

        [TestMethod]
        public void AddContactApi_RegistersCaptchaValidator()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration();

            // Act
            services.AddContactApi(configuration);
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var captchaValidator = serviceProvider.GetService<ICaptchaValidator>();
            Assert.IsNotNull(captchaValidator, "ICaptchaValidator should be registered");
            Assert.IsInstanceOfType(captchaValidator, typeof(NoOpCaptchaValidator),
                "ICaptchaValidator should default to NoOpCaptchaValidator");
        }

        [TestMethod]
        public void AddContactApi_RegistersSubmitContactFormHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration();

            // Act
            services.AddContactApi(configuration);
            RegisterMissingDependencies(services);
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var handler = serviceProvider.GetService<ICommandHandler<SubmitContactFormCommand, CommandResult<ContactFormResponse>>>();
            Assert.IsNotNull(handler, "SubmitContactFormHandler should be registered");
            Assert.IsInstanceOfType(handler, typeof(SubmitContactFormHandler),
                "Handler should resolve to SubmitContactFormHandler");
        }

        [TestMethod]
        public void AddContactApi_RegistersValidateCaptchaHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration();

            // Act
            services.AddContactApi(configuration);
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var handler = serviceProvider.GetService<IQueryHandler<ValidateCaptchaQuery, bool>>();
            Assert.IsNotNull(handler, "ValidateCaptchaHandler should be registered");
            Assert.IsInstanceOfType(handler, typeof(ValidateCaptchaHandler),
                "Handler should resolve to ValidateCaptchaHandler");
        }

        [TestMethod]
        public void AddContactApi_AllServicesAreScopedLifetime()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration();

            // Act
            services.AddContactApi(configuration);

            // Assert - Verify all services are registered with Scoped lifetime
            var scopedServices = services.Where(s => s.Lifetime == ServiceLifetime.Scoped).ToList();

            Assert.IsTrue(scopedServices.Any(s => s.ServiceType == typeof(IMediator)),
                "IMediator should be registered as Scoped");
            Assert.IsTrue(scopedServices.Any(s => s.ServiceType == typeof(IContactService)),
                "IContactService should be registered as Scoped");
            Assert.IsTrue(scopedServices.Any(s => s.ServiceType == typeof(ICaptchaValidator)),
                "ICaptchaValidator should be registered as Scoped");
            Assert.IsTrue(scopedServices.Any(s => s.ServiceType == typeof(ICommandHandler<SubmitContactFormCommand, CommandResult<ContactFormResponse>>)),
                "SubmitContactFormHandler should be registered as Scoped");
            Assert.IsTrue(scopedServices.Any(s => s.ServiceType == typeof(IQueryHandler<ValidateCaptchaQuery, bool>)),
                "ValidateCaptchaHandler should be registered as Scoped");
        }

        [TestMethod]
        public void AddContactApi_ReturnsServiceCollectionForChaining()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration();

            // Act
            var result = services.AddContactApi(configuration);

            // Assert
            Assert.AreSame(services, result, "AddContactApi should return the same IServiceCollection for method chaining");
        }

        [TestMethod]
        public void AddContactApi_CanBeCalledMultipleTimes()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration();

            // Act - Call multiple times (simulating multiple registrations)
            services.AddContactApi(configuration);
            services.AddContactApi(configuration);
            RegisterMissingDependencies(services);

            // Assert - Should not throw
            var serviceProvider = services.BuildServiceProvider();
            var contactService = serviceProvider.GetService<IContactService>();
            Assert.IsNotNull(contactService, "Services should still be resolvable after multiple registrations");
        }

        [TestMethod]
        public void AddContactApi_WithEmptyConfiguration_StillRegistersServices()
        {
            // Arrange
            var services = new ServiceCollection();
            var emptyConfiguration = new ConfigurationBuilder().Build();

            // Act
            services.AddContactApi(emptyConfiguration);
            RegisterMissingDependencies(services);
            var serviceProvider = services.BuildServiceProvider();

            // Assert - Core services should still be registered
            Assert.IsNotNull(serviceProvider.GetService<IContactService>(), "IContactService should be registered");
            Assert.IsNotNull(serviceProvider.GetService<ICaptchaValidator>(), "ICaptchaValidator should be registered");
            Assert.IsNotNull(serviceProvider.GetService<IMediator>(), "IMediator should be registered");
        }

        [TestMethod]
        public void AddContactApi_Configuration_SupportsNullValues()
        {
            // Arrange
            var services = new ServiceCollection();
            var configValues = new Dictionary<string, string>
            {
                { "ContactApi:CaptchaProvider", null },
                { "ContactApi:AdminEmail", null }
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            // Act
            services.AddContactApi(configuration);
            var serviceProvider = services.BuildServiceProvider();
            var config = serviceProvider.GetRequiredService<IOptions<ContactApiConfig>>().Value;

            // Assert
            Assert.IsNotNull(config, "Configuration should be created even with null values");
        }

        [TestMethod]
        public void AddContactApi_RegistersAllRequiredDependencies()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = CreateConfiguration();

            // Act
            services.AddContactApi(configuration);
            RegisterMissingDependencies(services);
            var serviceProvider = services.BuildServiceProvider();

            // Assert - Verify all critical dependencies can be resolved
            var dependencies = new[]
            {
                typeof(IOptions<ContactApiConfig>),
                typeof(IHttpClientFactory),
                typeof(IMediator),
                typeof(IContactService),
                typeof(ICaptchaValidator),
                typeof(ICommandHandler<SubmitContactFormCommand, CommandResult<ContactFormResponse>>),
                typeof(IQueryHandler<ValidateCaptchaQuery, bool>)
            };

            foreach (var dependency in dependencies)
            {
                var service = serviceProvider.GetService(dependency);
                Assert.IsNotNull(service, $"{dependency.Name} should be resolvable from DI container");
            }
        }

        #region Helper Methods

        private static IConfiguration CreateConfiguration()
        {
            var configValues = new Dictionary<string, string>
            {
                { "ContactApi:CaptchaProvider", "turnstile" },
                { "ContactApi:CaptchaSiteKey", "test-site-key" },
                { "ContactApi:CaptchaSecretKey", "test-secret-key" },
                { "ContactApi:AdminEmail", "test@example.com" },
                { "ContactApi:MaxMessageLength", "5000" }
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();
        }

        private static void RegisterMissingDependencies(IServiceCollection services)
        {
            // Register ICosmosEmailSender (required by ContactService and SubmitContactFormHandler)
            services.AddScoped<ICosmosEmailSender, CosmosNoOpEmailSender>();

            // Register ApplicationDbContext with in-memory database (required by SubmitContactFormHandler)
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase($"ContactApiTestDb_{Guid.NewGuid()}"));

            // Register IEmailConfigurationService mock (required by SubmitContactFormHandler)
            var mockEmailConfigService = new Mock<IEmailConfigurationService>();
            mockEmailConfigService.Setup(x => x.GetEmailSettingsAsync())
                .ReturnsAsync(new EmailSettings
                {
                    SenderEmail = "test@example.com",
                    IsConfigured = true
                });
            services.AddScoped(_ => mockEmailConfigService.Object);

            // Register logging (commonly needed)
            services.AddLogging();
        }

        #endregion
    }
}
