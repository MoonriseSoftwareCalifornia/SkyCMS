// <copyright file="TestDataBuilder.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Infrastructure
{
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.AspNetCore.Identity;

    /// <summary>
    /// Provides fluent builder methods for creating test data with unique identifiers
    /// to support parallel test execution without data conflicts.
    /// </summary>
    public static class TestDataBuilder
    {
        private static readonly Random _random = new();

        /// <summary>
        /// Creates a unique article for testing with guaranteed unique identifiers.
        /// </summary>
        /// <param name="title">Optional title. If null, generates unique title.</param>
        /// <param name="articleType">Article type. Default is General.</param>
        /// <returns>Article with unique Id and properties.</returns>
        public static Article CreateArticle(string? title = null, ArticleType articleType = ArticleType.General)
        {
            var uniqueId = Guid.NewGuid();
            return new Article
            {
                Id = uniqueId,
                Title = title ?? $"Test Article {uniqueId}",
                ArticleNumber = _random.Next(1000, 999999),
                UrlPath = $"/test-article-{uniqueId}",
                Content = $"<p>Test content for {uniqueId}</p>",
                Published = DateTimeOffset.UtcNow.AddDays(-1),
                Updated = DateTimeOffset.UtcNow,
                VersionNumber = 1,
                ArticleType = (int)articleType,
                StatusCode = (int)StatusCodeEnum.Active,
            };
        }

        /// <summary>
        /// Creates a unique published page for testing.
        /// </summary>
        /// <param name="articleNumber">Optional article number. If null, generates random number.</param>
        /// <param name="urlPath">Optional URL path. If null, generates unique path.</param>
        /// <returns>PublishedPage with unique identifiers.</returns>
        public static PublishedPage CreatePublishedPage(int? articleNumber = null, string? urlPath = null)
        {
            var uniqueId = Guid.NewGuid();
            var pageNumber = articleNumber ?? _random.Next(1000, 999999);

            return new PublishedPage
            {
                Id = uniqueId,
                ArticleNumber = pageNumber,
                UrlPath = urlPath ?? $"/test-page-{uniqueId}",
                Title = $"Test Page {uniqueId}",
                Content = $"<p>Published content for {uniqueId}</p>",
                Published = DateTimeOffset.UtcNow.AddDays(-1),
                VersionNumber = 1,
            };
        }

        /// <summary>
        /// Creates a unique layout for testing.
        /// </summary>
        /// <param name="layoutName">Optional name. If null, generates unique name.</param>
        /// <returns>Layout with unique identifiers.</returns>
        public static Layout CreateLayout(string? layoutName = null)
        {
            var uniqueId = Guid.NewGuid();
            return new Layout
            {
                Id = uniqueId,
                LayoutName = layoutName ?? $"Test Layout {uniqueId}",
                Head = "<title>Test</title>",
                HtmlHeader = "<header>Test Header</header>",
                FooterHtmlContent = "<footer>Test Footer</footer>",
                IsDefault = false,
            };
        }

        /// <summary>
        /// Creates a unique catalog entry for testing.
        /// </summary>
        /// <param name="articleNumber">Optional article number. If null, generates random number.</param>
        /// <returns>CatalogEntry with unique identifiers.</returns>
        public static CatalogEntry CreateCatalogEntry(int? articleNumber = null)
        {
            var catNumber = articleNumber ?? _random.Next(1000, 999999);

            return new CatalogEntry
            {
                ArticleNumber = catNumber,
                Title = $"Test Catalog Entry {catNumber}",
                UrlPath = $"/test-catalog-{catNumber}",
                Published = DateTimeOffset.UtcNow.AddDays(-1),
                Updated = DateTimeOffset.UtcNow,
                Status = "Active",
            };
        }

        /// <summary>
        /// Creates a unique setting for testing.
        /// </summary>
        /// <param name="name">Optional name. If null, generates unique name.</param>
        /// <param name="value">Optional value. If null, generates unique value.</param>
        /// <returns>Setting with unique identifiers.</returns>
        public static Setting CreateSetting(string? name = null, string? value = null)
        {
            var uniqueId = Guid.NewGuid();
            return new Setting
            {
                Id = uniqueId,
                Name = name ?? $"TestSetting_{uniqueId}",
                Value = value ?? $"TestValue_{uniqueId}",
            };
        }

        /// <summary>
        /// Creates a unique contact for testing.
        /// </summary>
        /// <param name="email">Optional email. If null, generates unique email.</param>
        /// <returns>Contact with unique identifiers.</returns>
        public static Contact CreateContact(string? email = null)
        {
            var uniqueId = Guid.NewGuid();
            return new Contact
            {
                Id = uniqueId,
                Email = email ?? $"test-{uniqueId}@example.com",
                FirstName = $"First{_random.Next(100, 999)}",
                LastName = $"Last{_random.Next(100, 999)}",
                Phone = $"+1555{_random.Next(1000000, 9999999)}",
            };
        }

        /// <summary>
        /// Creates a unique identity user for testing.
        /// </summary>
        /// <param name="userName">Optional username. If null, generates unique username.</param>
        /// <returns>IdentityUser with unique identifiers.</returns>
        public static IdentityUser CreateUser(string? userName = null)
        {
            var uniqueId = Guid.NewGuid().ToString();
            var username = userName ?? $"testuser_{uniqueId}";

            return new IdentityUser
            {
                Id = uniqueId,
                UserName = username,
                NormalizedUserName = username.ToUpperInvariant(),
                Email = $"{username}@test.com",
                NormalizedEmail = $"{username}@test.com".ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
            };
        }

        /// <summary>
        /// Creates a unique template for testing.
        /// </summary>
        /// <param name="title">Optional title. If null, generates unique title.</param>
        /// <returns>Template with unique identifiers.</returns>
        public static Template CreateTemplate(string? title = null)
        {
            var uniqueId = Guid.NewGuid();
            return new Template
            {
                Id = uniqueId,
                Title = title ?? $"Test Template {uniqueId}",
                Description = $"Description for {uniqueId}",
                Content = $"<div>Template content {uniqueId}</div>",
            };
        }

        /// <summary>
        /// Creates a unique article log entry for testing.
        /// </summary>
        /// <param name="articleId">Article ID this log is for.</param>
        /// <param name="userId">User ID who performed the action.</param>
        /// <returns>ArticleLog with unique identifiers.</returns>
        public static ArticleLog CreateArticleLog(Guid articleId, string userId)
        {
            var uniqueId = Guid.NewGuid();
            return new ArticleLog
            {
                Id = uniqueId,
                ArticleId = articleId,
                IdentityUserId = userId,
                ActivityNotes = $"Test log entry {uniqueId}",
                DateTimeStamp = DateTimeOffset.UtcNow,
            };
        }

        /// <summary>
        /// Creates a unique author info for testing.
        /// </summary>
        /// <param name="userId">User ID this author info is for.</param>
        /// <returns>AuthorInfo with unique identifiers.</returns>
        public static AuthorInfo CreateAuthorInfo(string userId)
        {
            var uniqueId = Guid.NewGuid();
            return new AuthorInfo
            {
                Id = userId,
                AuthorName = $"Test Author {uniqueId}",
                AuthorDescription = $"Description for author {uniqueId}",
            };
        }

        /// <summary>
        /// Generates a random string for testing purposes.
        /// </summary>
        /// <param name="length">Length of the random string.</param>
        /// <returns>Random alphanumeric string.</returns>
        public static string GenerateRandomString(int length = 10)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Range(0, length)
                .Select(_ => chars[_random.Next(chars.Length)])
                .ToArray());
        }
    }
}
