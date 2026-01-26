using Cosmos.Cms.Common;
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Microsoft.EntityFrameworkCore;

namespace Sky.Tests.Services
{
    [TestClass]
    [DoNotParallelize]
    public class TitleChangeServiceTests : SkyCmsTestBase
    {
        [TestInitialize]
        public new void Setup() => InitializeTestContext();

        #region Basic Title Change Tests

        [TestMethod]
        public async Task HandleTitleChangeAsync_RootPage_PreservesRootUrlPath()
        {
            // Arrange
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == rootArticle.ArticleNumber);
            
            Assert.AreEqual("root", article.UrlPath);

            // Capture the old URL path BEFORE changing the title
            var oldUrlPath = article.UrlPath; // "root"
            var oldTitle = article.Title;

            article.Title = "New Home Page";

            // Act - Pass the old URL path (not the old title)
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert
            var updated = await Db.Articles.FirstAsync(a => a.Id == article.Id);
            Assert.AreEqual("root", updated.UrlPath);
            Assert.AreEqual("New Home Page", updated.Title);
        }

        [TestMethod]
        public async Task HandleTitleChangeAsync_NonRootPage_ChangesUrlPath()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var nonRootArticle = await Logic.CreateArticle("About Us", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == nonRootArticle.ArticleNumber);
            
            var originalPath = article.UrlPath;
            var oldTitle = article.Title;
            Assert.AreNotEqual("root", originalPath);
            Assert.AreEqual("about-us", originalPath);

            // Capture the old URL path BEFORE changing the title
            var oldUrlPath = article.UrlPath; // "about-us"
            article.Title = "Company Information"; // Update to new title

