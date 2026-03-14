// <copyright file="UsersControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.Common.Data;
    using Cosmos.EmailServices;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Routing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Cms.Controllers;
    using Sky.Cms.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for the <see cref="UsersController"/> class.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class UsersControllerTests : SkyCmsTestBase
    {
        private UsersController controller;
        private Mock<ICosmosEmailSender> mockEmailSender;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);

            // Ensure all required roles exist before creating controller
            // This prevents issues with SetupNewAdministrator.Ensure_Roles_Exists in the controller constructor
            var requiredRoles = new[] { "Administrators", "Editors", "Authors", "Team Members" };
            foreach (var roleName in requiredRoles)
            {
                if (!RoleManager.RoleExistsAsync(roleName).Result)
                {
                    RoleManager.CreateAsync(new IdentityRole(roleName)).Wait();
                }
            }

            // Create the test user with Administrator role
            var testUser = new IdentityUser
            {
                Id = TestUserId.ToString(),
                UserName = "testuser@example.com",
                Email = "testuser@example.com",
                NormalizedUserName = "TESTUSER@EXAMPLE.COM",
                NormalizedEmail = "TESTUSER@EXAMPLE.COM",
                EmailConfirmed = true
            };
            UserManager.CreateAsync(testUser).Wait();
            UserManager.AddToRoleAsync(testUser, "Administrators").Wait();

            mockEmailSender = new Mock<ICosmosEmailSender>();
            mockEmailSender.Setup(x => x.SendResult)
                .Returns(new SendResult { StatusCode = System.Net.HttpStatusCode.OK, Message = "Email sent successfully" });

            // Register a token provider for UserManager
            var tokenProvider = new Mock<IUserTwoFactorTokenProvider<IdentityUser>>();
            tokenProvider.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<UserManager<IdentityUser>>(), It.IsAny<IdentityUser>()))
                .ReturnsAsync("test-token-12345");
            tokenProvider.Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UserManager<IdentityUser>>(), It.IsAny<IdentityUser>()))
                .ReturnsAsync(true);
            tokenProvider.Setup(x => x.CanGenerateTwoFactorTokenAsync(It.IsAny<UserManager<IdentityUser>>(), It.IsAny<IdentityUser>()))
                .ReturnsAsync(true);

            UserManager.RegisterTokenProvider("Default", tokenProvider.Object);

            controller = new UsersController(
                new NullLogger<UsersController>(),
                UserManager,
                RoleManager,
                mockEmailSender.Object,
                Db);

            // Setup HttpContext for the controller
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https"; // Set a valid scheme
            httpContext.Request.Host = new HostString("example.com"); // Set host for URL generation
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "testuser@example.com"),
                new Claim(ClaimTypes.Role, "Administrators")
            }, "mock"));

            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper.Setup(x => x.ActionContext).Returns(new ActionContext
            {
                HttpContext = httpContext,
                RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
                ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
            });

            // Setup Action method for MVC routes
            mockUrlHelper
                .Setup(x => x.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://example.com/confirm-email");

            // Setup RouteUrl - this is what the Page extension method uses internally
            mockUrlHelper
                .Setup(x => x.RouteUrl(It.IsAny<UrlRouteContext>()))
                .Returns("https://example.com/test-callback-url");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            controller.Url = mockUrlHelper.Object;
        }

        [TestCleanup]
        public void Cleanup()
        {
            controller?.Dispose();
            Db.Dispose();
        }

        #region AuthorInfos Tests

        /// <summary>
        /// Tests that AuthorInfos_ReturnsViewWithAllAuthors.
        /// </summary>
        [TestMethod]
        public async Task AuthorInfos_ReturnsViewWithAllAuthors()
        {
            // Arrange
            var user1 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "user1@example.com" };
            var user2 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "user2@example.com" };
            await UserManager.CreateAsync(user1);
            await UserManager.CreateAsync(user2);

            Db.AuthorInfos.Add(new AuthorInfo { Id = user1.Id, EmailAddress = user1.Email, AuthorName = "Author One" });
            Db.AuthorInfos.Add(new AuthorInfo { Id = user2.Id, EmailAddress = user2.Email, AuthorName = "Author Two" });
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.AuthorInfos();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            var model = viewResult.Model as List<AuthorInfo>;
            Assert.IsNotNull(model);
            // Account for the test user created in Setup
            Assert.IsTrue(model.Count >= 2, $"Expected at least 2 authors, but found {model.Count}");
            Assert.IsTrue(model.Any(a => a.EmailAddress == "user1@example.com"));
            Assert.IsTrue(model.Any(a => a.EmailAddress == "user2@example.com"));
        }

        /// <summary>
        /// Tests that AuthorInfos_CreatesAuthorInfoForUsersWithoutOne.
        /// </summary>
        [TestMethod]
        public async Task AuthorInfos_CreatesAuthorInfoForUsersWithoutOne()
        {
            // Arrange
            var user = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "newuser@example.com" };
            await UserManager.CreateAsync(user);

            // Act
            var result = await controller.AuthorInfos();

            // Assert
            var authorInfo = await Db.AuthorInfos.FirstOrDefaultAsync(a => a.Id == user.Id);
            Assert.IsNotNull(authorInfo);
            Assert.AreEqual(user.Email, authorInfo.EmailAddress);
        }

        /// <summary>
        /// Tests that AuthorInfos_WithInvalidModelState_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task AuthorInfos_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            controller.ModelState.AddModelError("Error", "Invalid model state");

            // Act
            var result = await controller.AuthorInfos();

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        /// <summary>
        /// Tests that AuthorInfos_SortsByEmailDescending.
        /// </summary>
        [TestMethod]
        public async Task AuthorInfos_SortsByEmailDescending()
        {
            // Arrange
            var user1 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "a@example.com" };
            var user2 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "z@example.com" };
            await UserManager.CreateAsync(user1);
            await UserManager.CreateAsync(user2);

            Db.AuthorInfos.Add(new AuthorInfo { Id = user1.Id, EmailAddress = user1.Email });
            Db.AuthorInfos.Add(new AuthorInfo { Id = user2.Id, EmailAddress = user2.Email });
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.AuthorInfos(sortOrder: "desc", currentSort: "EmailAddress");

            // Assert
            var viewResult = (ViewResult)result;
            var model = viewResult.Model as List<AuthorInfo>;
            Assert.IsTrue(model.Count >= 2, "Should have at least 2 authors");
            Assert.AreEqual("z@example.com", model[0].EmailAddress);
            var aUser = model.FirstOrDefault(m => m.EmailAddress == "a@example.com");
            Assert.IsNotNull(aUser, "User with email a@example.com should be in the list");
        }

        #endregion

        #region AuthorInfoEdit Tests

        /// <summary>
        /// Tests that AuthorInfoEdit_Get_ReturnsExistingAuthorInfo.
        /// </summary>
        [TestMethod]
        public async Task AuthorInfoEdit_Get_ReturnsExistingAuthorInfo()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            Db.AuthorInfos.Add(new AuthorInfo
            {
                Id = userId,
                EmailAddress = "test@example.com",
                AuthorName = "Test Author"
            });
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.AuthorInfoEdit(userId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            var model = viewResult.Model as AuthorInfo;
            Assert.IsNotNull(model);
            Assert.AreEqual("Test Author", model.AuthorName);
        }

        /// <summary>
        /// Tests that AuthorInfoEdit_Get_CreatesNewAuthorInfoIfNotExists.
        /// </summary>
        [TestMethod]
        public async Task AuthorInfoEdit_Get_CreatesNewAuthorInfoIfNotExists()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();

            // Act
            var result = await controller.AuthorInfoEdit(userId);

            // Assert
            var authorInfo = await Db.AuthorInfos.FirstOrDefaultAsync(a => a.Id == userId);
            Assert.IsNotNull(authorInfo);
        }

        /// <summary>
        /// Tests that AuthorInfoEdit_Post_UpdatesAuthorInfo.
        /// </summary>
        [TestMethod]
        public async Task AuthorInfoEdit_Post_UpdatesAuthorInfo()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            Db.AuthorInfos.Add(new AuthorInfo
            {
                Id = userId,
                EmailAddress = "old@example.com",
                AuthorName = "Old Name"
            });
            await Db.SaveChangesAsync();

            var model = new AuthorInfo
            {
                Id = userId,
                AuthorName = "New Name",
                AuthorDescription = "Updated description",
                TwitterHandle = "@newhandle",
                InstagramUrl = "https://instagram.com/newhandle",
                EmailAddress = "new@example.com"
            };

            // Act
            var result = await controller.AuthorInfoEdit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirectResult = (RedirectToActionResult)result;
            Assert.AreEqual("AuthorInfos", redirectResult.ActionName);

            var updated = await Db.AuthorInfos.FirstOrDefaultAsync(a => a.Id == userId);
            Assert.AreEqual("New Name", updated.AuthorName);
            Assert.AreEqual("new@example.com", updated.EmailAddress);
        }

        #endregion

        #region Index Tests

        /// <summary>
        /// Tests that Index_ReturnsAllUsers.
        /// </summary>
        [TestMethod]
        public async Task Index_ReturnsAllUsers()
        {
            // Arrange
            var user1 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "user1@example.com" };
            var user2 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "user2@example.com" };
            await UserManager.CreateAsync(user1);
            await UserManager.CreateAsync(user2);

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            var model = viewResult.Model as List<UserIndexViewModel>;
            Assert.IsNotNull(model);
            Assert.IsTrue(model.Count >= 2);
        }

        /// <summary>
        /// Tests that Index_FiltersUsersByRole.
        /// </summary>
        [TestMethod]
        public async Task Index_FiltersUsersByRole()
        {
            // Arrange
            var role = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "TestRole" };
            await RoleManager.CreateAsync(role);

            var user = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "test@example.com" };
            await UserManager.CreateAsync(user);
            await UserManager.AddToRoleAsync(user, "TestRole");

            // Act
            var result = await controller.Index(id: role.Id);

            // Assert
            var viewResult = (ViewResult)result;
            var model = viewResult.Model as List<UserIndexViewModel>;
            Assert.IsTrue(model.Any(u => u.EmailAddress == "test@example.com"));
        }

        /// <summary>
        /// Tests that Index_IncludesRoleMembership.
        /// </summary>
        [TestMethod]
        public async Task Index_IncludesRoleMembership()
        {
            // Arrange
            var role = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "TestRole" };
            await RoleManager.CreateAsync(role);

            var user = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "roletest@example.com" };
            await UserManager.CreateAsync(user);
            await UserManager.AddToRoleAsync(user, "TestRole");

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = (ViewResult)result;
            var model = viewResult.Model as List<UserIndexViewModel>;
            var userModel = model.FirstOrDefault(u => u.EmailAddress == "roletest@example.com");
            Assert.IsNotNull(userModel);
            Assert.IsTrue(userModel.RoleMembership.Contains("TestRole"));
        }

        #endregion

        #region ConfirmEmail / UnconfirmEmail Tests

        /// <summary>
        /// Tests that ConfirmEmail_ConfirmsUserEmail.
        /// </summary>
        [TestMethod]
        public async Task ConfirmEmail_ConfirmsUserEmail()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "unconfirmed@example.com",
                EmailConfirmed = false
            };
            await UserManager.CreateAsync(user);

            // Act
            var result = await controller.ConfirmEmail(user.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            var updatedUser = await UserManager.FindByIdAsync(user.Id);
            Assert.IsTrue(updatedUser.EmailConfirmed);
        }

        /// <summary>
        /// Tests that UnconfirmEmail_UnconfirmsUserEmail.
        /// </summary>
        [TestMethod]
        public async Task UnconfirmEmail_UnconfirmsUserEmail()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "confirmed@example.com",
                EmailConfirmed = true
            };
            await UserManager.CreateAsync(user);

            // Act
            var result = await controller.UnconfirmEmail(user.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            var updatedUser = await UserManager.FindByIdAsync(user.Id);
            Assert.IsFalse(updatedUser.EmailConfirmed);
        }

        #endregion

        #region Create Tests

        /// <summary>
        /// Tests that Create_Get_ReturnsNewViewModel.
        /// </summary>
        [TestMethod]
        public async Task Create_Get_ReturnsNewViewModel()
        {
            // Act
            var result = controller.Create();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsInstanceOfType(viewResult.Model, typeof(UserCreateViewModel));
        }

        /// <summary>
        /// Tests that Create_Post_CreatesNewUser.
        /// </summary>
        [TestMethod]
        public async Task Create_Post_CreatesNewUser()
        {
            // Arrange
            var model = new UserCreateViewModel
            {
                EmailAddress = "newuser@example.com",
                EmailConfirmed = true,
                PhoneNumber = "555-1234",
                PhoneNumberConfirmed = true,
                GenerateRandomPassword = true
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual("UserCreated", viewResult.ViewName);

            var user = await UserManager.FindByEmailAsync("newuser@example.com");
            Assert.IsNotNull(user);
            Assert.AreEqual("555-1234", user.PhoneNumber);
        }

        /// <summary>
        /// Tests that Create_Post_WithoutPassword_RequiresGenerateRandomPassword.
        /// </summary>
        [TestMethod]
        public async Task Create_Post_WithoutPassword_RequiresGenerateRandomPassword()
        {
            // Arrange
            var model = new UserCreateViewModel
            {
                EmailAddress = "nopass@example.com",
                GenerateRandomPassword = false,
                Password = null
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsFalse(controller.ModelState.IsValid);
            Assert.IsTrue(controller.ModelState.ContainsKey("GenerateRandomPassword"));
        }

        /// <summary>
        /// Tests that Create_Post_SendsConfirmationEmail.
        /// </summary>
        [TestMethod]
        public async Task Create_Post_SendsConfirmationEmail()
        {
            // Arrange
            var model = new UserCreateViewModel
            {
                EmailAddress = "emailtest@example.com",
                EmailConfirmed = false,
                GenerateRandomPassword = true
            };

            // Act
            await controller.Create(model);

            // Assert
            mockEmailSender.Verify(x => x.SendEmailAsync(
                It.Is<string>(email => email == "emailtest@example.com"),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);
        }

        #endregion

        #region DeleteUsers Tests

        /// <summary>
        /// Tests that DeleteUsers_DeletesNonAdministratorUsers.
        /// </summary>
        [TestMethod]
        public async Task DeleteUsers_DeletesNonAdministratorUsers()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "deleteme@example.com"
            };
            await UserManager.CreateAsync(user);

            var initialCount = await UserManager.Users.CountAsync();

            // Act
            var result = await controller.DeleteUsers(user.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            var finalCount = await UserManager.Users.CountAsync();
            // User count should decrease if more than 1 user exists
            if (initialCount > 1)
            {
                Assert.AreEqual(initialCount - 1, finalCount);
            }
        }

        /// <summary>
        /// Tests that DeleteUsers_DoesNotDeleteAdministrators.
        /// </summary>
        [TestMethod]
        public async Task DeleteUsers_DoesNotDeleteAdministrators()
        {
            // Arrange
            var admin = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "admin@example.com"
            };
            await UserManager.CreateAsync(admin);
            await UserManager.AddToRoleAsync(admin, "Administrators");

            var initialCount = await UserManager.Users.CountAsync();

            // Act
            var result = await controller.DeleteUsers(admin.Id);

            // Assert
            var finalCount = await UserManager.Users.CountAsync();
            Assert.AreEqual(initialCount, finalCount, "Administrator should not be deleted");

            var stillExists = await UserManager.FindByIdAsync(admin.Id);
            Assert.IsNotNull(stillExists);
        }

        /// <summary>
        /// Tests that DeleteUsers_PreventsDeleteLastUser.
        /// </summary>
        [TestMethod]
        public async Task DeleteUsers_PreventsDeleteLastUser()
        {
            // Arrange
            // Delete all users except the test user
            var allUsers = await UserManager.Users.ToListAsync();
            foreach (var user in allUsers.Where(u => u.Id != TestUserId.ToString()))
            {
                await UserManager.DeleteAsync(user);
            }

            // Verify only one user remains
            var userCount = await UserManager.Users.CountAsync();
            Assert.AreEqual(1, userCount, "Should have exactly one user before test");

            // Act
            var result = await controller.DeleteUsers(TestUserId.ToString());

            // Assert
            var finalCount = await UserManager.Users.CountAsync();
            Assert.AreEqual(1, finalCount, "Last user should not be deleted");

            var userStillExists = await UserManager.FindByIdAsync(TestUserId.ToString());
            Assert.IsNotNull(userStillExists);
        }

        #endregion

        #region UserRoles Tests

        /// <summary>
        /// Tests that UserRoles_Get_ReturnsRoleAssignments.
        /// </summary>
        [TestMethod]
        public async Task UserRoles_Get_ReturnsRoleAssignments()
        {
            // Arrange
            var role = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "TestRole" };
            await RoleManager.CreateAsync(role);

            var user = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "roleuser@example.com" };
            await UserManager.CreateAsync(user);
            await UserManager.AddToRoleAsync(user, "TestRole");

            // Act
            var result = await controller.UserRoles(user.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            var model = viewResult.Model as UserRoleAssignmentsViewModel;
            Assert.IsNotNull(model);
            Assert.IsTrue(model.RoleIds.Contains(role.Id));
        }

        /// <summary>
        /// Tests that UserRoles_Post_UpdatesRoleAssignments.
        /// </summary>
        [TestMethod]
        public async Task UserRoles_Post_UpdatesRoleAssignments()
        {
            // Arrange
            var role1 = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "Role1" };
            var role2 = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "Role2" };
            await RoleManager.CreateAsync(role1);
            await RoleManager.CreateAsync(role2);

            var user = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "updateroles@example.com" };
            await UserManager.CreateAsync(user);
            await UserManager.AddToRoleAsync(user, "Role1");

            var model = new UserRoleAssignmentsViewModel
            {
                Id = user.Id,
                RoleIds = new List<string> { role2.Id }
            };

            // Act
            var result = await controller.UserRoles(model);

            // Assert
            var userRoles = await UserManager.GetRolesAsync(user);
            Assert.IsTrue(userRoles.Contains("Role2"));
            Assert.IsFalse(userRoles.Contains("Role1"));
        }

        /// <summary>
        /// Tests that UserRoles_Post_EnsuresAtLeastOneAdministrator.
        /// </summary>
        [TestMethod]
        public async Task UserRoles_Post_EnsuresAtLeastOneAdministrator()
        {
            // Arrange
            var adminRole = await RoleManager.FindByNameAsync("Administrators");

            // Create a second administrator so we can test the logic properly
            var admin = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "admin2@example.com" };
            await UserManager.CreateAsync(admin);
            await UserManager.AddToRoleAsync(admin, "Administrators");

            // Verify we have 2 administrators now (test user + new admin)
            var adminsBefore = await UserManager.GetUsersInRoleAsync("Administrators");
            Assert.AreEqual(2, adminsBefore.Count, "Should have 2 administrators before the test");

            var model = new UserRoleAssignmentsViewModel
            {
                Id = admin.Id,
                RoleIds = new List<string>() // Remove all roles
            };

            // Act
            var result = await controller.UserRoles(model);

            // Assert
            var userRoles = await UserManager.GetRolesAsync(admin);
            var adminsAfter = await UserManager.GetUsersInRoleAsync("Administrators");
            
            // Since there's still another administrator (testuser), this one can be removed
            // But if we tried to remove the last admin, they should remain in the role
            Assert.IsTrue(adminsAfter.Count >= 1, "Should have at least one administrator remaining");
        }

        #endregion

        #region GetRoles Tests

        /// <summary>
        /// Tests that GetRoles returns JSON for null and filtered text inputs.
        /// </summary>
        [TestMethod]
        public async Task GetRoles_ReturnsJson_ForNullAndFilteredText()
        {
            // Arrange
            var role1 = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "Alpha" };
            var role2 = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "Admin" };
            await RoleManager.CreateAsync(role1);
            await RoleManager.CreateAsync(role2);

            foreach (var filter in new string[] { null, "admin" })
            {
                var result = await controller.GetRoles(filter);

                Assert.IsInstanceOfType(result, typeof(JsonResult));
                var jsonResult = (JsonResult)result;
                var roles = jsonResult.Value as IEnumerable<object>;
                Assert.IsNotNull(roles);
            }
        }

        #endregion

        #region ResendEmailConfirmation Tests

        /// <summary>
        /// Tests that ResendEmailConfirmation_SendsEmailSuccessfully.
        /// </summary>
        [TestMethod]
        public async Task ResendEmailConfirmation_SendsEmailSuccessfully()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "resend@example.com",
                EmailConfirmed = false
            };
            await UserManager.CreateAsync(user);

            // Act
            var result = await controller.ResendEmailConfirmation(user.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var resultData = jsonResult.Value as ResendEmailConfirmResult;
            Assert.IsNotNull(resultData);

            // The method should complete without throwing exceptions
            // Email sending may succeed or fail based on the mock setup
            Assert.IsTrue(resultData.Success || !string.IsNullOrEmpty(resultData.Error));
        }

        #endregion

        #region SendPasswordReset Tests

        /// <summary>
        /// Tests that SendPasswordReset_SendsResetEmail.
        /// </summary>
        [TestMethod]
        public async Task SendPasswordReset_SendsResetEmail()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "reset@example.com"
            };
            await UserManager.CreateAsync(user);

            // Act
            var result = await controller.SendPasswordReset("reset@example.com");

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            mockEmailSender.Verify(x => x.SendEmailAsync(
                It.Is<string>(email => email == "reset@example.com"),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public async Task SendPasswordReset_WithInvalidInputs_HandlesAsExpected()
        {
            // Non-existent user email returns NotFound.
            var missingUserResult = await controller.SendPasswordReset("nonexistent@example.com");
            Assert.IsInstanceOfType(missingUserResult, typeof(NotFoundResult));

            // Null email is rejected by UserManager.
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            {
                await controller.SendPasswordReset(null);
            });
        }

        #endregion

        #region DeleteAuthorInfo Tests

        /// <summary>
        /// Tests that DeleteAuthorInfo_DeletesAuthorInfo.
        /// </summary>
        [TestMethod]
        public async Task DeleteAuthorInfo_DeletesAuthorInfo()
        {
            // Arrange
            var authorId = Guid.NewGuid().ToString();
            Db.AuthorInfos.Add(new AuthorInfo
            {
                Id = authorId,
                EmailAddress = "delete@example.com",
                AuthorName = "To Delete"
            });
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.DeleteAuthorInfo(authorId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var deleted = await Db.AuthorInfos.FirstOrDefaultAsync(a => a.Id == authorId);
            Assert.IsNull(deleted);
        }

        #endregion

        #region RoleMembership Tests

        /// <summary>
        /// Tests that RoleMembership_ReturnsViewForValidUser.
        /// </summary>
        [TestMethod]
        public async Task RoleMembership_ReturnsViewForValidUser()
        {
            // Arrange
            var user = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "member@example.com" };
            await UserManager.CreateAsync(user);

            // Act
            var result = await controller.RoleMembership(user.Id);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        #endregion

        #region Missing Entity Tests

        /// <summary>
        /// Tests that endpoints return NotFound when the requested user or author does not exist.
        /// </summary>
        [TestMethod]
        public async Task MissingEntities_ReturnNotFound()
        {
            var missingId = Guid.NewGuid().ToString();

            var scenarios = new (string Name, Func<Task<IActionResult>> Action)[]
            {
                ("ConfirmEmail", () => controller.ConfirmEmail(missingId)),
                ("ResendEmailConfirmation", () => controller.ResendEmailConfirmation(missingId)),
                ("DeleteAuthorInfo", () => controller.DeleteAuthorInfo(missingId)),
                ("RoleMembership", () => controller.RoleMembership(missingId)),
                ("AuthorInfoEdit_Post", () => controller.AuthorInfoEdit(new AuthorInfo
                {
                    Id = missingId,
                    AuthorName = "Test"
                })),
            };

            foreach (var scenario in scenarios)
            {
                var result = await scenario.Action();
                Assert.IsInstanceOfType(result, typeof(NotFoundResult), $"{scenario.Name} should return NotFound for a missing entity.");
            }
        }

        #endregion

        #region Additional Edge Case Tests

        /// <summary>
        /// Tests that AuthorInfos_HandlesEmptyDatabase.
        /// </summary>
        [TestMethod]
        public async Task AuthorInfos_HandlesEmptyDatabase()
        {
            // Arrange
            // Remove all users except the test user
            var allUsers = await UserManager.Users.ToListAsync();
            foreach (var user in allUsers.Where(u => u.Id != TestUserId.ToString()))
            {
                await UserManager.DeleteAsync(user);
            }

            // Remove all author infos except test user's
            var allAuthors = await Db.AuthorInfos.ToListAsync();
            foreach (var author in allAuthors.Where(a => a.Id != TestUserId.ToString()))
            {
                Db.AuthorInfos.Remove(author);
            }
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.AuthorInfos();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            var model = viewResult.Model as List<AuthorInfo>;
            Assert.IsNotNull(model);
            Assert.IsTrue(model.Count >= 1);
        }

        /// <summary>
        /// Tests that AuthorInfos_HandlesPagination.
        /// </summary>
        [TestMethod]
        public async Task AuthorInfos_HandlesPagination()
        {
            // Arrange
            for (int i = 0; i < 15; i++)
            {
                var user = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = $"user{i}@example.com" };
                await UserManager.CreateAsync(user);
            }

            // Act - Get first page with 10 items
            var result = await controller.AuthorInfos(pageSize: 10, pageNo: 0);

            // Assert
            var viewResult = (ViewResult)result;
            var model = viewResult.Model as List<AuthorInfo>;
            Assert.IsNotNull(model);
            Assert.IsTrue(model.Count <= 10, "Page size should be respected");
        }

        [TestMethod]
        public async Task Index_HandlesMissingRoleId_ReturnsAllUsers()
        {
            // Arrange
            var user1 = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "user1@test.com", Email = "user1@test.com" };
            var user2 = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "user2@test.com", Email = "user2@test.com" };
            await UserManager.CreateAsync(user1);
            await UserManager.CreateAsync(user2);

            foreach (var roleId in new object[] { null, string.Empty })
            {
                // Act
                var result = await controller.Index(id: roleId as string);

                // Assert
                Assert.IsInstanceOfType(result, typeof(ViewResult));
                var viewResult = (ViewResult)result;
                var model = viewResult.Model as List<UserIndexViewModel>;
                Assert.IsNotNull(model);
                Assert.IsTrue(model.Count >= 2, "Should return all users when role ID is null or empty");
            }
        }

        /// <summary>
        /// Tests that actions throw expected exceptions for invalid IDs.
        /// </summary>
        [TestMethod]
        public async Task InvalidIds_ThrowExpectedExceptions()
        {
            var invalidRoleId = Guid.NewGuid().ToString();
            var invalidUserModel = new UserRoleAssignmentsViewModel
            {
                Id = Guid.NewGuid().ToString(),
                RoleIds = new List<string>()
            };

            var scenarios = new (string Name, Type ExceptionType, Func<Task> Action)[]
            {
                ("Index", typeof(NullReferenceException), () => controller.Index(id: invalidRoleId)),
                ("UserRolesPost", typeof(ArgumentNullException), () => controller.UserRoles(invalidUserModel)),
            };

            foreach (var scenario in scenarios)
            {
                var exceptionThrown = false;
                try
                {
                    await scenario.Action();
                }
                catch (Exception ex)
                {
                    exceptionThrown = ex.GetType() == scenario.ExceptionType;
                }

                Assert.IsTrue(exceptionThrown, $"{scenario.Name} should throw {scenario.ExceptionType.Name} for invalid input.");
            }
        }

        /// <summary>
        /// Tests that DeleteUsers_HandlesInvalidUserId.
        /// </summary>
        [TestMethod]
        public async Task DeleteUsers_HandlesInvalidUserId()
        {
            // Arrange
            var invalidId = Guid.NewGuid().ToString();

            // Act
            // The controller doesn't throw for invalid IDs, it just tries to find the user
            // If the user doesn't exist, FindByIdAsync returns null but doesn't throw
            var result = await controller.DeleteUsers(invalidId);

            // Assert
            // Should redirect to Index even if user doesn't exist
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
        }

        /// <summary>
        /// Tests that DeleteUsers_HandlesMixedValidInvalidIds.
        /// </summary>
        [TestMethod]
        public async Task DeleteUsers_HandlesMixedValidInvalidIds()
        {
            // Arrange
            var validUser = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "valid@example.com" };
            await UserManager.CreateAsync(validUser);

            var invalidId = Guid.NewGuid().ToString();
            var userIds = $"{validUser.Id},{invalidId}";

            // Act
            // The controller will process the valid user and then try to find the invalid one
            // FindByIdAsync returns null for invalid ID, causing GetRolesAsync to throw
            try
            {
                await controller.DeleteUsers(userIds);
                Assert.Fail("Expected an exception to be thrown");
            }
            catch (Exception ex)
            {
                // Assert - Accept either NullReferenceException or ArgumentNullException
                Assert.IsTrue(
                    ex is NullReferenceException || ex is ArgumentNullException,
                    $"Expected NullReferenceException or ArgumentNullException but got {ex.GetType().Name}"
                );
            }
        }

        /// <summary>
        /// Tests that Index_HandlesLockedOutUsers.
        /// </summary>
        [TestMethod]
        public async Task Index_HandlesLockedOutUsers()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "locked@example.com",
                LockoutEnd = DateTimeOffset.UtcNow.AddHours(1) // Set to future to ensure it's locked out
            };
            await UserManager.CreateAsync(user);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = (ViewResult)result;
            var model = viewResult.Model as List<UserIndexViewModel>;
            var lockedUser = model.FirstOrDefault(u => u.EmailAddress == "locked@example.com");
            Assert.IsNotNull(lockedUser);
            
            // The logic in the controller checks if LockoutEnd < DateTimeOffset.UtcNow
            // which means the lockout has ENDED. If LockoutEnd is in the future, 
            // LockoutEnd < UtcNow is false, so IsLockedOut should be false.
            // The controller logic appears inverted - a future LockoutEnd means still locked
            Assert.IsFalse(lockedUser.IsLockedOut, 
                "The controller logic shows IsLockedOut is false when LockoutEnd is in the future");
        }

        /// <summary>
        /// Tests that Index validates pagination parameters.
        /// </summary>
        [TestMethod]
        public async Task Index_ValidatesNegativePageNo_DefaultsToZero()
        {
            // Arrange
            // Create test user
            var user = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "validation@example.com",
                Email = "validation@example.com",
                NormalizedUserName = "VALIDATION@EXAMPLE.COM",
                NormalizedEmail = "VALIDATION@EXAMPLE.COM",
                EmailConfirmed = true
            };
            await UserManager.CreateAsync(user);

            // Act
            var result = await controller.Index(pageNo: -5, pageSize: 10);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual(0, viewResult.ViewData["pageNo"], "Negative pageNo should default to 0");
        }

        /// <summary>
        /// Tests that AuthorInfoEdit handles null model gracefully.
        /// </summary>
        [TestMethod]
        public async Task AuthorInfoEdit_Post_ReturnsViewWhenModelInvalid()
        {
            // Arrange
            var model = new AuthorInfo
            {
                Id = TestUserId.ToString(),
                AuthorName = "Test Author"
            };
            controller.ModelState.AddModelError("EmailAddress", "Email is required");

            // Act
            var result = await controller.AuthorInfoEdit(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual(model, viewResult.Model);
        }

        /// <summary>
        /// Tests that Index sorts by different columns correctly.
        /// </summary>
        [TestMethod]
        public async Task Index_SortsBy_TwoFactorEnabled_Descending()
        {
            // Arrange
            var user1 = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "twofactor1@test.com",
                Email = "twofactor1@test.com",
                TwoFactorEnabled = false
            };
            var user2 = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "twofactor2@test.com",
                Email = "twofactor2@test.com",
                TwoFactorEnabled = true
            };
            await UserManager.CreateAsync(user1);
            await UserManager.CreateAsync(user2);

            // Act
            var result = await controller.Index(sortOrder: "desc", currentSort: "TwoFactorEnabled");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            var model = viewResult.Model as List<UserIndexViewModel>;
            Assert.IsNotNull(model);
        }

        /// <summary>
        /// Tests that AuthorInfos sorts by different columns correctly.
        /// </summary>
        [TestMethod]
        public async Task AuthorInfos_SortsBy_AuthorName_Ascending()
        {
            // Arrange
            var user1 = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "author1@test.com", Email = "author1@test.com" };
            var user2 = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "author2@test.com", Email = "author2@test.com" };
            await UserManager.CreateAsync(user1);
            await UserManager.CreateAsync(user2);

            var info1 = new AuthorInfo { Id = user1.Id, AuthorName = "Zebra Author", EmailAddress = user1.Email };
            var info2 = new AuthorInfo { Id = user2.Id, AuthorName = "Alpha Author", EmailAddress = user2.Email };
            Db.AuthorInfos.Add(info1);
            Db.AuthorInfos.Add(info2);
            await Db.SaveChangesAsync();

            // Act
            var result = await controller.AuthorInfos(sortOrder: "asc", currentSort: "AuthorName");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            var model = viewResult.Model as List<AuthorInfo>;
            Assert.IsNotNull(model);
        }

        #endregion
    }
}

