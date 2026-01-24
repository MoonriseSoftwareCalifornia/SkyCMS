// <copyright file="ArticleTestBase.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Editor.Features.Articles
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Catalog;
    using Sky.Editor.Services.Html;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.Templates;
    using Sky.Editor.Services.Titles;

    /// <summary>
    /// Base class for article-related unit tests with shared setup and teardown.
    /// </summary>
    [TestClass]
    public abstract class ArticleTestBase
    {
        protected ApplicationDbContext DbContext { get; private set; }
        protected Mock<IArticleHtmlService> MockHtmlService { get; private set; }
        protected Mock<ICatalogService> MockCatalogService { get; private set; }
        protected Mock<IPublishingService> MockPublishingService { get; private set; }
        protected Mock<ITitleChangeService> MockTitleChangeService { get; private set; }
        protected Mock<ITemplateService> MockTemplateService { get; private set; }
        protected Mock<IClock> MockClock { get; private set; }
        protected Mock<ILogger> MockLogger { get; private set; }

        protected DateTimeOffset TestNow { get; } = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

        [TestInitialize]
        public void TestInitialize()
        {
            // Setup InMemory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;

            DbContext = new ApplicationDbContext(options);

            // Setup mocks
            MockHtmlService = new Mock<IArticleHtmlService>();
            MockCatalogService = new Mock<ICatalogService>();
            MockPublishingService = new Mock<IPublishingService>();
            MockTitleChangeService = new Mock<ITitleChangeService>();
            MockTemplateService = new Mock<ITemplateService>();
            MockClock = new Mock<IClock>();
            MockLogger = new Mock<ILogger>();

            // Default mock behaviors
            MockHtmlService
                .Setup(x => x.EnsureEditableMarkers(It.IsAny<string>()))
                .Returns<string>(content => content ?? string.Empty);

            MockClock
                .Setup(x => x.UtcNow)
                .Returns(TestNow);

            MockTitleChangeService
                .Setup(x => x.ValidateTitle(It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync(true);

        MockTitleChangeService
            .Setup(x => x.BuildArticleUrl(It.IsAny<Article>()))
            .Returns<Article>(a => a.Title.ToLower().Replace(" ", "-"));

        MockCatalogService
            .Setup(x => x.UpsertAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Article a, CancellationToken ct) => new CatalogEntry());

        MockPublishingService
            .Setup(x => x.PublishAsync(It.IsAny<Article>()))
            .ReturnsAsync(new List<Sky.Editor.Services.CDN.CdnResult>());
    }

        [TestCleanup]
        public void TestCleanup()
        {
            DbContext?.Dispose();
        }

        /// <summary>
        /// Seeds a template in the database.
        /// </summary>
        protected async Task<Template> SeedTemplateAsync(string title, string content = "<div>Test Template</div>")
        {
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Title = title,
                Content = content,
                Description = $"Test template: {title}",
                LayoutId = Guid.NewGuid()
            };

            DbContext.Templates.Add(template);
            await DbContext.SaveChangesAsync();
            return template;
        }

        /// <summary>
        /// Seeds an article in the database.
        /// </summary>
        protected async Task<Article> SeedArticleAsync(
            string title,
            int articleNumber = 1,
            string urlPath = null,
            bool published = false)
        {
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = articleNumber,
                Title = title,
                UrlPath = urlPath ?? title.ToLower().Replace(" ", "-"),
                Content = "<div>Test Content</div>",
                StatusCode = (int)Cosmos.Common.Data.Logic.StatusCodeEnum.Active,
                VersionNumber = 1,
                Updated = TestNow,
                Published = published ? TestNow : null,
                UserId = Guid.NewGuid().ToString(),
                BannerImage = string.Empty
            };

            DbContext.Articles.Add(article);
            await DbContext.SaveChangesAsync();
            return article;
        }

        /// <summary>
        /// Seeds an article number tracker.
        /// </summary>
        protected async Task SeedArticleNumberAsync(int lastNumber)
        {
            DbContext.ArticleNumbers.Add(new ArticleNumber { LastNumber = lastNumber });
            await DbContext.SaveChangesAsync();
        }
    }
}