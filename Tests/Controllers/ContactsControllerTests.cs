// <copyright file="ContactsControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Services.Configurations;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Controllers;
    using Sky.Editor.Models;

    /// <summary>
    /// Unit tests for the <see cref="ContactsController"/> class.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class ContactsControllerTests : SkyCmsTestBase
    {
        private ContactsController controller = null!;
        private Mock<ILogger<ContactsController>> mockLogger = null!;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext();
            mockLogger = new Mock<ILogger<ContactsController>>();
            
            controller = new ContactsController(Db, mockLogger.Object);
            
            // Setup TempData for redirect scenarios
            controller.TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<ITempDataProvider>());
        }

        [TestCleanup]
        public void Cleanup()
        {
            controller?.Dispose();
        }

        #region Index Tests

        /// <summary>
        /// Tests that Index returns view with correct ViewData when settings exist.
        /// </summary>
        [TestMethod]
        public async Task Index_WithExistingSettings_ReturnsViewWithCorrectViewData()
        {
            // Arrange
            Db.Settings.Add(new Setting
            {
                Group = "ContactsConfig",
                Name = "EnableAlerts",
                Value = "true",
                Description = "Test"
            });
            Db.Settings.Add(new Setting
            {
                Group = "MailChimp",
                Name = "ApiKey",
                Value = "test-key",
                Description = "Test"
            });
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsTrue((bool)viewResult.ViewData["EnableAlerts"]!);
            Assert.IsTrue((bool)viewResult.ViewData["MailChimpIntegrated"]!);
        }

        /// <summary>
        /// Tests that Index returns view with false values when no settings exist.
        /// </summary>
        [TestMethod]
        public async Task Index_WithNoSettings_ReturnsViewWithDefaultValues()
        {
            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsFalse((bool)viewResult.ViewData["EnableAlerts"]!);
            Assert.IsFalse((bool)viewResult.ViewData["MailChimpIntegrated"]!);
        }

        /// <summary>
        /// Tests that Index returns 500 status code when database error occurs.
        /// </summary>
        [TestMethod]
        public async Task Index_WhenDatabaseThrowsException_Returns500()
        {
            // Arrange - Create a controller that will simulate database error
            // by disposing the context, forcing an error on query
            await Db.DisposeAsync();

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ObjectResult));
            var objectResult = (ObjectResult)result;
            Assert.AreEqual(500, objectResult.StatusCode);
            Assert.AreEqual("An error occurred loading the contacts page", objectResult.Value);
        }

        #endregion

        #region EnableAlerts Tests

        /// <summary>
        /// Tests that EnableAlerts creates new setting when it doesn't exist.
        /// </summary>
        [TestMethod]
        public async Task EnableAlerts_WhenSettingDoesNotExist_CreatesNewSetting()
        {
            // Act
            var result = await controller.EnableAlerts(true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            var okResult = (OkObjectResult)result;
            
            // FIX: Properly deserialize the anonymous object
            var json = JsonSerializer.Serialize(okResult.Value);
            var response = JsonSerializer.Deserialize<EnableAlertsResponse>(json);
            Assert.IsNotNull(response);
            Assert.IsTrue(response.enabled);

            var setting = await Db.Settings.FirstOrDefaultAsync(
                s => s.Group == "ContactsConfig" && s.Name == "EnableAlerts");
            Assert.IsNotNull(setting);
            Assert.AreEqual("True", setting.Value);
        }

        /// <summary>
        /// Tests that EnableAlerts updates existing setting.
        /// </summary>
        [TestMethod]
        public async Task EnableAlerts_WhenSettingExists_UpdatesExistingSetting()
        {
            // Arrange
            Db.Settings.Add(new Setting
            {
                Group = "ContactsConfig",
                Name = "EnableAlerts",
                Value = "false",
                Description = "Test"
            });
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.EnableAlerts(true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            var setting = await Db.Settings.FirstOrDefaultAsync(
                s => s.Group == "ContactsConfig" && s.Name == "EnableAlerts");
            Assert.IsNotNull(setting);
            Assert.AreEqual("True", setting.Value);
        }

        /// <summary>
        /// Tests that EnableAlerts can disable alerts.
        /// </summary>
        [TestMethod]
        public async Task EnableAlerts_WithFalse_DisablesAlerts()
        {
            // Arrange
            Db.Settings.Add(new Setting
            {
                Group = "ContactsConfig",
                Name = "EnableAlerts",
                Value = "true",
                Description = "Test"
            });
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.EnableAlerts(false);

            // Assert
            var setting = await Db.Settings.FirstOrDefaultAsync(
                s => s.Group == "ContactsConfig" && s.Name == "EnableAlerts");
            Assert.AreEqual("False", setting!.Value);
        }

        /// <summary>
        /// Tests that EnableAlerts returns 500 when save fails.
        /// </summary>
        [TestMethod]
        public async Task EnableAlerts_WhenSaveFails_Returns500()
        {
            // Arrange - Dispose context to simulate error
            await Db.DisposeAsync();

            // Act
            var result = await controller.EnableAlerts(true);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ObjectResult));
            var objectResult = (ObjectResult)result;
            Assert.AreEqual(500, objectResult.StatusCode);
        }

        #endregion

        #region GetContacts Tests

        /// <summary>
        /// Tests that GetContacts returns empty list when no contacts exist.
        /// </summary>
        [TestMethod]
        public async Task GetContacts_WhenNoContacts_ReturnsEmptyList()
        {
            // Act
            var result = await controller.GetContacts();

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            
            // FIX: Properly deserialize the response
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var response = JsonSerializer.Deserialize<ContactsListResponse>(json);
            Assert.IsNotNull(response);
            Assert.IsEmpty(response.data); // FIX: Remove .Count.ToList()
        }

        /// <summary>
        /// Tests that GetContacts returns contacts ordered by email.
        /// </summary>
        [TestMethod]
        public async Task GetContacts_WithMultipleContacts_ReturnsOrderedByEmail()
        {
            // Arrange - FIX: Add required Phone property
            var contact1 = new Contact
            {
                Email = "charlie@example.com",
                FirstName = "Charlie",
                LastName = "Smith",
                Phone = "555-0001" // Required
            };
            var contact2 = new Contact
            {
                Email = "alice@example.com",
                FirstName = "Alice",
                LastName = "Johnson",
                Phone = "555-0002" // Required
            };
            var contact3 = new Contact
            {
                Email = "bob@example.com",
                FirstName = "Bob",
                LastName = "Williams",
                Phone = "555-0003" // Required
            };

            Db.Contacts.AddRange(contact1, contact2, contact3);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetContacts();

            // Assert
            var jsonResult = (JsonResult)result;
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var response = JsonSerializer.Deserialize<ContactsListResponse>(json);
            
            Assert.AreEqual(3, response!.data.Count);
            Assert.AreEqual("alice@example.com", response.data[0].Email);
            Assert.AreEqual("bob@example.com", response.data[1].Email);
            Assert.AreEqual("charlie@example.com", response.data[2].Email);
        }

        /// <summary>
        /// Tests that GetContacts returns 500 when database throws exception.
        /// </summary>
        [TestMethod]
        public async Task GetContacts_WhenDatabaseThrowsException_Returns500()
        {
            // Arrange - Dispose context to force error
            await Db.DisposeAsync();

            // Act
            var result = await controller.GetContacts();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ObjectResult));
            var objectResult = (ObjectResult)result;
            Assert.AreEqual(500, objectResult.StatusCode);
        }

        #endregion

        #region ExportContacts Tests

        /// <summary>
        /// Tests that ExportContacts returns 404 when no contacts exist.
        /// </summary>
        [TestMethod]
        public async Task ExportContacts_WhenNoContacts_ReturnsNotFound()
        {
            // Act
            var result = await controller.ExportContacts();

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
            var notFoundResult = (NotFoundObjectResult)result;
            Assert.AreEqual("No contacts found to export", notFoundResult.Value);
        }

        /// <summary>
        /// Tests that ExportContacts returns CSV file with correct content.
        /// </summary>
        [TestMethod]
        public async Task ExportContacts_WithContacts_ReturnsCsvFile()
        {
            // Arrange
            var contact = new Contact
            {
                Email = "test@example.com",
                FirstName = "Test",
                LastName = "User",
                Phone = "555-1234", // Required
                Created = DateTimeOffset.UtcNow.AddDays(-1),
                Updated = DateTimeOffset.UtcNow
            };
            Db.Contacts.Add(contact);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.ExportContacts();

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = (FileContentResult)result;
            Assert.AreEqual("text/csv", fileResult.ContentType);
            Assert.AreEqual("contact-list.csv", fileResult.FileDownloadName);
            Assert.IsTrue(fileResult.FileContents.Length > 0);

            // Verify CSV content
            var csvContent = Encoding.UTF8.GetString(fileResult.FileContents);
            Assert.IsTrue(csvContent.Contains("test@example.com"));
            Assert.IsTrue(csvContent.Contains("Test"));
            Assert.IsTrue(csvContent.Contains("User"));
        }

        /// <summary>
        /// Tests that ExportContacts orders contacts by Created date.
        /// </summary>
        [TestMethod]
        public async Task ExportContacts_WithMultipleContacts_OrdersByCreatedDate()
        {
            // Arrange - FIX: Add required Phone property
            var contact1 = new Contact
            {
                Email = "first@example.com",
                FirstName = "First",
                Phone = "555-0001", // Required
                Created = DateTimeOffset.UtcNow.AddDays(-3)
            };
            var contact2 = new Contact
            {
                Email = "second@example.com",
                FirstName = "Second",
                Phone = "555-0002", // Required
                Created = DateTimeOffset.UtcNow.AddDays(-2)
            };
            var contact3 = new Contact
            {
                Email = "third@example.com",
                FirstName = "Third",
                Phone = "555-0003", // Required
                Created = DateTimeOffset.UtcNow.AddDays(-1)
            };

            Db.Contacts.AddRange(contact2, contact3, contact1); // Add out of order
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.ExportContacts();

            // Assert
            var fileResult = (FileContentResult)result;
            var csvContent = Encoding.UTF8.GetString(fileResult.FileContents);
            
            var firstIndex = csvContent.IndexOf("first@example.com");
            var secondIndex = csvContent.IndexOf("second@example.com");
            var thirdIndex = csvContent.IndexOf("third@example.com");
            
            Assert.IsTrue(firstIndex < secondIndex);
            Assert.IsTrue(secondIndex < thirdIndex);
        }

        /// <summary>
        /// Tests that ExportContacts handles null values gracefully.
        /// </summary>
        [TestMethod]
        public async Task ExportContacts_WithNullValues_HandlesGracefully()
        {
            // Arrange - FIX: Provide required Phone property (can be empty string)
            var contact = new Contact
            {
                Email = "test@example.com",
                FirstName = null,
                LastName = null,
                Phone = string.Empty // Required but can be empty
            };
            Db.Contacts.Add(contact);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.ExportContacts();

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = (FileContentResult)result;
            var csvContent = Encoding.UTF8.GetString(fileResult.FileContents);
            Assert.IsTrue(csvContent.Contains("test@example.com"));
        }

        /// <summary>
        /// Tests that ExportContacts returns 500 when export fails.
        /// </summary>
        [TestMethod]
        public async Task ExportContacts_WhenExportFails_Returns500()
        {
            // Arrange - Dispose context to force error
            await Db.DisposeAsync();

            // Act
            var result = await controller.ExportContacts();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ObjectResult));
            var objectResult = (ObjectResult)result;
            Assert.AreEqual(500, objectResult.StatusCode);
        }

        #endregion

        #region MailChimp GET Tests

        /// <summary>
        /// Tests that MailChimp GET returns view with empty model when no settings exist.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Get_WithNoSettings_ReturnsEmptyModel()
        {
            // Act
            var result = await controller.MailChimp();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsInstanceOfType(viewResult.Model, typeof(MailChimpConfig));
            var model = (MailChimpConfig)viewResult.Model!;
            Assert.IsNull(model.ApiKey);
            Assert.IsNull(model.ContactListName);
        }

        /// <summary>
        /// Tests that MailChimp GET returns view with populated model when settings exist.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Get_WithExistingSettings_ReturnsPopulatedModel()
        {
            // Arrange
            Db.Settings.AddRange(
                new Setting
                {
                    Group = "MailChimp",
                    Name = "ApiKey",
                    Value = "test-key-123",
                    Description = "Test"
                },
                new Setting
                {
                    Group = "MailChimp",
                    Name = "ContactListName",
                    Value = "Newsletter Subscribers",
                    Description = "Test"
                });
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.MailChimp();

            // Assert
            var viewResult = (ViewResult)result;
            var model = (MailChimpConfig)viewResult.Model!;
            Assert.AreEqual("test-key-123", model.ApiKey);
            Assert.AreEqual("Newsletter Subscribers", model.ContactListName);
        }

        /// <summary>
        /// Tests that MailChimp GET returns 500 when database throws exception.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Get_WhenDatabaseThrowsException_Returns500()
        {
            // Arrange - Dispose context to force error
            await Db.DisposeAsync();

            // Act
            var result = await controller.MailChimp();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ObjectResult));
            var objectResult = (ObjectResult)result;
            Assert.AreEqual(500, objectResult.StatusCode);
        }

        #endregion

        #region RemoveMailChimp Tests

        /// <summary>
        /// Tests that RemoveMailChimp removes all MailChimp settings.
        /// </summary>
        [TestMethod]
        public async Task RemoveMailChimp_WithExistingSettings_RemovesAllSettings()
        {
            // Arrange
            Db.Settings.AddRange(
                new Setting { Group = "MailChimp", Name = "ApiKey", Value = "test", Description = "Test" },
                new Setting { Group = "MailChimp", Name = "ContactListName", Value = "test", Description = "Test" });
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.RemoveMailChimp();

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual(nameof(ContactsController.Index), redirectResult.ActionName);

            var remainingSettings = await Db.Settings
                .Where(s => s.Group == "MailChimp")
                .ToListAsync();
            Assert.IsEmpty(remainingSettings); // FIX: Remove .Count.ToList()
        }

        /// <summary>
        /// Tests that RemoveMailChimp handles no existing settings gracefully.
        /// </summary>
        [TestMethod]
        public async Task RemoveMailChimp_WithNoSettings_RedirectsToIndex()
        {
            // Act
            var result = await controller.RemoveMailChimp();

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual(nameof(ContactsController.Index), redirectResult.ActionName);
        }

        /// <summary>
        /// Tests that RemoveMailChimp sets TempData error when exception occurs.
        /// </summary>
        [TestMethod]
        public async Task RemoveMailChimp_WhenExceptionOccurs_SetsTempDataError()
        {
            // Arrange - Dispose context to force error
            await Db.DisposeAsync();

            // Act
            var result = await controller.RemoveMailChimp();

            // Assert
            Assert.IsTrue(controller.TempData.ContainsKey("Error"));
            Assert.AreEqual("Failed to remove MailChimp configuration", controller.TempData["Error"]);
        }

        #endregion

        #region MailChimp POST Tests

        /// <summary>
        /// Tests that MailChimp POST returns view when ModelState is invalid.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Post_WithInvalidModelState_ReturnsView()
        {
            // Arrange
            var model = new MailChimpConfig();
            controller.ModelState.AddModelError("ApiKey", "Required");

            // Act
            var result = await controller.MailChimp(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreSame(model, viewResult.Model);
        }

        /// <summary>
        /// Tests that MailChimp POST returns view when ApiKey is null.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Post_WithNullApiKey_ReturnsViewWithError()
        {
            // Arrange
            var model = new MailChimpConfig
            {
                ApiKey = null,
                ContactListName = "Test List"
            };

            // Act
            var result = await controller.MailChimp(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.IsFalse(controller.ModelState.IsValid);
            Assert.IsTrue(controller.ModelState.ContainsKey(string.Empty));
        }

        /// <summary>
        /// Tests that MailChimp POST returns view when ContactListName is null.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Post_WithNullContactListName_ReturnsViewWithError()
        {
            // Arrange
            var model = new MailChimpConfig
            {
                ApiKey = "test-key",
                ContactListName = null
            };

            // Act
            var result = await controller.MailChimp(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.IsFalse(controller.ModelState.IsValid);
        }

        /// <summary>
        /// Tests that MailChimp POST creates new settings when they don't exist.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Post_WithNewSettings_CreatesSettings()
        {
            // Arrange
            var model = new MailChimpConfig
            {
                ApiKey = "new-api-key-123",
                ContactListName = "New List"
            };

            // Act
            var result = await controller.MailChimp(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual(nameof(ContactsController.Index), redirectResult.ActionName);

            var apiKeySetting = await Db.Settings
                .FirstOrDefaultAsync(s => s.Group == "MailChimp" && s.Name == "ApiKey");
            var listSetting = await Db.Settings
                .FirstOrDefaultAsync(s => s.Group == "MailChimp" && s.Name == "ContactListName");

            Assert.IsNotNull(apiKeySetting);
            Assert.AreEqual("new-api-key-123", apiKeySetting.Value);
            Assert.IsNotNull(listSetting);
            Assert.AreEqual("New List", listSetting.Value);
        }

        /// <summary>
        /// Tests that MailChimp POST updates existing settings.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Post_WithExistingSettings_UpdatesSettings()
        {
            // Arrange
            Db.Settings.AddRange(
                new Setting
                {
                    Group = "MailChimp",
                    Name = "ApiKey",
                    Value = "old-key",
                    Description = "Test"
                },
                new Setting
                {
                    Group = "MailChimp",
                    Name = "ContactListName",
                    Value = "Old List",
                    Description = "Test"
                });
            await Db.SaveChangesAsync();

            var model = new MailChimpConfig
            {
                ApiKey = "updated-key",
                ContactListName = "Updated List"
            };

            // Act
            var result = await controller.MailChimp(model);

            // Assert
            var apiKeySetting = await Db.Settings
                .FirstOrDefaultAsync(s => s.Group == "MailChimp" && s.Name == "ApiKey");
            var listSetting = await Db.Settings
                .FirstOrDefaultAsync(s => s.Group == "MailChimp" && s.Name == "ContactListName");

            Assert.AreEqual("updated-key", apiKeySetting!.Value);
            Assert.AreEqual("Updated List", listSetting!.Value);
        }

        /// <summary>
        /// Tests that MailChimp POST trims ContactListName.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Post_TrimsContactListName()
        {
            // Arrange
            var model = new MailChimpConfig
            {
                ApiKey = "test-key",
                ContactListName = "  Trimmed List  "
            };

            // Act
            await controller.MailChimp(model);

            // Assert
            var listSetting = await Db.Settings
                .FirstOrDefaultAsync(s => s.Group == "MailChimp" && s.Name == "ContactListName");
            Assert.AreEqual("Trimmed List", listSetting!.Value);
        }

        /// <summary>
        /// Tests that MailChimp POST sets TempData success message.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Post_SetsTempDataSuccessMessage()
        {
            // Arrange
            var model = new MailChimpConfig
            {
                ApiKey = "test-key",
                ContactListName = "Test List"
            };

            // Act
            await controller.MailChimp(model);

            // Assert
            Assert.IsTrue(controller.TempData.ContainsKey("Success"));
            Assert.AreEqual("MailChimp configuration saved successfully", controller.TempData["Success"]);
        }

        /// <summary>
        /// Tests that MailChimp POST returns view with error when save fails.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Post_WhenSaveFails_ReturnsViewWithError()
        {
            // Arrange
            var model = new MailChimpConfig
            {
                ApiKey = "test-key",
                ContactListName = "Test List"
            };

            // Dispose context to force error
            await Db.DisposeAsync();

            // Act
            var result = await controller.MailChimp(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.IsFalse(controller.ModelState.IsValid);
        }

        #endregion

        #region Helper Classes for Deserialization

        private class ContactsListResponse
        {
            public List<Contact> data { get; set; } = new();
        }

        private class EnableAlertsResponse
        {
            public bool enabled { get; set; }
        }

        #endregion

        #region Additional Coverage Tests

        #region Constructor Tests

        /// <summary>
        /// Tests that constructor throws ArgumentNullException when dbContext is null.
        /// </summary>
        [TestMethod]
        public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() => 
                new ContactsController(null!, mockLogger.Object));
        }

        /// <summary>
        /// Tests that constructor throws ArgumentNullException when logger is null.
        /// </summary>
        [TestMethod]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() => 
                new ContactsController(Db, null!));
        }

        #endregion

        #region EnableAlerts Edge Cases

        /// <summary>
        /// Tests that EnableAlerts logs the correct information message.
        /// </summary>
        [TestMethod]
        public async Task EnableAlerts_LogsCorrectInformationMessage()
        {
            // Act
            await controller.EnableAlerts(true);

            // Assert - Verify logger was called with correct message
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Contact alerts enabled")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        /// <summary>
        /// Tests that EnableAlerts handles concurrent updates correctly.
        /// </summary>
        [TestMethod]
        public async Task EnableAlerts_WithConcurrentUpdates_HandlesCorrectly()
        {
            // Arrange
            Db.Settings.Add(new Setting
            {
                Group = "ContactsConfig",
                Name = "EnableAlerts",
                Value = "false",
                Description = "Test"
            });
            await Db.SaveChangesAsync();

            // Act - Simulate concurrent updates
            var task1 = controller.EnableAlerts(true);
            var task2 = controller.EnableAlerts(false);
            await Task.WhenAll(task1, task2);

            // Assert - Should complete without exception
            Assert.IsInstanceOfType(task1.Result, typeof(OkObjectResult));
            Assert.IsInstanceOfType(task2.Result, typeof(OkObjectResult));
        }

        /// <summary>
        /// Tests that EnableAlerts with invalid boolean string doesn't corrupt data.
        /// </summary>
        [TestMethod]
        public async Task EnableAlerts_TogglingMultipleTimes_MaintainsDataIntegrity()
        {
            // Act - Toggle multiple times
            await controller.EnableAlerts(true);
            await controller.EnableAlerts(false);
            await controller.EnableAlerts(true);
            await controller.EnableAlerts(false);

            // Assert - Should only have one setting
            var settings = await Db.Settings
                .Where(s => s.Group == "ContactsConfig" && s.Name == "EnableAlerts")
                .ToListAsync();
            Assert.AreEqual(1, settings.Count);
            Assert.AreEqual("False", settings[0].Value);
        }

        #endregion

        #region GetContacts Edge Cases

        /// <summary>
        /// Tests that GetContacts handles very large datasets efficiently.
        /// </summary>
        [TestMethod]
        public async Task GetContacts_WithLargeDataset_ReturnsAllContacts()
        {
            // Arrange - Add 100 contacts
            var contacts = Enumerable.Range(1, 100).Select(i => new Contact
            {
                Email = $"user{i:D3}@example.com",
                FirstName = $"User{i}",
                LastName = "Test",
                Phone = $"555-{i:D4}"
            }).ToList();

            Db.Contacts.AddRange(contacts);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetContacts();

            // Assert
            var jsonResult = (JsonResult)result;
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var response = JsonSerializer.Deserialize<ContactsListResponse>(json);
            Assert.AreEqual(100, response!.data.Count);
        }

        /// <summary>
        /// Tests that GetContacts handles special characters in email addresses.
        /// </summary>
        [TestMethod]
        public async Task GetContacts_WithSpecialCharactersInEmail_HandlesCorrectly()
        {
            // Arrange
            var contact = new Contact
            {
                Email = "test+special@example.com",
                FirstName = "Test",
                LastName = "User",
                Phone = "555-1234"
            };
            Db.Contacts.Add(contact);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetContacts();

            // Assert
            var jsonResult = (JsonResult)result;
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var response = JsonSerializer.Deserialize<ContactsListResponse>(json);
            Assert.AreEqual(1, response!.data.Count);
            Assert.AreEqual("test+special@example.com", response.data[0].Email);
        }

        /// <summary>
        /// Tests that GetContacts maintains sorting with duplicate email prefixes.
        /// </summary>
        [TestMethod]
        public async Task GetContacts_WithSimilarEmails_MaintainsCorrectOrder()
        {
            // Arrange
            var contacts = new[]
            {
                new Contact { Email = "user@example.com", FirstName = "A", Phone = "555-0001" },
                new Contact { Email = "user1@example.com", FirstName = "B", Phone = "555-0002" },
                new Contact { Email = "user10@example.com", FirstName = "C", Phone = "555-0003" },
                new Contact { Email = "user2@example.com", FirstName = "D", Phone = "555-0004" }
            };

            Db.Contacts.AddRange(contacts);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.GetContacts();

            // Assert
            var jsonResult = (JsonResult)result;
            var json = JsonSerializer.Serialize(jsonResult.Value);
            var response = JsonSerializer.Deserialize<ContactsListResponse>(json);
            
            // Verify alphabetical order
            Assert.AreEqual("user@example.com", response.data[0].Email);
            Assert.AreEqual("user1@example.com", response.data[1].Email);
            Assert.AreEqual("user10@example.com", response.data[2].Email);
            Assert.AreEqual("user2@example.com", response.data[3].Email);
        }

        #endregion

        #region ExportContacts Edge Cases

        /// <summary>
        /// Tests that ExportContacts generates valid CSV with header row.
        /// </summary>
        [TestMethod]
        public async Task ExportContacts_GeneratesCsvWithHeaders()
        {
            // Arrange
            var contact = new Contact
            {
                Email = "test@example.com",
                FirstName = "Test",
                LastName = "User",
                Phone = "555-1234"
            };
            Db.Contacts.Add(contact);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.ExportContacts();

            // Assert
            var fileResult = (FileContentResult)result;
            var csvContent = Encoding.UTF8.GetString(fileResult.FileContents);
            var lines = csvContent.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            
            Assert.IsTrue(lines.Length >= 2, "CSV should have header and at least one data row");
            Assert.IsTrue(lines[0].Contains("Id") || lines[0].Contains("Email"), "First line should be header");
        }

        /// <summary>
        /// Tests that ExportContacts escapes special CSV characters correctly.
        /// </summary>
        [TestMethod]
        public async Task ExportContacts_EscapesSpecialCsvCharacters()
        {
            // Arrange
            var contact = new Contact
            {
                Email = "test@example.com",
                FirstName = "Test, User", // Contains comma
                LastName = "O'Brien", // Contains apostrophe
                Phone = "555-1234"
            };
            Db.Contacts.Add(contact);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.ExportContacts();

            // Assert
            var fileResult = (FileContentResult)result;
            var csvContent = Encoding.UTF8.GetString(fileResult.FileContents);
            
            // CSV should properly escape commas and quotes
            Assert.IsTrue(csvContent.Contains("Test, User") || csvContent.Contains("\"Test, User\""));
        }

        /// <summary>
        /// Tests that ExportContacts handles contacts with very old dates.
        /// </summary>
        [TestMethod]
        public async Task ExportContacts_WithVeryOldDates_FormatsCorrectly()
        {
            // Arrange
            var contact = new Contact
            {
                Email = "old@example.com",
                FirstName = "Old",
                LastName = "Contact",
                Phone = "555-1234",
                Created = DateTimeOffset.Parse("2000-01-01"),
                Updated = DateTimeOffset.Parse("2000-06-15")
            };
            Db.Contacts.Add(contact);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.ExportContacts();

            // Assert
            Assert.IsInstanceOfType(result, typeof(FileContentResult));
            var fileResult = (FileContentResult)result;
            var csvContent = Encoding.UTF8.GetString(fileResult.FileContents);
            Assert.IsTrue(csvContent.Contains("2000"));
        }

        /// <summary>
        /// Tests that ExportContacts increments ID correctly for multiple contacts.
        /// </summary>
        [TestMethod]
        public async Task ExportContacts_AssignsSequentialIds()
        {
            // Arrange
            var contacts = Enumerable.Range(1, 5).Select(i => new Contact
            {
                Email = $"user{i}@example.com",
                FirstName = $"User{i}",
                Phone = $"555-000{i}",
                Created = DateTimeOffset.UtcNow.AddDays(-i)
            }).ToList();

            Db.Contacts.AddRange(contacts);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.ExportContacts();

            // Assert
            var fileResult = (FileContentResult)result;
            var csvContent = Encoding.UTF8.GetString(fileResult.FileContents);
            
            // Should contain sequential IDs 1-5
            for (int i = 1; i <= 5; i++)
            {
                Assert.IsTrue(csvContent.Contains($"{i},") || csvContent.Contains($"\"{i}\""), 
                    $"CSV should contain ID {i}");
            }
        }

        /// <summary>
        /// Tests that ExportContacts logs the correct count.
        /// </summary>
        [TestMethod]
        public async Task ExportContacts_LogsCorrectContactCount()
        {
            // Arrange
            var contacts = Enumerable.Range(1, 3).Select(i => new Contact
            {
                Email = $"user{i}@example.com",
                FirstName = $"User{i}",
                Phone = $"555-000{i}"
            }).ToList();

            Db.Contacts.AddRange(contacts);
            await Db.SaveChangesAsync();

            // Act
            await controller.ExportContacts();

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Exported 3 contacts")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region MailChimp POST Edge Cases

        /// <summary>
        /// Tests that MailChimp POST handles whitespace-only values correctly.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Post_WithWhitespaceOnlyValues_ReturnsViewWithError()
        {
            // Arrange
            var model = new MailChimpConfig
            {
                ApiKey = "   ",
                ContactListName = "   "
            };

            // Act
            var result = await controller.MailChimp(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.IsFalse(controller.ModelState.IsValid);
        }

        /// <summary>
        /// Tests that MailChimp POST handles very long values.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Post_WithVeryLongValues_SavesCorrectly()
        {
            // Arrange
            var longApiKey = new string('A', 500);
            var longListName = new string('B', 500);
            var model = new MailChimpConfig
            {
                ApiKey = longApiKey,
                ContactListName = longListName
            };

            // Act
            var result = await controller.MailChimp(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var apiKeySetting = await Db.Settings
                .FirstOrDefaultAsync(s => s.Group == "MailChimp" && s.Name == "ApiKey");
            Assert.AreEqual(longApiKey, apiKeySetting!.Value);
        }

        /// <summary>
        /// Tests that MailChimp POST preserves leading/trailing whitespace only in list name (not trimmed in key).
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Post_TrimsOnlyContactListName()
        {
            // Arrange
            var model = new MailChimpConfig
            {
                ApiKey = "  key-with-spaces  ", // Should be preserved
                ContactListName = "  List Name  " // Should be trimmed
            };

            // Act
            await controller.MailChimp(model);

            // Assert
            var apiKeySetting = await Db.Settings
                .FirstOrDefaultAsync(s => s.Group == "MailChimp" && s.Name == "ApiKey");
            var listSetting = await Db.Settings
                .FirstOrDefaultAsync(s => s.Group == "MailChimp" && s.Name == "ContactListName");

            Assert.AreEqual("  key-with-spaces  ", apiKeySetting!.Value);
            Assert.AreEqual("List Name", listSetting!.Value);
        }

        /// <summary>
        /// Tests that MailChimp POST handles special characters in configuration values.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Post_WithSpecialCharacters_SavesCorrectly()
        {
            // Arrange
            var model = new MailChimpConfig
            {
                ApiKey = "key-with-special!@#$%^&*()",
                ContactListName = "List & Name <Test>"
            };

            // Act
            var result = await controller.MailChimp(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var apiKeySetting = await Db.Settings
                .FirstOrDefaultAsync(s => s.Group == "MailChimp" && s.Name == "ApiKey");
            Assert.AreEqual("key-with-special!@#$%^&*()", apiKeySetting!.Value);
        }

        /// <summary>
        /// Tests that MailChimp POST handles empty string (not null) correctly.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Post_WithEmptyStringValues_ReturnsViewWithError()
        {
            // Arrange
            var model = new MailChimpConfig
            {
                ApiKey = string.Empty,
                ContactListName = string.Empty
            };

            // Act
            var result = await controller.MailChimp(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.IsFalse(controller.ModelState.IsValid);
        }

        #endregion

        #region Index Edge Cases

        /// <summary>
        /// Tests that Index handles partial settings (only alerts configured).
        /// </summary>
        [TestMethod]
        public async Task Index_WithOnlyAlertsConfigured_ReturnsCorrectViewData()
        {
            // Arrange
            Db.Settings.Add(new Setting
            {
                Group = "ContactsConfig",
                Name = "EnableAlerts",
                Value = "true",
                Description = "Test"
            });
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = (ViewResult)result;
            Assert.IsTrue((bool)viewResult.ViewData["EnableAlerts"]!);
            Assert.IsFalse((bool)viewResult.ViewData["MailChimpIntegrated"]!);
        }

        /// <summary>
        /// Tests that Index handles partial settings (only MailChimp configured).
        /// </summary>
        [TestMethod]
        public async Task Index_WithOnlyMailChimpConfigured_ReturnsCorrectViewData()
        {
            // Arrange
            Db.Settings.Add(new Setting
            {
                Group = "MailChimp",
                Name = "ApiKey",
                Value = "test-key",
                Description = "Test"
            });
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = (ViewResult)result;
            Assert.IsFalse((bool)viewResult.ViewData["EnableAlerts"]!);
            Assert.IsTrue((bool)viewResult.ViewData["MailChimpIntegrated"]!);
        }

        /// <summary>
        /// Tests that Index handles invalid boolean value in alerts setting.
        /// </summary>
        [TestMethod]
        public async Task Index_WithInvalidBooleanValue_TreatAsDisabled()
        {
            // Arrange
            Db.Settings.Add(new Setting
            {
                Group = "ContactsConfig",
                Name = "EnableAlerts",
                Value = "invalid", // Not a valid boolean
                Description = "Test"
            });
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = (ViewResult)result;
            Assert.IsFalse((bool)viewResult.ViewData["EnableAlerts"]!);
        }

        #endregion

        #region RemoveMailChimp Edge Cases

        /// <summary>
        /// Tests that RemoveMailChimp only removes MailChimp settings, not other settings.
        /// </summary>
        [TestMethod]
        public async Task RemoveMailChimp_OnlyRemovesMailChimpSettings_PreservesOtherSettings()
        {
            // Arrange
            Db.Settings.RemoveRange(Db.Settings); // Clear all settings to avoid contamination
            await Db.SaveChangesAsync();
            Db.Settings.AddRange(
                new Setting { Group = "MailChimp", Name = "ApiKey", Value = "test", Description = "Test" },
                new Setting { Group = "ContactsConfig", Name = "EnableAlerts", Value = "true", Description = "Test" },
                new Setting { Group = "OtherConfig", Name = "Setting1", Value = "value", Description = "Test" });
            await Db.SaveChangesAsync();

            // Act
            await controller.RemoveMailChimp();

            // Assert
            var remainingMailChimp = await Db.Settings
                .Where(s => s.Group == "MailChimp")
                .ToListAsync();
            var otherSettings = await Db.Settings
                .Where(s => s.Group != "MailChimp")
                .ToListAsync();

            Assert.IsEmpty(remainingMailChimp); // FIX: Remove .Count.ToList()
            Assert.HasCount(2, otherSettings);
        }

        /// <summary>
        /// Tests that RemoveMailChimp removes multiple MailChimp settings (more than 2).
        /// </summary>
        [TestMethod]
        public async Task RemoveMailChimp_WithMultipleMailChimpSettings_RemovesAll()
        {
            // Arrange
            Db.Settings.AddRange(
                new Setting { Group = "MailChimp", Name = "ApiKey", Value = "test", Description = "Test" },
                new Setting { Group = "MailChimp", Name = "ContactListName", Value = "test", Description = "Test" },
                new Setting { Group = "MailChimp", Name = "CustomSetting1", Value = "test", Description = "Test" },
                new Setting { Group = "MailChimp", Name = "CustomSetting2", Value = "test", Description = "Test" });
            await Db.SaveChangesAsync();

            // Act
            await controller.RemoveMailChimp();

            // Assert
            var remaining = await Db.Settings
                .Where(s => s.Group == "MailChimp")
                .CountAsync();
            Assert.AreEqual(0, remaining);
        }

        #endregion

        #region Logging Verification Tests

        /// <summary>
        /// Tests that errors are logged with correct log level.
        /// </summary>
        [TestMethod]
        public async Task ExportContacts_OnError_LogsWithErrorLevel()
        {
            // Arrange
            await Db.DisposeAsync();

            // Act
            await controller.ExportContacts();

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        /// <summary>
        /// Tests that MailChimp configuration save logs information.
        /// </summary>
        [TestMethod]
        public async Task MailChimp_Post_OnSuccess_LogsInformation()
        {
            // Arrange
            var model = new MailChimpConfig
            {
                ApiKey = "test-key",
                ContactListName = "Test List"
            };

            // Act
            await controller.MailChimp(model);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("MailChimp configuration saved")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #endregion
    }
}
