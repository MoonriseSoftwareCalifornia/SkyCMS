// <copyright file="AuthorizationTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Security
{
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Cms.Controllers;
    using Sky.Editor.Controllers;

    /// <summary>
    /// Comprehensive role-based authorization tests for SkyCMS controllers.
    /// Verifies that authorization attributes are properly enforced across all controller actions.
    /// </summary>
    [DoNotParallelize]
    [TestClass]
    public class AuthorizationTests : SkyCmsTestBase
    {
        #region EditorController Authorization Tests

        /// <summary>
        /// Tests that EditorController requires authentication (rejects anonymous users).
        /// </summary>
        [TestMethod]
        public void EditorController_AnonymousUser_RequiresAuthentication()
        {
            // Arrange
            var controller = CreateEditorControllerWithUser(null, isAuthenticated: false);

            // Assert - Verify controller has Authorize attribute
            var controllerType = typeof(EditorController);
            var authorizeAttribute = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsNotNull(authorizeAttribute, "EditorController should have [Authorize] attribute");
            Assert.AreEqual("Reviewers, Administrators, Editors, Authors", authorizeAttribute?.Roles);
        }

        /// <summary>
        /// Tests that Reviewer role can access EditorController.Index.
        /// </summary>
        [TestMethod]
        public async Task EditorController_ReviewerRole_CanAccessIndex()
        {
            // Arrange
            var controller = CreateEditorControllerWithUser("Reviewers");

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsNotNull(result, "Reviewers should have access to Index");
            Assert.IsInstanceOfType(result, typeof(ViewResult));
        }

        /// <summary>
        /// Tests that Author role can access EditorController.
        /// </summary>
        [TestMethod]
        public async Task EditorController_AuthorRole_CanAccessIndex()
        {
            // Arrange
            var controller = CreateEditorControllerWithUser("Authors");

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsNotNull(result, "Authors should have access to Index");
        }

        /// <summary>
        /// Tests that Editor role can access EditorController.
        /// </summary>
        [TestMethod]
        public async Task EditorController_EditorRole_CanAccessIndex()
        {
            // Arrange
            var controller = CreateEditorControllerWithUser("Editors");

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsNotNull(result, "Editors should have access to Index");
        }

        /// <summary>
        /// Tests that Administrator role can access EditorController.
        /// </summary>
        [TestMethod]
        public async Task EditorController_AdminRole_CanAccessIndex()
        {
            // Arrange
            var controller = CreateEditorControllerWithUser("Administrators");

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsNotNull(result, "Administrators should have access to Index");
        }

        /// <summary>
        /// Tests that Authors cannot access Trash (requires Administrators, Editors, or Authors role).
        /// </summary>
        [TestMethod]
        public void EditorController_Trash_RequiresProperRoles()
        {
            // Assert - Verify Trash action has proper authorization
            var trashMethod = typeof(EditorController).GetMethod("Trash");
            var authorizeAttribute = trashMethod?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsNotNull(authorizeAttribute, "Trash should have [Authorize] attribute");
            Assert.AreEqual("Administrators, Editors, Authors", authorizeAttribute?.Roles);
        }

        /// <summary>
        /// Tests that Create action requires Team Members role in addition to standard roles.
        /// </summary>
        [TestMethod]
        public void EditorController_Create_AllowsTeamMembers()
        {
            // Assert - Verify Create action has proper authorization
            var createMethod = typeof(EditorController).GetMethod("Create", new[] { typeof(string), typeof(string), typeof(string), typeof(int), typeof(int) });
            var authorizeAttribute = createMethod?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsNotNull(authorizeAttribute, "Create should have [Authorize] attribute");
            Assert.IsTrue(authorizeAttribute?.Roles?.Contains("Team Members") ?? false);
        }

        #endregion

        #region TemplatesController Authorization Tests

        /// <summary>
        /// Tests that TemplatesController requires Administrators or Editors role.
        /// </summary>
        [TestMethod]
        public void TemplatesController_RequiresAdministratorsOrEditorsRole()
        {
            // Assert
            var controllerType = typeof(TemplatesController);
            var authorizeAttribute = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsNotNull(authorizeAttribute, "TemplatesController should have [Authorize] attribute");
            Assert.AreEqual("Administrators, Editors", authorizeAttribute?.Roles);
        }

        /// <summary>
        /// Tests that Authors cannot access TemplatesController.
        /// </summary>
        [TestMethod]
        public async Task TemplatesController_AuthorRole_CannotAccess()
        {
            // Arrange
            var controller = CreateTemplatesControllerWithUser("Authors");

            // Act
            var result = await controller.Index();

            // Assert - Authors shouldn't have access based on controller-level attribute
            // In production, this would return 403 Forbidden
            var controllerType = typeof(TemplatesController);
            var authorizeAttribute = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsFalse(authorizeAttribute?.Roles?.Contains("Authors") ?? true, "Authors should not be in allowed roles for TemplatesController");
        }

        /// <summary>
        /// Tests that Administrators can access TemplatesController.
        /// </summary>
        [TestMethod]
        public async Task TemplatesController_AdminRole_CanAccess()
        {
            // Arrange
            var controller = CreateTemplatesControllerWithUser("Administrators");

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsNotNull(result, "Administrators should have access to TemplatesController");
        }

        /// <summary>
        /// Tests that Editors can access TemplatesController.
        /// </summary>
        [TestMethod]
        public async Task TemplatesController_EditorRole_CanAccess()
        {
            // Arrange
            var controller = CreateTemplatesControllerWithUser("Editors");

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsNotNull(result, "Editors should have access to TemplatesController");
        }

        #endregion

        #region LayoutsController Authorization Tests

        /// <summary>
        /// Tests that LayoutsController requires Administrators or Editors role.
        /// </summary>
        [TestMethod]
        public void LayoutsController_RequiresAdministratorsOrEditorsRole()
        {
            // Assert
            var controllerType = typeof(LayoutsController);
            var authorizeAttribute = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsNotNull(authorizeAttribute, "LayoutsController should have [Authorize] attribute");
            Assert.AreEqual("Administrators, Editors", authorizeAttribute?.Roles);
        }

        /// <summary>
        /// Tests that Authors cannot access LayoutsController.
        /// </summary>
        [TestMethod]
        public void LayoutsController_AuthorRole_NotAllowed()
        {
            // Assert
            var controllerType = typeof(LayoutsController);
            var authorizeAttribute = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsFalse(authorizeAttribute?.Roles?.Contains("Authors") ?? true, "Authors should not be allowed to access LayoutsController");
        }

        /// <summary>
        /// Tests that Reviewers cannot access LayoutsController.
        /// </summary>
        [TestMethod]
        public void LayoutsController_ReviewerRole_NotAllowed()
        {
            // Assert
            var controllerType = typeof(LayoutsController);
            var authorizeAttribute = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsFalse(authorizeAttribute?.Roles?.Contains("Reviewers") ?? true, "Reviewers should not be allowed to access LayoutsController");
        }

        #endregion

        #region UsersController Authorization Tests

        /// <summary>
        /// Tests that UsersController requires Administrators role only.
        /// </summary>
        [TestMethod]
        public void UsersController_RequiresAdministratorsRoleOnly()
        {
            // Assert
            var controllerType = typeof(UsersController);
            var authorizeAttribute = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsNotNull(authorizeAttribute, "UsersController should have [Authorize] attribute");
            Assert.AreEqual("Administrators", authorizeAttribute?.Roles);
        }

        /// <summary>
        /// Tests that Editors cannot access UsersController.
        /// </summary>
        [TestMethod]
        public void UsersController_EditorRole_NotAllowed()
        {
            // Assert
            var controllerType = typeof(UsersController);
            var authorizeAttribute = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsFalse(authorizeAttribute?.Roles?.Contains("Editors") ?? true, "Editors should not be allowed to manage users");
        }

        /// <summary>
        /// Tests that Authors cannot access UsersController.
        /// </summary>
        [TestMethod]
        public void UsersController_AuthorRole_NotAllowed()
        {
            // Assert
            var controllerType = typeof(UsersController);
            var authorizeAttribute = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsFalse(authorizeAttribute?.Roles?.Contains("Authors") ?? true, "Authors should not be allowed to manage users");
        }

        #endregion

        #region RolesController Authorization Tests

        /// <summary>
        /// Tests that RolesController requires Administrators role only.
        /// </summary>
        [TestMethod]
        public void RolesController_RequiresAdministratorsRoleOnly()
        {
            // Assert
            var controllerType = typeof(RolesController);
            var authorizeAttribute = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsNotNull(authorizeAttribute, "RolesController should have [Authorize] attribute");
            Assert.AreEqual("Administrators", authorizeAttribute?.Roles);
        }

        #endregion

        #region BlogController Authorization Tests

        /// <summary>
        /// Tests that BlogController requires authentication.
        /// </summary>
        [TestMethod]
        public void BlogController_RequiresAuthentication()
        {
            // Assert
            var controllerType = typeof(BlogController);
            var authorizeAttribute = controllerType.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsNotNull(authorizeAttribute, "BlogController should have [Authorize] attribute");
        }

        /// <summary>
        /// Tests that BlogController.PreviewStream allows anonymous access.
        /// </summary>
        [TestMethod]
        public void BlogController_PreviewStream_AllowsAnonymous()
        {
            // Assert
            var previewMethod = typeof(BlogController).GetMethod("PreviewStream");
            var allowAnonymousAttribute = previewMethod?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true)
                .FirstOrDefault();

            Assert.IsNotNull(allowAnonymousAttribute, "PreviewStream should allow anonymous access");
        }

        #endregion

        #region Publishing Authorization Tests

        /// <summary>
        /// Tests that PublishPage requires proper authorization.
        /// </summary>
        [TestMethod]
        public void EditorController_PublishPage_RequiresEditorsOrAdmins()
        {
            // Assert - Verify Publish action is restricted
            var publishMethod = typeof(EditorController).GetMethod("Publish", Type.EmptyTypes);
            var authorizeAttribute = publishMethod?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsNotNull(authorizeAttribute, "Publish should have [Authorize] attribute");
            Assert.AreEqual("Administrators, Editors", authorizeAttribute?.Roles);
        }

        /// <summary>
        /// Tests that UnpublishPage requires Administrators or Editors role.
        /// </summary>
        [TestMethod]
        public void EditorController_UnpublishPage_RequiresEditorsOrAdmins()
        {
            // Assert
            var unpublishMethod = typeof(EditorController).GetMethod("UnpublishPage");
            var authorizeAttribute = unpublishMethod?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsNotNull(authorizeAttribute, "UnpublishPage should have [Authorize] attribute");
            Assert.AreEqual("Administrators, Editors", authorizeAttribute?.Roles);
        }

        #endregion

        #region Article Permission Tests

        /// <summary>
        /// Tests that Permissions management requires Administrators or Editors role.
        /// </summary>
        [TestMethod]
        public void EditorController_Permissions_RequiresEditorsOrAdmins()
        {
            // Assert - Both GET and POST should be restricted
            var getPermissionsMethod = typeof(EditorController).GetMethod("Permissions", new[] { typeof(int), typeof(bool), typeof(string), typeof(string), typeof(int), typeof(int) });
            var getAuthorizeAttribute = getPermissionsMethod?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsNotNull(getAuthorizeAttribute, "Permissions GET should have [Authorize] attribute");
            Assert.AreEqual("Administrators, Editors", getAuthorizeAttribute?.Roles);

            var postPermissionsMethod = typeof(EditorController).GetMethod("Permissions", new[] { typeof(int), typeof(string[]) });
            var postAuthorizeAttribute = postPermissionsMethod?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.IsNotNull(postAuthorizeAttribute, "Permissions POST should have [Authorize] attribute");
            Assert.AreEqual("Administrators, Editors", postAuthorizeAttribute?.Roles);
        }

        #endregion

        #region Multi-Role Inheritance Tests

        /// <summary>
        /// Tests that a user with multiple roles can access appropriate resources.
        /// </summary>
        [TestMethod]
        public async Task EditorController_UserWithMultipleRoles_HasAccess()
        {
            // Arrange - Create user with both Editor and Author roles
            var controller = CreateEditorControllerWithUser("Editors,Authors");

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsNotNull(result, "User with multiple roles should have access");
        }

        /// <summary>
        /// Tests that Administrator role has access to all controllers.
        /// </summary>
        [TestMethod]
        public async Task Administrator_HasAccessToAllControllers()
        {
            // Arrange
            var editorController = CreateEditorControllerWithUser("Administrators");
            var templatesController = CreateTemplatesControllerWithUser("Administrators");

            // Act
            var editorResult = await editorController.Index();
            var templatesResult = await templatesController.Index();

            // Assert
            Assert.IsNotNull(editorResult, "Administrator should have access to EditorController");
            Assert.IsNotNull(templatesResult, "Administrator should have access to TemplatesController");
        }

        #endregion

        #region Role Escalation Prevention Tests

        /// <summary>
        /// Tests that non-administrators cannot grant themselves admin rights.
        /// </summary>
        [TestMethod]
        public void UsersController_NonAdmins_CannotAccessRoleManagement()
        {
            // Assert
            var userRolesMethod = typeof(UsersController).GetMethod("UserRoles", new[] { typeof(string) });
            var controllerAuthorizeAttribute = typeof(UsersController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

            Assert.AreEqual("Administrators", controllerAuthorizeAttribute?.Roles, "Only Administrators should manage user roles");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates an EditorController with a user having the specified role.
        /// </summary>
        private EditorController CreateEditorControllerWithUser(string roles, bool isAuthenticated = true)
        {
            var controller = new EditorController(
                Logger,
                Db,
                UserManager,
                RoleManager,
                Logic,
                EditorSettings,
                ViewRenderService,
                Storage,
                Hub.Object,
                PublishingService,
                ArticleHtmlService,
                ReservedPaths,
                TitleChangeService,
                TemplateService,
                Mediator,
                Cache,
                DynamicConfigurationProvider);

            SetupControllerContext(controller, roles, isAuthenticated);
            return controller;
        }

        /// <summary>
        /// Creates a TemplatesController with a user having the specified role.
        /// </summary>
        private TemplatesController CreateTemplatesControllerWithUser(string roles, bool isAuthenticated = true)
        {
            var controller = new TemplatesController(
                Db,
                UserManager,
                Storage,
                EditorSettings,
                ArticleHtmlService,
                TemplateService,
                Mediator,
                Cache,                           // ✅ Add memory cache
                DynamicConfigurationProvider);   // ✅ Add config provider

            SetupControllerContext(controller, roles, isAuthenticated);
            return controller;
        }

        /// <summary>
        /// Sets up controller context with user claims for the specified roles.
        /// </summary>
        private void SetupControllerContext(Controller controller, string roles, bool isAuthenticated)
        {
            var claims = new System.Collections.Generic.List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "testuser@example.com")
            };

            if (!string.IsNullOrEmpty(roles))
            {
                foreach (var role in roles.Split(','))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
                }
            }

            var identity = isAuthenticated
                ? new ClaimsIdentity(claims, "TestAuth")
                : new ClaimsIdentity(claims); // Not authenticated

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            };

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        #endregion
    }
}
