// <copyright file="AuthorizeUserForArticleQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.Queries
{
    using System;
    using System.Collections.Generic;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Queries;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="AuthorizeUserForArticleQueryHandler"/>.
    /// Validates article authorization logic including anonymous, authenticated, user-specific, and role-based access.
    /// </summary>
    [TestClass]
    public class AuthorizeUserForArticleQueryHandlerTests : CommonTestsBase
    {
        /// <summary>
        /// Initializes the shared test infrastructure for this test class.
        /// </summary>
        /// <param name="context">Test context provided by MSTest.</param>
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            ContextPool = new TestDbContextPool();
        }

        /// <summary>
        /// Cleans up the shared test infrastructure after all tests complete.
        /// </summary>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            ContextPool?.Dispose();
        }

        [TestMethod]
        public void Constructor_WithNullDbContext_ShouldThrowArgumentNullException()
        {
            try
            {
                var handler = new AuthorizeUserForArticleQueryHandler(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("dbContext", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task HandleAsync_WithNullQuery_ShouldThrowArgumentNullException()
        {
            using var context = GetIsolatedContext();
            var handler = new AuthorizeUserForArticleQueryHandler(context);

            try
            {
                await handler.HandleAsync(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("query", ex.ParamName);
            }
        }

        [TestMethod]
        public async Task HandleAsync_WithNonExistentArticle_ShouldReturnFalse()
        {
            using var context = GetIsolatedContext();
            var handler = new AuthorizeUserForArticleQueryHandler(context);
            var user = new ClaimsPrincipal();

            var result = await handler.HandleAsync(new AuthorizeUserForArticleQuery(user, 99999));

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithArticleButNoPermissions_ShouldReturnFalse()
        {
            using var context = GetIsolatedContext();
            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.ArticlePermissions = new List<ArticlePermission>();
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var handler = new AuthorizeUserForArticleQueryHandler(context);
            var user = new ClaimsPrincipal();

            var result = await handler.HandleAsync(new AuthorizeUserForArticleQuery(user, catalog.ArticleNumber));

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithAnonymousRolePermission_ShouldReturnTrueForAnyUser()
        {
            using var context = GetIsolatedContext();
            
            var anonymousRole = new IdentityRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Anonymous",
                NormalizedName = "ANONYMOUS"
            };
            context.Roles.Add(anonymousRole);

            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.ArticlePermissions = new List<ArticlePermission>
            {
                new ArticlePermission
                {
                    IdentityObjectId = anonymousRole.Id,
                    IsRoleObject = true,
                    Permission = "Read"
                }
            };
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var handler = new AuthorizeUserForArticleQueryHandler(context);
            var user = new ClaimsPrincipal();

            var result = await handler.HandleAsync(new AuthorizeUserForArticleQuery(user, catalog.ArticleNumber));

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithAuthenticatedRolePermission_ShouldReturnFalseForUnauthenticatedUser()
        {
            using var context = GetIsolatedContext();

            var authenticatedRole = new IdentityRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Authenticated",
                NormalizedName = "AUTHENTICATED"
            };
            context.Roles.Add(authenticatedRole);

            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.ArticlePermissions = new List<ArticlePermission>
            {
                new ArticlePermission
                {
                    IdentityObjectId = authenticatedRole.Id,
                    IsRoleObject = true,
                    Permission = "Read"
                }
            };
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var handler = new AuthorizeUserForArticleQueryHandler(context);
            var unauthenticatedIdentity = new System.Security.Claims.ClaimsIdentity(); // Not authenticated
            var unauthenticatedUser = new ClaimsPrincipal(unauthenticatedIdentity);

            var result = await handler.HandleAsync(new AuthorizeUserForArticleQuery(unauthenticatedUser, catalog.ArticleNumber));

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithAuthenticatedRolePermission_ShouldReturnTrueForAuthenticatedUser()
        {
            using var context = GetIsolatedContext();
            
            var authenticatedRole = new IdentityRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Authenticated",
                NormalizedName = "AUTHENTICATED"
            };
            context.Roles.Add(authenticatedRole);

            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.ArticlePermissions = new List<ArticlePermission>
            {
                new ArticlePermission
                {
                    IdentityObjectId = authenticatedRole.Id,
                    IsRoleObject = true,
                    Permission = "Read"
                }
            };
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var handler = new AuthorizeUserForArticleQueryHandler(context);
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "testuser") }, "TestAuth");
            var authenticatedUser = new ClaimsPrincipal(identity);

            var result = await handler.HandleAsync(new AuthorizeUserForArticleQuery(authenticatedUser, catalog.ArticleNumber));

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithUserSpecificPermission_ShouldReturnTrueForThatUser()
        {
            using var context = GetIsolatedContext();
            
            var userId = Guid.NewGuid().ToString();
            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.ArticlePermissions = new List<ArticlePermission>
            {
                new ArticlePermission
                {
                    IdentityObjectId = userId,
                    IsRoleObject = false,
                    Permission = "Read"
                }
            };
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var handler = new AuthorizeUserForArticleQueryHandler(context);
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            var result = await handler.HandleAsync(new AuthorizeUserForArticleQuery(user, catalog.ArticleNumber));

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithUserSpecificPermission_ShouldReturnFalseForDifferentUser()
        {
            using var context = GetIsolatedContext();
            
            var allowedUserId = Guid.NewGuid().ToString();
            var differentUserId = Guid.NewGuid().ToString();

            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.ArticlePermissions = new List<ArticlePermission>
            {
                new ArticlePermission
                {
                    IdentityObjectId = allowedUserId,
                    IsRoleObject = false,
                    Permission = "Read"
                }
            };
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var handler = new AuthorizeUserForArticleQueryHandler(context);
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, differentUserId) }, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            var result = await handler.HandleAsync(new AuthorizeUserForArticleQuery(user, catalog.ArticleNumber));

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithRoleBasedPermission_ShouldReturnTrueForUserInRole()
        {
            using var context = GetIsolatedContext();
            
            var userId = Guid.NewGuid().ToString();
            var roleId = Guid.NewGuid().ToString();
            
            var role = new IdentityRole { Id = roleId, Name = "Editor", NormalizedName = "EDITOR" };
            context.Roles.Add(role);

            var userRole = new IdentityUserRole<string> { UserId = userId, RoleId = roleId };
            context.UserRoles.Add(userRole);

            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.ArticlePermissions = new List<ArticlePermission>
            {
                new ArticlePermission
                {
                    IdentityObjectId = roleId,
                    IsRoleObject = true,
                    Permission = "Read"
                }
            };
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var handler = new AuthorizeUserForArticleQueryHandler(context);
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            var result = await handler.HandleAsync(new AuthorizeUserForArticleQuery(user, catalog.ArticleNumber));

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithRoleBasedPermission_ShouldReturnFalseForUserNotInRole()
        {
            using var context = GetIsolatedContext();
            
            var userId = Guid.NewGuid().ToString();
            var roleId = Guid.NewGuid().ToString();
            
            var role = new IdentityRole { Id = roleId, Name = "Editor", NormalizedName = "EDITOR" };
            context.Roles.Add(role);

            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.ArticlePermissions = new List<ArticlePermission>
            {
                new ArticlePermission
                {
                    IdentityObjectId = roleId,
                    IsRoleObject = true,
                    Permission = "Read"
                }
            };
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var handler = new AuthorizeUserForArticleQueryHandler(context);
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            var result = await handler.HandleAsync(new AuthorizeUserForArticleQuery(user, catalog.ArticleNumber));

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithMultipleRoles_ShouldReturnTrueIfUserHasAnyRole()
        {
            using var context = GetIsolatedContext();
            
            var userId = Guid.NewGuid().ToString();
            var role1Id = Guid.NewGuid().ToString();
            var role2Id = Guid.NewGuid().ToString();
            
            context.Roles.Add(new IdentityRole { Id = role1Id, Name = "Editor", NormalizedName = "EDITOR" });
            context.Roles.Add(new IdentityRole { Id = role2Id, Name = "Admin", NormalizedName = "ADMIN" });

            context.UserRoles.Add(new IdentityUserRole<string> { UserId = userId, RoleId = role2Id });

            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.ArticlePermissions = new List<ArticlePermission>
            {
                new ArticlePermission { IdentityObjectId = role1Id, IsRoleObject = true },
                new ArticlePermission { IdentityObjectId = role2Id, IsRoleObject = true }
            };
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var handler = new AuthorizeUserForArticleQueryHandler(context);
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            var result = await handler.HandleAsync(new AuthorizeUserForArticleQuery(user, catalog.ArticleNumber));

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithMixedPermissions_AnonymousShouldTakePrecedence()
        {
            using var context = GetIsolatedContext();
            
            var anonymousRoleId = Guid.NewGuid().ToString();
            var adminRoleId = Guid.NewGuid().ToString();
            
            context.Roles.Add(new IdentityRole { Id = anonymousRoleId, Name = "Anonymous", NormalizedName = "ANONYMOUS" });
            context.Roles.Add(new IdentityRole { Id = adminRoleId, Name = "Admin", NormalizedName = "ADMIN" });

            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.ArticlePermissions = new List<ArticlePermission>
            {
                new ArticlePermission { IdentityObjectId = anonymousRoleId, IsRoleObject = true },
                new ArticlePermission { IdentityObjectId = adminRoleId, IsRoleObject = true }
            };
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var handler = new AuthorizeUserForArticleQueryHandler(context);
            var unauthenticatedUser = new ClaimsPrincipal();

            var result = await handler.HandleAsync(new AuthorizeUserForArticleQuery(unauthenticatedUser, catalog.ArticleNumber));

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task HandleAsync_WithException_ShouldReturnFalse()
        {
            using var context = GetIsolatedContext();
            context.Dispose();

            var handler = new AuthorizeUserForArticleQueryHandler(context);
            var user = new ClaimsPrincipal();

            var result = await handler.HandleAsync(new AuthorizeUserForArticleQuery(user, 1));

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task HandleAsync_UserIdComparison_ShouldBeCaseInsensitive()
        {
            using var context = GetIsolatedContext();
            
            var userId = "USER-ID-123";
            var catalog = TestDataBuilder.CreateCatalogEntry();
            catalog.ArticlePermissions = new List<ArticlePermission>
            {
                new ArticlePermission
                {
                    IdentityObjectId = userId.ToLowerInvariant(),
                    IsRoleObject = false
                }
            };
            context.ArticleCatalog.Add(catalog);
            await context.SaveChangesAsync();

            var handler = new AuthorizeUserForArticleQueryHandler(context);
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToUpperInvariant()) }, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            var result = await handler.HandleAsync(new AuthorizeUserForArticleQuery(user, catalog.ArticleNumber));

            Assert.IsTrue(result);
        }
    }
}
