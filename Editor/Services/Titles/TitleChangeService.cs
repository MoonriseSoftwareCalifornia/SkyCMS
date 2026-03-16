// <copyright file="TitleChangeService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Titles
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Services.BlogPublishing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Domain.Events;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.Authors;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.Redirects;
    using Sky.Editor.Services.ReservedPaths;
    using Sky.Editor.Services.Slugs;

    /// <summary>
    /// Coordinates updates required when an article title changes: slug normalization, child URL adjustments,
    /// redirect creation for published articles, version synchronization, and domain event emission.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service orchestrates the complex side effects of changing an article's title, ensuring
    /// consistency across URLs, slugs, redirects, and related content (especially blog streams and posts).
    /// </para>
    /// <para>
    /// When a title changes, the service:
    /// </para>
    /// <list type="number">
    ///   <item><description>Normalizes the new title into a URL-safe slug</description></item>
    ///   <item><description>Updates the article's <see cref="Article.UrlPath"/> and <see cref="Article.BlogKey"/></description></item>
    ///   <item><description>For blog streams, cascades the change to all associated blog posts</description></item>
    ///   <item><description>Synchronizes all article versions to use the new slug</description></item>
    ///   <item><description>Creates redirects from old URLs to new URLs for published content</description></item>
    ///   <item><description>Republishes affected articles if they were previously published</description></item>
    ///   <item><description>Dispatches domain events to notify subscribers of the change</description></item>
    /// </list>
    /// <para>
    /// The service maintains referential integrity by ensuring that blog posts always reference
    /// their parent blog stream's current slug as their blog key.
    /// </para>
    /// </remarks>
    public sealed class TitleChangeService : ITitleChangeService
    {
        private readonly ITitleChangeContext context;
        private readonly ISlugService slugs;
        private readonly IRedirectService redirects;
        private readonly IPublishingService publishingService;
        private readonly IReservedPaths reservedPaths;
        private readonly IBlogStreamRenderingService blogStreamRenderingService;
        private readonly ILogger<TitleChangeService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TitleChangeService"/> class.
        /// </summary>
        /// <param name="context">Title change context providing database, clock, and event context.Dispatcher.</param>
        /// <param name="slugs">Slug normalization service for converting titles to URL-safe segments.</param>
        /// <param name="redirects">Redirect management service for creating permanent redirects from old to new URLs.</param>
        /// <param name="publishingService">Publishing service for regenerating static content after title changes.</param>
        /// <param name="reservedPaths">Reserved paths service for validating that new titles don't conflict with system routes.</param>
        /// <param name="blogStreamRenderingService">Blog stream rendering service for regenerating blog stream HTML content with client-side orchestration.</param>
        /// <param name="logger">Logger for diagnostic and error events.</param>
        public TitleChangeService(
            ITitleChangeContext context,
            ISlugService slugs,
            IRedirectService redirects,
            IPublishingService publishingService,
            IReservedPaths reservedPaths,
            IBlogStreamRenderingService blogStreamRenderingService,
            ILogger<TitleChangeService> logger)
        {
            this.context = context;
            this.slugs = slugs;
            this.redirects = redirects;
            this.publishingService = publishingService;
            this.reservedPaths = reservedPaths;
            this.blogStreamRenderingService = blogStreamRenderingService;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public string BuildArticleUrl(Article article)
        {
            if (article.ArticleType == (int)ArticleType.BlogPost)
            {
                return slugs.Normalize(article.Title, article.BlogKey);
            }

            return slugs.Normalize(article.Title);
        }

        /// <inheritdoc/>
        public async Task HandleTitleChangeAsync(Article article, string oldTitle, string oldUrlPath)
        {
            // Use a database transaction to ensure atomicity (if supported by the database provider)
            // If any critical operation fails, all changes will be rolled back
            // Note: In-memory databases may not support transactions
            var transaction = await context.Database.Database.BeginTransactionAsync();
            
            try
            {
                // Use a list to track URL changes with published status for this operation
                var changedUrls = new List<UrlChange>();

                var oldSlug = oldUrlPath; // The old URL path is already normalized
                var newSlug = BuildArticleUrl(article);

                // ✅ FIX: Capture the published status BEFORE UpdateVersionsAsync (which may modify it via EF Core tracking)
                var wasPublished = article.Published.HasValue && article.Published <= context.Clock.UtcNow;

                // **PRESERVE ROOT PAGE URL PATH**
                // The root page must always remain at "root" regardless of title changes
                var isRootPage = article.UrlPath.Equals("root", StringComparison.OrdinalIgnoreCase);

                if (isRootPage)
                {
                    logger.LogInformation(
                        "Title change for root page (article {ArticleNumber}) from '{OldTitle}' to '{NewTitle}'. UrlPath will remain 'root'.",
                        article.ArticleNumber,
                        oldTitle,
                        article.Title);

                    // Update versions but don't change URL paths for root page
                    await UpdateVersionsForRootPageAsync(article);

                    // Save the main article entity changes (title, etc.)
                    await context.Database.SaveChangesAsync();

                    // Republish if the article is currently published
                    if (wasPublished)
                    {
                        await publishingService.PublishAsync(article);
                    }

                    // Commit transaction before dispatching events
                    await transaction.CommitAsync();

                    // ✅ FIX: Dispatch event with the actual old title, not the URL path
                    await context.Dispatcher.DispatchAsync(new TitleChangedEvent(article.ArticleNumber, oldTitle, article.Title));

                    return;
                }

                // Validate that the new slug doesn't conflict with existing articles
                var slugConflict = await context.Database.Articles
                    .AnyAsync(a =>
                        a.ArticleNumber != article.ArticleNumber &&
                        a.UrlPath == newSlug &&
                        a.StatusCode != (int)StatusCodeEnum.Deleted);

                if (slugConflict)
                {
                    logger.LogWarning(
                        "Title change for article {ArticleNumber} would create slug conflict with existing article. Old: {OldSlug}, New: {NewSlug}",
                        article.ArticleNumber,
                        oldSlug,
                        newSlug);
                    throw new InvalidOperationException($"The slug '{newSlug}' is already in use by another article.");
                }

                // Track the URL change for redirect creation with published status
                changedUrls.Add(new UrlChange
                {
                    OldUrl = oldSlug,
                    NewUrl = newSlug,
                    IsPublished = wasPublished,
                    ArticleNumber = article.ArticleNumber
                });

                // Update the article's URL path and blog key (for blog posts and blog streams)
                article.UrlPath = newSlug;

                // If this is a blog stream, the blog key must match the UrlPath (new slug).
                if (article.ArticleType == (int)ArticleType.BlogStream)
                {
                    article.BlogKey = newSlug;
                }

                // If this is a blog stream, cascade changes to all associated blog posts
                if (article.ArticleType == (int)ArticleType.BlogStream)
                {
                    await HandleBlogStreamEntriesAsync(article, oldSlug, newSlug, oldTitle, changedUrls);  // ✅ Pass oldTitle
                }
                else if (article.ArticleType == (int)ArticleType.General)
                {
                    await HandleTitleChangesForChildren(article, oldSlug, newSlug, changedUrls);
                }

                // Synchronize all versions of this article
                await UpdateVersionsAsync(article, newSlug, oldSlug);

                // Save the main article entity changes (title, URL, etc.)
                await context.Database.SaveChangesAsync();

                // Republish if the article is currently published
                if (wasPublished)
                {
                    await publishingService.PublishAsync(article);
                }

                // Only proceed if the slug actually changed
                RedirectCreationResult redirectResult = null;
                if (!oldSlug.Equals(newSlug, StringComparison.OrdinalIgnoreCase))
                {
                    // Create redirects only for published articles
                    // Filter to only URL changes where the article is published
                    var publishedChanges = changedUrls.Where(c => c.IsPublished).ToList();

                    if (publishedChanges.Any())
                    {
                        redirectResult = await CreateRedirectsAsync(publishedChanges, article.UserId);

                        // Check if redirect creation had critical failures
                        if (redirectResult.FailedRedirects.Any())
                        {
                            // Log warnings but don't fail the entire operation
                            // Redirects are important but not critical enough to rollback article changes
                            logger.LogWarning(
                                "Some redirects failed to create for article {ArticleNumber}: {FailureCount} failed out of {TotalCount}",
                                article.ArticleNumber,
                                redirectResult.FailedRedirects.Count,
                                redirectResult.TotalAttempted);
                        }
                    }
                }

                // Commit the transaction - all database changes are now permanent
                await transaction.CommitAsync();

                logger.LogInformation(
                    "Title change transaction committed successfully for article {ArticleNumber}",
                    article.ArticleNumber);

                // Log redirect creation summary after successful commit
                if (redirectResult != null)
                {
                    logger.LogInformation(
                        "Redirect creation completed for article {ArticleNumber}: {SuccessCount} created, {FailureCount} failed, {SkippedCount} skipped",
                        article.ArticleNumber,
                        redirectResult.SuccessCount,
                        redirectResult.FailedRedirects.Count,
                        redirectResult.SkippedCount);

                    // Log details of failed redirects
                    if (redirectResult.FailedRedirects.Any())
                    {
                        foreach (var (articleNumber, oldUrl, newUrl, error) in redirectResult.FailedRedirects)
                        {
                            logger.LogWarning(
                                "Failed redirect for article {ArticleNumber}: '{OldUrl}' -> '{NewUrl}': {Error}",
                                articleNumber,
                                oldUrl,
                                newUrl,
                                error);
                        }
                    }
                }

                // ✅ FIX: Dispatch event with the actual old title AFTER successful commit
                await context.Dispatcher.DispatchAsync(new TitleChangedEvent(article.ArticleNumber, oldTitle, article.Title));
            }
            catch (Exception ex)
            {
                // Rollback transaction on any failure
                await transaction.RollbackAsync();

                logger.LogError(
                    ex,
                    "Title change transaction rolled back for article {ArticleNumber} due to error. Old title: '{OldTitle}', New title: '{NewTitle}'",
                    article.ArticleNumber,
                    oldTitle,
                    article.Title);

                // Re-throw to let the caller handle the error
                throw;
            }
            finally
            {
                await transaction.DisposeAsync();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateTitle(string title, int? articleNumber)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            var normalizedTitle = title.Trim();
            
            // Generate the URL slug that would be used for this title
            var slug = slugs.Normalize(normalizedTitle);

            // Check against reserved paths (system routes that cannot be used for articles)
            // Reserved paths are URL patterns, so we compare against the generated slug
            var paths = (await reservedPaths.GetReservedPaths()).Select(s => s.Path.ToLower()).ToArray();
            foreach (var reservedPath in paths)
            {
                if (reservedPath.EndsWith('*'))
                {
                    // Wildcard reserved path - check if slug starts with the prefix
                    var value = reservedPath.TrimEnd('*').TrimEnd('/');
                    if (slug.StartsWith(value + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                else if (slug.Equals(reservedPath, StringComparison.OrdinalIgnoreCase))
                {
                    // Exact match reserved path
                    return false;
                }
            }

            // Check for title conflicts with other existing articles
            Article existingArticle = articleNumber.HasValue
                ? await context.Database.Articles.FirstOrDefaultAsync(a =>
                    a.ArticleNumber != articleNumber &&
                    a.Title.ToLower() == normalizedTitle.ToLower() &&
                    a.StatusCode != (int)StatusCodeEnum.Deleted)
                : await context.Database.Articles.FirstOrDefaultAsync(a =>
                    a.Title.ToLower() == normalizedTitle.ToLower() &&
                    a.StatusCode != (int)StatusCodeEnum.Deleted);

            return existingArticle == null;
        }

        /// <summary>
        /// Handles title changes for blog stream articles by updating the blog key, regenerating blog entry URLs,
        /// creating redirects for published entries, and re-rendering the blog stream content.
        /// </summary>
        /// <param name="blogStreamArticle">The blog stream article whose title has changed. Must be of type <see cref="ArticleType.BlogStream"/>.</param>
        /// <param name="oldBlogKey">The previous normalized slug of the blog stream, derived from the old title.</param>
        /// <param name="newBlogKey">The new normalized slug of the blog stream, derived from the new title.</param>
        /// <param name="oldTitle">The previous title of the blog stream before the change.</param>
        /// <param name="changedUrls">The dictionary tracking URL changes for redirect creation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// When a blog stream's title changes, its slug (used as the <see cref="Article.BlogKey"/>) also changes.
        /// This affects all blog posts associated with that stream, as they reference the stream's slug in their URLs.
        /// </para>
        /// <para>
        /// This method performs the following operations:
        /// </para>
        /// <list type="number">
        ///   <item><description>Queries all blog posts (<see cref="ArticleType.BlogPost"/>) associated with the old blog key</description></item>
        ///   <item><description>For each blog post:
        ///     <list type="bullet">
        ///       <item><description>Updates the <see cref="Article.BlogKey"/> to reference the new blog stream slug</description></item>
        ///       <item><description>Recalculates the <see cref="Article.UrlPath"/> using the new blog key</description></item>
        ///       <item><description>Synchronizes all versions of the blog post</description></item>
        ///       <item><description>If published, tracks the URL change for redirect creation</description></item>
        ///       <item><description>If published, republishes the post at its new URL</description></item>
        ///     </list>
        ///   </description></item>
        ///   <item><description>Regenerates the blog stream's HTML content to reflect the new structure</description></item>
        ///   <item><description>Persists all changes to the database</description></item>
        ///   <item><description>Dispatches a <see cref="TitleChangedEvent"/> for the blog stream</description></item>
        /// </list>
        /// <para>
        /// This ensures that the entire blog hierarchy remains consistent and accessible at the correct URLs
        /// after a blog stream title change.
        /// </para>
        /// </remarks>
        private async Task HandleBlogStreamEntriesAsync(
            Article blogStreamArticle,
            string oldBlogKey,
            string newBlogKey,
            string oldTitle,  // ✅ ADD: New parameter for the actual old title
            List<UrlChange> changedUrls)
        {
            // Find all blog posts associated with the old blog key
            var blogEntries = await context.Database.Articles
                .Where(a => a.BlogKey == oldBlogKey && a.ArticleType == (int)ArticleType.BlogPost)
                .ToListAsync();

            // Update each blog entry to use the new blog key and recalculated URL
            foreach (var entry in blogEntries)
            {
                var oldPath = entry.UrlPath;
                var newPath = slugs.Normalize(entry.Title, newBlogKey);

                entry.BlogKey = newBlogKey;
                entry.UrlPath = newPath;

                // Track URL change if paths differ, with published status for this specific blog entry
                if (!oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
                {
                    var isEntryPublished = entry.Published.HasValue && entry.Published <= context.Clock.UtcNow;
                    changedUrls.Add(new UrlChange
                    {
                        OldUrl = oldPath,
                        NewUrl = newPath,
                        IsPublished = isEntryPublished,
                        ArticleNumber = entry.ArticleNumber
                    });
                }

                // Synchronize all versions of this blog post
                await UpdateVersionsAsync(entry, newBlogKey, oldPath);

                // If published, republish at new URL
                if (entry.Published != null && entry.Published <= context.Clock.UtcNow)
                {
                    await publishingService.PublishAsync(entry);
                }
            }

            // Save all blog entry changes in a single transaction
            await context.Database.SaveChangesAsync();

            // Regenerate the blog stream's HTML content with updated links using modern client-side architecture
            var generatedHtml = await blogStreamRenderingService.GenerateBlogStreamWrapperAsync(blogStreamArticle, newBlogKey);

            if (string.IsNullOrEmpty(generatedHtml))
            {
                logger.LogWarning(
                    "Blog rendering service returned null or empty HTML for blog stream article {ArticleNumber}",
                    blogStreamArticle.ArticleNumber);
            }
            else
            {
                blogStreamArticle.Content = generatedHtml;
            }

            await context.Database.SaveChangesAsync();

            // ✅ FIX: Use the actual old title, not the old slug
            await context.Dispatcher.DispatchAsync(new TitleChangedEvent(
                blogStreamArticle.ArticleNumber,
                oldTitle,  // ✅ Changed from oldBlogKey to oldTitle
                blogStreamArticle.Title));
        }

        /// <summary>
        /// Updates all versions of an article to reflect a new title and slug, and URL path.
        /// </summary>
        /// <param name="article">The article whose versions are to be updated. Versions are identified by matching <see cref="Article.ArticleNumber"/>.</param>
        /// <param name="newSlug">The new slug to assign to all versions of the article.</param>
        /// <param name="oldSlug">The old slug for logging purposes.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// Articles can have multiple versions (drafts, historical versions) identified by the same <see cref="Article.ArticleNumber"/>.
        /// When a title (and thus slug) changes, all versions must be synchronized to maintain consistency.
        /// </para>
        /// <para>
        /// This method:
        /// </para>
        /// <list type="number">
        ///   <item><description>Queries all versions with the same <see cref="Article.ArticleNumber"/> (excluding the current article instance)</description></item>
        ///   <item><description>Updates each version's <see cref="Article.BlogKey"/> (for blog posts only) and <see cref="Article.UrlPath"/></description></item>
        ///   <item><description>Republishes any versions that are currently published (published timestamp is in the past)</description></item>
        ///   <item><description>Persists changes in batches of 20 for performance optimization</description></item>
        /// </list>
        /// <para>
        /// Batching ensures that large numbers of versions don't cause transaction timeouts or memory issues.
        /// </para>
        /// </remarks>
        private async Task UpdateVersionsAsync(Article article, string newSlug, string oldSlug)
        {
            var articleNumber = article.ArticleNumber;
            var id = article.Id;

            // Find all other versions of this article
            var versions = await context.Database.Articles
                .Where(av => av.ArticleNumber == articleNumber && av.Id != id)
                .ToListAsync();

            if (!versions.Any())
            {
                return;
            }

            logger.LogInformation(
                "Updating {Count} versions for article {ArticleNumber} from slug '{OldSlug}' to '{NewSlug}'",
                versions.Count,
                articleNumber,
                oldSlug,
                newSlug);

            var count = 0;
            foreach (var version in versions)
            {
                version.Title = article.Title;

                // Only update BlogKey for BlogPost and BlogStream articles
                if (version.ArticleType == (int)ArticleType.BlogPost || version.ArticleType == (int)ArticleType.BlogStream)
                {
                    version.BlogKey = article.BlogKey;  // Use article's BlogKey, not newSlug
                }

                // Always update the URL path using the BuildArticleUrl logic
                // UNLESS it's a root page version (preserve "root")
                if (version.UrlPath.Equals("root", StringComparison.OrdinalIgnoreCase))
                {
                    version.UrlPath = "root";
                }
                else
                {
                    version.UrlPath = version.ArticleType == (int)ArticleType.BlogPost
                       ? slugs.Normalize(version.Title, article.BlogKey)  // Use article.BlogKey
                       : slugs.Normalize(version.Title);
                }

                // Republish if this version is currently published
                if (version.Published.HasValue && version.Published <= context.Clock.UtcNow)
                {
                    await context.Database.SaveChangesAsync();
                    count = 0;
                    await publishingService.PublishAsync(version);
                }

                // Batch save every 20 records to optimize performance
                if (++count >= 20)
                {
                    await context.Database.SaveChangesAsync();
                    count = 0;
                }
            }

            // Save any remaining changes
            if (count > 0)
            {
                await context.Database.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Creates redirect articles for all URL changes accumulated during a title change operation.
        /// </summary>
        /// <param name="userId">The string identifier of the user initiating the title change, used for audit tracking in redirect records.</param>
        /// <returns>A <see cref="RedirectCreationResult"/> containing details about the redirect creation operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="userId"/> is not a valid GUID format.</exception>
        /// <remarks>
        /// <para>
        /// This method iterates through all URL changes collected during a title change operation
        /// and creates or updates redirect articles for each mapping. This ensures that visitors
        /// following old links (from bookmarks, search engines, etc.) are automatically redirected
        /// to the new URL.
        /// </para>
        /// <para>
        /// Redirects are permanent (301) and are implemented via the <see cref="IRedirectService"/>,
        /// which creates special redirect-type articles in the database.
        /// </para>
        /// <para>
        /// The <paramref name="userId"/> is validated and converted to a <see cref="Guid"/> before
        /// being passed to the redirect service. If the conversion fails, an <see cref="ArgumentException"/> is thrown.
        /// </para>
        /// </remarks>
        private async Task<RedirectCreationResult> CreateRedirectsAsync(List<UrlChange> urlChanges, string userId)
        {
            var result = new RedirectCreationResult();

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty when creating redirects.", nameof(userId));
            }

            // Validate and parse the user ID to a GUID
            if (!Guid.TryParse(userId, out var userGuid))
            {
                throw new ArgumentException($"User ID '{userId}' is not a valid GUID format.", nameof(userId));
            }

            // Group by old URL to handle potential duplicates (last one wins)
            var groupedChanges = urlChanges
                .GroupBy(c => c.OldUrl, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToList();

            if (groupedChanges.Count < urlChanges.Count)
            {
                result.SkippedCount = urlChanges.Count - groupedChanges.Count;
                logger.LogWarning(
                    "Detected {DuplicateCount} duplicate old URLs during redirect creation, keeping most recent targets",
                    result.SkippedCount);
            }

            foreach (var change in groupedChanges)
            {
                // Skip if old and new URLs are the same (shouldn't happen, but defensive)
                if (change.OldUrl.Equals(change.NewUrl, StringComparison.OrdinalIgnoreCase))
                {
                    result.SkippedCount++;
                    logger.LogDebug(
                        "Skipping redirect creation for article {ArticleNumber}: old and new URLs are identical ('{Url}')",
                        change.ArticleNumber,
                        change.OldUrl);
                    continue;
                }

                try
                {
                    // Resolve the final destination to avoid redirect chains
                    var finalDestination = await ResolveFinalDestinationAsync(change.NewUrl);

                    if (!finalDestination.Equals(change.NewUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInformation(
                            "Detected redirect chain for article {ArticleNumber}: '{OldUrl}' -> '{NewUrl}' -> '{FinalUrl}', redirecting directly to final destination",
                            change.ArticleNumber,
                            change.OldUrl,
                            change.NewUrl,
                            finalDestination);
                    }

                    await redirects.CreateOrUpdateRedirectAsync(change.OldUrl, finalDestination, userGuid);

                    logger.LogDebug(
                        "Created redirect for article {ArticleNumber}: '{OldUrl}' -> '{NewUrl}'",
                        change.ArticleNumber,
                        change.OldUrl,
                        finalDestination);

                    // Update any existing redirects that point TO the old URL to point to the final destination
                    // This prevents redirect chains when an article's title changes multiple times
                    var incomingRedirects = await context.Database.Articles
                        .Where(a => a.StatusCode == (int)StatusCodeEnum.Redirect &&
                                   a.RedirectTarget == change.OldUrl)
                        .ToListAsync();

                    if (incomingRedirects.Any())
                    {
                        logger.LogInformation(
                            "Updating {Count} incoming redirects that point to '{OldUrl}' to point to '{FinalUrl}'",
                            incomingRedirects.Count,
                            change.OldUrl,
                            finalDestination);

                        foreach (var incomingRedirect in incomingRedirects)
                        {
                            incomingRedirect.RedirectTarget = finalDestination;
                            logger.LogDebug(
                                "Updated redirect chain: '{Source}' -> '{OldTarget}' => '{Source}' -> '{NewTarget}'",
                                incomingRedirect.UrlPath,
                                change.OldUrl,
                                incomingRedirect.UrlPath,
                                finalDestination);
                        }

                        await context.Database.SaveChangesAsync();
                    }

                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to create redirect for article {ArticleNumber} from '{OldUrl}' to '{NewUrl}' for user {UserId}",
                        change.ArticleNumber,
                        change.OldUrl,
                        change.NewUrl,
                        userGuid);

                    // Record the failure for summary reporting (include article number)
                    result.FailedRedirects.Add((change.ArticleNumber, change.OldUrl, change.NewUrl, ex.Message));
                }
            }

            return result;
        }

        /// <summary>
        /// Handles title changes for general articles by recursively updating all descendant articles
        /// whose URL paths are prefixed by the old slug, adjusting their paths to use the new slug.
        /// </summary>
        /// <param name="article">The general article whose title has changed. Must be of type <see cref="ArticleType.General"/>.</param>
        /// <param name="oldSlug">The previous normalized slug of the article, derived from the old title.</param>
        /// <param name="newSlug">The new normalized slug of the article, derived from the new title.</param>
        /// <param name="changedUrls">The dictionary tracking URL changes for redirect creation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// When a general article's title (and thus slug) changes, any child articles whose URL paths
        /// start with the old slug must have their paths updated to reflect the new parent slug.
        /// This ensures hierarchical URL consistency across the content tree.
        /// </para>
        /// <para>
        /// This method performs the following operations:
        /// </para>
        /// <list type="number">
        ///   <item><description>Queries all articles whose <see cref="Article.UrlPath"/> starts with the old slug followed by "/"</description></item>
        ///   <item><description>For each descendant article:
        ///     <list type="bullet">
        ///       <item><description>Replaces the old slug prefix with the new slug in the <see cref="Article.UrlPath"/></description></item>
        ///       <item><description>Synchronizes all versions of the descendant article</description></item>
        ///       <item><description>If published, tracks the URL change for redirect creation</description></item>
        ///       <item><description>If published, republishes the article at its new URL</description></item>
        ///     </list>
        ///   </description></item>
        ///   <item><description>Persists all changes to the database in batches of 20 for performance optimization</description></item>
        /// </list>
        /// <para>
        /// This cascading update maintains referential integrity throughout the content hierarchy,
        /// ensuring that all child pages remain accessible at their correct relative URLs.
        /// </para>
        /// </remarks>
        private async Task HandleTitleChangesForChildren(Article article, string oldSlug, string newSlug, List<UrlChange> changedUrls)
        {
            // Find all articles that have the old slug as a parent (URL path starts with old slug)
            var childArticles = await context.Database.Articles
                .Where(a => a.UrlPath.StartsWith(oldSlug + "/") && a.StatusCode != (int)StatusCodeEnum.Deleted)
                .ToListAsync();

            if (!childArticles.Any())
            {
                return;
            }

            logger.LogInformation(
                "Updating {Count} child articles for parent article {ArticleNumber} from slug '{OldSlug}' to '{NewSlug}'",
                childArticles.Count,
                article.ArticleNumber,
                oldSlug,
                newSlug);

            var count = 0;
            foreach (var child in childArticles)
            {
                var oldChildPath = child.UrlPath;

                // Replace the old slug prefix with the new slug in the URL path
                // Note: Child article titles remain unchanged - only the URL path hierarchy is updated
                var newChildPath = newSlug + oldChildPath.Substring(oldSlug.Length);

                child.UrlPath = newChildPath;

                // Track URL change if paths differ, with published status for this specific child
                if (!oldChildPath.Equals(newChildPath, StringComparison.OrdinalIgnoreCase))
                {
                    var isChildPublished = child.Published.HasValue && child.Published <= context.Clock.UtcNow;
                    changedUrls.Add(new UrlChange
                    {
                        OldUrl = oldChildPath,
                        NewUrl = newChildPath,
                        IsPublished = isChildPublished,
                        ArticleNumber = child.ArticleNumber
                    });
                }

                // Synchronize all versions of this child article
                // UpdateVersionsAsync will update versions with the child's current title and new URL path
                await UpdateVersionsAsync(child, newChildPath, oldChildPath);

                // Republish if the child article is currently published
                if (child.Published != null && child.Published <= context.Clock.UtcNow)
                {
                    await context.Database.SaveChangesAsync();
                    count = 0;
                    await publishingService.PublishAsync(child);
                }

                // Batch save every 20 records to optimize performance
                if (++count >= 20)
                {
                    await context.Database.SaveChangesAsync();
                    count = 0;
                }
            }

            // Save any remaining changes
            if (count > 0)
            {
                await context.Database.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Updates all versions of the root page article, preserving the "root" URL path.
        /// </summary>
        /// <param name="article">The root page article whose title has changed.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// The root page is special - its URL path must always remain "root" regardless of title changes.
        /// This method ensures all versions maintain this constraint while allowing title updates.
        /// </remarks>
        private async Task UpdateVersionsForRootPageAsync(Article article)
        {
            var articleNumber = article.ArticleNumber;
            var id = article.Id;

            // Find all other versions of this article
            var versions = await context.Database.Articles
                .Where(av => av.ArticleNumber == articleNumber && av.Id != id)
                .ToListAsync();

            if (!versions.Any())
            {
                return;
            }

            logger.LogInformation(
                "Updating {Count} versions for root page article {ArticleNumber}, preserving 'root' URL path",
                versions.Count,
                articleNumber);

            var count = 0;
            foreach (var version in versions)
            {
                // Update title to match the current article
                version.Title = article.Title;

                // Ensure UrlPath remains "root" for all versions
                version.UrlPath = "root";

                // Republish if this version is currently published
                if (version.Published.HasValue && version.Published <= context.Clock.UtcNow)
                {
                    await context.Database.SaveChangesAsync();
                    count = 0;
                    await publishingService.PublishAsync(version);
                }

                // Batch save every 20 records to optimize performance
                if (++count >= 20)
                {
                    await context.Database.SaveChangesAsync();
                    count = 0;
                }
            }

            // Save any remaining changes
            if (count > 0)
            {
                await context.Database.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Resolves the final destination of a URL by following any existing redirect chains.
        /// </summary>
        /// <param name="targetUrl">The URL to resolve.</param>
        /// <returns>The final destination URL after following all redirects, or the original URL if no redirects exist.</returns>
        /// <remarks>
        /// <para>
        /// This method prevents redirect chains by checking if the target URL is itself a redirect,
        /// and if so, following the chain to the final destination. This ensures that new redirects
        /// always point to actual content, not to other redirects.
        /// </para>
        /// <para>
        /// The method includes protection against infinite loops by limiting the chain depth to 10 redirects.
        /// If a chain exceeds this limit, an error is logged and the original target URL is returned.
        /// </para>
        /// </remarks>
        private async Task<string> ResolveFinalDestinationAsync(string targetUrl)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = targetUrl;
            const int maxDepth = 10;

            while (visited.Add(current) && visited.Count <= maxDepth)
            {
                var redirect = await context.Database.Articles
                    .Where(a => a.UrlPath == current &&
                               a.StatusCode == (int)StatusCodeEnum.Redirect)
                    .FirstOrDefaultAsync();

                if (redirect == null || string.IsNullOrWhiteSpace(redirect.RedirectTarget))
                {
                    // Found final destination (not a redirect)
                    return current;
                }

                current = redirect.RedirectTarget;
            }

            if (visited.Count > maxDepth)
            {
                logger.LogError(
                    "Redirect chain exceeds maximum depth of {MaxDepth} for URL '{Url}'. Chain: {Chain}",
                    maxDepth,
                    targetUrl,
                    string.Join(" -> ", visited));
                return targetUrl; // Return original to prevent infinite loop
            }

            return current;
        }
    }
}
