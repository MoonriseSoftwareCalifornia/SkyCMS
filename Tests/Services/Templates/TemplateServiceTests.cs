// <copyright file="TemplateServiceTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Templates;

using Cosmos.Common.Data;
using Cosmos.DynamicConfig;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Sky.Editor.Services.Templates;

/// <summary>
/// Unit tests for TemplateService template application functionality.
/// </summary>
/// <remarks>
/// Tests the new template application features:
/// - PreviewTemplateApplicationAsync
/// - ApplyTemplateToArticleAsync
/// - ApplyTemplateToArticlesAsync
/// - PublishTemplateChangesAsync
/// </remarks>
[TestClass]
public class TemplateServiceTests
{
    private ApplicationDbContext _dbContext;
    private Mock<ILogger<TemplateService>> _mockLogger;
    private Mock<IWebHostEnvironment> _mockEnvironment;
    private Mock<IDynamicConfigurationProvider> _mockConfigProvider;
    private TemplateService _templateService;
    private DbContextOptions<ApplicationDbContext> _dbOptions;

    /// <summary>
    /// Test initialization - runs before each test method.
    /// </summary>
    [TestInitialize]
    public void TestInitialize()
    {
        // Create in-memory database with unique name per test
        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(_dbOptions);

        // Create mocks
        _mockLogger = new Mock<ILogger<TemplateService>>();
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockConfigProvider = new Mock<IDynamicConfigurationProvider>();

        // Setup mock to return a test tenant ID
        _mockConfigProvider
            .Setup(x => x.GetCurrentTenantIdAsync())
            .ReturnsAsync(Guid.NewGuid());

        // Create service under test
        _templateService = new TemplateService(
            _mockEnvironment.Object,
            _mockLogger.Object,
            _dbContext,
            _mockConfigProvider.Object);
    }

    /// <summary>
    /// Test cleanup - runs after each test method.
    /// </summary>
    [TestCleanup]
    public void TestCleanup()
    {
        _dbContext?.Database.EnsureDeleted();
        _dbContext?.Dispose();
    }

    // ============================================================
    // TEST HELPER METHODS
    // ============================================================

    /// <summary>
    /// Seeds the test database with a template and associated articles.
    /// </summary>
    /// <param name="templateId">Template ID to create.</param>
    /// <param name="articleCount">Number of articles to create using this template.</param>
    /// <param name="withPublishedVersions">Whether articles should have published versions.</param>
    /// <returns>List of created article numbers.</returns>
    private async Task<List<int>> SeedTemplateAndArticlesAsync(
        Guid templateId,
        int articleCount = 3,
        bool withPublishedVersions = true)
    {
        // Create template
        var template = new Template
        {
            Id = templateId,
            Title = "Test Template",
            Content = """
                <div data-ccms-ceid="region1">Original Region 1</div>
                <div data-ccms-ceid="region2">Original Region 2</div>
                """,
            LayoutId = Guid.NewGuid(),
            LayoutNumber = 1,
            CommunityLayoutId = "test-layout"
        };

        _dbContext!.Templates.Add(template);

        var articleNumbers = new List<int>();

        for (int i = 1; i <= articleCount; i++)
        {
            var articleNumber = 100 + i;
            articleNumbers.Add(articleNumber);

            // Create article version 1
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = articleNumber,
                VersionNumber = 1,
                Title = $"Test Article {i}",
                UrlPath = $"test-article-{i}",
                Content = """
                    <div data-ccms-ceid="region1">User Content 1</div>
                    <div data-ccms-ceid="region2">User Content 2</div>
                    """,
                TemplateId = templateId,
                Published = withPublishedVersions ? DateTimeOffset.UtcNow.AddDays(-1) : null,
                Updated = DateTimeOffset.UtcNow
            };

            _dbContext.Articles.Add(article);

            // Add to catalog
            _dbContext.ArticleCatalog.Add(new CatalogEntry
            {
                ArticleNumber = articleNumber,
                Title = article.Title,
                UrlPath = article.UrlPath,
                TemplateId = templateId,
                Published = article.Published,
                Updated = article.Updated
            });
        }

