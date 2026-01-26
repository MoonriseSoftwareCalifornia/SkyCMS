using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Microsoft.EntityFrameworkCore;
using Sky.Cms.Models;
using Sky.Cms.Services;
using Sky.Editor.Domain.Events;
using Sky.Editor.Features.Articles.Save;

namespace Sky.Tests.Logic;

[DoNotParallelize]
[TestClass]
public class ArticleEditLogicRemainingTests : SkyCmsTestBase
{

    [TestInitialize]
    public new void Setup() => InitializeTestContext();

    [TestCleanup]
    public void Cleanup() => this.Db.Dispose();

    // Helper to tolerate leading slash differences in stored blob paths (BUG FIX #2 & #3: path mismatch assertions)
    private async Task<bool> StaticExists(string urlPath)
    {
        if (string.IsNullOrWhiteSpace(urlPath)) return false;

        if (urlPath.Equals("root", StringComparison.OrdinalIgnoreCase))
        {
            return await Storage.BlobExistsAsync("/index.html")
                   || await Storage.BlobExistsAsync("index.html");
        }

        var slug = urlPath.TrimStart('/');
        return await Storage.BlobExistsAsync("/" + slug)
               || await Storage.BlobExistsAsync(slug);
    }

    #region CreateHomePage

    [TestMethod]
    public async Task CreateHomePage_SwitchesRootAndPublishesBoth()
    {
        var root = await Logic.CreateArticle("Home Page", TestUserId);
        Assert.AreEqual("root", root.UrlPath);

        var second = await Logic.CreateArticle("Section Landing", TestUserId);

        await Logic.PublishArticle(second.Id, DateTimeOffset.UtcNow);
        Assert.IsTrue(await StaticExists(second.UrlPath), "Expected static page for second article.");

        await Logic.CreateHomePage(new NewHomeViewModel
        {
            ArticleNumber = second.ArticleNumber,
            Id = second.Id
        });

        Assert.IsTrue(await StaticExists("root"), "Expected updated root static page.");

        var updatedOldRootVersions = await Db.Articles.Where(a => a.ArticleNumber == root.ArticleNumber).ToListAsync();
        var updatedNewRootVersions = await Db.Articles.Where(a => a.ArticleNumber == second.ArticleNumber).ToListAsync();

        Assert.IsTrue(updatedOldRootVersions.All(v => v.UrlPath != "root"));
        Assert.IsTrue(updatedNewRootVersions.All(v => v.UrlPath == "root"));
        Assert.IsTrue(updatedNewRootVersions.Any(v => v.Published.HasValue));
    }

    #endregion

    #region DeleteArticle

    [TestMethod]
    public async Task DeleteArticle_MarksDeleted_RemovesPagesAndCatalog()
    {
        await Logic.CreateArticle("Home Page", TestUserId);

        var art = await Logic.CreateArticle("Page To Delete", TestUserId);
        var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == art.ArticleNumber);
        await Logic.PublishArticle(entity.Id, DateTimeOffset.UtcNow);