            // Act - Pass the old URL path (not the old title)
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert
            var updated = await Db.Articles.FirstAsync(a => a.Id == article.Id);
            Assert.AreNotEqual(originalPath, updated.UrlPath);
            Assert.AreEqual("company-information", updated.UrlPath);
        }

        [TestMethod]
        public async Task HandleTitleChangeAsync_RootPageWithChildren_ChildrenUnaffected()
        {
            // Arrange
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);
            var childArticle = await Logic.CreateArticle("root/child", TestUserId);
            
            var root = await Db.Articles.FirstAsync(a => a.ArticleNumber == rootArticle.ArticleNumber);
            var child = await Db.Articles.FirstAsync(a => a.ArticleNumber == childArticle.ArticleNumber);

            // **FIX**: Store old title and update to new title
            var oldTitle = root.Title;
            var oldUrlPath = root.UrlPath;

            root.Title = "Main Page";

            // Act - Change root title
            await TitleChangeService.HandleTitleChangeAsync(root, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert
            var updatedRoot = await Db.Articles.FirstAsync(a => a.Id == root.Id);
            var updatedChild = await Db.Articles.FirstAsync(a => a.Id == child.Id);
            
            Assert.AreEqual("root", updatedRoot.UrlPath);
            // Child should not be affected since root UrlPath didn't change
            Assert.AreEqual("root/child", updatedChild.UrlPath);
        }

        [TestMethod]
        public async Task BuildArticleUrl_RootPage_ReturnsNormalizedSlug()
        {
            // Arrange
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == rootArticle.ArticleNumber);

            // Act
            var url = TitleChangeService.BuildArticleUrl(article);

            // Assert
            // BuildArticleUrl doesn't know about root - it just normalizes
            // The HandleTitleChangeAsync method is responsible for preserving "root"
            Assert.AreEqual("home-page", url);
        }

        #endregion

        #region Version Cascade Tests - Requirement 1 & 2

        [TestMethod]
        public async Task HandleTitleChangeAsync_WithVersions_CascadesTitleToAllVersions()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("Original Title", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);
            
            // Create multiple versions of the same article
            var version2 = new Article
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Original Title",
                UrlPath = article.UrlPath,
                VersionNumber = 2,
                Content = "Version 2 content",
                UserId = TestUserId.ToString(),
                StatusCode = 0
            };
            
            var version3 = new Article
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Original Title",
                UrlPath = article.UrlPath,
                VersionNumber = 3,
                Content = "Version 3 content",
                UserId = TestUserId.ToString(),
                StatusCode = 0
            };
            
            Db.Articles.Add(version2);
            Db.Articles.Add(version3);
            await Db.SaveChangesAsync();

            // Capture old values
            var oldTitle = article.Title;
            var oldUrlPath = article.UrlPath;
            
            // Change the title
            article.Title = "Updated Title";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - All versions should have the new title
            var allVersions = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderBy(a => a.VersionNumber)
                .ToListAsync();
            
            Assert.AreEqual(3, allVersions.Count);
            Assert.IsTrue(allVersions.All(v => v.Title == "Updated Title"), "All versions should have the updated title");
        }

        [TestMethod]
        public async Task HandleTitleChangeAsync_WithVersions_CascadesUrlPathAndSlugToAllVersions()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("Original Title", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);
            
            var expectedOldUrlPath = "original-title";
            Assert.AreEqual(expectedOldUrlPath, article.UrlPath);
            
            // Create multiple versions with the same UrlPath
            var version2 = new Article
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Original Title",
                UrlPath = expectedOldUrlPath,
                VersionNumber = 2,
                Content = "Version 2 content",
                UserId = TestUserId.ToString(),
                StatusCode = 0
            };
            
            var version3 = new Article
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Original Title",
                UrlPath = expectedOldUrlPath,
                VersionNumber = 3,
                Content = "Version 3 content",
                UserId = TestUserId.ToString(),
                StatusCode = 0
            };
            
            Db.Articles.Add(version2);
            Db.Articles.Add(version3);
            await Db.SaveChangesAsync();

            // Capture old values
            var oldTitle = article.Title;
            var oldUrlPath = article.UrlPath;
            
            // Change the title (which should update the URL path and slug)
            article.Title = "Updated Title";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - All versions should have the new URL path/slug
            var expectedNewUrlPath = "updated-title";
            var allVersions = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderBy(a => a.VersionNumber)
                .ToListAsync();
            
            Assert.AreEqual(3, allVersions.Count);
            Assert.IsTrue(allVersions.All(v => v.UrlPath == expectedNewUrlPath), 
                $"All versions should have the updated URL path/slug '{expectedNewUrlPath}'");
        }

        [TestMethod]
        public async Task HandleTitleChangeAsync_RootPageWithVersions_PreservesRootUrlPathInAllVersions()
        {
            // Arrange
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == rootArticle.ArticleNumber);
            
            Assert.AreEqual("root", article.UrlPath);
            
            // Create multiple versions of the root page
            var version2 = new Article
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Home Page",
                UrlPath = "root",
                VersionNumber = 2,
                Content = "Version 2 content",
                UserId = TestUserId.ToString(),
                StatusCode = 0
            };
            
            var version3 = new Article
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Home Page",
                UrlPath = "root",
                VersionNumber = 3,
                Content = "Version 3 content",
                UserId = TestUserId.ToString(),
                StatusCode = 0
            };
            
            Db.Articles.Add(version2);
            Db.Articles.Add(version3);
            await Db.SaveChangesAsync();

            // Capture old values
            var oldTitle = article.Title;
            var oldUrlPath = article.UrlPath;
            
            // Change the title
            article.Title = "Welcome Page";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - All versions should have "root" URL path (not changed) - Requirement 3
            var allVersions = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderBy(a => a.VersionNumber)
                .ToListAsync();
            
            Assert.AreEqual(3, allVersions.Count);
            Assert.IsTrue(allVersions.All(v => v.UrlPath == "root"), 
                "All versions of root page should preserve 'root' URL path");
            Assert.IsTrue(allVersions.All(v => v.Title == "Welcome Page"), 
                "All versions should have the updated title");
        }

        [TestMethod]
        public async Task HandleTitleChangeAsync_BlogPostWithVersions_CascadesBlogKeyToAllVersions()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            
            // Create a blog stream
            var blogStreamResult = await Logic.CreateArticle("My Blog", TestUserId);
            var blogStream = await Db.Articles.FirstAsync(a => a.ArticleNumber == blogStreamResult.ArticleNumber);
            blogStream.ArticleType = (int)ArticleType.BlogStream;
            blogStream.BlogKey = "my-blog";
            await Db.SaveChangesAsync();
            
            // Create a blog post
            var blogPostResult = await Logic.CreateArticle("Blog Post Title", TestUserId);
            var blogPost = await Db.Articles.FirstAsync(a => a.ArticleNumber == blogPostResult.ArticleNumber);
            blogPost.ArticleType = (int)ArticleType.BlogPost;
            blogPost.BlogKey = "my-blog";
            blogPost.UrlPath = "my-blog/blog-post-title";
            
            // Create versions of the blog post
            var version2 = new Article
            {
                ArticleNumber = blogPost.ArticleNumber,
                Title = "Blog Post Title",
                UrlPath = "my-blog/blog-post-title",
                BlogKey = "my-blog",
                ArticleType = (int)ArticleType.BlogPost,
                VersionNumber = 2,
                Content = "Version 2 content",
                UserId = TestUserId.ToString(),
                StatusCode = 0
            };
            
            Db.Articles.Add(version2);
            await Db.SaveChangesAsync();

            // Capture old values
            var oldTitle = blogPost.Title;
            var oldUrlPath = blogPost.UrlPath;
            
            // Change the blog post title
            blogPost.Title = "Updated Blog Post";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(blogPost, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - All versions should have updated URL path with blog key
            var expectedNewUrlPath = "my-blog/updated-blog-post";
            var allVersions = await Db.Articles
                .Where(a => a.ArticleNumber == blogPost.ArticleNumber)
                .OrderBy(a => a.VersionNumber)
                .ToListAsync();
            
            Assert.AreEqual(2, allVersions.Count);
            Assert.IsTrue(allVersions.All(v => v.Title == "Updated Blog Post"), 
                "All versions should have the updated title");
            Assert.IsTrue(allVersions.All(v => v.UrlPath == expectedNewUrlPath), 
                $"All versions should have the updated URL path '{expectedNewUrlPath}'");
            Assert.IsTrue(allVersions.All(v => v.BlogKey == "my-blog"), 
                "All versions should preserve the blog key");
        }

        #endregion

        #region Redirect Creation Tests - Requirement 4

        [TestMethod]
        public async Task HandleTitleChangeAsync_PublishedNonRootPage_CreatesRedirectFromOldToNewUrl()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("About Us", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);
            
            // Publish the article
            article.Published = Clock.UtcNow.AddDays(-1);
            await Db.SaveChangesAsync();
            
            var oldTitle = article.Title;
            var oldUrlPath = article.UrlPath; // "about-us"
            
            // Change the title
            article.Title = "Company Information";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - Verify article was updated
            var updatedArticle = await Db.Articles.FirstAsync(a => a.Id == article.Id);
            Assert.AreEqual("company-information", updatedArticle.UrlPath);

            // Assert - Verify redirect was created (Requirement 4)
            var redirect = await Db.Articles.FirstOrDefaultAsync(a => 
                a.StatusCode == (int)StatusCodeEnum.Redirect && 
                a.UrlPath == oldUrlPath);
            
            Assert.IsNotNull(redirect, "A redirect should have been created from the old URL path");
            Assert.AreEqual("about-us", redirect.UrlPath, "Redirect UrlPath should be the OLD URL");
            Assert.IsTrue(redirect.HeaderJavaScript.Contains("window.location.href"), 
                "Redirect should contain JavaScript redirect in content");
            Assert.IsTrue(redirect.HeaderJavaScript.Contains("company-information"), 
                "Redirect should point to the NEW URL in the redirect script");
        }

        [TestMethod]
        public async Task HandleTitleChangeAsync_UnpublishedNonRootPage_DoesNotCreateRedirect()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("About Us", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);
            
            // Leave article unpublished (Published is null)
            Assert.IsNull(article.Published, "Article should be unpublished");
            
            var oldTitle = article.Title;
            var oldUrlPath = article.UrlPath; // "about-us"
            
            // Change the title
            article.Title = "Company Information";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - Verify article was updated
            var updatedArticle = await Db.Articles.FirstAsync(a => a.Id == article.Id);
            Assert.AreEqual("company-information", updatedArticle.UrlPath);

            // Assert - Verify NO redirect was created (only published articles get redirects)
            var redirect = await Db.Articles.FirstOrDefaultAsync(a => 
                a.StatusCode == (int)StatusCodeEnum.Redirect && 
                a.UrlPath == oldUrlPath);
            
            Assert.IsNull(redirect, "No redirect should be created for unpublished articles");
        }

        [TestMethod]
        public async Task HandleTitleChangeAsync_PublishedRootPage_DoesNotCreateRedirect()
        {
            // Arrange
            var rootArticle = await Logic.CreateArticle("Home Page", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == rootArticle.ArticleNumber);
            
            // Publish the root article
            article.Published = Clock.UtcNow.AddDays(-1);
            await Db.SaveChangesAsync();
            
            Assert.AreEqual("root", article.UrlPath);
            
            var oldTitle = article.Title;
            var oldUrlPath = article.UrlPath;
            
            // Change the title
            article.Title = "Welcome Page";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - Verify article still has root URL path (Requirement 3)
            var updatedArticle = await Db.Articles.FirstAsync(a => a.Id == article.Id);
            Assert.AreEqual("root", updatedArticle.UrlPath);

            // Assert - Verify NO redirect was created (root pages don't get redirects)
            var redirectCount = await Db.Articles.CountAsync(a => 
                a.StatusCode == (int)StatusCodeEnum.Redirect && 
                a.UrlPath == "root");
            
            Assert.AreEqual(0, redirectCount, "No redirect should be created from root URL path");
        }

        [TestMethod]
        public async Task HandleTitleChangeAsync_PublishedPageWithVersions_CreatesRedirectAndUpdatesAllVersions()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("Services", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);
            
            // Publish the article
            article.Published = Clock.UtcNow.AddDays(-1);
            
            // Create additional versions
            var version2 = new Article
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Services",
                UrlPath = article.UrlPath,
                VersionNumber = 2,
                Content = "Version 2 content",
                UserId = TestUserId.ToString(),
                StatusCode = 0
            };
            
            Db.Articles.Add(version2);
            await Db.SaveChangesAsync();
            
            var oldTitle = article.Title;
            var oldUrlPath = article.UrlPath; // "services"
            
            // Change the title
            article.Title = "Our Services";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - All versions should have updated URL path
            var allVersions = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .ToListAsync();
            
            Assert.AreEqual(2, allVersions.Count);
            Assert.IsTrue(allVersions.All(v => v.UrlPath == "our-services"), 
                "All versions should have the new URL path");
            Assert.IsTrue(allVersions.All(v => v.Title == "Our Services"), 
                "All versions should have the new title");

            // Assert - Verify redirect was created with old URL → new URL (Requirement 4)
            var redirect = await Db.Articles.FirstOrDefaultAsync(a => 
                a.StatusCode == (int)StatusCodeEnum.Redirect && 
                a.UrlPath == "services");
            
            Assert.IsNotNull(redirect, "A redirect should have been created");
            Assert.IsTrue(redirect.HeaderJavaScript.Contains("our-services"), 
                "Redirect should point to the new URL");
        }

        #endregion

        #region Case Sensitivity Tests - Requirement 5

        [TestMethod]
        public async Task HandleTitleChangeAsync_CaseOnlyChange_UpdatesTitleButPreservesUrlPath()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("About Us", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);
            
            var oldTitle = article.Title; // "About Us"
            var oldUrlPath = article.UrlPath; // "about-us"
            
            // Change only the case of the title (Requirement 5)
            article.Title = "ABOUT US";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert
            var updatedArticle = await Db.Articles.FirstAsync(a => a.Id == article.Id);
            Assert.AreEqual("ABOUT US", updatedArticle.Title, "Title should be updated with new case (titles are case sensitive)");
            Assert.AreEqual("about-us", updatedArticle.UrlPath, "URL path should remain unchanged (URLs/slugs are case insensitive)");
        }

        [TestMethod]
        public async Task HandleTitleChangeAsync_CaseOnlyChangeWithVersions_UpdatesAllVersionTitlesButNotUrls()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("Contact Us", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);
            
            // Create additional versions
            var version2 = new Article
            {
                ArticleNumber = article.ArticleNumber,
                Title = "Contact Us",
                UrlPath = article.UrlPath,
                VersionNumber = 2,
                Content = "Version 2 content",
                UserId = TestUserId.ToString(),
                StatusCode = 0
            };
            
            Db.Articles.Add(version2);
            await Db.SaveChangesAsync();
            
            var oldTitle = article.Title; // "Contact Us"
            var oldUrlPath = article.UrlPath; // "contact-us"
            
            // Change only the case (Requirement 5)
            article.Title = "CONTACT US";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - All versions should have new case-sensitive title but same URL
            var allVersions = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .OrderBy(a => a.VersionNumber)
                .ToListAsync();
            
            Assert.AreEqual(2, allVersions.Count);
            Assert.IsTrue(allVersions.All(v => v.Title == "CONTACT US"), 
                "All versions should have the updated title with new case (titles are case sensitive)");
            Assert.IsTrue(allVersions.All(v => v.UrlPath == "contact-us"), 
                "All versions should preserve the same URL path (URLs/slugs are case insensitive)");
        }

        [TestMethod]
        public async Task HandleTitleChangeAsync_CaseOnlyChangePublished_DoesNotCreateRedirect()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("Privacy Policy", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);
            
            // Publish the article
            article.Published = Clock.UtcNow.AddDays(-1);
            await Db.SaveChangesAsync();
            
            var oldTitle = article.Title; // "Privacy Policy"
            var oldUrlPath = article.UrlPath; // "privacy-policy"
            
            // Change only the case (Requirement 5)
            article.Title = "PRIVACY POLICY";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - Title updated, URL unchanged
            var updatedArticle = await Db.Articles.FirstAsync(a => a.Id == article.Id);
            Assert.AreEqual("PRIVACY POLICY", updatedArticle.Title, "Title case should be updated");
            Assert.AreEqual("privacy-policy", updatedArticle.UrlPath, "URL should remain lowercase");

            // Assert - No redirect should be created (URL didn't actually change) (Requirement 5)
            var redirectCount = await Db.Articles.CountAsync(a => 
                a.StatusCode == (int)StatusCodeEnum.Redirect);
            
            Assert.AreEqual(0, redirectCount, 
                "No redirect should be created when only title case changes (slug/URL remains the same)");
        }

        [TestMethod]
        public async Task HandleTitleChangeAsync_MixedCaseTitle_UrlNormalizedToLowercase()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("products", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);
            
            Assert.AreEqual("products", article.UrlPath, "Initial URL should be lowercase");
            
            var oldTitle = article.Title; // "products"
            var oldUrlPath = article.UrlPath; // "products"
            
            // Change to mixed case title that normalizes to same slug (Requirement 5)
            article.Title = "Products"; // Different case but same normalized slug

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert
            var updatedArticle = await Db.Articles.FirstAsync(a => a.Id == article.Id);
            Assert.AreEqual("Products", updatedArticle.Title, "Title should have new case");
            Assert.AreEqual("products", updatedArticle.UrlPath, "URL should remain lowercase (normalized/case insensitive)");
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public async Task HandleTitleChangeAsync_SlugConflict_ThrowsInvalidOperationException()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var article1 = await Logic.CreateArticle("Original Article", TestUserId);
            var article2 = await Logic.CreateArticle("Another Article", TestUserId);

            var articleToChange = await Db.Articles.FirstAsync(a => a.ArticleNumber == article2.ArticleNumber);
            var conflictingSlug = "original-article";

            var oldTitle = articleToChange.Title;
            var oldUrlPath = articleToChange.UrlPath;

            // Change article2's title to conflict with article1's slug
            articleToChange.Title = "Original Article";

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await TitleChangeService.HandleTitleChangeAsync(articleToChange, oldTitle, oldUrlPath));

            Assert.IsTrue(exception.Message.Contains("original-article"));
            Assert.IsTrue(exception.Message.Contains("already in use"));
        }

        [TestMethod]
        public async Task CreateRedirectsAsync_InvalidUserId_ThrowsArgumentException()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("Test Article", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);

            // Set invalid user ID
            article.UserId = "not-a-valid-guid";
            var oldTitle = article.Title;
            var oldUrlPath = article.UrlPath;

            // Publish the article so redirect would be created
            article.Published = Clock.UtcNow.AddDays(-1);
            article.Title = "Changed Title";
            await Db.SaveChangesAsync();

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                async () => await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath));

            Assert.IsTrue(exception.Message.Contains("not a valid GUID"));
        }

        #endregion

        #region Blog Stream Tests

        [TestMethod]
        public async Task HandleTitleChangeAsync_BlogStreamWithMultipleEntries_UpdatesAllEntries()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first

            // Create a blog stream
            var blogStreamResult = await Logic.CreateArticle("My Blog", TestUserId);
            var blogStream = await Db.Articles.FirstAsync(a => a.ArticleNumber == blogStreamResult.ArticleNumber);
            blogStream.ArticleType = (int)ArticleType.BlogStream;
            blogStream.BlogKey = "my-blog";
            await Db.SaveChangesAsync();

            // Create multiple blog posts
            var post1Result = await Logic.CreateArticle("First Post", TestUserId);
            var post1 = await Db.Articles.FirstAsync(a => a.ArticleNumber == post1Result.ArticleNumber);
            post1.ArticleType = (int)ArticleType.BlogPost;
            post1.BlogKey = "my-blog";
            post1.UrlPath = "my-blog/first-post";

            var post2Result = await Logic.CreateArticle("Second Post", TestUserId);
            var post2 = await Db.Articles.FirstAsync(a => a.ArticleNumber == post2Result.ArticleNumber);
            post2.ArticleType = (int)ArticleType.BlogPost;
            post2.BlogKey = "my-blog";
            post2.UrlPath = "my-blog/second-post";

            var post3Result = await Logic.CreateArticle("Third Post", TestUserId);
            var post3 = await Db.Articles.FirstAsync(a => a.ArticleNumber == post3Result.ArticleNumber);
            post3.ArticleType = (int)ArticleType.BlogPost;
            post3.BlogKey = "my-blog";
            post3.UrlPath = "my-blog/third-post";

            await Db.SaveChangesAsync();

            // Capture old values
            var oldTitle = blogStream.Title;
            var oldUrlPath = blogStream.UrlPath;

            // Change the blog stream title
            blogStream.Title = "Tech Blog";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(blogStream, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - All blog posts should have updated blog key and URL paths
            var updatedPosts = await Db.Articles
                .Where(a => a.BlogKey == "tech-blog" && a.ArticleType == (int)ArticleType.BlogPost)
                .ToListAsync();

            Assert.AreEqual(3, updatedPosts.Count, "All 3 blog posts should be updated");
            Assert.IsTrue(updatedPosts.Any(p => p.UrlPath == "tech-blog/first-post"));
            Assert.IsTrue(updatedPosts.Any(p => p.UrlPath == "tech-blog/second-post"));
            Assert.IsTrue(updatedPosts.Any(p => p.UrlPath == "tech-blog/third-post"));

            // Verify no posts remain with old blog key
            var oldPosts = await Db.Articles
                .Where(a => a.BlogKey == "my-blog" && a.ArticleType == (int)ArticleType.BlogPost)
                .CountAsync();
            Assert.AreEqual(0, oldPosts, "No posts should remain with old blog key");
        }

        [TestMethod]
        public async Task HandleTitleChangeAsync_BlogStreamWithVersions_CascadesToAllPostVersions()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first

            // Create a blog stream
            var blogStreamResult = await Logic.CreateArticle("My Blog", TestUserId);
            var blogStream = await Db.Articles.FirstAsync(a => a.ArticleNumber == blogStreamResult.ArticleNumber);
            blogStream.ArticleType = (int)ArticleType.BlogStream;
            blogStream.BlogKey = "my-blog";
            await Db.SaveChangesAsync();

            // Create a blog post with multiple versions
            var postResult = await Logic.CreateArticle("Blog Post", TestUserId);
            var post = await Db.Articles.FirstAsync(a => a.ArticleNumber == postResult.ArticleNumber);
            post.ArticleType = (int)ArticleType.BlogPost;
            post.BlogKey = "my-blog";
            post.UrlPath = "my-blog/blog-post";

            var postVersion2 = new Article
            {
                ArticleNumber = post.ArticleNumber,
                Title = "Blog Post",
                UrlPath = "my-blog/blog-post",
                BlogKey = "my-blog",
                ArticleType = (int)ArticleType.BlogPost,
                VersionNumber = 2,
                Content = "Version 2",
                UserId = TestUserId.ToString(),
                StatusCode = 0
            };

            Db.Articles.Add(postVersion2);
            await Db.SaveChangesAsync();

            // Capture old values
            var oldTitle = blogStream.Title;
            var oldUrlPath = blogStream.UrlPath;

            // Change the blog stream title
            blogStream.Title = "Tech Blog";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(blogStream, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - All versions of the blog post should be updated
            var allPostVersions = await Db.Articles
                .Where(a => a.ArticleNumber == post.ArticleNumber)
                .ToListAsync();

            Assert.AreEqual(2, allPostVersions.Count);
            Assert.IsTrue(allPostVersions.All(v => v.BlogKey == "tech-blog"),
                "All versions should have updated blog key");
            Assert.IsTrue(allPostVersions.All(v => v.UrlPath == "tech-blog/blog-post"),
                "All versions should have updated URL path");
        }

        #endregion

        #region Hierarchical/Nested Article Tests

        [TestMethod]
        public async Task HandleTitleChangeAsync_DeepNestedChildren_CascadesCorrectly()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first

            // Create a hierarchy: parent/child/grandchild
            var parentResult = await Logic.CreateArticle("Parent", TestUserId);
            var parent = await Db.Articles.FirstAsync(a => a.ArticleNumber == parentResult.ArticleNumber);

            var childResult = await Logic.CreateArticle("parent/child", TestUserId);
            var child = await Db.Articles.FirstAsync(a => a.ArticleNumber == childResult.ArticleNumber);

            var grandchildResult = await Logic.CreateArticle("parent/child/grandchild", TestUserId);
            var grandchild = await Db.Articles.FirstAsync(a => a.ArticleNumber == grandchildResult.ArticleNumber);

            var greatGrandchildResult = await Logic.CreateArticle("parent/child/grandchild/great", TestUserId);
            var greatGrandchild = await Db.Articles.FirstAsync(a => a.ArticleNumber == greatGrandchildResult.ArticleNumber);

            // Capture old values
            var oldTitle = parent.Title;
            var oldUrlPath = parent.UrlPath;

            // Change the parent title
            parent.Title = "New Parent";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(parent, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - All descendants should have updated URL paths
            var updatedParent = await Db.Articles.FirstAsync(a => a.Id == parent.Id);
            var updatedChild = await Db.Articles.FirstAsync(a => a.Id == child.Id);
            var updatedGrandchild = await Db.Articles.FirstAsync(a => a.Id == grandchild.Id);
            var updatedGreatGrandchild = await Db.Articles.FirstAsync(a => a.Id == greatGrandchild.Id);

            Assert.AreEqual("new-parent", updatedParent.UrlPath);
            Assert.AreEqual("new-parent/child", updatedChild.UrlPath);
            Assert.AreEqual("new-parent/child/grandchild", updatedGrandchild.UrlPath);
            Assert.AreEqual("new-parent/child/grandchild/great", updatedGreatGrandchild.UrlPath);
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public async Task HandleTitleChangeAsync_SameTitle_NoUnnecessaryChanges()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("Test Article", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);

            // Publish the article
            article.Published = Clock.UtcNow.AddDays(-1);
            await Db.SaveChangesAsync();

            var oldTitle = article.Title;
            var oldUrlPath = article.UrlPath;
            var originalUpdated = article.Updated;

            // Set the same title (no actual change)
            article.Title = "Test Article";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - No redirect should be created for same slug
            var redirectCount = await Db.Articles.CountAsync(a =>
                a.StatusCode == (int)StatusCodeEnum.Redirect &&
                a.UrlPath == oldUrlPath);

            Assert.AreEqual(0, redirectCount, "No redirect should be created when slug doesn't change");
        }

        [TestMethod]
        public async Task HandleTitleChangeAsync_SpecialCharactersInTitle_NormalizesCorrectly()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("Normal Title", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);

            var oldTitle = article.Title;
            var oldUrlPath = article.UrlPath;

            // Change to title with special characters
            article.Title = "Special & Title: With @ Symbols!";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - URL should be normalized (actual normalization depends on your SlugService)
            var updatedArticle = await Db.Articles.FirstAsync(a => a.Id == article.Id);
            Assert.AreEqual("Special & Title: With @ Symbols!", updatedArticle.Title, "Title should preserve special characters");
            // The UrlPath assertion depends on your SlugService implementation
            Assert.IsFalse(updatedArticle.UrlPath.Contains("&"));
            Assert.IsFalse(updatedArticle.UrlPath.Contains("@"));
            Assert.IsFalse(updatedArticle.UrlPath.Contains("!"));
        }

        #endregion

        #region Concurrency/Batching Tests

        [TestMethod]
        public async Task HandleTitleChangeAsync_Exactly20Versions_BatchSavesCorrectly()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("Test Article", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);

            // Create exactly 20 additional versions (21 total including original)
            for (int i = 2; i <= 21; i++)
            {
                var version = new Article
                {
                    ArticleNumber = article.ArticleNumber,
                    Title = "Test Article",
                    UrlPath = article.UrlPath,
                    VersionNumber = i,
                    Content = $"Version {i}",
                    UserId = TestUserId.ToString(),
                    StatusCode = 0
                };
                Db.Articles.Add(version);
            }
            await Db.SaveChangesAsync();

            var oldTitle = article.Title;
            var oldUrlPath = article.UrlPath;

            // Change the title
            article.Title = "Updated Article";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - All 21 versions should have updated title
            var allVersions = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .ToListAsync();

            Assert.AreEqual(21, allVersions.Count);
            Assert.IsTrue(allVersions.All(v => v.Title == "Updated Article"),
                "All 21 versions should have the updated title");
            Assert.IsTrue(allVersions.All(v => v.UrlPath == "updated-article"),
                "All 21 versions should have the updated URL path");
        }

        [TestMethod]
        public async Task HandleTitleChangeAsync_MoreThan40Versions_AllUpdatedCorrectly()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("Test Article", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);

            // Create 42 additional versions (43 total) to test multiple batches
            for (int i = 2; i <= 43; i++)
            {
                var version = new Article
                {
                    ArticleNumber = article.ArticleNumber,
                    Title = "Test Article",
                    UrlPath = article.UrlPath,
                    VersionNumber = i,
                    Content = $"Version {i}",
                    UserId = TestUserId.ToString(),
                    StatusCode = 0
                };
                Db.Articles.Add(version);
            }
            await Db.SaveChangesAsync();

            var oldTitle = article.Title;
            var oldUrlPath = article.UrlPath;

            // Change the title
            article.Title = "Updated Article";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - All 43 versions should have updated title (tests batching across 3 batches: 20, 20, 3)
            var allVersions = await Db.Articles
                .Where(a => a.ArticleNumber == article.ArticleNumber)
                .ToListAsync();

            Assert.AreEqual(43, allVersions.Count);
            Assert.IsTrue(allVersions.All(v => v.Title == "Updated Article"),
                "All 43 versions should have the updated title");
        }

        #endregion

        #region Publishing Integration Tests

        [TestMethod]
        public async Task HandleTitleChangeAsync_FuturePublishedDate_DoesNotRepublish()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            var articleResult = await Logic.CreateArticle("Test Article", TestUserId);
            var article = await Db.Articles.FirstAsync(a => a.ArticleNumber == articleResult.ArticleNumber);

            // Set a future publish date
            article.Published = Clock.UtcNow.AddDays(7);
            await Db.SaveChangesAsync();

            var oldTitle = article.Title;
            var oldUrlPath = article.UrlPath;

            // Change the title
            article.Title = "Updated Article";

            // Act
            await TitleChangeService.HandleTitleChangeAsync(article, oldTitle, oldUrlPath);
            await Db.SaveChangesAsync();

            // Assert - Title updated but no redirect created (not yet published)
            var updatedArticle = await Db.Articles.FirstAsync(a => a.Id == article.Id);
            Assert.AreEqual("Updated Article", updatedArticle.Title);
            Assert.AreEqual("updated-article", updatedArticle.UrlPath);

            // No redirect should be created for future-published articles
            var redirectCount = await Db.Articles.CountAsync(a =>
                a.StatusCode == (int)StatusCodeEnum.Redirect);
            Assert.AreEqual(0, redirectCount);
        }

        #endregion

        #region Reserved Paths Tests

        [TestMethod]
        public async Task ValidateTitle_ReservedPath_ReturnsFalse()
        {
            // Arrange
            // Assuming "admin" is a reserved path - adjust based on your actual reserved paths
            var title = "admin";

            // Act
            var isValid = await TitleChangeService.ValidateTitle(title, null);

            // Assert
            Assert.IsFalse(isValid, "Reserved path 'admin' should not be valid as a title");
        }

        [TestMethod]
        public async Task ValidateTitle_EmptyOrWhitespace_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(await TitleChangeService.ValidateTitle("", null));
            Assert.IsFalse(await TitleChangeService.ValidateTitle("   ", null));
            Assert.IsFalse(await TitleChangeService.ValidateTitle(null, null));
        }

        [TestMethod]
        public async Task ValidateTitle_DuplicateTitle_ReturnsFalse()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first
            await Logic.CreateArticle("Existing Article", TestUserId);

            // Act
            var isValid = await TitleChangeService.ValidateTitle("Existing Article", null);

            // Assert
            Assert.IsFalse(isValid, "Duplicate title should not be valid");
        }

        [TestMethod]
        public async Task ValidateTitle_ValidTitle_ReturnsTrue()
        {
            // Arrange
            await Logic.CreateArticle("Home Page", TestUserId); // Create root first

            // Act
            var isValid = await TitleChangeService.ValidateTitle("New Unique Article", null);

            // Assert
            Assert.IsTrue(isValid, "Unique, non-reserved title should be valid");
        }

        #endregion
    }
}