        await _dbContext.SaveChangesAsync();
        return articleNumbers;
    }

    /// <summary>
    /// Creates a template with specific editable regions for testing merge scenarios.
    /// </summary>
    /// <param name="templateId">Template ID.</param>
    /// <param name="regionCount">Number of editable regions to include.</param>
    /// <returns>Created template.</returns>
    private async Task<Template> CreateTemplateWithRegionsAsync(Guid templateId, int regionCount)
    {
        var regions = string.Join("\n", Enumerable.Range(1, regionCount)
            .Select(i => $"""<div data-ccms-ceid="region{i}">Template Content {i}</div>"""));

        var template = new Template
        {
            Id = templateId,
            Title = "Multi-Region Template",
            Content = regions,
            LayoutId = Guid.NewGuid(),
            LayoutNumber = 1
        };

        _dbContext!.Templates.Add(template);
        await _dbContext.SaveChangesAsync();

        return template;
    }

    /// <summary>
    /// Creates an article with multiple versions for testing version preservation.
    /// </summary>
    /// <param name="articleNumber">Article number.</param>
    /// <param name="versionCount">Number of versions to create.</param>
    /// <param name="templateId">Template ID to associate with.</param>
    /// <param name="publishedVersionNumber">Which version should be published (null for none).</param>
    /// <returns>List of created article version IDs.</returns>
    private async Task<List<Guid>> CreateArticleWithVersionsAsync(
        int articleNumber,
        int versionCount,
        Guid templateId,
        int? publishedVersionNumber = null)
    {
        var versionIds = new List<Guid>();
        for (int i = 1; i <= versionCount; i++)
        {
            var versionId = Guid.NewGuid();
            versionIds.Add(versionId);

            var article = new Article
            {
                Id = versionId,
                ArticleNumber = articleNumber,
                VersionNumber = i,
                Title = $"Article {articleNumber} v{i}",
                UrlPath = $"article-{articleNumber}",
                Content = $"""<div data-ccms-ceid="region1">Version {i} Content</div>""",
                TemplateId = templateId,
                Published = (publishedVersionNumber.HasValue && i == publishedVersionNumber.Value)
                    ? DateTimeOffset.UtcNow.AddDays(-i)
                    : null,
                Updated = DateTimeOffset.UtcNow.AddDays(-versionCount + i)
            };

            _dbContext!.Articles.Add(article);
        }

        // Save articles first so they can be queried
        await _dbContext.SaveChangesAsync();

        // Add to catalog (latest version)
        var latest = await _dbContext.Articles
            .Where(a => a.ArticleNumber == articleNumber)
            .OrderByDescending(a => a.VersionNumber)
            .FirstAsync();

        _dbContext.ArticleCatalog.Add(new CatalogEntry
        {
            ArticleNumber = articleNumber,
            Title = latest.Title,
            UrlPath = latest.UrlPath,
            TemplateId = templateId,
            Published = latest.Published,
            Updated = latest.Updated
        });

        await _dbContext.SaveChangesAsync();
        return versionIds;
    }

    // ============================================================
    // INTEGRATION TEST: Complete Template Lifecycle
    // ============================================================

    /// <summary>
    /// Integration test: Complete template lifecycle from creation through publish.
    /// </summary>
    /// <remarks>
    /// This test validates the entire workflow:
    /// 1. CREATE: Template and articles are created
    /// 2. MODIFY: Template structure is updated
    /// 3. PREVIEW: Shows which articles will be affected
    /// 4. APPLY: Creates draft versions with new template
    /// 5. VERIFY: Old versions are preserved
    /// 6. PUBLISH: Selected drafts go live
    /// 7. VERIFY: Published articles have new template
    /// </remarks>
    [TestMethod]
    [TestCategory("Integration")]
    public async Task TemplateLifecycle_CompleteWorkflow_SuccessfullyCreatesModifiesAppliesAndPublishes()
    {
        // ============================================================
        // PHASE 1: SETUP - Create template and initial articles
        // ============================================================
        var templateId = Guid.NewGuid();
        var layoutId = Guid.NewGuid();

        // Create initial template
        var template = new Template
        {
            Id = templateId,
            Title = "Blog Post Template v1",
            Content = """
                <article>
                    <header>
                        <h1>Static Template Header</h1>
                    </header>
                    <div data-ccms-ceid="title">Article Title</div>
                    <div data-ccms-ceid="content">Article Content</div>
                    <footer>
                        <p>Static Footer v1</p>
                    </footer>
                </article>
                """,
            LayoutId = layoutId,
            LayoutNumber = 1,
            CommunityLayoutId = "blog-layout"
        };
        _dbContext!.Templates.Add(template);
        await _dbContext.SaveChangesAsync();

        // Create 3 articles using this template
        var articleNumbers = new List<int> { 201, 202, 203 };
        foreach (var articleNumber in articleNumbers)
        {
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = articleNumber,
                VersionNumber = 1,
                Title = $"Article {articleNumber}",
                UrlPath = $"blog/article-{articleNumber}",
                Content = $$"""
                    <article>
                        <header>
                            <h1>Static Template Header</h1>
                        </header>
                        <div data-ccms-ceid="title">User Title {{articleNumber}}</div>
                        <div data-ccms-ceid="content">User wrote amazing content for article {{articleNumber}}</div>
                        <footer>
                            <p>Static Footer v1</p>
                        </footer>
                    </article>
                    """,
                TemplateId = templateId,
                Published = DateTimeOffset.UtcNow.AddDays(-7),
                Updated = DateTimeOffset.UtcNow.AddDays(-7)
            };
            _dbContext.Articles.Add(article);

            _dbContext.ArticleCatalog.Add(new CatalogEntry
            {
                ArticleNumber = articleNumber,
                Title = article.Title,
                UrlPath = article.UrlPath,
                TemplateId = templateId,
                Published = article.Published,
                Updated = article.Updated
            });
        }
        await _dbContext.SaveChangesAsync();

        // VERIFY Phase 1
        var initialArticles = await _dbContext.Articles
            .Where(a => articleNumbers.Contains(a.ArticleNumber))
            .ToListAsync();
        Assert.AreEqual(3, initialArticles.Count);
        Assert.IsTrue(initialArticles.All(a => a.VersionNumber == 1));
        Assert.IsTrue(initialArticles.All(a => a.Published.HasValue));

        // ============================================================
        // PHASE 2: MODIFY - Update template structure
        // ============================================================
        template.Content = """
            <article class="modern-layout">
                <header class="hero">
                    <h1>NEW Modern Template Header</h1>
                </header>
                <div data-ccms-ceid="title">Article Title</div>
                <div data-ccms-ceid="content">Article Content</div>
                <aside data-ccms-ceid="sidebar">NEW Sidebar Region</aside>
                <footer class="modern-footer">
                    <p>Static Footer v2 - Updated Design</p>
                </footer>
            </article>
            """;
        _dbContext.Templates.Update(template);
        await _dbContext.SaveChangesAsync();

        // VERIFY Phase 2
        var updatedTemplate = await _dbContext.Templates.FindAsync(templateId);
        Assert.IsNotNull(updatedTemplate);
        Assert.IsTrue(updatedTemplate.Content.Contains("NEW Modern Template Header"));

        // ============================================================
        // PHASE 3: PREVIEW - Check impact before applying
        // ============================================================
        var preview = await _templateService!.PreviewTemplateApplicationAsync(templateId);

        // VERIFY Phase 3
        Assert.AreEqual(templateId, preview.TemplateId);
        Assert.AreEqual(3, preview.TotalAffectedArticles);
        Assert.AreEqual(3, preview.Articles.Count);

        // ============================================================
        // PHASE 4: APPLY - Create draft versions for all articles
        // ============================================================
        var applyResult = await _templateService.ApplyTemplateToArticlesAsync(templateId, null);

        // VERIFY Phase 4
        Assert.AreEqual(3, applyResult.SuccessCount);
        Assert.AreEqual(0, applyResult.FailureCount);
        Assert.IsTrue(applyResult.AllSucceeded);

        // ============================================================
        // PHASE 5: VERIFY - Old versions preserved, drafts exist
        // ============================================================
        foreach (var articleNumber in articleNumbers)
        {
            var versions = await _dbContext.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .OrderBy(a => a.VersionNumber)
                .ToListAsync();

            Assert.AreEqual(2, versions.Count);
            Assert.IsNotNull(versions[0].Published, "Version 1 still published");
            Assert.IsNull(versions[1].Published, "Version 2 is draft");
            Assert.IsTrue(versions[1].Content.Contains("NEW Modern Template Header"));
        }

        // ============================================================
        // PHASE 6: PUBLISH - Selectively publish 201 and 202
        // ============================================================
        var publishResult = await _templateService.PublishTemplateChangesAsync(
            templateId,
            new List<int> { 201, 202 });

        // VERIFY Phase 6
        Assert.AreEqual(2, publishResult.PublishedCount);
        Assert.AreEqual(0, publishResult.FailureCount);
        Assert.AreEqual(0, publishResult.SkippedCount); // Article 203 not selected, not "skipped"
        Assert.AreEqual(2, publishResult.Results.Count); // Only 2 articles were processed

        // ============================================================
        // PHASE 7: FINAL VERIFICATION
        // ============================================================
        var article201v2 = await _dbContext.Articles
            .FirstAsync(a => a.ArticleNumber == 201 && a.VersionNumber == 2);
        Assert.IsNotNull(article201v2.Published);

        var article203v2 = await _dbContext.Articles
            .FirstAsync(a => a.ArticleNumber == 203 && a.VersionNumber == 2);
        Assert.IsNull(article203v2.Published, "Article 203 v2 still draft");
    }

    // ============================================================
    // BATCH 1: ApplyTemplateToArticleAsync - Happy Path Tests
    // ============================================================

    /// <summary>
    /// Applies a template to an article and creates a new draft version with expected metadata.
    /// </summary>
    [TestMethod]
    public async Task ApplyTemplateToArticleAsync_CreatesNewDraftVersion()
    {
        var templateId = Guid.NewGuid();
        var articleNumbers = await SeedTemplateAndArticlesAsync(templateId, articleCount: 1);
        var articleNumber = articleNumbers.First();

        var result = await _templateService!.ApplyTemplateToArticleAsync(articleNumber, templateId);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(articleNumber, result.ArticleNumber);
        Assert.AreEqual(2, result.NewVersionNumber);
        Assert.IsTrue(result.IsDraft);
        Assert.AreNotEqual(Guid.Empty, result.NewVersionId);
    }

    /// <summary>
    /// Ensures applying a template preserves existing article versions and published state.
    /// </summary>
    [TestMethod]
    public async Task ApplyTemplateToArticleAsync_PreservesOldVersions()
    {
        var templateId = Guid.NewGuid();
        await CreateTemplateWithRegionsAsync(templateId, regionCount: 2);
        var articleNumber = 999;
        await CreateArticleWithVersionsAsync(articleNumber, versionCount: 3, templateId, publishedVersionNumber: 2);

        var result = await _templateService!.ApplyTemplateToArticleAsync(articleNumber, templateId);

        Assert.IsTrue(result.Success);

        var allVersions = await _dbContext!.Articles
            .Where(a => a.ArticleNumber == articleNumber)
            .OrderBy(a => a.VersionNumber)
            .ToListAsync();

        Assert.AreEqual(4, allVersions.Count);
        Assert.AreEqual(1, allVersions[0].VersionNumber);
        Assert.AreEqual(2, allVersions[1].VersionNumber);
        Assert.AreEqual(3, allVersions[2].VersionNumber);

        var publishedVersion = allVersions.FirstOrDefault(a => a.VersionNumber == 2);
        Assert.IsNotNull(publishedVersion);
        Assert.IsNotNull(publishedVersion.Published);
    }

    /// <summary>
    /// Verifies the version number increments when creating a new draft via template application.
    /// </summary>
    [TestMethod]
    public async Task ApplyTemplateToArticleAsync_IncrementsVersionNumber()
    {
        var templateId = Guid.NewGuid();
        await CreateTemplateWithRegionsAsync(templateId, regionCount: 2);
        var articleNumber = 888;
        await CreateArticleWithVersionsAsync(articleNumber, versionCount: 5, templateId);

        var result = await _templateService!.ApplyTemplateToArticleAsync(articleNumber, templateId);

        Assert.AreEqual(6, result.NewVersionNumber);

        var newVersion = await _dbContext!.Articles
            .FirstOrDefaultAsync(a => a.ArticleNumber == articleNumber && a.VersionNumber == 6);

        Assert.IsNotNull(newVersion);
        Assert.IsNull(newVersion.Published);
    }

    /// <summary>
    /// Confirms editable region content from the template merges with user content correctly.
    /// </summary>
    [TestMethod]
    public async Task ApplyTemplateToArticleAsync_MergesEditableContentCorrectly()
    {
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            Title = "Test Template",
            Content = """
                <div>Static Template Header</div>
                <div data-ccms-ceid="region1">DEFAULT CONTENT 1</div>
                <div data-ccms-ceid="region2">DEFAULT CONTENT 2</div>
                <div>Static Template Footer</div>
                """,
            LayoutId = Guid.NewGuid(),
            LayoutNumber = 1
        };
        _dbContext!.Templates.Add(template);

        var articleNumber = 777;
        var article = new Article
        {
            Id = Guid.NewGuid(),
            ArticleNumber = articleNumber,
            VersionNumber = 1,
            Title = "Test Article",
            UrlPath = "test-article",
            Content = """
                <div>OLD Static Header</div>
                <div data-ccms-ceid="region1">USER CONTENT ONE</div>
                <div data-ccms-ceid="region2">USER CONTENT TWO</div>
                <div>OLD Static Footer</div>
                """,
            TemplateId = templateId,
            Published = DateTimeOffset.UtcNow.AddDays(-1),
            Updated = DateTimeOffset.UtcNow
        };
        _dbContext.Articles.Add(article);
        await _dbContext.SaveChangesAsync();

        var result = await _templateService!.ApplyTemplateToArticleAsync(articleNumber, templateId);

        Assert.IsTrue(result.Success);

        var newVersion = await _dbContext.Articles
            .FirstAsync(a => a.Id == result.NewVersionId);

        Assert.IsTrue(newVersion.Content.Contains("Static Template Header"));
        Assert.IsTrue(newVersion.Content.Contains("Static Template Footer"));
        Assert.IsTrue(newVersion.Content.Contains("USER CONTENT ONE"));
        Assert.IsTrue(newVersion.Content.Contains("USER CONTENT TWO"));
        Assert.IsFalse(newVersion.Content.Contains("OLD Static Header"));
        Assert.IsFalse(newVersion.Content.Contains("OLD Static Footer"));
    }

    // ============================================================
    // BATCH 2: ApplyTemplateToArticleAsync - Error Handling Tests
    // ============================================================

    /// <summary>
    /// Returns a failure result when the template id does not exist for the article operation.
    /// </summary>
    [TestMethod]
    public async Task ApplyTemplateToArticleAsync_TemplateNotFound_ReturnsFailureResult()
    {
        var nonExistentTemplateId = Guid.NewGuid();
        var validTemplateId = Guid.NewGuid();
        var articleNumbers = await SeedTemplateAndArticlesAsync(validTemplateId, articleCount: 1);
        var articleNumber = articleNumbers.First();

        var result = await _templateService!.ApplyTemplateToArticleAsync(articleNumber, nonExistentTemplateId);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(articleNumber, result.ArticleNumber);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.IsTrue(
            result.ErrorMessage.Contains("template", StringComparison.OrdinalIgnoreCase) ||
            result.ErrorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(0, result.NewVersionNumber);
        Assert.AreEqual(Guid.Empty, result.NewVersionId);
    }

    /// <summary>
    /// Returns a failure result when the target article is not found for template application.
    /// </summary>
    [TestMethod]
    public async Task ApplyTemplateToArticleAsync_ArticleNotFound_ReturnsFailureResult()
    {
        Console.WriteLine("Test started");
        var templateId = Guid.NewGuid();

        Console.WriteLine("Seeding data...");
        await SeedTemplateAndArticlesAsync(templateId, articleCount: 1);

        // Clear EF Core change tracker to avoid contamination
        _dbContext!.ChangeTracker.Clear();  // ← ADD THIS

        Console.WriteLine("Calling ApplyTemplateToArticleAsync...");
        var nonExistentArticleNumber = 99999;
        var result = await _templateService!.ApplyTemplateToArticleAsync(nonExistentArticleNumber, templateId);

        Console.WriteLine($"Result received: Success={result.Success}");
        Assert.IsFalse(result.Success);
    }

    /// <summary>
    /// Handles corrupted or malformed HTML in the template without throwing exceptions.
    /// </summary>
    [TestMethod]
    public async Task ApplyTemplateToArticleAsync_CorruptedHtml_HandlesGracefully()
    {
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            Title = "Corrupted Template",
            Content = """
                <div data-ccms-ceid="region1">Valid Region</div>
                <div This is broken HTML without proper tags
                <div data-ccms-ceid="region2">Another Region</div>
                """,
            LayoutId = Guid.NewGuid(),
            LayoutNumber = 1
        };
        _dbContext!.Templates.Add(template);

        var articleNumber = 555;
        var article = new Article
        {
            Id = Guid.NewGuid(),
            ArticleNumber = articleNumber,
            VersionNumber = 1,
            Title = "Test Article",
            UrlPath = "test",
            Content = """
                <div>Completely broken HTML <
                No closing tags anywhere
                <div data-ccms-ceid="region1">User Content</div
                """,
            TemplateId = templateId,
            Published = DateTimeOffset.UtcNow.AddDays(-1),
            Updated = DateTimeOffset.UtcNow
        };
        _dbContext.Articles.Add(article);
        await _dbContext.SaveChangesAsync();

        var result = await _templateService!.ApplyTemplateToArticleAsync(articleNumber, templateId);

        Assert.IsNotNull(result);

        if (!result.Success)
        {
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(
                result.ErrorMessage.Contains("parse", StringComparison.OrdinalIgnoreCase) ||
                result.ErrorMessage.Contains("HTML", StringComparison.OrdinalIgnoreCase) ||
                result.ErrorMessage.Contains("malformed", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            Assert.IsTrue(result.Warnings.Count > 0);
        }
    }

    /// <summary>
    /// Verifies warnings are produced when a template defines regions that don't match the article's regions.
    /// </summary>
    [TestMethod]
    public async Task ApplyTemplateToArticleAsync_MismatchedRegions_GeneratesWarnings()
    {
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            Title = "New Template",
            Content = """
                <div data-ccms-ceid="header">Template Header</div>
                <div data-ccms-ceid="body">Template Body</div>
                <div data-ccms-ceid="sidebar">NEW Sidebar Region</div>
                """,
            LayoutId = Guid.NewGuid(),
            LayoutNumber = 1
        };
        _dbContext!.Templates.Add(template);

        var articleNumber = 444;
        var article = new Article
        {
            Id = Guid.NewGuid(),
            ArticleNumber = articleNumber,
            VersionNumber = 1,
            Title = "Test Article",
            UrlPath = "test",
            Content = """
                <div data-ccms-ceid="header">User Header Content</div>
                <div data-ccms-ceid="body">User Body Content</div>
                <div data-ccms-ceid="footer">User Footer Content</div>
                """,
            TemplateId = templateId,
            Published = DateTimeOffset.UtcNow.AddDays(-1),
            Updated = DateTimeOffset.UtcNow
        };
        _dbContext.Articles.Add(article);
        await _dbContext.SaveChangesAsync();

        var result = await _templateService!.ApplyTemplateToArticleAsync(articleNumber, templateId);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.NewVersionNumber);
        Assert.IsTrue(result.Warnings.Count > 0);

        var warningText = string.Join(" ", result.Warnings).ToLower();
        Assert.IsTrue(
            warningText.Contains("footer") || warningText.Contains("lost") || warningText.Contains("missing"));

        var newVersion = await _dbContext.Articles.FirstAsync(a => a.Id == result.NewVersionId);
        Assert.IsTrue(newVersion.Content.Contains("User Header Content"));
        Assert.IsTrue(newVersion.Content.Contains("User Body Content"));
        Assert.IsFalse(newVersion.Content.Contains("User Footer Content"));
        Assert.IsTrue(newVersion.Content.Contains("sidebar"));
    }

    /// <summary>
    /// Tests that ApplyTemplateToArticleAsync_ArticleWithNoEditableRegions_SucceedsWithWarning.
    /// </summary>
    [TestMethod]
    public async Task ApplyTemplateToArticleAsync_ArticleWithNoEditableRegions_SucceedsWithWarning()
    {
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            Title = "Template With Regions",
            Content = """
                <div data-ccms-ceid="region1">Template Content 1</div>
                <div data-ccms-ceid="region2">Template Content 2</div>
                """,
            LayoutId = Guid.NewGuid(),
            LayoutNumber = 1
        };
        _dbContext!.Templates.Add(template);

        var articleNumber = 333;
        var article = new Article
        {
            Id = Guid.NewGuid(),
            ArticleNumber = articleNumber,
            VersionNumber = 1,
            Title = "Static Article",
            UrlPath = "static",
            Content = """
                <div>This is completely static content</div>
                <p>No editable regions at all!</p>
                """,
            TemplateId = templateId,
            Published = DateTimeOffset.UtcNow.AddDays(-1),
            Updated = DateTimeOffset.UtcNow
        };
        _dbContext.Articles.Add(article);
        await _dbContext.SaveChangesAsync();

        var result = await _templateService!.ApplyTemplateToArticleAsync(articleNumber, templateId);

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.Warnings.Count > 0);

        var warningText = string.Join(" ", result.Warnings).ToLower();
        Assert.IsTrue(
            warningText.Contains("no editable") ||
            warningText.Contains("no regions") ||
            warningText.Contains("content lost"));

        var newVersion = await _dbContext.Articles.FirstAsync(a => a.Id == result.NewVersionId);
        Assert.IsTrue(newVersion.Content.Contains("data-ccms-ceid"));
        Assert.IsFalse(newVersion.Content.Contains("completely static content"));
    }

    /// <summary>
    /// Tests that ApplyTemplateToArticleAsync_ConcurrentVersionCreation_HandlesCorrectly.
    /// </summary>
    [TestMethod]
    public async Task ApplyTemplateToArticleAsync_ConcurrentVersionCreation_HandlesCorrectly()
    {
        var templateId = Guid.NewGuid();
        await CreateTemplateWithRegionsAsync(templateId, regionCount: 2);
        var articleNumber = 666;
        await CreateArticleWithVersionsAsync(articleNumber, versionCount: 3, templateId);

        var concurrentVersion = new Article
        {
            Id = Guid.NewGuid(),
            ArticleNumber = articleNumber,
            VersionNumber = 4,
            Title = $"Article {articleNumber} v4 (concurrent)",
            UrlPath = $"article-{articleNumber}",
            Content = """<div data-ccms-ceid="region1">Concurrent Version</div>""",
            TemplateId = templateId,
            Published = null,
            Updated = DateTimeOffset.UtcNow
        };
        _dbContext!.Articles.Add(concurrentVersion);
        await _dbContext.SaveChangesAsync();

        var result = await _templateService!.ApplyTemplateToArticleAsync(articleNumber, templateId);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(5, result.NewVersionNumber);

        var allVersions = await _dbContext.Articles
            .Where(a => a.ArticleNumber == articleNumber)
            .OrderBy(a => a.VersionNumber)
            .ToListAsync();

        Assert.AreEqual(5, allVersions.Count);
        Assert.IsTrue(allVersions.Any(v => v.VersionNumber == 4 && v.Content.Contains("Concurrent")));
        Assert.IsTrue(allVersions.Any(v => v.VersionNumber == 5 && v.Id == result.NewVersionId));
    }

    // ============================================================
    // BATCH 3: PreviewTemplateApplicationAsync Tests
    // ============================================================

    /// <summary>
    /// PreviewTemplateApplicationAsync returns a preview containing all articles
    /// that would be affected by applying the provided template.
    /// </summary>
    [TestMethod]
    public async Task PreviewTemplateApplicationAsync_ReturnsAllAffectedArticles()
    {
        var templateId = Guid.NewGuid();
        var articleNumbers = await SeedTemplateAndArticlesAsync(templateId, articleCount: 5);

        var preview = await _templateService!.PreviewTemplateApplicationAsync(templateId);

        Assert.IsNotNull(preview);
        Assert.AreEqual(templateId, preview.TemplateId);
        Assert.AreEqual("Test Template", preview.TemplateName);
        Assert.AreEqual(5, preview.TotalAffectedArticles);
        Assert.AreEqual(5, preview.Articles.Count);

        foreach (var articleNumber in articleNumbers)
        {
            var previewItem = preview.Articles.FirstOrDefault(a => a.ArticleNumber == articleNumber);
            Assert.IsNotNull(previewItem);
        }
    }

    /// <summary>
    /// PreviewTemplateApplicationAsync correctly identifies which articles have
    /// published versions and exposes their last published metadata.
    /// </summary>
    [TestMethod]
    public async Task PreviewTemplateApplicationAsync_DetectsPublishedStatus()
    {
        var templateId = Guid.NewGuid();
        await CreateTemplateWithRegionsAsync(templateId, regionCount: 2);

        var publishedArticle = 801;
        var draftArticle = 802;

        var published = new Article
        {
            Id = Guid.NewGuid(),
            ArticleNumber = publishedArticle,
            VersionNumber = 1,
            Title = "Published Article",
            UrlPath = "published",
            Content = """<div data-ccms-ceid="region1">Content</div>""",
            TemplateId = templateId,
            Published = DateTimeOffset.UtcNow.AddDays(-5),
            Updated = DateTimeOffset.UtcNow.AddDays(-5)
        };
        _dbContext!.Articles.Add(published);
        _dbContext.ArticleCatalog.Add(new CatalogEntry
        {
            ArticleNumber = publishedArticle,
            Title = published.Title,
            UrlPath = published.UrlPath,
            TemplateId = templateId,
            Published = published.Published,
            Updated = published.Updated
        });

        var draft = new Article
        {
            Id = Guid.NewGuid(),
            ArticleNumber = draftArticle,
            VersionNumber = 1,
            Title = "Draft Article",
            UrlPath = "draft",
            Content = """<div data-ccms-ceid="region1">Content</div>""",
            TemplateId = templateId,
            Published = null,
            Updated = DateTimeOffset.UtcNow
        };
        _dbContext.Articles.Add(draft);
        _dbContext.ArticleCatalog.Add(new CatalogEntry
        {
            ArticleNumber = draftArticle,
            Title = draft.Title,
            UrlPath = draft.UrlPath,
            TemplateId = templateId,
            Published = null,
            Updated = draft.Updated
        });

        await _dbContext.SaveChangesAsync();

        var preview = await _templateService!.PreviewTemplateApplicationAsync(templateId);

        Assert.AreEqual(2, preview.TotalAffectedArticles);

        var publishedPreview = preview.Articles.First(a => a.ArticleNumber == publishedArticle);
        var draftPreview = preview.Articles.First(a => a.ArticleNumber == draftArticle);

        Assert.IsTrue(publishedPreview.HasPublishedVersion);
        Assert.IsNotNull(publishedPreview.LastPublished);

        Assert.IsFalse(draftPreview.HasPublishedVersion);
        Assert.IsNull(draftPreview.LastPublished);
    }

    /// <summary>
    /// PreviewTemplateApplicationAsync detects merge warnings when article
    /// editable regions differ from the template and surfaces appropriate messages.
    /// </summary>
    [TestMethod]
    public async Task PreviewTemplateApplicationAsync_DetectsMergeWarnings()
    {
        var templateId = Guid.NewGuid();

        var template = new Template
        {
            Id = templateId,
            Title = "New Template",
            Content = """
                <div data-ccms-ceid="header">Header</div>
                <div data-ccms-ceid="content">Content</div>
                """,
            LayoutId = Guid.NewGuid(),
            LayoutNumber = 1
        };
        _dbContext!.Templates.Add(template);

        var compatibleArticle = new Article
        {
            Id = Guid.NewGuid(),
            ArticleNumber = 701,
            VersionNumber = 1,
            Title = "Compatible Article",
            UrlPath = "compatible",
            Content = """
                <div data-ccms-ceid="header">User Header</div>
                <div data-ccms-ceid="content">User Content</div>
                """,
            TemplateId = templateId,
            Published = DateTimeOffset.UtcNow.AddDays(-1),
            Updated = DateTimeOffset.UtcNow
        };
        _dbContext.Articles.Add(compatibleArticle);
        _dbContext.ArticleCatalog.Add(new CatalogEntry
        {
            ArticleNumber = 701,
            Title = compatibleArticle.Title,
            UrlPath = compatibleArticle.UrlPath,
            TemplateId = templateId,
            Published = compatibleArticle.Published,
            Updated = compatibleArticle.Updated
        });

        var incompatibleArticle = new Article
        {
            Id = Guid.NewGuid(),
            ArticleNumber = 702,
            VersionNumber = 1,
            Title = "Incompatible Article",
            UrlPath = "incompatible",
            Content = """
                <div data-ccms-ceid="header">User Header</div>
                <div data-ccms-ceid="content">User Content</div>
                <div data-ccms-ceid="sidebar">User Sidebar</div>
                <div data-ccms-ceid="footer">User Footer</div>
                """,
            TemplateId = templateId,
            Published = DateTimeOffset.UtcNow.AddDays(-1),
            Updated = DateTimeOffset.UtcNow
        };
        _dbContext.Articles.Add(incompatibleArticle);
        _dbContext.ArticleCatalog.Add(new CatalogEntry
        {
            ArticleNumber = 702,
            Title = incompatibleArticle.Title,
            UrlPath = incompatibleArticle.UrlPath,
            TemplateId = templateId,
            Published = incompatibleArticle.Published,
            Updated = incompatibleArticle.Updated
        });

        await _dbContext.SaveChangesAsync();

        var preview = await _templateService!.PreviewTemplateApplicationAsync(templateId);

        Assert.AreEqual(2, preview.TotalAffectedArticles);

        var compatiblePreview = preview.Articles.First(a => a.ArticleNumber == 701);
        var incompatiblePreview = preview.Articles.First(a => a.ArticleNumber == 702);

        Assert.IsTrue(compatiblePreview.CanMerge);
        Assert.AreEqual(2, compatiblePreview.EditableRegionsCount);
        Assert.IsTrue(string.IsNullOrEmpty(compatiblePreview.MergeWarning));

        Assert.IsTrue(incompatiblePreview.CanMerge);
        Assert.AreEqual(4, incompatiblePreview.EditableRegionsCount);
        Assert.IsFalse(string.IsNullOrEmpty(incompatiblePreview.MergeWarning));

        var warningLower = incompatiblePreview.MergeWarning.ToLower();
        Assert.IsTrue(
            warningLower.Contains("sidebar") || warningLower.Contains("footer") ||
            warningLower.Contains("region") || warningLower.Contains("lost"));

        Assert.IsFalse(preview.AllArticlesSafe);
        Assert.AreEqual(1, preview.WarningCount);
    }

    /// <summary>
    /// PreviewTemplateApplicationAsync returns an empty preview when no articles
    /// are associated with the template and marks the preview as safe.
    /// </summary>
    [TestMethod]
    public async Task PreviewTemplateApplicationAsync_NoArticles_ReturnsEmptyPreview()
    {
        var templateId = Guid.NewGuid();
        var unusedTemplate = new Template
        {
            Id = templateId,
            Title = "Unused Template",
            Content = """<div data-ccms-ceid="region1">Content</div>""",
            LayoutId = Guid.NewGuid(),
            LayoutNumber = 1
        };
        _dbContext!.Templates.Add(unusedTemplate);
        await _dbContext.SaveChangesAsync();

        var preview = await _templateService!.PreviewTemplateApplicationAsync(templateId);

        Assert.IsNotNull(preview);
        Assert.AreEqual(templateId, preview.TemplateId);
        Assert.AreEqual("Unused Template", preview.TemplateName);
        Assert.AreEqual(0, preview.TotalAffectedArticles);
        Assert.AreEqual(0, preview.Articles.Count);
        Assert.IsTrue(preview.AllArticlesSafe);
        Assert.AreEqual(0, preview.WarningCount);
    }

    /// <summary>
    /// PreviewTemplateApplicationAsync includes current version numbers and
    /// published information for affected articles in the preview.
    /// </summary>
    [TestMethod]
    public async Task PreviewTemplateApplicationAsync_ShowsCurrentVersionNumbers()
    {
        var templateId = Guid.NewGuid();
        await CreateTemplateWithRegionsAsync(templateId, regionCount: 2);

        var articleNumber = 901;
        await CreateArticleWithVersionsAsync(
            articleNumber,
            versionCount: 5,
            templateId,
            publishedVersionNumber: 3);

        var preview = await _templateService!.PreviewTemplateApplicationAsync(templateId);

        Assert.AreEqual(1, preview.TotalAffectedArticles);

        var previewItem = preview.Articles.First();
        Assert.AreEqual(articleNumber, previewItem.ArticleNumber);
        Assert.AreEqual(5, previewItem.CurrentVersionNumber);
        Assert.IsTrue(previewItem.HasPublishedVersion);
        Assert.IsNotNull(previewItem.LastPublished);
    }

    /// <summary>
    /// PreviewTemplateApplicationAsync throws InvalidOperationException when
    /// the provided template id does not exist.
    /// </summary>
    [TestMethod]
    public async Task PreviewTemplateApplicationAsync_TemplateNotFound_ThrowsException()
    {
        var nonExistentTemplateId = Guid.NewGuid();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await _templateService!.PreviewTemplateApplicationAsync(nonExistentTemplateId));
    }

    /// <summary>
    /// PreviewTemplateApplicationAsync reports warnings for articles that have
    /// no editable regions relative to the template being previewed.
    /// </summary>
    [TestMethod]
    public async Task PreviewTemplateApplicationAsync_ArticleWithNoEditableRegions_ShowsWarning()
    {
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            Title = "Template With Regions",
            Content = """
                <div data-ccms-ceid="region1">Content 1</div>
                <div data-ccms-ceid="region2">Content 2</div>
                """,
            LayoutId = Guid.NewGuid(),
            LayoutNumber = 1
        };
        _dbContext!.Templates.Add(template);

        var staticArticle = new Article
        {
            Id = Guid.NewGuid(),
            ArticleNumber = 951,
            VersionNumber = 1,
            Title = "Static Article",
            UrlPath = "static",
            Content = """
                <div>Completely static content</div>
                <p>No editable regions!</p>
                """,
            TemplateId = templateId,
            Published = DateTimeOffset.UtcNow.AddDays(-1),
            Updated = DateTimeOffset.UtcNow
        };
        _dbContext.Articles.Add(staticArticle);
        _dbContext.ArticleCatalog.Add(new CatalogEntry
        {
            ArticleNumber = 951,
            Title = staticArticle.Title,
            UrlPath = staticArticle.UrlPath,
            TemplateId = templateId,
            Published = staticArticle.Published,
            Updated = staticArticle.Updated
        });
        await _dbContext.SaveChangesAsync();

        var preview = await _templateService!.PreviewTemplateApplicationAsync(templateId);

        Assert.AreEqual(1, preview.TotalAffectedArticles);

        var previewItem = preview.Articles.First();
        Assert.AreEqual(0, previewItem.EditableRegionsCount);
        Assert.IsTrue(previewItem.CanMerge);
        Assert.IsFalse(string.IsNullOrEmpty(previewItem.MergeWarning));

        var warningLower = previewItem.MergeWarning.ToLower();
        Assert.IsTrue(
            warningLower.Contains("no editable") ||
            warningLower.Contains("static") ||
            (warningLower.Contains("content") && warningLower.Contains("lost")));

        Assert.IsFalse(preview.AllArticlesSafe);
        Assert.AreEqual(1, preview.WarningCount);
    }

    // ============================================================
    // BATCH 4: GetTemplateDesignVersionsAsync Tests
    // ============================================================

    /// <summary>
    /// GetTemplateDesignVersionsAsync auto-creates and returns a default
    /// published design version when no versions exist for the page type.
    /// </summary>
    [TestMethod]
    public async Task GetTemplateDesignVersionsAsync_CreatesDefaultVersion_WhenNoneExist()
    {
        // Arrange - Create a template with no versions
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            Title = "Test Template",
            PageType = "test-page",
            Content = """<div data-ccms-ceid="region1">Default Content</div>""",
            LayoutId = Guid.NewGuid(),
            LayoutNumber = 1
        };
        _dbContext!.Templates.Add(template);
        await _dbContext.SaveChangesAsync();

        // Act
        var versions = await _templateService!.GetTemplateDesignVersionsAsync("test-page");

        // Assert
        Assert.IsNotNull(versions);
        Assert.AreEqual(1, versions.Count);
        Assert.AreEqual(1, versions[0].Version);
        Assert.AreEqual("test-page", versions[0].PageType);
        Assert.AreEqual(template.Title, versions[0].Title);
        Assert.AreEqual(template.Content, versions[0].Content);
        Assert.IsNotNull(versions[0].Published, "Auto-created version should be published");
    }

    /// <summary>
    /// GetTemplateDesignVersionsAsync returns design versions ordered
    /// descending by version number, with published metadata preserved.
    /// </summary>
    [TestMethod]
    public async Task GetTemplateDesignVersionsAsync_ReturnsVersionsInDescendingOrder()
    {
        // Arrange - Create template with multiple versions
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            Title = "Versioned Template",
            PageType = "versioned-page",
            Content = """<div>Version 3 Content</div>""",
            LayoutId = Guid.NewGuid(),
            LayoutNumber = 1
        };
        _dbContext!.Templates.Add(template);

        // Create versions 1, 2, 3
        for (int i = 1; i <= 3; i++)
        {
            var version = new PageDesignVersion
            {
                Id = Guid.NewGuid(),
                TemplateId = templateId,
                PageType = "versioned-page",
                Version = i,
                Title = $"Version {i}",
                Content = $"""<div>Version {i} Content</div>""",
                Published = i == 2 ? DateTimeOffset.UtcNow : null, // Version 2 is published
                Modified = DateTimeOffset.UtcNow.AddDays(-i)
            };
            _dbContext.PageDesignVersions.Add(version);
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var versions = await _templateService!.GetTemplateDesignVersionsAsync("versioned-page");

        // Assert
        Assert.AreEqual(3, versions.Count);
        Assert.AreEqual(3, versions[0].Version, "Should be in descending order");
        Assert.AreEqual(2, versions[1].Version);
        Assert.AreEqual(1, versions[2].Version);
        Assert.IsNotNull(versions[1].Published, "Version 2 should be published");
    }

    /// <summary>
    /// GetTemplateDesignVersionsAsync returns an empty list when no templates
    /// exist for the provided page type.
    /// </summary>
    [TestMethod]
    public async Task GetTemplateDesignVersionsAsync_ReturnsEmptyList_WhenTemplateNotFound()
    {
        // Act
        var versions = await _templateService!.GetTemplateDesignVersionsAsync("non-existent-page");

        // Assert
        Assert.IsNotNull(versions);
        Assert.AreEqual(0, versions.Count, "Should return empty list when no template found");
    }

    /// <summary>
    /// GetTemplateDesignVersionsAsync returns existing versions without
    /// creating a new default when versions already exist for the page type.
    /// </summary>
    [TestMethod]
    public async Task GetTemplateDesignVersionsAsync_ReturnsExistingVersions_WithoutCreatingNew()
    {
        // Arrange - Create template with existing version
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            Title = "Existing Template",
            PageType = "update-test",
            Content = """<div>Template Content</div>""",
            LayoutId = Guid.NewGuid(),
            LayoutNumber = 1
        };
        _dbContext!.Templates.Add(template);

        var existingVersion = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            PageType = "update-test",
            Version = 1,
            Title = "Existing Version",
            Content = """<div>Existing Version Content</div>""",
            Published = DateTimeOffset.UtcNow.AddDays(-5),
            Modified = DateTimeOffset.UtcNow.AddDays(-5)
        };
        _dbContext.PageDesignVersions.Add(existingVersion);
        await _dbContext.SaveChangesAsync();

        var initialCount = await _dbContext.PageDesignVersions.CountAsync();

        // Act
        var versions = await _templateService!.GetTemplateDesignVersionsAsync("update-test");

        // Assert
        var finalCount = await _dbContext.PageDesignVersions.CountAsync();
        Assert.AreEqual(initialCount, finalCount, "Should not create new versions");
        Assert.AreEqual(1, versions.Count);
        Assert.AreEqual("Existing Version", versions[0].Title);
    }

    // ============================================================
    // BATCH 6: Save and Publish Tests
    // ============================================================

    /// <summary>
    /// Save creates a new PageDesignVersion in the database when the
    /// provided version does not already exist.
    /// </summary>
    [TestMethod]
    public async Task Save_CreatesNewVersion_WhenNotExists()
    {
        // Arrange - Create a new PageDesignVersion that doesn't exist in DB
        var templateId = Guid.NewGuid();
        var newVersion = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            PageType = "new-page",
            Version = 1,
            Title = "New Version",
            Description = "New description",
            Content = """<div>New Content</div>""",
            Published = null,
            Modified = DateTimeOffset.UtcNow
        };

        // Act
        await _templateService!.Save(newVersion);

        // Assert
        var savedVersion = await _dbContext!.PageDesignVersions.FindAsync(newVersion.Id);
        Assert.IsNotNull(savedVersion, "Version should be created in database");
        Assert.AreEqual("New Version", savedVersion.Title);
        Assert.AreEqual("New description", savedVersion.Description);
        Assert.AreEqual("""<div>New Content</div>""", savedVersion.Content);
        Assert.AreEqual(1, savedVersion.Version);
    }

    /// <summary>
    /// Save updates an existing PageDesignVersion while preserving its
    /// version number and not altering publish metadata.
    /// </summary>
    [TestMethod]
    public async Task Save_UpdatesExistingVersion_PreservesVersionNumber()
    {
        // Arrange - Create existing version
        var templateId = Guid.NewGuid();
        var existingVersion = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            PageType = "update-page",
            Version = 3,
            Title = "Original Title",
            Description = "Original description",
            Content = """<div>Original Content</div>""",
            Published = null,
            Modified = DateTimeOffset.UtcNow.AddDays(-5)
        };
        _dbContext!.PageDesignVersions.Add(existingVersion);
        await _dbContext.SaveChangesAsync();

        // Modify the version
        existingVersion.Title = "Updated Title";
        existingVersion.Description = "Updated description";
        existingVersion.Content = """<div>Updated Content</div>""";

        // Act
        await _templateService!.Save(existingVersion);

        // Assert
        var updatedVersion = await _dbContext.PageDesignVersions.FindAsync(existingVersion.Id);
        Assert.IsNotNull(updatedVersion);
        Assert.AreEqual("Updated Title", updatedVersion.Title);
        Assert.AreEqual("Updated description", updatedVersion.Description);
        Assert.AreEqual("""<div>Updated Content</div>""", updatedVersion.Content);
        Assert.AreEqual(3, updatedVersion.Version, "Version number should not change");
        Assert.IsNull(updatedVersion.Published, "Published date should not change");
    }

    /// <summary>
    /// Publish ensures the selected design version becomes published and
    /// that other versions for the same template are unpublished.
    /// </summary>
    [TestMethod]
    public async Task Publish_UnpublishesOtherVersions_PublishesSelected()
    {
        // Arrange - Create multiple versions of the same template
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            Title = "Multi-Version Template",
            PageType = "publish-test",
            Content = """<div>Template Content</div>""",
            LayoutId = Guid.NewGuid(),
            LayoutNumber = 1
        };
        _dbContext!.Templates.Add(template);

        // Create versions 1 (published), 2 (unpublished), 3 (to be published)
        var version1 = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            PageType = "publish-test",
            Version = 1,
            Title = "Version 1",
            Content = """<div>V1 Content</div>""",
            Published = DateTimeOffset.UtcNow.AddDays(-10),
            Modified = DateTimeOffset.UtcNow.AddDays(-10)
        };
        var version2 = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            PageType = "publish-test",
            Version = 2,
            Title = "Version 2",
            Content = """<div>V2 Content</div>""",
            Published = null,
            Modified = DateTimeOffset.UtcNow.AddDays(-5)
        };
        var version3 = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            PageType = "publish-test",
            Version = 3,
            Title = "Version 3",
            Content = """<div>V3 Content</div>""",
            Published = null,
            Modified = DateTimeOffset.UtcNow
        };

        _dbContext.PageDesignVersions.AddRange(version1, version2, version3);
        await _dbContext.SaveChangesAsync();

        // Act - Publish version 3
        await _templateService!.Publish(version3);

        // Assert
        var v1Updated = await _dbContext.PageDesignVersions.FindAsync(version1.Id);
        var v2Updated = await _dbContext.PageDesignVersions.FindAsync(version2.Id);
        var v3Updated = await _dbContext.PageDesignVersions.FindAsync(version3.Id);

        Assert.IsNull(v1Updated!.Published, "Version 1 should be unpublished");
        Assert.IsNull(v2Updated!.Published, "Version 2 should remain unpublished");
        Assert.IsNotNull(v3Updated!.Published, "Version 3 should be published");
        Assert.IsTrue(v3Updated.Published >= DateTimeOffset.UtcNow.AddSeconds(-5), "Published date should be recent");
    }

    /// <summary>
    /// Publish updates the corresponding template record with the new
    /// content and metadata from the published design version.
    /// </summary>
    [TestMethod]
    public async Task Publish_UpdatesCorrespondingTemplate_WithNewContent()
    {
        // Arrange - Create template and version
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            Title = "Original Template Title",
            Description = "Original description",
            PageType = "template-update-test",
            Content = """<div>Original Template Content</div>""",
            LayoutId = Guid.NewGuid(),
            LayoutNumber = 1
        };
        _dbContext!.Templates.Add(template);

        var version = new PageDesignVersion
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            PageType = "template-update-test",
            Version = 2,
            Title = "New Version Title",
            Description = "New version description",
            Content = """<div>New Version Content</div>""",
            Published = null,
            Modified = DateTimeOffset.UtcNow
        };
        _dbContext.PageDesignVersions.Add(version);
        await _dbContext.SaveChangesAsync();

        // Act - Publish the version
        await _templateService!.Publish(version);

        // Assert - Template should be updated
        var updatedTemplate = await _dbContext.Templates.FindAsync(templateId);
        Assert.IsNotNull(updatedTemplate);
        Assert.AreEqual("New Version Title", updatedTemplate.Title, "Template title should be updated");
        Assert.AreEqual("New version description", updatedTemplate.Description, "Template description should be updated");
        Assert.AreEqual("""<div>New Version Content</div>""", updatedTemplate.Content, "Template content should be updated");

        // Verify version is published
        var publishedVersion = await _dbContext.PageDesignVersions.FindAsync(version.Id);
        Assert.IsNotNull(publishedVersion!.Published);
    }

    // ============================================================
    // BATCH 7: Batch Operations Tests
    // ============================================================

    /// <summary>
    /// ApplyTemplateToArticlesAsync applies the template to all articles
    /// when the article list parameter is null.
    /// </summary>
    [TestMethod]
    public async Task ApplyTemplateToArticlesAsync_AppliestoAllArticles_WhenNullList()
    {
        // Arrange - Create template with multiple articles
        var templateId = Guid.NewGuid();
        var articleNumbers = await SeedTemplateAndArticlesAsync(templateId, articleCount: 5);

        // Act - Apply with null list (should process ALL articles)
        var result = await _templateService!.ApplyTemplateToArticlesAsync(templateId, null);

        // Assert
        Assert.AreEqual(5, result.SuccessCount, "Should process all 5 articles");
        Assert.AreEqual(0, result.FailureCount);
        Assert.AreEqual(5, result.Results.Count);

        // Verify each article has a new draft version
        foreach (var articleNumber in articleNumbers)
        {
            var versions = await _dbContext!.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .ToListAsync();

            Assert.AreEqual(2, versions.Count, $"Article {articleNumber} should have 2 versions");
            Assert.IsTrue(versions.Any(v => v.VersionNumber == 2 && v.Published == null),
                $"Article {articleNumber} should have a draft v2");
        }
    }

    /// <summary>
    /// ApplyTemplateToArticlesAsync applies the template only to the
    /// specified list of article numbers when provided.
    /// </summary>
    [TestMethod]
    public async Task ApplyTemplateToArticlesAsync_AppliesOnlyToSpecified_WhenListProvided()
    {
        // Arrange - Create template with 5 articles
        var templateId = Guid.NewGuid();
        var allArticles = await SeedTemplateAndArticlesAsync(templateId, articleCount: 5);
        var selectedArticles = new List<int> { allArticles[0], allArticles[2], allArticles[4] }; // Apply to 3 articles

        // Act - Apply to specific articles only
        var result = await _templateService!.ApplyTemplateToArticlesAsync(templateId, selectedArticles);

        // Assert
        Assert.AreEqual(3, result.SuccessCount, "Should process only selected 3 articles");
        Assert.AreEqual(0, result.FailureCount);
        Assert.AreEqual(3, result.Results.Count);

        // Verify only selected articles have new versions
        foreach (var articleNumber in selectedArticles)
        {
            var versions = await _dbContext!.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .ToListAsync();
            Assert.AreEqual(2, versions.Count, $"Selected article {articleNumber} should have 2 versions");
        }

        // Verify non-selected articles still have only 1 version
        var nonSelectedArticles = allArticles.Except(selectedArticles).ToList();
        foreach (var articleNumber in nonSelectedArticles)
        {
            var versions = await _dbContext!.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .ToListAsync();
            Assert.AreEqual(1, versions.Count, $"Non-selected article {articleNumber} should still have 1 version");
        }
    }

    /// <summary>
    /// ApplyTemplateToArticlesAsync continues processing when some
    /// articles fail and reports success/failure counts and messages.
    /// </summary>
    [TestMethod]
    public async Task ApplyTemplateToArticlesAsync_ContinuesOnError_ReportsFailures()
    {
        // Arrange - Create template with some valid articles
        var templateId = Guid.NewGuid();
        var validArticles = await SeedTemplateAndArticlesAsync(templateId, articleCount: 3);

        // Add non-existent article numbers to the list
        var mixedList = new List<int>
        {
            validArticles[0],   // Valid
            99991,              // Invalid
            validArticles[1],   // Valid
            99992,              // Invalid
            validArticles[2]    // Valid
        };

        // Act - Apply to mix of valid and invalid articles
        var result = await _templateService!.ApplyTemplateToArticlesAsync(templateId, mixedList);

        // Assert
        Assert.AreEqual(3, result.SuccessCount, "Should succeed for 3 valid articles");
        Assert.AreEqual(2, result.FailureCount, "Should fail for 2 invalid articles");
        Assert.AreEqual(5, result.Results.Count, "Should have results for all 5 attempts");

        // Verify valid articles were processed
        var successfulResults = result.Results.Where(r => r.Success).ToList();
        Assert.AreEqual(3, successfulResults.Count);
        Assert.IsTrue(validArticles.All(an => successfulResults.Any(r => r.ArticleNumber == an)));

        // Verify failures have error messages
        var failedResults = result.Results.Where(r => !r.Success).ToList();
        Assert.AreEqual(2, failedResults.Count);
        Assert.IsTrue(failedResults.All(r => !string.IsNullOrEmpty(r.ErrorMessage)),
            "Failed results should have error messages");
    }

    /// <summary>
    /// PublishTemplateChangesAsync publishes draft changes for all
    /// articles when the article list is null.
    /// </summary>
    [TestMethod]
    public async Task PublishTemplateChangesAsync_PublishesAllArticles_WhenNullList()
    {
        // Arrange - Create template and articles
        var templateId = Guid.NewGuid();
        var articleNumbers = await SeedTemplateAndArticlesAsync(templateId, articleCount: 4);

        // Apply template to create drafts for all articles
        await _templateService!.ApplyTemplateToArticlesAsync(templateId, null);

        // Act - Publish all articles (null list)
        var result = await _templateService.PublishTemplateChangesAsync(templateId, null);

        // Assert
        Assert.AreEqual(4, result.Results.Count(r => r.Success), "Should publish all 4 articles");
        Assert.AreEqual(0, result.Results.Count(r => !r.Success), "No failures expected");

        // Verify all articles have published v2
        foreach (var articleNumber in articleNumbers)
        {
            // Detach all tracked entities to ensure fresh query from database
            _dbContext!.ChangeTracker.Clear();

            var v2 = await _dbContext.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == articleNumber && a.VersionNumber == 2);

            Assert.IsNotNull(v2, $"Article {articleNumber} should have v2");
            Assert.IsNotNull(v2.Published, $"Article {articleNumber} v2 should be published");

            // Verify v1 is unpublished
            var v1 = await _dbContext.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == articleNumber && a.VersionNumber == 1);
            Assert.IsNull(v1!.Published, $"Article {articleNumber} v1 should be unpublished");
        }
    }

    /// <summary>
    /// PublishTemplateChangesAsync publishes changes only for the
    /// provided list of article numbers when specified.
    /// </summary>
    [TestMethod]
    public async Task PublishTemplateChangesAsync_PublishesOnlySpecified_WhenListProvided()
    {
        // Arrange - Create template and articles
        var templateId = Guid.NewGuid();
        var allArticles = await SeedTemplateAndArticlesAsync(templateId, articleCount: 5);

        // Apply template to create drafts for all
        await _templateService!.ApplyTemplateToArticlesAsync(templateId, null);

        // Select 2 articles to publish
        var selectedArticles = new List<int> { allArticles[1], allArticles[3] };

        // Act - Publish only selected articles
        var result = await _templateService.PublishTemplateChangesAsync(templateId, selectedArticles);

        // Assert
        Assert.AreEqual(2, result.Results.Count, "Should process 2 articles");
        Assert.AreEqual(2, result.Results.Count(r => r.Success), "Both should succeed");

        // Verify selected articles are published
        foreach (var articleNumber in selectedArticles)
        {
            // Detach all tracked entities to ensure fresh query from database
            _dbContext!.ChangeTracker.Clear();

            var v2 = await _dbContext.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == articleNumber && a.VersionNumber == 2);
            Assert.IsNotNull(v2!.Published, $"Selected article {articleNumber} v2 should be published");
        }

        // Verify non-selected articles remain as drafts
        var nonSelected = allArticles.Except(selectedArticles).ToList();
        foreach (var articleNumber in nonSelected)
        {
            // Detach all tracked entities to ensure fresh query from database
            _dbContext!.ChangeTracker.Clear();

            var v2 = await _dbContext.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == articleNumber && a.VersionNumber == 2);
            Assert.IsNull(v2!.Published, $"Non-selected article {articleNumber} v2 should remain draft");

            // v1 should still be published
            var v1 = await _dbContext.Articles
                .FirstOrDefaultAsync(a => a.ArticleNumber == articleNumber && a.VersionNumber == 1);
            Assert.IsNotNull(v1!.Published, $"Non-selected article {articleNumber} v1 should still be published");
        }
    }

    /// <summary>
    /// PublishTemplateChangesAsync ensures the newly published version is
    /// marked published and previous versions for the article are
    /// un-published as appropriate.
    /// </summary>
    [TestMethod]
    public async Task PublishTemplateChangesAsync_UnpublishesPreviousVersions()
    {
        // Arrange - Create template and article with multiple versions
        var templateId = Guid.NewGuid();
        await CreateTemplateWithRegionsAsync(templateId, regionCount: 2);

        var articleNumber = 500;

        // Create article with 3 versions: v1 (old published), v2 (unpublished), v3 (current published)
        var v1 = new Article
        {
            Id = Guid.NewGuid(),
            ArticleNumber = articleNumber,
            VersionNumber = 1,
            Title = "Article v1",
            UrlPath = "test-article",
            Content = """<div data-ccms-ceid="region1">V1 Content</div>""",
            TemplateId = templateId,
            Published = null, // Was published, but got unpublished when v3 was published
            Updated = DateTimeOffset.UtcNow.AddDays(-10)
        };

        var v2 = new Article
        {
            Id = Guid.NewGuid(),
            ArticleNumber = articleNumber,
            VersionNumber = 2,
            Title = "Article v2",
            UrlPath = "test-article",
            Content = """<div data-ccms-ceid="region1">V2 Content</div>""",
            TemplateId = templateId,
            Published = null, // Never published
            Updated = DateTimeOffset.UtcNow.AddDays(-5)
        };

        var v3 = new Article
        {
            Id = Guid.NewGuid(),
            ArticleNumber = articleNumber,
            VersionNumber = 3,
            Title = "Article v3",
            UrlPath = "test-article",
            Content = """<div data-ccms-ceid="region1">V3 Content</div>""",
            TemplateId = templateId,
            Published = DateTimeOffset.UtcNow.AddDays(-2), // Currently published
            Updated = DateTimeOffset.UtcNow.AddDays(-2)
        };

        _dbContext!.Articles.AddRange(v1, v2, v3);
        _dbContext.ArticleCatalog.Add(new CatalogEntry
        {
            ArticleNumber = articleNumber,
            Title = "Article v3",
            UrlPath = "test-article",
            TemplateId = templateId,
            Published = v3.Published,
            Updated = v3.Updated
        });
        await _dbContext.SaveChangesAsync();

        // Apply template to create v4
        await _templateService!.ApplyTemplateToArticleAsync(articleNumber, templateId);

        // Act - Publish v4
        var result = await _templateService.PublishTemplateChangesAsync(templateId, new List<int> { articleNumber });

        // Assert
        Assert.AreEqual(1, result.Results.Count(r => r.Success));

        // Verify v4 is now published
        var v4 = await _dbContext.Articles
            .FirstOrDefaultAsync(a => a.ArticleNumber == articleNumber && a.VersionNumber == 4);
        Assert.IsNotNull(v4);
        Assert.IsNotNull(v4.Published, "V4 should be published");

        // Verify all other versions are unpublished
        var v1Updated = await _dbContext.Articles.FindAsync(v1.Id);
        var v2Updated = await _dbContext.Articles.FindAsync(v2.Id);
        var v3Updated = await _dbContext.Articles.FindAsync(v3.Id);

        Assert.IsNull(v1Updated!.Published, "V1 should be unpublished");
        Assert.IsNull(v2Updated!.Published, "V2 should remain unpublished");
        Assert.IsNull(v3Updated!.Published, "V3 should be unpublished (was previously published)");
    }
}
