// <copyright file="MultiTenantSetupService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Cms.Common;
    using Cosmos.Cms.Data;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Features.Shared;
    using CommonMediator = Cosmos.Common.Features.Shared.IMediator;
    using Cosmos.DynamicConfig;
    using Cosmos.Editor.Services;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Articles.Create;
    using Sky.Editor.Services.Layouts;

    /// <summary>
    /// Multi-tenant setup service for post-provisioning tenant configuration.
    /// </summary>
    public class MultiTenantSetupService : IMultiTenantSetupService
    {
        private readonly ApplicationDbContext applicationDbContext;
        private readonly DynamicConfigDbContext configDbContext;
        private readonly UserManager<IdentityUser> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly ILayoutImportService layoutImportService;
        private readonly ArticleEditLogic articleEditLogic;
        private readonly CommonMediator mediator;
        private readonly IConfiguration configuration;
        private readonly ILogger<MultiTenantSetupService> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiTenantSetupService"/> class.
        /// </summary>
        public MultiTenantSetupService(
            ApplicationDbContext applicationDbContext,
            DynamicConfigDbContext configDbContext,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILayoutImportService layoutImportService,
            ArticleEditLogic articleEditLogic,
            CommonMediator mediator,
            IConfiguration configuration,
            ILogger<MultiTenantSetupService> logger)
        {
            this.applicationDbContext = applicationDbContext;
            this.configDbContext = configDbContext;
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.layoutImportService = layoutImportService;
            this.articleEditLogic = articleEditLogic;
            this.mediator = mediator;
            this.configuration = configuration;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async Task<bool> TenantRequiresSetupAsync()
        {
            try
            {
                // Check if tenant allows setup
                var allowSetup = configuration.GetValue<bool?>("CosmosAllowSetup") ?? false;
                if (!allowSetup)
                {
                    return false;
                }

                // Check if admin account exists
                var adminAccounts = await userManager.GetUsersInRoleAsync(RequiredIdentityRoles.Administrators);
                if (adminAccounts.Any())
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check tenant setup status");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<TenantSetupStatus> GetTenantSetupStatusAsync()
        {
            try
            {
                var status = new TenantSetupStatus
                {
                    SetupRequired = await TenantRequiresSetupAsync()
                };

                // Get tenant connection info
                var currentDomain = configuration.GetValue<string>("CurrentTenantDomain");
                if (!string.IsNullOrEmpty(currentDomain))
                {
                    var connection = await configDbContext.Connections
                        .FirstOrDefaultAsync(c => c.DomainNames.Contains(currentDomain));

                    if (connection != null)
                    {
                        status.WebsiteUrl = connection.WebsiteUrl;
                        status.OwnerEmail = connection.OwnerEmail ?? string.Empty;
                    }
                }

                // Check for admin account
                var adminAccounts = await userManager.GetUsersInRoleAsync(RequiredIdentityRoles.Administrators);
                status.HasAdminAccount = adminAccounts.Any();

                // Check for layout
                var defaultLayout = await applicationDbContext.Layouts
                    .FirstOrDefaultAsync(l => l.IsDefault);
                status.HasLayout = defaultLayout != null;

                // Check for home page
                var homePage = await applicationDbContext.Articles
                    .FirstOrDefaultAsync(a => a.ArticleNumber == 1 && a.UrlPath == "root");
                status.HasHomePage = homePage != null;

                return status;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get tenant setup status");
                return new TenantSetupStatus { SetupRequired = false };
            }
        }

        /// <inheritdoc/>
        public async Task<SetupCompletionResult> CreateTenantAdminAsync(string email, string password)
        {
            try
            {
                logger.LogInformation("Creating tenant administrator account for {Email}", email);

                // Create user
                var user = new IdentityUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, password);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger.LogWarning("Failed to create tenant admin account: {Errors}", errors);
                    return new SetupCompletionResult
                    {
                        Success = false,
                        Message = $"Failed to create admin account: {errors}"
                    };
                }

                // Add to Administrators role
                var addToRoleResult = await SetupNewAdministrator.Ensure_RolesAndAdmin_Exists(roleManager, userManager, user);

                if (!addToRoleResult)
                {
                    logger.LogWarning("Failed to assign tenant admin role");
                    return new SetupCompletionResult
                    {
                        Success = false,
                        Message = "Failed to assign admin role."
                    };
                }

                logger.LogInformation("Tenant admin user {Email} created successfully", email);

                return new SetupCompletionResult
                {
                    Success = true,
                    Message = "Admin account created successfully"
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create tenant admin account");
                return new SetupCompletionResult
                {
                    Success = false,
                    Message = $"Failed to create admin account: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<SetupCompletionResult> ImportLayoutAsync(string layoutId)
        {
            try
            {
                logger.LogInformation("Importing layout {LayoutId} for tenant", layoutId);

                var layout = await layoutImportService.GetCommunityLayoutAsync(layoutId, true);
                var communityPages = await layoutImportService.GetCommunityTemplatePagesAsync(layoutId);

                if (!await mediator.QueryAsync(new Cosmos.Common.Features.Layouts.Queries.CheckDefaultLayoutExistsQuery()))
                {
                    layout.Version = 1;
                    layout.IsDefault = true;
                }
                else
                {
                    layout.Version = await applicationDbContext.Layouts.CountAsync() + 1;
                    layout.IsDefault = false;
                }

                applicationDbContext.Layouts.Add(layout);
                await applicationDbContext.SaveChangesAsync();

                foreach (var page in communityPages)
                {
                    page.LayoutId = layout.Id;
                }

                applicationDbContext.Templates.AddRange(communityPages);
                await applicationDbContext.SaveChangesAsync();

                logger.LogInformation("Layout {LayoutId} imported successfully for tenant", layoutId);

                return new SetupCompletionResult
                {
                    Success = true,
                    Message = "Layout imported successfully"
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to import layout for tenant");
                return new SetupCompletionResult
                {
                    Success = false,
                    Message = $"Failed to import layout: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<SetupCompletionResult> CreateHomePageAsync(string title, Guid? templateId = null)
        {
            try
            {
                logger.LogInformation("Creating home page for tenant with title: {Title}", title);

                // Get the first admin user
                var adminAccounts = await userManager.GetUsersInRoleAsync(RequiredIdentityRoles.Administrators);
                if (!adminAccounts.Any())
                {
                    return new SetupCompletionResult
                    {
                        Success = false,
                        Message = "No administrator account found. Create an admin account first."
                    };
                }

                var adminUser = adminAccounts.First();

                // If no template specified, try to find "Home Page" template
                if (templateId == null)
                {
                    var template = await applicationDbContext.Templates
                        .FirstOrDefaultAsync(f => f.Title.ToLower() == "home page");
                    templateId = template?.Id;
                }

                // REFACTORED: Use CreateArticleCommand instead of CreateArticle + SaveArticle
                var createCommand = new CreateArticleCommand
                {
                    Title = title,
                    TemplateId = templateId,
                    UserId = Guid.Parse(adminUser.Id),
                    ArticleType = ArticleType.General,
                    BlogKey = string.Empty,

                    // Special home page properties
                    Published = DateTimeOffset.UtcNow,      // Auto-publish
                    StatusCode = StatusCodeEnum.Active,     // Active status
                    UrlPathOverride = "root"                // Home page must be "root"
                };

                var result = await mediator.SendAsync(createCommand);

                if (!result.IsSuccess)
                {
                    var errorMessage = result.ErrorMessage ?? 
                        string.Join(", ", result.Errors?.SelectMany(e => e.Value) ?? Array.Empty<string>());
                    
                    logger.LogError("Failed to create home page: {Error}", errorMessage);
                    
                    return new SetupCompletionResult
                    {
                        Success = false,
                        Message = $"Failed to create home page: {errorMessage}"
                    };
                }

                logger.LogInformation("Home page created successfully with article number {ArticleNumber}", result.Data.ArticleNumber);

                return new SetupCompletionResult
                {
                    Success = true,
                    Message = "Home page created successfully"
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create home page for tenant");
                return new SetupCompletionResult
                {
                    Success = false,
                    Message = $"Failed to create home page: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<SetupCompletionResult> CompleteTenantSetupAsync()
        {
            try
            {
                logger.LogInformation("Completing tenant setup");

                // Update AllowSetup to false in tenant's Settings table
                var setting = await applicationDbContext.Settings
                    .FirstOrDefaultAsync(s => s.Group == "SYSTEM" && s.Name == "AllowSetup");

                if (setting == null)
                {
                    setting = new Setting
                    {
                        Id = Guid.NewGuid(),
                        Group = "SYSTEM",
                        Name = "AllowSetup",
                        Value = "false",
                        Description = "Allow setup mode",
                        IsRequired = false
                    };
                    applicationDbContext.Settings.Add(setting);
                }
                else
                {
                    setting.Value = "false";
                }

                await applicationDbContext.SaveChangesAsync();

                // Also update in the Connection table
                var currentDomain = configuration.GetValue<string>("CurrentTenantDomain");
                if (!string.IsNullOrEmpty(currentDomain))
                {
                    var connection = await configDbContext.Connections
                        .FirstOrDefaultAsync(c => c.DomainNames.Contains(currentDomain));

                    if (connection != null)
                    {
                        connection.AllowSetup = false;
                        await configDbContext.SaveChangesAsync();
                        logger.LogInformation("Tenant setup completed and disabled for {Domain}", currentDomain);
                    }
                }

                return new SetupCompletionResult
                {
                    Success = true,
                    Message = "Tenant setup completed successfully"
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to complete tenant setup");
                return new SetupCompletionResult
                {
                    Success = false,
                    Message = $"Failed to complete setup: {ex.Message}"
                };
            }
        }
    }
}
