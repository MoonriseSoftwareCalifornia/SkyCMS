// <copyright file="PostSetupInitializationService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Features.Articles.Create;
    using CommonMediator = Cosmos.Common.Features.Shared.IMediator;

    /// <summary>
    /// Background service that runs post-setup initialization tasks after application restart.
    /// </summary>
    public class PostSetupInitializationService : IHostedService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<PostSetupInitializationService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostSetupInitializationService"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider.</param>
        /// <param name="logger">Logger.</param>
        public PostSetupInitializationService(
            IServiceProvider serviceProvider,
            ILogger<PostSetupInitializationService> logger)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                // Check if this is multi-tenant mode
                var isMultiTenant = configuration.GetValue<bool?>("MultiTenantEditor") ?? false;

                if (isMultiTenant)
                {
                    logger.LogInformation("Multi-tenant mode detected. Post-setup initialization will be handled per-tenant on first request.");

                    // Don't process here - let middleware handle it per tenant
                    return;
                }

                // Single-tenant mode - process immediately
                logger.LogInformation("Single-tenant mode detected. Processing post-setup initialization...");

                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Check if home page creation is pending
                var pendingSetting = await dbContext.Settings
                    .FirstOrDefaultAsync(s => s.Group == "SETUP" && s.Name == "PendingHomePageCreation", cancellationToken);

                if (pendingSetting?.Value == "true")
                {
                    logger.LogInformation("Detected pending home page creation. Creating home page...");

                    var userIdSetting = await dbContext.Settings
                        .FirstOrDefaultAsync(s => s.Group == "SETUP" && s.Name == "HomePageUserId", cancellationToken);
                    var titleSetting = await dbContext.Settings
                        .FirstOrDefaultAsync(s => s.Group == "SETUP" && s.Name == "HomePageTitle", cancellationToken);
                    var templateIdSetting = await dbContext.Settings
                        .FirstOrDefaultAsync(s => s.Group == "SETUP" && s.Name == "HomePageTemplateId", cancellationToken);

                    if (userIdSetting != null && titleSetting != null && Guid.TryParse(userIdSetting.Value, out var userId))
                    {
                        // Check if home page already exists
                        var existingHomePage = await dbContext.Articles
                            .FirstOrDefaultAsync(a => a.ArticleNumber == 1 && a.UrlPath == "root", cancellationToken);

                        if (existingHomePage == null)
                        {
                            var mediator = scope.ServiceProvider.GetRequiredService<CommonMediator>();

                            Guid? templateId = null;
                            if (templateIdSetting != null && Guid.TryParse(templateIdSetting.Value, out var parsedTemplateId))
                            {
                                templateId = parsedTemplateId;
                            }

                            // REFACTORED: Use CreateArticleCommand instead of CreateArticle
                            var createCommand = new CreateArticleCommand
                            {
                                Title = titleSetting.Value,
                                TemplateId = templateId,
                                UserId = userId,
                                ArticleType = ArticleType.General,
                                BlogKey = string.Empty,

                                // Home page properties (auto-publish first article)
                                UrlPathOverride = "root",  // Force home page URL
                                StatusCode = StatusCodeEnum.Active
                            };

                            var result = await mediator.SendAsync(createCommand);

                            if (!result.IsSuccess)
                            {
                                var errorMessage = result.ErrorMessage ??
                                    string.Join(", ", result.Errors?.SelectMany(e => e.Value) ?? Array.Empty<string>());

                                logger.LogError("Failed to create home page: {Error}", errorMessage);
                            }
                            else
                            {
                                logger.LogInformation("Home page created successfully with article number {ArticleNumber}", result.Data.ArticleNumber);
                            }
                        }
                        else
                        {
                            logger.LogInformation("Home page already exists, skipping creation");
                        }

                        // Clear the pending flags
                        dbContext.Settings.Remove(pendingSetting);
                        if (userIdSetting != null)
                        {
                            dbContext.Settings.Remove(userIdSetting);
                        }

                        if (titleSetting != null)
                        {
                            dbContext.Settings.Remove(titleSetting);
                        }

                        if (templateIdSetting != null)
                        {
                            dbContext.Settings.Remove(templateIdSetting);
                        }

                        await dbContext.SaveChangesAsync(cancellationToken);

                        logger.LogInformation("Post-setup initialization completed successfully");
                    }
                    else
                    {
                        logger.LogWarning("Missing or invalid settings for home page creation");
                    }
                }
                else
                {
                    logger.LogInformation("No pending post-setup initialization tasks found");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to complete post-setup initialization");

                // Don't throw - this shouldn't prevent application startup
            }
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}