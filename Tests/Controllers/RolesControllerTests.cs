// <copyright file="RolesControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Models;
    using Sky.Editor.Controllers;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for the <see cref="RolesController"/> class.
    /// </summary>
    [TestClass]
    public class RolesControllerTests : SkyCmsTestBase
    {
        private RolesController controller;

        [TestInitialize]
        public new void Setup()
        {
            InitializeTestContext(seedLayout: true);

            // Ensure all required roles exist before creating controller
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

            controller = new RolesController(
                UserManager,
                RoleManager,
                Db);

            // Setup HttpContext for the controller
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("example.com");
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "testuser@example.com"),
                new Claim(ClaimTypes.Role, "Administrators")
            }, "mock"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        [TestCleanup]
        public void Cleanup()
        {
            Db.Dispose();
        }

        #region Create Tests

        /// <summary>
        /// Tests that Create_WithValidRoleName_CreatesRole.
        /// </summary>
        [TestMethod]
        public async Task Create_WithValidRoleName_CreatesRole()
        {
            // Arrange
            var roleName = "TestRole";

            // Act
            var result = await controller.Create(roleName);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
            var roleExists = await RoleManager.RoleExistsAsync(roleName);
            Assert.IsTrue(roleExists);
        }

        /// <summary>
        /// Tests that Create rejects null and empty role names.
        /// </summary>
        [TestMethod]
        public async Task Create_WithMissingRoleName_ReturnsBadRequest()
        {
            foreach (var roleName in new string[] { string.Empty, null })
            {
                // Act
                var result = await controller.Create(roleName);

                // Assert
                Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
                var badRequest = result as BadRequestObjectResult;
                Assert.AreEqual("Rule name is required.", badRequest.Value);
            }
        }

        /// <summary>
        /// Tests that Create_WithDuplicateRoleName_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task Create_WithDuplicateRoleName_ReturnsBadRequest()
        {
            // Arrange
            var roleName = "DuplicateRole";
            await RoleManager.CreateAsync(new IdentityRole(roleName));

            // Act
            var result = await controller.Create(roleName);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequest = result as BadRequestObjectResult;
            Assert.IsTrue(badRequest.Value.ToString().Contains("already exists"));
        }

        /// <summary>
        /// Tests that Create_WithInvalidModelState_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task Create_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            controller.ModelState.AddModelError("Error", "Invalid model state");

            // Act
            var result = await controller.Create("TestRole");

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        #region Index Tests

        /// <summary>
        /// Tests that Index_ReturnsViewWithAllRoles.
        /// </summary>
        [TestMethod]
        public async Task Index_ReturnsViewWithAllRoles()
        {
            // Arrange
            await RoleManager.CreateAsync(new IdentityRole("Role1"));
            await RoleManager.CreateAsync(new IdentityRole("Role2"));

            // Act
            var result = await controller.Index(null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<IdentityRole>;
            Assert.IsNotNull(model);
            Assert.IsTrue(model.Count >= 2);
        }

        /// <summary>
        /// Tests that Index_SortsRolesByNameAscending.
        /// </summary>
        [TestMethod]
        public async Task Index_SortsRolesByNameAscending()
        {
            // Arrange
            await RoleManager.CreateAsync(new IdentityRole("ZRole"));
            await RoleManager.CreateAsync(new IdentityRole("ARole"));

            // Act
            var result = await controller.Index(null, sortOrder: "asc", currentSort: "Name");

            // Assert
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<IdentityRole>;
            Assert.IsTrue(model.Any(r => r.Name == "ARole"));
            Assert.IsTrue(model.Any(r => r.Name == "ZRole"));
        }

        /// <summary>
        /// Tests that Index_SortsRolesByNameDescending.
        /// </summary>
        [TestMethod]
        public async Task Index_SortsRolesByNameDescending()
        {
            // Arrange
            await RoleManager.CreateAsync(new IdentityRole("ZRole"));
            await RoleManager.CreateAsync(new IdentityRole("ARole"));

            // Act
            var result = await controller.Index(null, sortOrder: "desc", currentSort: "Name");

            // Assert
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<IdentityRole>;
            Assert.IsNotNull(model);
            Assert.IsTrue(model.Any(r => r.Name == "ARole"));
        }

        /// <summary>
        /// Tests that Index_HandlesPagination.
        /// </summary>
        [TestMethod]
        public async Task Index_HandlesPagination()
        {
            // Arrange
            for (int i = 0; i < 15; i++)
            {
                await RoleManager.CreateAsync(new IdentityRole($"Role{i}"));
            }

            // Act
            var result = await controller.Index(null, pageSize: 10, pageNo: 0);

            // Assert
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<IdentityRole>;
            Assert.IsTrue(model.Count <= 10);
        }

        /// <summary>
        /// Tests that Index_WithIds_SetsViewData.
        /// </summary>
        [TestMethod]
        public async Task Index_WithIds_SetsViewData()
        {
            // Arrange
            var role = await RoleManager.CreateAsync(new IdentityRole("TestRole"));
            var roleId = (await RoleManager.FindByNameAsync("TestRole")).Id;

            // Act
            var result = await controller.Index(roleId);

            // Assert
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult.ViewData["Ids"]);
        }

        #endregion

        #region Delete Tests

        /// <summary>
        /// Tests that Delete_WithValidRoleId_DeletesRole.
        /// </summary>
        [TestMethod]
        public async Task Delete_WithValidRoleId_DeletesRole()
        {
            // Arrange
            var role = new IdentityRole("RoleToDelete");
            await RoleManager.CreateAsync(role);
            var roleId = (await RoleManager.FindByNameAsync("RoleToDelete")).Id;

            // Act
            var result = await controller.Delete(new[] { roleId });

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
            var roleExists = await RoleManager.RoleExistsAsync("RoleToDelete");
            Assert.IsFalse(roleExists);
        }

        /// <summary>
        /// Tests that Delete_WithProtectedRole_DoesNotDelete.
        /// </summary>
        [TestMethod]
        public async Task Delete_WithProtectedRole_DoesNotDelete()
        {
            // Arrange
            var protectedRoles = new[] { "Administrators", "Authors", "Editors" };
            var roleIds = new List<string>();

            foreach (var roleName in protectedRoles)
            {
                var role = await RoleManager.FindByNameAsync(roleName);
                if (role != null)
                {
                    roleIds.Add(role.Id);
                }
            }

            // Act
            var result = await controller.Delete(roleIds.ToArray());

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
            foreach (var roleName in protectedRoles)
            {
                var roleExists = await RoleManager.RoleExistsAsync(roleName);
                Assert.IsTrue(roleExists, $"Protected role {roleName} should not be deleted");
            }
        }

        /// <summary>
        /// Tests that Delete_WithMultipleRoles_DeletesAllNonProtected.
        /// </summary>
        [TestMethod]
        public async Task Delete_WithMultipleRoles_DeletesAllNonProtected()
        {
            // Arrange
            await RoleManager.CreateAsync(new IdentityRole("Delete1"));
            await RoleManager.CreateAsync(new IdentityRole("Delete2"));
            var role1Id = (await RoleManager.FindByNameAsync("Delete1")).Id;
            var role2Id = (await RoleManager.FindByNameAsync("Delete2")).Id;

            // Act
            var result = await controller.Delete(new[] { role1Id, role2Id });

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
            Assert.IsFalse(await RoleManager.RoleExistsAsync("Delete1"));
            Assert.IsFalse(await RoleManager.RoleExistsAsync("Delete2"));
        }

        /// <summary>
        /// Tests that Delete_WithEmptyArray_ReturnsOk.
        /// </summary>
        [TestMethod]
        public async Task Delete_WithEmptyArray_ReturnsOk()
        {
            // Act
            var result = await controller.Delete(Array.Empty<string>());

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
        }

        #endregion

        #region GetUsers Tests

        /// <summary>
        /// Tests that GetUsers_ReturnsAllUsers.
        /// </summary>
        [TestMethod]
        public async Task GetUsers_ReturnsAllUsers()
        {
            // Arrange
            var user1 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "user1@example.com" };
            var user2 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "user2@example.com" };
            await UserManager.CreateAsync(user1);
            await UserManager.CreateAsync(user2);

            // Act
            var result = await controller.GetUsers(null);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = result as JsonResult;
            Assert.IsNotNull(jsonResult.Value);
        }

        /// <summary>
        /// Tests that GetUsers_WithStartsWith_FiltersUsers.
        /// </summary>
        [TestMethod]
        public async Task GetUsers_WithStartsWith_FiltersUsers()
        {
            // Arrange
            var user1 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "alpha@example.com" };
            var user2 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "beta@example.com" };
            await UserManager.CreateAsync(user1);
            await UserManager.CreateAsync(user2);

            // Act
            var result = await controller.GetUsers("alpha");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        /// <summary>
        /// Tests that GetUsers_IsCaseInsensitive.
        /// </summary>
        [TestMethod]
        public async Task GetUsers_IsCaseInsensitive()
        {
            // Arrange
            var user = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "Test@example.com" };
            await UserManager.CreateAsync(user);

            // Act
            var result = await controller.GetUsers("test");

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
        }

        #endregion

        #region UsersInRole Tests

        /// <summary>
        /// Tests that UsersInRole_Get_ReturnsUsersInRole.
        /// </summary>
        [TestMethod]
        public async Task UsersInRole_Get_ReturnsUsersInRole()
        {
            // Arrange
            var role = new IdentityRole("TestRole");
            await RoleManager.CreateAsync(role);
            var roleId = (await RoleManager.FindByNameAsync("TestRole")).Id;

            var user = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "roleuser@example.com" };
            await UserManager.CreateAsync(user);
            await UserManager.AddToRoleAsync(user, "TestRole");

            // Act
            var result = await controller.UsersInRole(roleId);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<UserIndexViewModel>;
            Assert.IsNotNull(model);
            Assert.IsTrue(model.Any(u => u.EmailAddress == "roleuser@example.com"));
        }

        /// <summary>
        /// Tests that UsersInRole_Get_WithInvalidRoleId_ReturnsNotFound.
        /// </summary>
        [TestMethod]
        public async Task UsersInRole_Get_WithInvalidRoleId_ReturnsNotFound()
        {
            // Act
            var result = await controller.UsersInRole(Guid.NewGuid().ToString());

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundResult));
        }

        /// <summary>
        /// Tests that UsersInRole_Get_SetsViewData.
        /// </summary>
        [TestMethod]
        public async Task UsersInRole_Get_SetsViewData()
        {
            // Arrange
            var role = new IdentityRole("TestRole");
            await RoleManager.CreateAsync(role);
            var roleId = (await RoleManager.FindByNameAsync("TestRole")).Id;

            // Act
            var result = await controller.UsersInRole(roleId);

            // Assert
            var viewResult = result as ViewResult;
            Assert.IsNotNull(viewResult.ViewData["RoleInfo"]);
            Assert.IsNotNull(viewResult.ViewData["sortOrder"]);
            Assert.IsNotNull(viewResult.ViewData["currentSort"]);
        }

        /// <summary>
        /// Tests that UsersInRole_Get_HandlesSorting.
        /// </summary>
        [TestMethod]
        public async Task UsersInRole_Get_HandlesSorting()
        {
            // Arrange
            var role = new IdentityRole("TestRole");
            await RoleManager.CreateAsync(role);
            var roleId = (await RoleManager.FindByNameAsync("TestRole")).Id;

            var user1 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "a@example.com" };
            var user2 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "z@example.com" };
            await UserManager.CreateAsync(user1);
            await UserManager.CreateAsync(user2);
            await UserManager.AddToRoleAsync(user1, "TestRole");
            await UserManager.AddToRoleAsync(user2, "TestRole");

            // Act
            var result = await controller.UsersInRole(roleId, sortOrder: "desc", currentSort: "EmailAddress");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<UserIndexViewModel>;
            Assert.IsNotNull(model);
        }

        /// <summary>
        /// Tests that UsersInRole_Post_AddsUsersToRole.
        /// </summary>
        [TestMethod]
        public async Task UsersInRole_Post_AddsUsersToRole()
        {
            // Arrange
            var role = new IdentityRole("TestRole");
            await RoleManager.CreateAsync(role);
            var roleId = (await RoleManager.FindByNameAsync("TestRole")).Id;

            var user = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "newuser@example.com" };
            await UserManager.CreateAsync(user);

            var model = new UsersInRoleViewModel
            {
                RoleId = roleId,
                RoleName = "TestRole",
                UserIds = new string[] { user.Id }
            };

            // Act
            var result = await controller.UsersInRole(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var userRoles = await UserManager.GetRolesAsync(user);
            Assert.IsTrue(userRoles.Contains("TestRole"));
        }

        /// <summary>
        /// Tests that UsersInRole_Post_WithInvalidModelState_ReturnsViewWithModel.
        /// </summary>
        [TestMethod]
        public async Task UsersInRole_Post_WithInvalidModelState_ReturnsViewWithModel()
        {
            // Arrange
            controller.ModelState.AddModelError("Error", "Invalid");
            var user = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "test@example.com" };
            await UserManager.CreateAsync(user);

            var model = new UsersInRoleViewModel
            {
                RoleId = Guid.NewGuid().ToString(),
                RoleName = "TestRole",
                UserIds = new string[] { user.Id }
            };

            // Act
            var result = await controller.UsersInRole(model);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = result as ViewResult;
            var returnedModel = viewResult.Model as UsersInRoleViewModel;
            Assert.IsNotNull(returnedModel.Users);
        }

        /// <summary>
        /// Tests that UsersInRole_Get_HandlesPagination.
        /// </summary>
        [TestMethod]
        public async Task UsersInRole_Get_HandlesPagination()
        {
            // Arrange
            var role = new IdentityRole("TestRole");
            await RoleManager.CreateAsync(role);
            var roleId = (await RoleManager.FindByNameAsync("TestRole")).Id;

            for (int i = 0; i < 15; i++)
            {
                var user = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = $"user{i}@example.com" };
                await UserManager.CreateAsync(user);
                await UserManager.AddToRoleAsync(user, "TestRole");
            }

            // Act
            var result = await controller.UsersInRole(roleId, pageSize: 10, pageNo: 0);

            // Assert
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<UserIndexViewModel>;
            Assert.IsTrue(model.Count <= 10);
        }

        #endregion

        #region RemoveUsers Tests

        /// <summary>
        /// Tests that RemoveUsers_RemovesUsersFromRole.
        /// </summary>
        [TestMethod]
        public async Task RemoveUsers_RemovesUsersFromRole()
        {
            // Arrange
            var role = new IdentityRole("TestRole");
            await RoleManager.CreateAsync(role);
            var roleId = (await RoleManager.FindByNameAsync("TestRole")).Id;

            var user = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "removeuser@example.com" };
            await UserManager.CreateAsync(user);
            await UserManager.AddToRoleAsync(user, "TestRole");

            // Act
            var result = await controller.RemoveUsers(roleId, new[] { user.Id });

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var userRoles = await UserManager.GetRolesAsync(user);
            Assert.IsFalse(userRoles.Contains("TestRole"));
        }

        /// <summary>
        /// Tests that RemoveUsers_WithMultipleUsers_RemovesAll.
        /// </summary>
        [TestMethod]
        public async Task RemoveUsers_WithMultipleUsers_RemovesAll()
        {
            // Arrange
            var role = new IdentityRole("TestRole");
            await RoleManager.CreateAsync(role);
            var roleId = (await RoleManager.FindByNameAsync("TestRole")).Id;

            var user1 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "user1@example.com" };
            var user2 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "user2@example.com" };
            await UserManager.CreateAsync(user1);
            await UserManager.CreateAsync(user2);
            await UserManager.AddToRoleAsync(user1, "TestRole");
            await UserManager.AddToRoleAsync(user2, "TestRole");

            // Act
            var result = await controller.RemoveUsers(roleId, new[] { user1.Id, user2.Id });

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var user1Roles = await UserManager.GetRolesAsync(user1);
            var user2Roles = await UserManager.GetRolesAsync(user2);
            Assert.IsFalse(user1Roles.Contains("TestRole"));
            Assert.IsFalse(user2Roles.Contains("TestRole"));
        }

        /// <summary>
        /// Tests that RemoveUsers_WithUserAdministratorsRole_MaintainsAtLeastOne.
        /// </summary>
        [TestMethod]
        public async Task RemoveUsers_WithUserAdministratorsRole_MaintainsAtLeastOne()
        {
            // Arrange
            var role = new IdentityRole("User Administrators");
            await RoleManager.CreateAsync(role);
            var roleId = (await RoleManager.FindByNameAsync("User Administrators")).Id;

            var user1 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "admin1@example.com" };
            var user2 = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "admin2@example.com" };
            await UserManager.CreateAsync(user1);
            await UserManager.CreateAsync(user2);
            await UserManager.AddToRoleAsync(user1, "User Administrators");
            await UserManager.AddToRoleAsync(user2, "User Administrators");

            // Act
            var result = await controller.RemoveUsers(roleId, new[] { user1.Id });

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var admins = await UserManager.GetUsersInRoleAsync("User Administrators");
            Assert.IsTrue(admins.Count >= 1, "At least one User Administrator should remain");
        }

        /// <summary>
        /// Tests that RemoveUsers_WithEmptyUserArray_ReturnsRedirect.
        /// </summary>
        [TestMethod]
        public async Task RemoveUsers_WithEmptyUserArray_ReturnsRedirect()
        {
            // Arrange
            var role = new IdentityRole("TestRole");
            await RoleManager.CreateAsync(role);
            var roleId = (await RoleManager.FindByNameAsync("TestRole")).Id;

            // Act
            var result = await controller.RemoveUsers(roleId, Array.Empty<string>());

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
        }

        #endregion

        #region Edge Case Tests

        /// <summary>
        /// Tests that actions return BadRequest when model state is invalid.
        /// </summary>
        [TestMethod]
        public async Task Actions_WithInvalidModelState_ReturnBadRequest()
        {
            var scenarios = new (string Name, Func<Task<IActionResult>> Action)[]
            {
                ("Index", async () => (IActionResult)await controller.Index(null)),
                ("Delete", async () => (IActionResult)await controller.Delete(new[] { "id" })),
                ("GetUsers", async () => (IActionResult)await controller.GetUsers("test")),
                ("RemoveUsers", async () => (IActionResult)await controller.RemoveUsers("roleId", new[] { "userId" })),
                ("UsersInRole", async () => (IActionResult)await controller.UsersInRole("roleId")),
            };

            foreach (var scenario in scenarios)
            {
                controller.ModelState.AddModelError("Error", "Invalid");
                var result = await scenario.Action();

                Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), $"{scenario.Name} should return BadRequest when ModelState is invalid.");
                controller.ModelState.Clear();
            }
        }

        /// <summary>
        /// Tests that UsersInRole_Get_IncludesRoleMembershipForUsers.
        /// </summary>
        [TestMethod]
        public async Task UsersInRole_Get_IncludesRoleMembershipForUsers()
        {
            // Arrange
            var role1 = new IdentityRole("Role1");
            var role2 = new IdentityRole("Role2");
            await RoleManager.CreateAsync(role1);
            await RoleManager.CreateAsync(role2);
            var role1Id = (await RoleManager.FindByNameAsync("Role1")).Id;

            var user = new IdentityUser { Id = Guid.NewGuid().ToString(), Email = "multiuser@example.com" };
            await UserManager.CreateAsync(user);
            await UserManager.AddToRoleAsync(user, "Role1");
            await UserManager.AddToRoleAsync(user, "Role2");

            // Act
            var result = await controller.UsersInRole(role1Id);

            // Assert
            var viewResult = result as ViewResult;
            var model = viewResult.Model as List<UserIndexViewModel>;
            var userModel = model.FirstOrDefault(u => u.EmailAddress == "multiuser@example.com");
            Assert.IsNotNull(userModel);
            Assert.IsTrue(userModel.RoleMembership.Contains("Role1"));
            Assert.IsTrue(userModel.RoleMembership.Contains("Role2"));
        }

        /// <summary>
        /// Tests that Delete_WithNullArray_ReturnsBadRequest.
        /// </summary>
        [TestMethod]
        public async Task Delete_WithNullArray_ReturnsBadRequest()
        {
            // Act & Assert
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            {
                await controller.Delete(null);
            });
        }

        /// <summary>
        /// Tests that Index validates negative pageNo parameter.
        /// </summary>
        [TestMethod]
        public async Task Index_ValidatesNegativePageNo_UsesZero()
        {
            // Arrange
            await RoleManager.CreateAsync(new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "TestRole1" });

            // Act
            var result = await controller.Index(ids: null, pageNo: -5, pageSize: 10);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.IsNotNull(viewResult.Model);
        }

        /// <summary>
        /// Tests that Index handles multiple role IDs correctly.
        /// </summary>
        [TestMethod]
        public async Task Index_HandlesMultipleIds_ParsesCommaSeparated()
        {
            // Arrange
            var role1 = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "Role1" };
            var role2 = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "Role2" };
            await RoleManager.CreateAsync(role1);
            await RoleManager.CreateAsync(role2);

            // Act
            var result = await controller.Index(ids: $"{role1.Id},{role2.Id}");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            var idsInViewData = viewResult.ViewData["Ids"] as string[];
            Assert.IsNotNull(idsInViewData);
            Assert.AreEqual(2, idsInViewData.Length);
        }

        /// <summary>
        /// Tests that GetUsers handles empty startsWith parameter.
        /// </summary>
        [TestMethod]
        public async Task GetUsers_WithEmptyStartsWith_ReturnsAllUsers()
        {
            // Arrange
            var user1 = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "alpha@test.com", Email = "alpha@test.com" };
            var user2 = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "beta@test.com", Email = "beta@test.com" };
            await UserManager.CreateAsync(user1);
            await UserManager.CreateAsync(user2);

            // Act
            var result = await controller.GetUsers(string.Empty);

            // Assert
            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            var users = jsonResult.Value as IEnumerable<object>;
            Assert.IsNotNull(users);
            Assert.IsTrue(users.Count() >= 2, "Should return all users when startsWith is empty");
        }

        /// <summary>
        /// Tests that UsersInRole GET validates pagination bounds.
        /// </summary>
        [TestMethod]
        public async Task UsersInRole_Get_ValidatesLargePageSize()
        {
            // Arrange
            var role = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "TestRole" };
            await RoleManager.CreateAsync(role);

            // Act
            var result = await controller.UsersInRole(id: role.Id, pageSize: 1000);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            Assert.AreEqual(1000, viewResult.ViewData["pageSize"]);
        }

        /// <summary>
        /// Tests that UsersInRole GET sorts by different fields.
        /// </summary>
        [TestMethod]
        public async Task UsersInRole_Get_SortsByTwoFactorEnabled()
        {
            // Arrange
            var role = new IdentityRole { Id = Guid.NewGuid().ToString(), Name = "SortTestRole" };
            await RoleManager.CreateAsync(role);

            var user1 = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "twofactor1@test.com",
                Email = "twofactor1@test.com",
                TwoFactorEnabled = true
            };
            var user2 = new IdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "twofactor2@test.com",
                Email = "twofactor2@test.com",
                TwoFactorEnabled = false
            };
            await UserManager.CreateAsync(user1);
            await UserManager.CreateAsync(user2);
            await UserManager.AddToRoleAsync(user1, role.Name);
            await UserManager.AddToRoleAsync(user2, role.Name);

            // Act
            var result = await controller.UsersInRole(id: role.Id, sortOrder: "desc", currentSort: "TwoFactorEnabled");

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;
            var model = viewResult.Model as List<UserIndexViewModel>;
            Assert.IsNotNull(model);
        }

        /// <summary>
        /// Tests that Create returns BadRequest when role name exceeds maximum length.
        /// </summary>
        [TestMethod]
        public async Task Create_HandlesException_ReturnsBadRequest()
        {
            // Arrange
            // Create a role with a very long name to trigger validation error
            var longRoleName = new string('A', 300); // Exceeds 256 character limit

            // Act
            var result = await controller.Create(longRoleName);

            // Assert
            // Should return BadRequest due to length validation
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequest = result as BadRequestObjectResult;
            Assert.AreEqual("Role name cannot exceed 256 characters.", badRequest.Value);
        }

        /// <summary>
        /// Tests that Create accepts role name at maximum length boundary.
        /// </summary>
        [TestMethod]
        public async Task Create_WithMaxLengthRoleName_CreatesRole()
        {
            // Arrange
            var maxLengthRoleName = new string('B', 256); // Exactly at 256 character limit

            // Act
            var result = await controller.Create(maxLengthRoleName);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkResult));
            var roleExists = await RoleManager.RoleExistsAsync(maxLengthRoleName);
            Assert.IsTrue(roleExists);
        }

        #endregion
    }
}

