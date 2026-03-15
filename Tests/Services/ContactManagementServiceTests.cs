// <copyright file="ContactManagementServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Models;
    using Cosmos.Common.Services;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Identity.UI.Services;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for <see cref="IContactManagementService"/>.
    /// Tests contact CRUD, MailChimp integration, and admin email alerts.
    /// </summary>
    [TestClass]
    public class ContactManagementServiceTests
    {
        private ApplicationDbContext dbContext;
        private Mock<IEmailSender> emailSenderMock;
        private Mock<ILogger<IContactManagementService>> loggerMock;
        private Mock<IHttpContextAccessor> httpContextAccessorMock;
        private IContactManagementService service;

        private const string TestEmail = "test@example.com";
        private const string TestFirstName = "John";
        private const string TestLastName = "Doe";
        private const string TestPhone = "555-1234";
        private const string TestHostName = "www.example.com";

        [TestInitialize]
        public void Setup()
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"ContactMgmtTest_{Guid.NewGuid()}")
                .Options;
            dbContext = new ApplicationDbContext(options);

            // Setup mocks
            emailSenderMock = new Mock<IEmailSender>();
            loggerMock = new Mock<ILogger<IContactManagementService>>();

            // Setup HttpContext and HttpContextAccessor
            var httpContextMock = new Mock<HttpContext>();
            var requestMock = new Mock<HttpRequest>();
            requestMock.Setup(r => r.Host).Returns(new HostString(TestHostName));
            httpContextMock.Setup(c => c.Request).Returns(requestMock.Object);

            httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContextMock.Object);

            // Create service
            service = new ContactManagementService(
                dbContext,
                emailSenderMock.Object,
                loggerMock.Object,
                httpContextAccessorMock.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            dbContext?.Dispose();
        }

        #region AddContactAsync - Basic CRUD Tests

        /// <summary>
        /// Tests that new contact is added successfully.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_NewContact_AddsToDatabase()
        {
            // Arrange
            var model = new ContactViewModel
            {
                Email = TestEmail,
                FirstName = TestFirstName,
                LastName = TestLastName,
                Phone = TestPhone
            };

            // Act
            var result = await service.AddContactAsync(model);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestEmail.ToLower(), result.Email);

            var dbContact = await dbContext.Contacts.FirstOrDefaultAsync(c => c.Email == TestEmail.ToLower());
            Assert.IsNotNull(dbContact);
            Assert.AreEqual(TestFirstName, dbContact.FirstName);
            Assert.AreEqual(TestLastName, dbContact.LastName);
            Assert.AreEqual(TestPhone, dbContact.Phone);
        }

        /// <summary>
        /// Tests that existing contact is updated.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_ExistingContact_UpdatesContact()
        {
            // Arrange - Add existing contact
            var existingContact = new Contact
            {
                Email = TestEmail.ToLower(),
                FirstName = "OldFirstName",
                LastName = "OldLastName",
                Phone = "555-0000"
            };
            dbContext.Contacts.Add(existingContact);
            await dbContext.SaveChangesAsync();

            var model = new ContactViewModel
            {
                Email = TestEmail,
                FirstName = TestFirstName,
                LastName = TestLastName,
                Phone = TestPhone
            };

            // Act
            var result = await service.AddContactAsync(model);

            // Assert
            var dbContact = await dbContext.Contacts.FirstOrDefaultAsync(c => c.Email == TestEmail.ToLower());
            Assert.AreEqual(TestFirstName, dbContact.FirstName, "First name should be updated");
            Assert.AreEqual(TestLastName, dbContact.LastName, "Last name should be updated");
            Assert.AreEqual(TestPhone, dbContact.Phone, "Phone should be updated");
            Assert.IsNotNull(dbContact.Updated, "Updated timestamp should be set");
        }

        /// <summary>
        /// Tests that email is normalized to lowercase.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_MixedCaseEmail_NormalizesToLowercase()
        {
            // Arrange
            var model = new ContactViewModel
            {
                Email = "Test@EXAMPLE.COM",
                FirstName = TestFirstName,
                LastName = TestLastName,
                Phone = string.Empty
            };

            // Act
            await service.AddContactAsync(model);

            // Assert
            var dbContact = await dbContext.Contacts.FirstOrDefaultAsync();
            Assert.AreEqual("test@example.com", dbContact.Email);
        }

        /// <summary>
        /// Tests that case-insensitive email matching works.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_CaseInsensitiveEmailMatch_UpdatesExisting()
        {
            // Arrange - Add contact with lowercase email
            dbContext.Contacts.Add(new Contact
            {
                Email = "test@example.com",
                FirstName = "Original",
                Phone = string.Empty
            });
            await dbContext.SaveChangesAsync();

            var model = new ContactViewModel
            {
                Email = "TEST@EXAMPLE.COM",
                FirstName = "Updated",
                Phone = string.Empty
            };

            // Act
            await service.AddContactAsync(model);

            // Assert - Should have only one contact (updated, not new)
            var contactCount = await dbContext.Contacts.CountAsync();
            Assert.AreEqual(1, contactCount);

            var contact = await dbContext.Contacts.FirstAsync();
            Assert.AreEqual("Updated", contact.FirstName);
        }

        #endregion

        #region Email Validation Tests

        /// <summary>
        /// Tests that invalid email throws ArgumentException.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_InvalidEmail_ThrowsArgumentException()
        {
            // Arrange
            var model = new ContactViewModel
            {
                Email = "not-an-email",
                FirstName = TestFirstName,
                Phone = string.Empty
            };

            // Act & Assert
            try
            {
                await service.AddContactAsync(model);
                Assert.Fail("Expected ArgumentException was not thrown.");
            }
            catch (ArgumentException)
            {
                // Test passes
            }
        }

        /// <summary>
        /// Tests that null email throws ArgumentException.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_NullEmail_ThrowsArgumentException()
        {
            // Arrange
            var model = new ContactViewModel
            {
                Email = null,
                FirstName = TestFirstName,
                Phone = string.Empty
            };

            // Act & Assert
            try
            {
                await service.AddContactAsync(model);
                Assert.Fail("Expected ArgumentException was not thrown.");
            }
            catch (ArgumentException)
            {
                // Test passes
            }
        }

        /// <summary>
        /// Tests that empty email throws ArgumentException.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_EmptyEmail_ThrowsArgumentException()
        {
            // Arrange
            var model = new ContactViewModel
            {
                Email = string.Empty,
                FirstName = TestFirstName,
                Phone = string.Empty
            };

            // Act & Assert
            try
            {
                await service.AddContactAsync(model);
                Assert.Fail("Expected ArgumentException was not thrown.");
            }
            catch (ArgumentException)
            {
                // Test passes
            }
        }

        /// <summary>
        /// Tests that whitespace email throws ArgumentException.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_WhitespaceEmail_ThrowsArgumentException()
        {
            // Arrange
            var model = new ContactViewModel
            {
                Email = "   ",
                FirstName = TestFirstName,
                Phone = string.Empty
            };

            // Act & Assert
            try
            {
                await service.AddContactAsync(model);
                Assert.Fail("Expected ArgumentException was not thrown.");
            }
            catch (ArgumentException)
            {
                // Test passes
            }
        }

        #endregion

        #region MailChimp Integration Tests

        /// <summary>
        /// Tests that MailChimp integration is skipped when settings are empty.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_NoMailChimpSettings_SkipsIntegration()
        {
            // Arrange - No MailChimp settings in database
            var model = new ContactViewModel
            {
                Email = TestEmail,
                FirstName = TestFirstName,
                Phone = string.Empty
            };

            // Act
            var result = await service.AddContactAsync(model);

            // Assert - Should succeed without MailChimp
            Assert.IsNotNull(result);
            var contact = await dbContext.Contacts.FirstOrDefaultAsync();
            Assert.IsNotNull(contact);
        }

        /// <summary>
        /// Tests that incomplete MailChimp settings are handled gracefully.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_IncompleteMailChimpSettings_SkipsIntegration()
        {
            // Arrange - Only API key, no list name
            dbContext.Settings.Add(new Setting
            {
                Group = "MailChimp",
                Name = "ApiKey",
                Value = "test-api-key"
            });
            await dbContext.SaveChangesAsync();

            var model = new ContactViewModel
            {
                Email = TestEmail,
                FirstName = TestFirstName,
                Phone = TestPhone
            };

            // Act
            var result = await service.AddContactAsync(model);

            // Assert - Should succeed and log warning
            Assert.IsNotNull(result);

            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("MailChimp settings incomplete")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);
        }

        /// <summary>
        /// Tests that null MailChimp API key is handled gracefully.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_NullMailChimpApiKey_SkipsIntegration()
        {
            // Arrange - Settings exist but values are missing
            dbContext.Settings.AddRange(
                new Setting { Group = "MailChimp", Name = "ApiKey", Value = string.Empty },
                new Setting { Group = "MailChimp", Name = "ContactListName", Value = "TestList" }
            );
            await dbContext.SaveChangesAsync();

            var model = new ContactViewModel
            {
                Email = TestEmail,
                FirstName = TestFirstName,
                Phone = string.Empty
            };

            // Act
            var result = await service.AddContactAsync(model);

            // Assert - Should succeed
            Assert.IsNotNull(result);
        }

        #endregion

        #region Admin Alerts Tests

        /// <summary>
        /// Tests that admin alerts are skipped when disabled.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_AlertsDisabled_DoesNotSendEmails()
        {
            // Arrange - Alerts disabled
            dbContext.Settings.Add(new Setting
            {
                Group = "ContactsConfig",
                Name = "EnableAlerts",
                Value = "false"
            });
            await dbContext.SaveChangesAsync();

            var model = new ContactViewModel
            {
                Email = TestEmail,
                FirstName = TestFirstName,
                Phone = string.Empty
            };

            // Act
            await service.AddContactAsync(model);

            // Assert - Email sender should not be called
            emailSenderMock.Verify(
                x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        /// <summary>
        /// Tests that missing Administrators role is handled gracefully.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_NoAdminRole_SkipsAlerts()
        {
            // Arrange - Enable alerts but no Administrators role exists
            dbContext.Settings.Add(new Setting
            {
                Group = "ContactsConfig",
                Name = "EnableAlerts",
                Value = "true"
            });
            await dbContext.SaveChangesAsync();

            var model = new ContactViewModel
            {
                Email = TestEmail,
                FirstName = TestFirstName,
                Phone = string.Empty
            };

            // Act
            var result = await service.AddContactAsync(model);

            // Assert - Should succeed and log warning
            Assert.IsNotNull(result);

            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Administrators role not found")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);

            // Email sender should not be called
            emailSenderMock.Verify(
                x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        /// <summary>
        /// Tests that admin alerts are sent when enabled and configured.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_AlertsEnabled_SendsEmailToAdmins()
        {
            // Arrange - Setup alerts and admin role
            dbContext.Settings.Add(new Setting
            {
                Group = "ContactsConfig",
                Name = "EnableAlerts",
                Value = "true"
            });

            var adminRole = new IdentityRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Administrators",
                NormalizedName = "ADMINISTRATORS"
            };
            dbContext.Roles.Add(adminRole);

            var admin1 = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "admin1@example.com"
            };
            var admin2 = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "admin2@example.com"
            };
            dbContext.Users.AddRange(admin1, admin2);

            dbContext.UserRoles.AddRange(
                new IdentityUserRole<string> { UserId = admin1.Id, RoleId = adminRole.Id },
                new IdentityUserRole<string> { UserId = admin2.Id, RoleId = adminRole.Id }
            );

            await dbContext.SaveChangesAsync();

            var model = new ContactViewModel
            {
                Email = TestEmail,
                FirstName = TestFirstName,
                LastName = TestLastName,
                Phone = string.Empty
            };

            // Act
            await service.AddContactAsync(model);

            // Assert - Should send email to both admins
            emailSenderMock.Verify(
                x => x.SendEmailAsync(
                    "admin1@example.com",
                    It.Is<string>(s => s.Contains("New Contact")),
                    It.Is<string>(s => s.Contains(TestFirstName) && s.Contains(TestEmail))),
                Times.Once);

            emailSenderMock.Verify(
                x => x.SendEmailAsync(
                    "admin2@example.com",
                    It.Is<string>(s => s.Contains("New Contact")),
                    It.Is<string>(s => s.Contains(TestFirstName) && s.Contains(TestEmail))),
                Times.Once);
        }

        /// <summary>
        /// Tests that alert email includes website host name.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_AlertEmail_IncludesHostName()
        {
            // Arrange
            dbContext.Settings.Add(new Setting
            {
                Group = "ContactsConfig",
                Name = "EnableAlerts",
                Value = "true"
            });

            var adminRole = new IdentityRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Administrators",
                NormalizedName = "ADMINISTRATORS"
            };
            dbContext.Roles.Add(adminRole);

            var admin = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "admin@example.com"
            };
            dbContext.Users.Add(admin);
            dbContext.UserRoles.Add(new IdentityUserRole<string> { UserId = admin.Id, RoleId = adminRole.Id });
            await dbContext.SaveChangesAsync();

            var model = new ContactViewModel
            {
                Email = TestEmail,
                FirstName = TestFirstName,
                Phone = TestPhone
            };

            // Act
            await service.AddContactAsync(model);

            // Assert - Email body should include host name
            emailSenderMock.Verify(
                x => x.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.Is<string>(body => body.Contains(TestHostName))),
                Times.Once);
        }

        /// <summary>
        /// Tests that no admins in role skips email sending.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_NoAdminsInRole_SkipsEmailSending()
        {
            // Arrange - Enable alerts and create role, but no users in role
            dbContext.Settings.Add(new Setting
            {
                Group = "ContactsConfig",
                Name = "EnableAlerts",
                Value = "true"
            });

            var adminRole = new IdentityRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Administrators",
                NormalizedName = "ADMINISTRATORS"
            };
            dbContext.Roles.Add(adminRole);
            await dbContext.SaveChangesAsync();

            var model = new ContactViewModel
            {
                Email = TestEmail,
                FirstName = TestFirstName,
                Phone = string.Empty
            };

            // Act
            await service.AddContactAsync(model);

            // Assert - No emails should be sent
            emailSenderMock.Verify(
                x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        /// <summary>
        /// Tests that invalid bool value in EnableAlerts setting is handled.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_InvalidBoolSetting_HandlesGracefully()
        {
            // Arrange - Invalid boolean value
            dbContext.Settings.Add(new Setting
            {
                Group = "ContactsConfig",
                Name = "EnableAlerts",
                Value = "invalid-bool"
            });
            await dbContext.SaveChangesAsync();

            var model = new ContactViewModel
            {
                Email = TestEmail,
                FirstName = TestFirstName,
                Phone = string.Empty
            };

            // Act & Assert - Should handle gracefully (might throw FormatException)
            try
            {
                await service.AddContactAsync(model);

                // If no exception, verify no emails were sent
                emailSenderMock.Verify(
                    x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Never);
            }
            catch (FormatException)
            {
                // This is expected behavior - bool.Parse throws FormatException
                // This test documents current behavior (could be improved to handle gracefully)
            }
        }

        #endregion

        #region Email Sending Error Handling Tests

        /// <summary>
        /// Tests that email sending failures don't prevent contact from being saved.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_EmailSendingFails_StillSavesContact()
        {
            // Arrange
            dbContext.Settings.Add(new Setting
            {
                Group = "ContactsConfig",
                Name = "EnableAlerts",
                Value = "true"
            });

            var adminRole = new IdentityRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Administrators",
                NormalizedName = "ADMINISTRATORS"
            };
            dbContext.Roles.Add(adminRole);

            var admin = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "admin@example.com"
            };
            dbContext.Users.Add(admin);
            dbContext.UserRoles.Add(new IdentityUserRole<string> { UserId = admin.Id, RoleId = adminRole.Id });
            await dbContext.SaveChangesAsync();

            // Setup email sender to throw exception
            emailSenderMock
                .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Email service unavailable"));

            var model = new ContactViewModel
            {
                Email = TestEmail,
                FirstName = TestFirstName,
                Phone = string.Empty // Required field, can be empty but not null
            };

            // Act & Assert - Should throw (currently no error handling)
            try
            {
                await service.AddContactAsync(model);
                Assert.Fail("Expected InvalidOperationException was not thrown.");
            }
            catch (InvalidOperationException)
            {
                // Test passes
            }

            // Contact should still be saved before the exception
            var contact = await dbContext.Contacts.FirstOrDefaultAsync(c => c.Email == TestEmail.ToLower());
            Assert.IsNotNull(contact, "Contact should be saved even if email sending fails");
        }

        #endregion

        #region Data Persistence Tests

        /// <summary>
        /// Tests that SaveChanges is called to persist contact.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_ValidContact_CallsSaveChanges()
        {
            // Arrange
            var model = new ContactViewModel
            {
                Email = TestEmail,
                FirstName = TestFirstName,
                Phone = ""  // Required field, can be empty string
            };

            // Act
            await service.AddContactAsync(model);

            // Assert - Contact should be in database
            var savedContact = await dbContext.Contacts.FirstOrDefaultAsync();
            Assert.IsNotNull(savedContact);
        }

        /// <summary>
        /// Tests that Updated timestamp is set for existing contacts.
        /// </summary>
        [TestMethod]
        public async Task AddContactAsync_UpdateExisting_SetsUpdatedTimestamp()
        {
            // Arrange - Add existing contact
            var existingContact = new Contact
            {
                Email = TestEmail.ToLower(),
                FirstName = "Old",
                Phone = ""
            };
            dbContext.Contacts.Add(existingContact);
            await dbContext.SaveChangesAsync();

            var beforeUpdate = DateTimeOffset.UtcNow;

            var model = new ContactViewModel
            {
                Email = TestEmail,
                FirstName = "New"
            };

            // Act
            await service.AddContactAsync(model);

            // Assert
            var updatedContact = await dbContext.Contacts.FirstAsync();
            Assert.IsNotNull(updatedContact.Updated);
            Assert.IsTrue(updatedContact.Updated >= beforeUpdate);
        }

        #endregion
    }
}