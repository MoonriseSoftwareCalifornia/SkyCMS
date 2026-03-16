// <copyright file="GetArticlesForUserQueryHandlerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Features.Articles.Queries
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.Queries;
    using Cosmos.Common.Tests.Infrastructure;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="GetArticlesForUserQueryHandler"/>.
    /// Validates article retrieval based on user roles and permissions.
    /// </summary>
    [TestClass]
    public class GetArticlesForUserQueryHandlerTests : CommonTestsBase
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
        public void Constructor_WithValidDependencies_ShouldSucceed()
        {
            using var context = GetIsolatedContext();

            var handler = new GetArticlesForUserQueryHandler(context);

            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public async Task HandleAsync_WithNullQuery_ShouldThrowArgumentNullException()
        {
            using var context = GetIsolatedContext();
            var handler = new GetArticlesForUserQueryHandler(context);

            try
            {
                await handler.HandleAsync(null!);
                Assert.Fail("Expected ArgumentNullException was not thrown");
            }
            catch (ArgumentNullException)
            {
                // Expected exception - test passes
            }
        }

        [TestMethod]
        public async Task HandleAsync_WithUserWithNoPermissions_ShouldReturnPublicArticles()
        {
            using var context = GetIsolatedContext();
            var userId = Guid.NewGuid().ToString();

            // Create catalog entry with no permissions (public)
            var publicCatalog = TestDataBuilder.CreateCatalogEntry(articleNumber: 100);
            publicCatalog.ArticlePermissions = new List<ArticlePermission>();
            context.ArticleCatalog.Add(publicCatalog);

            // Create published page for the catalog
            var page = TestDataBuilder.CreatePublishedPage();
            page.ArticleNumber = 100;
            page.Title = "Public Article";
            context.Pages.Add(page);

            await context.SaveChangesAsync();

            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            var handler = new GetArticlesForUserQueryHandler(context);
            var query = new GetArticlesForUserQuery(user);

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Public Article", result[0].Title);
        }

        [TestMethod]
        public async Task HandleAsync_WithUserHavingDirectPermission_ShouldIncludeArticle()
        {
            using var context = GetIsolatedContext();
            var userId = Guid.NewGuid().ToString();

            // Create catalog entry with user-specific permission
            var catalog = TestDataBuilder.CreateCatalogEntry(articleNumber: 200);
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

            // Create published page
            var page = TestDataBuilder.CreatePublishedPage();
            page.ArticleNumber = 200;
            page.Title = "User-Specific Article";
            context.Pages.Add(page);

            await context.SaveChangesAsync();

            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            var handler = new GetArticlesForUserQueryHandler(context);
            var query = new GetArticlesForUserQuery(user);

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("User-Specific Article", result[0].Title);
        }

        [TestMethod]
        public async Task HandleAsync_WithUserHavingRolePermission_ShouldIncludeArticle()
        {
            using var context = GetIsolatedContext();
            var userId = Guid.NewGuid().ToString();
            var roleId = Guid.NewGuid().ToString();

            // Create user role
            var userRole = new IdentityUserRole<string>
            {
                UserId = userId,
                RoleId = roleId
            };
            context.UserRoles.Add(userRole);

            // Create catalog entry with role permission
            var catalog = TestDataBuilder.CreateCatalogEntry(articleNumber: 300);
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

            // Create published page
            var page = TestDataBuilder.CreatePublishedPage();
            page.ArticleNumber = 300;
            page.Title = "Role-Based Article";
            context.Pages.Add(page);

            await context.SaveChangesAsync();

            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            var handler = new GetArticlesForUserQueryHandler(context);
            var query = new GetArticlesForUserQuery(user);

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Role-Based Article", result[0].Title);
        }

        [TestMethod]
        public async Task HandleAsync_WithUserWithNoAccess_ShouldExcludeRestrictedArticles()
        {
            using var context = GetIsolatedContext();
            var userId = Guid.NewGuid().ToString();
            var otherUserId = Guid.NewGuid().ToString();

            // Create restricted catalog (only for other user)
            var restrictedCatalog = TestDataBuilder.CreateCatalogEntry(articleNumber: 400);
            restrictedCatalog.ArticlePermissions = new List<ArticlePermission>
            {
                new ArticlePermission
                {
                    IdentityObjectId = otherUserId,
                    IsRoleObject = false,
                    Permission = "Read"
                }
            };
            context.ArticleCatalog.Add(restrictedCatalog);

            var page = TestDataBuilder.CreatePublishedPage();
            page.ArticleNumber = 400;
            page.Title = "Restricted Article";
            context.Pages.Add(page);

            await context.SaveChangesAsync();

            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            var handler = new GetArticlesForUserQueryHandler(context);
            var query = new GetArticlesForUserQuery(user);

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task HandleAsync_WithMultipleArticles_ShouldReturnAllAccessibleArticles()
        {
            using var context = GetIsolatedContext();
            var userId = Guid.NewGuid().ToString();

            // Public article
            var publicCatalog = TestDataBuilder.CreateCatalogEntry(articleNumber: 100);
            publicCatalog.ArticlePermissions = new List<ArticlePermission>();
            context.ArticleCatalog.Add(publicCatalog);
            var publicPage = TestDataBuilder.CreatePublishedPage();
            publicPage.ArticleNumber = 100;
            publicPage.Title = "Public Article";
            context.Pages.Add(publicPage);

            // User-specific article
            var userCatalog = TestDataBuilder.CreateCatalogEntry(articleNumber: 200);
            userCatalog.ArticlePermissions = new List<ArticlePermission>
            {
                new ArticlePermission { IdentityObjectId = userId, IsRoleObject = false }
            };
            context.ArticleCatalog.Add(userCatalog);
            var userPage = TestDataBuilder.CreatePublishedPage();
            userPage.ArticleNumber = 200;
            userPage.Title = "User Article";
            context.Pages.Add(userPage);

            await context.SaveChangesAsync();

            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            var handler = new GetArticlesForUserQueryHandler(context);
            var query = new GetArticlesForUserQuery(user);

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Any(r => r.Title == "Public Article"));
            Assert.IsTrue(result.Any(r => r.Title == "User Article"));
        }

        [TestMethod]
        public async Task HandleAsync_WithNoCatalogEntries_ShouldReturnEmptyList()
        {
            using var context = GetIsolatedContext();
            var userId = Guid.NewGuid().ToString();

            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            var handler = new GetArticlesForUserQueryHandler(context);
            var query = new GetArticlesForUserQuery(user);

            var result = await handler.HandleAsync(query);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }
    }
}
