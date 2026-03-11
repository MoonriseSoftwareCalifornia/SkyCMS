// <copyright file="ArticleScheduler.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Scheduling
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AspNetCore.Identity.FlexDb;
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.DynamicConfig;
    using Cosmos.EmailServices;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Sky.Editor.Infrastructure.Time;
    using Sky.Editor.Services.EditorSettings;
    using Sky.Editor.Services.Setup;

    /// <inheritdoc/>
    /// <remarks>
    /// TODO: Enforce that when a content creator uses calendar date/time scheduling,
    /// they can only publish in the future to prevent conflicts with currently published versions.
    /// This validation should be added in the UI/controller layer before saving.
    /// </remarks>
    public class ArticleScheduler : IArticleScheduler
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<ArticleScheduler> logger;
        private readonly IEditorSettings settings;
        private readonly IClock clock;
        private readonly IDynamicConfigurationProvider configurationProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArticleScheduler"/> class.
        /// </summary>
        /// <param name="clock">Clock abstraction for testable time.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="settings">Editor settings.</param>
        /// <param name="serviceProvider">Service provider for creating scoped dependencies.</param>
        public ArticleScheduler(
            ILogger<ArticleScheduler> logger,
            IEditorSettings settings,
            IClock clock,
            IServiceProvider serviceProvider)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            if (settings.IsMultiTenantEditor)
            {
                configurationProvider = serviceProvider.GetRequiredService<IDynamicConfigurationProvider>();
            }
            else
            {
                configurationProvider = null;
            }
        }

        /// <inheritdoc/>
        public async Task ExecuteAsync()
        {
            var now = clock.UtcNow;
            // logger.LogInformation("ArticleScheduler: Starting scheduled execution at {ExecutionTime}", now);

            if (!settings.IsMultiTenantEditor)
            {
                try
                {
                    await RunForTenant(string.Empty);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "ArticleScheduler: Error processing scheduler.");
                }

                return;
            }

            var domainNames = await configurationProvider.GetAllDomainNamesAsync();
            foreach (var domainName in domainNames)
            {
                try
                {
                    await RunForTenant(domainName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "ArticleScheduler: Error processing tenant {Domain}", domainName);
                }
            }
        }

        private async Task RunForTenant(string domainName)
        {
            // Create a new scope for each tenant to ensure proper dependency isolation
            using var scope = serviceProvider.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var memoryCache = scopedServices.GetRequiredService<IMemoryCache>();

            try
            {
                // These services need to be scoped to a particular domainName.
                if (settings.IsMultiTenantEditor)
                {
                    // All services must be scoped to the tenant's connection
                    var connection = await configurationProvider.GetTenantConnectionAsync(domainName);

                    if (connection == null)
                    {
                        logger.LogWarning("No connection configuration found for tenant {Domain}", domainName);
                        return;
                    }

                    using (var dbContext = new ApplicationDbContext(connection.DbConn))
                    {
                        var storageContext = new StorageContext(connection.StorageConn, memoryCache);

                        await Run(dbContext, storageContext, domainName, scopedServices);
                    }
                }
                else
                {
                    // Single-tenant mode: Try to resolve DbContext from DI first (for tests),
                    // otherwise create manually to avoid DI scope issues with Hangfire
                    var dbContextFromDI = scopedServices.GetService<ApplicationDbContext>();
                    
                    if (dbContextFromDI != null)
                    {
                        // Use the DbContext from DI (typically in test scenarios with in-memory database)
                        var storageContext = scopedServices.GetRequiredService<IStorageContext>();
                        await Run(dbContextFromDI, storageContext, domainName, scopedServices);
                    }
                    else
                    {
                        // Create DbContext manually (production/Hangfire scenarios)
                        // Hangfire worker threads don't have HTTP context, so we can't use scoped DI resolution
                        var configuration = scopedServices.GetRequiredService<IConfiguration>();
                        var connectionString = configuration.GetConnectionString("ApplicationDbContextConnection");

                        // Create ApplicationDbContext directly instead of resolving from DI (which requires HTTP context)
                        using (var dbContext = new ApplicationDbContext(
                            CosmosDbOptionsBuilder.GetDbOptions<ApplicationDbContext>(connectionString)))
                        {
                            // Get the storage connection string and create StorageContext manually
                            var storageConnectionString = configuration.GetConnectionString("StorageConnectionString") 
                                ?? configuration.GetValue<string>("StorageConnectionString");
                            var storageContext = new StorageContext(storageConnectionString, memoryCache);

                            await Run(dbContext, storageContext, domainName, scopedServices);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ArticleScheduler: Error during scheduled execution for domain {Domain}", domainName);
                throw;
            }
        }

        private async Task Run(ApplicationDbContext dbContext, IStorageContext storageContext, string domainName, IServiceProvider scopedServices)
        {
            var now = clock.UtcNow;

            // Note: EF Core for Cosmos DB does not support grouping and counting directly in the database for
            // this scenario, so we retrieve the article numbers first and then filter in-memory.
            // Find all article numbers that have 2+ versions with non-null Published dates
            var articleNumbers = await dbContext.Articles
                .Where(a => a.Published != null
                            && a.Published <= now
                            && a.StatusCode != (int)Cosmos.Common.Data.Logic.StatusCodeEnum.Deleted)
                .Select(a => a.ArticleNumber)
                .ToListAsync();

            // Step 2: Filter in-memory for multiples
            var articlesWithMultiplePublishedVersions = articleNumbers
                .GroupBy(n => n)
                .Where(g => g.Count() >= 2)
                .Select(g => new { ArticleNumber = g.Key, Count = g.Count() })
                .ToList();

            foreach (var art in articlesWithMultiplePublishedVersions)
            {
                await ProcessArticleVersions(now, dbContext, storageContext, art.ArticleNumber, domainName, scopedServices);
            }

            // logger.LogInformation("ArticleScheduler: Completed execution for domain {Domain} at {CompletionTime}", domainName, clock.UtcNow);
        }

        /// <summary>
        /// Processes all versions of a specific article, activating the most recent non-future version.
        /// </summary>
        /// <param name="now">Current UTC timestamp.</param>
        /// <param name="dbContext">The database context.</param>
        /// <param name="storageContext">The storage context.</param>
        /// <param name="articleNumber">Article number.</param>
        /// <param name="domainName">Domain name for logging.</param>
        /// <param name="scopedServices">Scoped service provider.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ProcessArticleVersions(
            DateTimeOffset now,
            ApplicationDbContext dbContext,
            IStorageContext storageContext,
            int articleNumber,
            string domainName,
            IServiceProvider scopedServices)
        {
            try
            {
                var versions = await dbContext.Articles
                    .Where(a => a.ArticleNumber == articleNumber
                    && a.Published != null && a.Published <= now
                    && a.StatusCode != (int)Cosmos.Common.Data.Logic.StatusCodeEnum.Deleted)
                    .OrderByDescending(a => a.Published)
                    .ThenByDescending(a => a.VersionNumber)
                    .ToListAsync();

                if (versions.Count < 2)
                {
                    return;
                }

                var activeVersion = versions.FirstOrDefault();
                if (activeVersion == null)
                {
                    logger.LogDebug("Article {ArticleNumber}: All versions are scheduled for future publication", articleNumber);
                    return;
                }

                logger.LogInformation(
                    "Article {ArticleNumber} (Domain: {Domain}): Activating version {VersionNumber} (Published: {PublishedDate})",
                    articleNumber,
                    domainName,
                    activeVersion.VersionNumber,
                    activeVersion.Published);

                // ✅ Publish active version FIRST
                var factory = scopedServices.GetRequiredService<ITenantArticleLogicFactory>();
                var articleLogic = await factory.CreateForTenantAsync(domainName);
                await articleLogic.PublishArticle(activeVersion.Id, activeVersion.Published);

                // ✅ THEN unpublish old versions (after successful publication)
                var oldVersions = versions.Where(v =>
                    v.Id != activeVersion.Id &&
                    (v.Published < activeVersion.Published ||
                     (v.Published == activeVersion.Published && v.VersionNumber < activeVersion.VersionNumber))).ToList();

                foreach (var oldVersion in oldVersions)
                {
                    oldVersion.Published = null;
                }

                if (oldVersions.Any())
                {
                    await dbContext.SaveChangesAsync();
                }

                // Send email notification to the author
                await SendPublicationNotificationAsync(activeVersion, domainName, scopedServices, dbContext);

                logger.LogInformation(
                    "Article {ArticleNumber} (Domain: {Domain}): Successfully activated version {VersionNumber}",
                    articleNumber,
                    domainName,
                    activeVersion.VersionNumber);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error processing article versions for ArticleNumber {ArticleNumber} (Domain: {Domain})",
                    articleNumber,
                    domainName);
            }
        }

        /// <summary>
        /// Sends an email notification to the article author when their scheduled post goes live.
        /// </summary>
        /// <param name="article">The published article.</param>
        /// <param name="domainName">The domain name or site identifier.</param>
        /// <param name="scopedServices">Scoped service provider for resolving dependencies.</param>
        /// <param name="dbContext">Database context for user lookup.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SendPublicationNotificationAsync(
            Article article,
            string domainName,
            IServiceProvider scopedServices,
            ApplicationDbContext dbContext)
        {
            try
            {
                var emailSender = scopedServices.GetService<ICosmosEmailSender>();
                if (emailSender == null)
                {
                    logger.LogWarning("Email sender service not available. Skipping notification for article {ArticleNumber}", article.ArticleNumber);
                    return;
                }

                var userManager = scopedServices.GetService<UserManager<IdentityUser>>();
                if (userManager == null)
                {
                    logger.LogWarning("UserManager not available. Skipping notification for article {ArticleNumber}", article.ArticleNumber);
                    return;
                }

                // Get the author's email address
                var author = await userManager.FindByIdAsync(article.UserId);
                if (author == null || string.IsNullOrWhiteSpace(author.Email))
                {
                    logger.LogWarning("Author not found or has no email for article {ArticleNumber}", article.ArticleNumber);
                    return;
                }

                // Get the website name from the home page or use domain name as fallback
                var homePage = await dbContext.Pages
                    .Select(s => new { s.Title, s.UrlPath })
                    .FirstOrDefaultAsync(f => f.UrlPath == "root");
                var websiteName = homePage?.Title ?? domainName ?? "your website";

                // Construct the article URL
                var articleUrl = string.IsNullOrWhiteSpace(domainName)
                    ? $"/{article.UrlPath}"
                    : $"https://{domainName}/{article.UrlPath}";

                // Build the email content
                var subject = $"Your scheduled article \"{article.Title}\" is now published";
                var htmlMessage = $@"
<html>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <h2 style='color: #2c3e50;'>Your Scheduled Article is Now Live!</h2>
    <p>Hello,</p>
    <p>Your scheduled article has been successfully published on <strong>{websiteName}</strong>.</p>
    
    <div style='background-color: #f8f9fa; padding: 15px; border-left: 4px solid #007bff; margin: 20px 0;'>
        <h3 style='margin-top: 0; color: #007bff;'>{article.Title}</h3>
        <p><strong>Article Number:</strong> {article.ArticleNumber}</p>
        <p><strong>Version:</strong> {article.VersionNumber}</p>
        <p><strong>Published:</strong> {article.Published:F}</p>
        <p><strong>URL Path:</strong> {article.UrlPath}</p>
    </div>
    
    <p>You can view your published article here:</p>
    <p><a href='{articleUrl}' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>View Article</a></p>
    
    <hr style='margin: 30px 0; border: none; border-top: 1px solid #ddd;' />
    <p style='font-size: 12px; color: #666;'>
        This is an automated notification from {websiteName}.<br/>
        Publication Date: {article.Published:F} UTC
    </p>
</body>
</html>";

                await emailSender.SendEmailAsync(author.Email, subject, htmlMessage);

                logger.LogInformation(
                    "Sent publication notification to {Email} for article {ArticleNumber} (Title: {Title})",
                    author.Email,
                    article.ArticleNumber,
                    article.Title);
            }
            catch (Exception ex)
            {
                // Don't throw - email failure shouldn't stop the publication process
                logger.LogError(
                    ex,
                    "Failed to send publication notification for article {ArticleNumber}",
                    article.ArticleNumber);
            }
        }
    }
}