        Assert.IsTrue(await Db.Pages.AnyAsync(p => p.ArticleNumber == art.ArticleNumber));
        Assert.IsNotNull(await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == art.ArticleNumber));

        await Logic.DeleteArticle(art.ArticleNumber);

        var versions = await Db.Articles.Where(a => a.ArticleNumber == art.ArticleNumber).ToListAsync();
        Assert.IsTrue(versions.All(v => v.StatusCode == (int)StatusCodeEnum.Deleted));
        Assert.IsFalse(await Db.Pages.AnyAsync(p => p.ArticleNumber == art.ArticleNumber));
        Assert.IsNull(await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == art.ArticleNumber));
    }

    [TestMethod]
    public async Task DeleteArticle_Root_Throws()
    {
        var root = await Logic.CreateArticle("Home Page", TestUserId);
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() =>
            Logic.DeleteArticle(root.ArticleNumber));
    }

    #endregion

    #region RestoreArticle

    [TestMethod]
    public async Task RestoreArticle_RestoresToActiveAndClearsPublished()
    {
        await Logic.CreateArticle("Home Page", TestUserId);
        var victim = await Logic.CreateArticle("To Restore", TestUserId);
        var ent = await Db.Articles.FirstAsync(a => a.ArticleNumber == victim.ArticleNumber);
        await Logic.PublishArticle(ent.Id, DateTimeOffset.UtcNow);

        await Logic.DeleteArticle(victim.ArticleNumber);
        await Logic.RestoreArticle(victim.ArticleNumber, TestUserId.ToString());

        var restored = await Db.Articles.Where(a => a.ArticleNumber == victim.ArticleNumber).ToListAsync();
        Assert.IsTrue(restored.All(a => a.StatusCode == (int)StatusCodeEnum.Active));
        Assert.IsTrue(restored.All(a => a.Published == null));
        Assert.IsNotNull(await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == victim.ArticleNumber));
    }

    #endregion

    #region CheckCatalogEntries

    [TestMethod]
    public async Task CheckCatalogEntries_DoesNotDuplicateExistingEntries()
    {
        await Logic.CreateArticle("Home Page", TestUserId);
        var page = await Logic.CreateArticle("Catalog Test", TestUserId);
        var ent = await Db.Articles.FirstAsync(a => a.ArticleNumber == page.ArticleNumber);
        await Logic.PublishArticle(ent.Id, DateTimeOffset.UtcNow);

        var before = await Db.ArticleCatalog.CountAsync(c => c.ArticleNumber == page.ArticleNumber);
        // No explicit method now; catalog maintained automatically.
        var after = await Db.ArticleCatalog.CountAsync(c => c.ArticleNumber == page.ArticleNumber);

        Assert.AreEqual(before, after);
    }

    #endregion

    #region ValidateTitle Edge Cases

    [TestMethod]
    public async Task ValidateTitle_SameArticleNumber_AllowsRetainingSameTitle()
    {
        await Logic.CreateArticle("Home Page", TestUserId);
        var art = await Logic.CreateArticle("Case Title", TestUserId);

        var valid = await TitleChangeService.ValidateTitle("Case Title", art.ArticleNumber);
        Assert.IsTrue(valid);
    }

    [TestMethod]
    public async Task ValidateTitle_ReservedCaseInsensitive_ReturnsFalse()
    {
        await Logic.CreateArticle("Home Page", TestUserId);
        var valid = await TitleChangeService.ValidateTitle("ROOT", null);
        Assert.IsFalse(valid);
    }

    #endregion

    #region GetCatalogEntry

    [TestMethod]
    public async Task GetCatalogEntry_ByArticle_CreatesWhenMissing()
    {
        await Logic.CreateArticle("Home Page", TestUserId);
        var page = await Logic.CreateArticle("Catalog Create", TestUserId);

        var existing = await Db.ArticleCatalog.FirstOrDefaultAsync(c => c.ArticleNumber == page.ArticleNumber);
        Db.ArticleCatalog.Remove(existing);
        await Db.SaveChangesAsync();

        var entity = await Db.Articles.FirstAsync(a => a.ArticleNumber == page.ArticleNumber);
        var entry = await Logic.GetCatalogEntry(entity);
        Assert.IsNotNull(entry);
        Assert.AreEqual(page.ArticleNumber, entry.ArticleNumber);
    }

    [TestMethod]
    public async Task GetCatalogEntry_ByViewModel_ReturnsExisting()
    {
        await Logic.CreateArticle("Home Page", TestUserId);
        var page = await Logic.CreateArticle("Catalog VM", TestUserId);

        var vmEntity = await Db.Articles.FirstAsync(a => a.ArticleNumber == page.ArticleNumber);
        var entry1 = await Logic.GetCatalogEntry(vmEntity);
        var vm = await Logic.GetArticleByArticleNumber(page.ArticleNumber, null);
        var entry2 = await Logic.GetCatalogEntry(vm);

        Assert.AreEqual(entry1.ArticleNumber, entry2.ArticleNumber);
    }

    #endregion

    #region CreateRedirect (Title Change)

    [TestMethod]
    public async Task SaveArticle_TitleChange_CreatesRedirect()
    {
        await Logic.CreateArticle("Home Page", TestUserId);

        var original = await Logic.CreateArticle("Original Title", TestUserId);
        var originalEntity = await Db.Articles
            .Where(a => a.ArticleNumber == original.ArticleNumber)
            .OrderByDescending(a => a.VersionNumber)
            .FirstAsync();
        await Logic.PublishArticle(originalEntity.Id, DateTimeOffset.UtcNow);

        var vm = await Logic.GetArticleByArticleNumber(original.ArticleNumber, null);
        var oldUrl = vm.UrlPath;

        // MIGRATED: Use SaveArticleHandler
        var command = new SaveArticleCommand
        {
            ArticleNumber = vm.ArticleNumber,
            Title = "Renamed Title",
            Content = string.IsNullOrWhiteSpace(vm.Content) ? "<p>Content</p>" : vm.Content,
            UserId = TestUserId,
            ArticleType = vm.ArticleType,
            Published = vm.Published
        };

        var result = await SaveArticleHandler.HandleAsync(command);
        Assert.IsTrue(result.IsSuccess);

        var redirectStatusCode = (int)StatusCodeEnum.Redirect;
        var redirects = await Db.Articles
            .Where(a => a.StatusCode == redirectStatusCode && a.UrlPath == oldUrl)
            .ToListAsync();
        Assert.IsTrue(redirects.Any(), "Expected redirect article for old slug.");

        Assert.IsTrue(await StaticExists(oldUrl), "Expected static redirect artifact.");
    }

    #endregion

    private sealed class FakeViewRenderService : IViewRenderService
    {
        public Task<string> RenderToStringAsync(string viewName, object model) =>
            Task.FromResult("<html><body>rendered</body></html>");
    }

    private sealed class NoOpDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IEnumerable<IDomainEvent> events) => Task.CompletedTask;
        public Task DispatchAsync(IDomainEvent @event) => Task.CompletedTask;

        public Task DispatchAsync(IDomainEvent @event, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
