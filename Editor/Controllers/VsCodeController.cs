// <copyright file="VsCodeController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Models;
    using Cosmos.DynamicConfig;
    using MailChimp.Net.Models;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using MimeTypes;
    using Sky.Cms.Services;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Articles.GetEditable;
    using Sky.Editor.Features.Articles.Inventory;
    using Sky.Editor.Features.Layouts.GetEditable;
    using Sky.Editor.Features.Templates.Create;
    using Sky.Editor.Features.Templates.Get;
    using Sky.Editor.Services.Layouts;
    using Sky.Editor.Services.Publishing;
    using Sky.Editor.Services.Templates;

    /// <summary>
    /// API endpoints used by the SkyCMS VS Code extension.
    /// </summary>
    [ApiController]
    [Route("api/vscode")]
    public class VsCodeController : Controller
    {
        // Constants for magic strings
        private const string DefaultLayoutName = "Default Layout";
        private const string DefaultLayoutNotes = "Default layout created. Please customize using code editor.";

        private const string StateCachePrefix = "vscode:auth:state:";
        private const string CodeCachePrefix = "vscode:auth:code:";
        private const string TokenCachePrefix = "vscode:auth:token:";
        private const string PollCachePrefix = "vscode:auth:poll:";

        private static readonly TimeSpan BrowserStateLifetime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan OneTimeCodeLifetime = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan BearerTokenLifetime = TimeSpan.FromHours(8);

        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<VsCodeController> logger;
        private readonly IMemoryCache memoryCache;
        private readonly IStorageContext storageContext;
        private readonly ILayoutVersioningService layoutVersioningService;
        private readonly IDynamicConfigurationProvider configProvider;
        private readonly IMediator mediator;
        private readonly ITemplateService templateService;
        private readonly ArticleEditLogic articleLogic;
        private readonly IPublishingService publishingService;
        private readonly IPublicFileEntryTitleResolver titleResolver;
        private readonly IFolderListingService folderListingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="VsCodeController"/> class.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="memoryCache">Memory cache for one-time auth exchange.</param>
        /// <param name="storageContext">Storage context for file operations.</param>
        /// <param name="layoutVersioningService">Layout import service.</param>
        /// <param name="mediator">Mediator service.</param>
        /// <param name="templateService">Template service.</param>
        /// <param name="configProvider">Dynamic configuration provider for tenant settings.</param>
        /// <param name="articleLogic">Article edit logic for publish/unpublish operations.</param>
        /// <param name="publishingService">Publishing service for unpublish operations.</param>
        /// <param name="titleResolver">Shared file entry title resolver.</param>
        /// <param name="folderListingService">Shared folder-listing service.</param>
        public VsCodeController(
            ApplicationDbContext dbContext,
            ILogger<VsCodeController> logger,
            IMemoryCache memoryCache,
            IStorageContext storageContext,
            ILayoutVersioningService layoutVersioningService,
            IMediator mediator,
            ITemplateService templateService,
            IDynamicConfigurationProvider configProvider,
            ArticleEditLogic articleLogic,
            IPublishingService publishingService,
            IPublicFileEntryTitleResolver titleResolver,
            IFolderListingService folderListingService)
        {
            this.dbContext = dbContext;
            this.logger = logger;
            this.memoryCache = memoryCache;
            this.storageContext = storageContext;
            this.layoutVersioningService = layoutVersioningService;
            this.mediator = mediator;
            this.templateService = templateService;
            this.configProvider = configProvider;
            this.articleLogic = articleLogic;
            this.publishingService = publishingService;
            this.titleResolver = titleResolver;
            this.folderListingService = folderListingService;
        }

        /// <summary>
        /// Starts browser-based authentication flow for the VS Code extension.
        /// </summary>
        /// <returns>Bootstrap details for browser login.</returns>
        [AllowAnonymous]
        [HttpGet("auth/browser/start")]
        public IActionResult StartBrowserAuth()
        {
            var state = Guid.NewGuid().ToString("N");
            var callbackPath = $"/api/vscode/auth/browser/complete/{Uri.EscapeDataString(state)}";
            var callbackUrl = $"{Request.Scheme}://{Request.Host}{callbackPath}";
            var loginUrl = $"{Request.Scheme}://{Request.Host}/Identity/Account/Login?returnUrl={Uri.EscapeDataString(callbackPath)}";

            memoryCache.Set(
                StateCachePrefix + state,
                new BrowserStateCacheEntry { State = state },
                BrowserStateLifetime);

            logger.LogInformation("Started VS Code browser auth bootstrap.");

            return Ok(new
            {
                loginUrl,
                state,
                expiresInSeconds = (int)BrowserStateLifetime.TotalSeconds,
            });
        }

        /// <summary>
        /// Completes browser auth after the user logs in and displays a one-time code for the extension.
        /// </summary>
        /// <param name="state">Correlation state from auth start.</param>
        /// <returns>One-time code display page.</returns>
        [Authorize]
        [HttpGet("auth/browser/complete")]
        [HttpGet("auth/browser/complete/{state}")]
        public async Task<IActionResult> CompleteBrowserAuth(string? state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                var errorModel = new Sky.Cms.Models.VsCodeAuthViewModel
                {
                    ErrorMessage = "Missing auth state parameter. Please start sign-in again from VS Code.",
                    VsCodeCallbackUri = BuildVsCodeErrorUri("invalid_request", "Missing auth state parameter."),
                };
                return View("AuthFailed", errorModel);
            }

            if (!memoryCache.TryGetValue(StateCachePrefix + state, out BrowserStateCacheEntry? _))
            {
                var errorModel = new Sky.Cms.Models.VsCodeAuthViewModel
                {
                    ErrorMessage = "This sign-in request has expired. Please start sign-in again from VS Code.",
                    VsCodeCallbackUri = BuildVsCodeErrorUri("expired_request", "Sign-in request expired."),
                };
                return View("AuthFailed", errorModel);
            }

            var role = ResolveUserRole();
            if (role is null)
            {
                var errorModel = new Sky.Cms.Models.VsCodeAuthViewModel
                {
                    ErrorMessage = "Your account must have Editor or Administrator access to use the VS Code extension.",
                    VsCodeCallbackUri = BuildVsCodeErrorUri("access_denied", "Insufficient role."),
                };
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("AuthFailed", errorModel);
            }

            memoryCache.Remove(StateCachePrefix + state);

            // Resolve site metadata for enriching the extension tree node.
            var websiteTitle = await dbContext.Articles
                .Where(a => a.UrlPath == "root")
                .OrderByDescending(a => a.VersionNumber)
                .Select(a => a.Title)
                .FirstOrDefaultAsync() ?? string.Empty;

            var publicUrl = string.Empty;
            try
            {
                var domain = configProvider.GetTenantDomainNameFromRequest();
                var connection = await configProvider.GetTenantConnectionAsync(domain);
                publicUrl = connection?.WebsiteUrl ?? string.Empty;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not resolve tenant public URL during VS Code auth.");
            }

            var code = GenerateOneTimeCode();
            var username = User.Identity?.Name ?? string.Empty;
            var codeEntry = new OneTimeCodeCacheEntry
            {
                Code = code,
                State = state,
                Username = username,
                Role = role,
                DisplayName = username,
                WebsiteTitle = websiteTitle,
                PublicUrl = publicUrl,
            };

            memoryCache.Set(CodeCachePrefix + code, codeEntry, OneTimeCodeLifetime);

            // Write a second entry keyed by state so the poll endpoint can notify the
            // extension without the extension needing to know the code in advance.
            memoryCache.Set(PollCachePrefix + state, codeEntry, OneTimeCodeLifetime);

            var callbackUri = BuildVsCodeCallbackUri(code, state, websiteTitle, publicUrl);

            var model = new Sky.Cms.Models.VsCodeAuthViewModel
            {
                Code = code,
                State = state,
                VsCodeCallbackUri = callbackUri,
                WebsiteTitle = websiteTitle,
                PublicUrl = publicUrl,
            };

            return View(model);
        }

        /// <summary>
        /// Polls for auth completion. Called repeatedly by the VS Code extension while the
        /// user signs in. Returns <c>pending</c> until the browser completes sign-in, then
        /// returns <c>complete</c> with the one-time code so the extension can exchange it.
        /// </summary>
        /// <param name="state">Correlation state from auth start.</param>
        /// <returns>Poll status payload.</returns>
        [AllowAnonymous]
        [HttpGet("auth/poll")]
        public IActionResult PollBrowserAuth([FromQuery] string? state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return BadRequest(new { status = "error", message = "state is required." });
            }

            // If the state entry is still alive but no poll entry exists yet, the user
            // hasn't finished logging in yet.
            if (memoryCache.TryGetValue(PollCachePrefix + state, out OneTimeCodeCacheEntry? pollEntry) && pollEntry != null)
            {
                return Ok(new
                {
                    status = "complete",
                    code = pollEntry.Code,
                    websiteTitle = pollEntry.WebsiteTitle,
                    publicUrl = pollEntry.PublicUrl,
                });
            }

            // Check whether the state is still valid (not expired/consumed).
            if (!memoryCache.TryGetValue(StateCachePrefix + state, out BrowserStateCacheEntry? _))
            {
                return Ok(new { status = "expired" });
            }

            return Ok(new { status = "pending" });
        }

        /// <summary>
        /// Completes browser-based authentication flow for the VS Code extension.
        /// </summary>
        /// <param name="request">Auth exchange payload.</param>
        /// <returns>Exchange result.</returns>
        [AllowAnonymous]
        [HttpPost("auth/browser/exchange")]
        public IActionResult ExchangeBrowserAuth([FromBody] AuthExchangeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.State))
            {
                return BadRequest(new { message = "State and code are required." });
            }

            var code = request.Code.Trim().ToUpperInvariant();
            if (!memoryCache.TryGetValue(CodeCachePrefix + code, out OneTimeCodeCacheEntry? codeEntry) || codeEntry == null)
            {
                return Unauthorized(new { message = "Invalid or expired exchange code." });
            }

            if (!string.Equals(codeEntry.State, request.State, StringComparison.Ordinal))
            {
                return Unauthorized(new { message = "Code/state mismatch." });
            }

            memoryCache.Remove(CodeCachePrefix + code);

            var token = GenerateBearerToken();
            var tokenEntry = new BearerTokenCacheEntry
            {
                Token = token,
                Username = codeEntry.Username,
                Role = codeEntry.Role,
                DisplayName = codeEntry.DisplayName,
            };

            memoryCache.Set(TokenCachePrefix + token, tokenEntry, BearerTokenLifetime);
            logger.LogInformation("Issued VS Code extension bearer token for {Username}.", tokenEntry.Username);

            return Ok(new
            {
                token,
                role = tokenEntry.Role,
                displayName = tokenEntry.DisplayName,
                websiteTitle = codeEntry.WebsiteTitle,
                publicUrl = codeEntry.PublicUrl,
                expiresInSeconds = (int)BearerTokenLifetime.TotalSeconds,
            });
        }

        /// <summary>
        /// Invalidates the current VS Code auth session.
        /// </summary>
        /// <returns>Success.</returns>
        [HttpPost("auth/logout")]
        public IActionResult Logout()
        {
            var token = ExtractBearerToken();
            if (!string.IsNullOrWhiteSpace(token))
            {
                memoryCache.Remove(TokenCachePrefix + token);
            }

            return Ok();
        }

        /// <summary>
        /// Returns current user identity details for token validation.
        /// </summary>
        /// <returns>Identity payload.</returns>
        [HttpGet("auth/me")]
        public IActionResult Me()
        {
            if (TryGetBearerIdentity(out var bearerIdentity))
            {
                return Ok(new
                {
                    username = bearerIdentity!.Username,
                    displayName = bearerIdentity.DisplayName,
                    role = bearerIdentity.Role,
                });
            }

            var role = ResolveUserRole();
            if (User.Identity?.IsAuthenticated != true || role is null)
            {
                return Unauthorized();
            }

            var username = User.Identity?.Name ?? string.Empty;

            return Ok(new
            {
                username,
                displayName = username,
                role,
            });
        }

        /// <summary>
        /// Lists layout entities for tree rendering.
        /// </summary>
        /// <returns>Layout list.</returns>
        [HttpGet("layouts")]
        public async Task<IActionResult> GetLayouts()
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var layout = await GetLayoutForEdit();

            return Ok(new[]
            {
                new
                {
                    id = layout.Id,
                    layoutNumber = layout.LayoutNumber,
                    version = layout.Version ?? 0,
                    name = layout.LayoutName,
                    isDefault = layout.IsDefault,
                    isPublished = layout.Published.HasValue,
                    lastPublished = layout.Published?.UtcDateTime.ToString("o"),
                    published = layout.Published,
                    isEditable = !layout.Published.HasValue,
                    lastModified = layout.LastModified,
                },
            });
        }

        /// <summary>
        /// Lists all versions for a layout family, newest first.
        /// </summary>
        /// <param name="layoutNumber">Stable layout family number.</param>
        /// <returns>Version metadata for tree rendering.</returns>
        [HttpGet("layouts/{layoutNumber:int}/versions")]
        public async Task<IActionResult> GetLayoutVersions(int layoutNumber)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var family = await dbContext.Layouts
                .AsNoTracking()
                .Where(l => l.LayoutNumber == layoutNumber)
                .ToListAsync();

            if (family.Count == 0)
            {
                return NotFound();
            }

            var maxVersion = family.Max(l => l.Version ?? 0);

            var versions = family
                .OrderByDescending(l => l.Version ?? 0)
                .Select(l => new
                {
                    id = l.Id,
                    layoutNumber = l.LayoutNumber,
                    version = l.Version ?? 0,
                    name = l.LayoutName,
                    isDefault = l.IsDefault,
                    isPublished = l.Published.HasValue,
                    lastPublished = l.Published?.UtcDateTime.ToString("o"),
                    published = l.Published,
                    isEditable = !l.Published.HasValue && (l.Version ?? 0) == maxVersion,
                    lastModified = l.LastModified,
                })
                .ToList();

            return Ok(versions);
        }

        /// <summary>
        /// Lists templates for tree rendering.
        /// </summary>
        /// <returns>Template list.</returns>
        [HttpGet("templates")]
        public async Task<IActionResult> GetTemplates()
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            await templateService.EnsureDefaultTemplatesExistAsync();

            var templateEntities = await GetTemplatesForCurrentLayoutAsync();

            // Keep ordering/projection client-side for provider compatibility (including Cosmos).
            var templates = templateEntities
                .OrderBy(t => t.Title ?? string.Empty)
                .Select(t => new
                {
                    templateId = t.Id,
                    name = t.Title,
                    layoutNumber = t.LayoutNumber,
                })
                .ToList();

            return Ok(templates);
        }

        /// <summary>
        /// Lists editor inventory rows for articles, including nested blog children.
        /// </summary>
        /// <returns>Editor inventory rows.</returns>
        [HttpGet("articles")]
        public async Task<IActionResult> GetArticles()
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var inventory = await mediator.QueryAsync(new GetEditorInventoryQuery
            {
                PublishedOnly = false,
            });

            return Ok(inventory);
        }

        /// <summary>
        /// Lists blog streams (blogs) for tree rendering.
        /// </summary>
        /// <returns>Blog list ordered by title.</returns>
        [HttpGet("blogs")]
        public async Task<IActionResult> GetBlogs()
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var blogStreamType = (int)Cosmos.Cms.Common.ArticleType.BlogStream;
            var all = await dbContext.Articles
                .AsNoTracking()
                .Where(a => a.ArticleType == blogStreamType)
                .Select(a => new
                {
                    a.ArticleNumber,
                    a.VersionNumber,
                    a.Title,
                    a.BlogKey,
                })
                .ToListAsync();

            var latest = all
                .GroupBy(a => a.ArticleNumber)
                .Select(g => g.OrderByDescending(a => a.VersionNumber).First())
                .OrderBy(a => a.Title)
                .Select(a => new
                {
                    articleNumber = a.ArticleNumber,
                    name = a.Title,
                    blogKey = a.BlogKey,
                })
                .ToList();

            return Ok(latest);
        }

        /// <summary>
        /// Lists blog posts for a given blog key, sorted newest-first by publish date.
        /// </summary>
        /// <param name="blogKey">The blog key identifying the parent blog stream.</param>
        /// <returns>Blog post list.</returns>
        [HttpGet("blogs/{blogKey}/posts")]
        public async Task<IActionResult> GetBlogPosts(string blogKey)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            if (string.IsNullOrWhiteSpace(blogKey))
            {
                return BadRequest(new { message = "Blog key is required." });
            }

            var blogPostType = (int)Cosmos.Cms.Common.ArticleType.BlogPost;
            var all = await dbContext.Articles
                .AsNoTracking()
                .Where(a => a.BlogKey == blogKey && a.ArticleType == blogPostType)
                .Select(a => new
                {
                    a.Id,
                    a.ArticleNumber,
                    a.VersionNumber,
                    a.Title,
                    a.Published,
                })
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;
            var latest = all
                .GroupBy(a => a.ArticleNumber)
                .Select(g => g.OrderByDescending(a => a.VersionNumber).First())
                .OrderByDescending(a => a.Published ?? DateTimeOffset.MinValue)
                .Select(a => new
                {
                    id = a.Id,
                    articleNumber = a.ArticleNumber,
                    title = a.Title,
                    isPublished = a.Published.HasValue && a.Published <= now,
                })
                .ToList();

            return Ok(latest);
        }

        /// <summary>
        /// Gets a layout field payload.
        /// </summary>
        /// <param name="layoutNumber">Layout number identifier.</param>
        /// <param name="fieldKey">Field key.</param>
        /// <returns>Field payload.</returns>
        [HttpGet("layouts/{layoutNumber:int}/{fieldKey}")]
        public async Task<IActionResult> GetLayoutField(int layoutNumber, string fieldKey)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var layout = await GetEditableLayout(layoutNumber);
            if (layout == null)
            {
                return NotFound();
            }

            return fieldKey.ToLowerInvariant() switch
            {
                "layoutname" => Ok(new { value = layout.LayoutName }),
                "notes" => Ok(new { content = layout.Notes }),
                "head" => Ok(new { content = layout.Head }),
                "header" => Ok(new { content = layout.HtmlHeader }),
                "footer" => Ok(new { content = layout.FooterHtmlContent }),
                _ => NotFound(),
            };
        }

        /// <summary>
        /// Gets a layout field payload for a specific version (read-only history access).
        /// </summary>
        /// <param name="layoutNumber">Layout number identifier.</param>
        /// <param name="version">Layout version.</param>
        /// <param name="fieldKey">Field key.</param>
        /// <returns>Field payload.</returns>
        [HttpGet("layouts/{layoutNumber:int}/{version:int}/{fieldKey}")]
        public async Task<IActionResult> GetLayoutVersionField(int layoutNumber, int version, string fieldKey)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var layout = await dbContext.Layouts
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LayoutNumber == layoutNumber && (l.Version ?? 0) == version);

            if (layout == null)
            {
                return NotFound();
            }

            return fieldKey.ToLowerInvariant() switch
            {
                "layoutname" => Ok(new { value = layout.LayoutName }),
                "notes" => Ok(new { content = layout.Notes }),
                "head" => Ok(new { content = layout.Head }),
                "header" => Ok(new { content = layout.HtmlHeader }),
                "footer" => Ok(new { content = layout.FooterHtmlContent }),
                _ => NotFound(),
            };
        }

        /// <summary>
        /// Updates a layout field payload.
        /// </summary>
        /// <param name="layoutNumber">Layout number identifier.</param>
        /// <param name="fieldKey">Field key.</param>
        /// <param name="request">Payload body.</param>
        /// <returns>Success.</returns>
        [HttpPut("layouts/{layoutNumber:int}/{fieldKey}")]
        public async Task<IActionResult> SetLayoutField(int layoutNumber, string fieldKey, [FromBody] FieldUpdateRequest request)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var layout = await GetEditableLayout(layoutNumber);
            if (layout == null)
            {
                return NotFound();
            }

            switch (fieldKey.ToLowerInvariant())
            {
                case "layoutname":
                    layout.LayoutName = request.Value ?? string.Empty;
                    break;
                case "notes":
                    layout.Notes = request.Content ?? string.Empty;
                    break;
                case "head":
                    layout.Head = request.Content ?? string.Empty;
                    break;
                case "header":
                    layout.HtmlHeader = request.Content ?? string.Empty;
                    break;
                case "footer":
                    layout.FooterHtmlContent = request.Content ?? string.Empty;
                    break;
                default:
                    return NotFound();
            }

            layout.LastModified = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// Gets a template field payload.
        /// </summary>
        /// <param name="templateId">Template identifier.</param>
        /// <param name="fieldKey">Field key.</param>
        /// <returns>Field payload.</returns>
        [HttpGet("templates/{templateId:guid}/{fieldKey}")]
        public async Task<IActionResult> GetTemplateField(Guid templateId, string fieldKey)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var template = await GetTemplateAsync(templateId);
            if (template == null)
            {
                return NotFound();
            }

            return fieldKey.ToLowerInvariant() switch
            {
                "title" => Ok(new { value = template.Title }),
                "content" => Ok(new { content = template.Content }),
                "description" => Ok(new { content = template.Description }),
                _ => NotFound(),
            };
        }

        /// <summary>
        /// Updates a template field payload.
        /// </summary>
        /// <param name="templateId">Template identifier.</param>
        /// <param name="fieldKey">Field key.</param>
        /// <param name="request">Payload body.</param>
        /// <returns>Success.</returns>
        [HttpPut("templates/{templateId:guid}/{fieldKey}")]
        public async Task<IActionResult> SetTemplateField(Guid templateId, string fieldKey, [FromBody] FieldUpdateRequest request)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var template = await GetTemplateAsync(templateId);
            if (template == null)
            {
                return NotFound();
            }

            var trackedTemplate = dbContext.Templates.Local.FirstOrDefault(t => t.Id == templateId);
            if (trackedTemplate != null)
            {
                template = trackedTemplate;
            }
            else
            {
                dbContext.Attach(template);
            }

            switch (fieldKey.ToLowerInvariant())
            {
                case "title":
                    template.Title = request.Value ?? string.Empty;
                    dbContext.Entry(template).Property(t => t.Title).IsModified = true;
                    break;
                case "content":
                    template.Content = request.Content ?? string.Empty;
                    dbContext.Entry(template).Property(t => t.Content).IsModified = true;
                    break;
                case "description":
                    template.Description = request.Content ?? string.Empty;
                    dbContext.Entry(template).Property(t => t.Description).IsModified = true;
                    break;
                default:
                    return NotFound();
            }

            await dbContext.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// Creates a new template and its initial page-design version.
        /// </summary>
        /// <returns>Created template metadata.</returns>
        [HttpPost("templates")]
        public async Task<IActionResult> CreateTemplate()
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var defaultLayout = await GetCurrentLayoutAsync();

            var entity = new Cosmos.Common.Data.Template
            {
                Id = Guid.NewGuid(),
                Title = "New Template " + await dbContext.Templates.CountAsync(),
                Description = "<p>New template, please add descriptive and helpful information here.</p>",
                Content = "<p>" + LoremIpsum.SubSection1 + "</p>",
                LayoutId = defaultLayout?.Id,
                LayoutNumber = defaultLayout?.LayoutNumber ?? 0,
                CommunityLayoutId = defaultLayout?.CommunityLayoutId
            };

            if (!dbContext.Database.IsCosmos())
            {
                using (var transaction = await dbContext.Database.BeginTransactionAsync())
                {
                    try
                    {
                        dbContext.Templates.Add(entity);
                        await dbContext.SaveChangesAsync();

                        var createVersionCommand = new CreatePageDesignVersionCommand
                        {
                            TemplateId = entity.Id,
                            Title = entity.Title,
                            Description = entity.Description,
                            Content = entity.Content,
                            PageType = "template",
                            LayoutId = entity.LayoutId,
                            CommunityLayoutId = entity.CommunityLayoutId
                        };

                        var versionResult = await mediator.SendAsync(createVersionCommand);
                        if (!versionResult.IsSuccess)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new { message = $"Failed to create template version: {versionResult.ErrorMessage}" });
                        }

                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = $"Error creating template: {ex.Message}" });
                    }
                }
            }
            else
            {
                try
                {
                    dbContext.Templates.Add(entity);
                    await dbContext.SaveChangesAsync();

                    var createVersionCommand = new CreatePageDesignVersionCommand
                    {
                        TemplateId = entity.Id,
                        Title = entity.Title,
                        Description = entity.Description,
                        Content = entity.Content,
                        PageType = "template",
                        LayoutId = entity.LayoutId,
                        CommunityLayoutId = entity.CommunityLayoutId
                    };

                    var versionResult = await mediator.SendAsync(createVersionCommand);
                    if (!versionResult.IsSuccess)
                    {
                        return BadRequest(new { message = $"Failed to create template version: {versionResult.ErrorMessage}" });
                    }
                }
                catch (Exception ex)
                {
                    return BadRequest(new { message = $"Error creating template: {ex.Message}" });
                }
            }

            return Ok(new
            {
                templateId = entity.Id,
                title = entity.Title,
                layoutNumber = entity.LayoutNumber,
            });
        }

        private async Task<List<Cosmos.Common.Data.Template>> GetTemplatesForCurrentLayoutAsync()
        {
            var layout = await GetCurrentLayoutAsync();

            if (layout == null)
            {
                return new List<Cosmos.Common.Data.Template>();
            }

            var layoutId = layout.Id;
            var layoutNumber = layout.LayoutNumber;

            return await dbContext.Templates
                .AsNoTracking()
                .Where(t => t.LayoutNumber == layoutNumber ||
                            (t.LayoutNumber == 0 && t.LayoutId == layoutId))
                .ToListAsync();
        }

        private async Task<Layout?> GetCurrentLayoutAsync()
        {
            var layoutViewModel = await mediator.QueryAsync(new Cosmos.Common.Features.Layouts.Queries.GetDefaultLayoutQuery());
            if (layoutViewModel == null)
            {
                return null;
            }

            return await dbContext.Layouts
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == layoutViewModel.Id);
        }

        private async Task<Cosmos.Common.Data.Template?> GetTemplateAsync(Guid templateId)
        {
            var result = await mediator.QueryAsync(new GetTemplateQuery { TemplateId = templateId });
            if (!result.IsSuccess || result.Data?.Template == null)
            {
                return null;
            }

            return result.Data.Template;
        }

        /// <summary>
        /// Gets an article field payload from the latest editable version.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <param name="fieldKey">Field key.</param>
        /// <returns>Field payload.</returns>
        [HttpGet("articles/{articleNumber:int}/{fieldKey}")]
        public async Task<IActionResult> GetArticleField(int articleNumber, string fieldKey)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var article = await GetEditableArticle(articleNumber);
            if (article == null)
            {
                return NotFound();
            }

            return fieldKey.ToLowerInvariant() switch
            {
                "id" => Ok(new { value = article.Id.ToString() }),
                "published" => Ok(new { value = article.Published?.ToString("O") }),
                "title" => Ok(new { value = article.Title }),
                "bannerimage" => Ok(new { value = article.BannerImage }),
                "category" => Ok(new { value = article.Category }),
                "introduction" => Ok(new { content = article.Introduction }),
                "content" => Ok(new { content = article.Content }),
                "headerjavascript" => Ok(new { content = article.HeaderJavaScript }),
                "footerjavascript" => Ok(new { content = article.FooterJavaScript }),
                _ => NotFound(),
            };
        }

        /// <summary>
        /// Updates an article field payload on the latest editable version.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <param name="fieldKey">Field key.</param>
        /// <param name="request">Payload body.</param>
        /// <returns>Success.</returns>
        [HttpPut("articles/{articleNumber:int}/{fieldKey}")]
        public async Task<IActionResult> SetArticleField(int articleNumber, string fieldKey, [FromBody] FieldUpdateRequest request)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var article = await GetEditableArticle(articleNumber);
            if (article == null)
            {
                return NotFound();
            }

            switch (fieldKey.ToLowerInvariant())
            {
                case "published":
                    article.Published = ParsePublishedValue(request.Value);
                    break;
                case "title":
                    article.Title = request.Value ?? string.Empty;
                    break;
                case "bannerimage":
                    article.BannerImage = request.Value ?? string.Empty;
                    break;
                case "category":
                    article.Category = request.Value ?? string.Empty;
                    break;
                case "introduction":
                    article.Introduction = request.Content ?? string.Empty;
                    break;
                case "content":
                    article.Content = request.Content ?? string.Empty;
                    break;
                case "headerjavascript":
                    article.HeaderJavaScript = request.Content ?? string.Empty;
                    break;
                case "footerjavascript":
                    article.FooterJavaScript = request.Content ?? string.Empty;
                    break;
                default:
                    return NotFound();
            }

            article.Updated = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// Publishes an article (sets <see cref="Article.Published"/> to now).
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>Success or not found.</returns>
        [HttpPost("articles/{articleNumber:int}/publish")]
        public async Task<IActionResult> PublishArticle(int articleNumber)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var article = await GetLatestArticleVersion(articleNumber);
            if (article == null)
            {
                return NotFound();
            }

            await articleLogic.PublishArticle(article.Id, null);
            return Ok();
        }

        /// <summary>
        /// Unpublishes an article (clears <see cref="Article.Published"/>).
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <returns>Success or not found.</returns>
        [HttpPost("articles/{articleNumber:int}/unpublish")]
        public async Task<IActionResult> UnpublishArticle(int articleNumber)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var article = await GetLatestArticleVersion(articleNumber);
            if (article == null)
            {
                return NotFound();
            }

            await publishingService.UnpublishAsync(article);
            return Ok();
        }

        /// <summary>
        /// Lists paged version metadata for an article family, newest first.
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <param name="skip">Number of items to skip (0-based).</param>
        /// <param name="take">Maximum number of items to return.</param>
        /// <returns>Paged version metadata.</returns>
        [HttpGet("articles/{articleNumber:int}/versions")]
        public async Task<IActionResult> GetArticleVersions(int articleNumber, int skip = 0, int take = 10)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var family = await dbContext.Articles
                .AsNoTracking()
                .Where(a => a.ArticleNumber == articleNumber)
                .ToListAsync();

            if (family.Count == 0)
            {
                return NotFound();
            }

            var maxVersionNumber = family.Max(a => a.VersionNumber);

            var ordered = family
                .OrderByDescending(a => a.VersionNumber)
                .ToList();

            var total = ordered.Count;
            var page = ordered.Skip(skip).Take(take).ToList();

            var items = page.Select(a => new
            {
                versionId = a.Id,
                versionNumber = a.VersionNumber,
                isEditable = !a.Published.HasValue && a.VersionNumber == maxVersionNumber,
                isPublished = a.Published.HasValue,
                publishedDate = a.Published?.UtcDateTime.ToString("o"),
                updated = a.Updated.UtcDateTime.ToString("o"),
            }).ToList();

            return Ok(new
            {
                items,
                total,
                hasMore = skip + take < total,
            });
        }

        /// <summary>
        /// Gets a specific article field for a version by its ID (read-only history access).
        /// </summary>
        /// <param name="articleNumber">Article number.</param>
        /// <param name="versionId">Version row ID (GUID).</param>
        /// <param name="fieldKey">Field key.</param>
        /// <returns>Field payload.</returns>
        [HttpGet("articles/{articleNumber:int}/versions/{versionId:guid}/{fieldKey}")]
        public async Task<IActionResult> GetArticleVersionField(int articleNumber, Guid versionId, string fieldKey)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var article = await dbContext.Articles
                .AsNoTracking()
                .Where(a => a.ArticleNumber == articleNumber && a.Id == versionId)
                .FirstOrDefaultAsync();

            if (article == null)
            {
                return NotFound();
            }

            return fieldKey.ToLowerInvariant() switch
            {
                "title" => Ok(new { value = article.Title }),
                "bannerimage" => Ok(new { value = article.BannerImage }),
                "category" => Ok(new { value = article.Category }),
                "introduction" => Ok(new { content = article.Introduction }),
                "content" => Ok(new { content = article.Content }),
                "headerjavascript" => Ok(new { content = article.HeaderJavaScript }),
                "footerjavascript" => Ok(new { content = article.FooterJavaScript }),
                _ => NotFound(),
            };
        }

        /// <summary>
        /// Creates a new article with the provided title and optional article type.
        /// </summary>
        /// <param name="request">Creation payload.</param>
        /// <returns>New article number and title.</returns>
        [HttpPost("articles")]
        public async Task<IActionResult> CreateArticle([FromBody] CreateArticleRequest request)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new { message = "Title is required." });
            }

            var maxNumber = await dbContext.Articles.AnyAsync()
                ? await dbContext.Articles.MaxAsync(a => a.ArticleNumber)
                : 0;

            var article = new Article
            {
                ArticleNumber = maxNumber + 1,
                VersionNumber = 1,
                Title = request.Title.Trim(),
                ArticleType = request.ArticleType,
                UserId = string.Empty,
                Updated = DateTimeOffset.UtcNow,
            };

            dbContext.Articles.Add(article);
            await dbContext.SaveChangesAsync();

            return Ok(new
            {
                articleNumber = article.ArticleNumber,
                title = article.Title,
            });
        }

        /// <summary>
        /// Publishes a layout version (sets <see cref="Layout.Published"/> to now).
        /// </summary>
        /// <param name="layoutNumber">Layout number.</param>
        /// <param name="version">Version number.</param>
        /// <returns>Success or not found.</returns>
        [HttpPost("layouts/{layoutNumber:int}/{version:int}/publish")]
        public async Task<IActionResult> PublishLayoutVersion(int layoutNumber, int version)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var layout = await dbContext.Layouts
                .FirstOrDefaultAsync(l => l.LayoutNumber == layoutNumber && l.Version == version);

            if (layout == null)
            {
                return NotFound();
            }

            layout.Published = DateTimeOffset.UtcNow;
            layout.LastModified = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// Marks a layout version as the default, clearing the flag on all other versions in the same family.
        /// </summary>
        /// <param name="layoutNumber">Layout number.</param>
        /// <param name="version">Version number.</param>
        /// <returns>Success or not found.</returns>
        [HttpPost("layouts/{layoutNumber:int}/{version:int}/set-default")]
        public async Task<IActionResult> SetDefaultLayoutVersion(int layoutNumber, int version)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var target = await dbContext.Layouts
                .FirstOrDefaultAsync(l => l.LayoutNumber == layoutNumber && l.Version == version);

            if (target == null)
            {
                return NotFound();
            }

            var family = await dbContext.Layouts
                .Where(l => l.LayoutNumber == layoutNumber)
                .ToListAsync();

            foreach (var member in family)
            {
                member.IsDefault = member.Version == version;
            }

            await dbContext.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// Duplicates the latest version of a layout, creating a new editable version.
        /// </summary>
        /// <param name="layoutNumber">Layout number to duplicate.</param>
        /// <returns>New version number and layout number.</returns>
        [HttpPost("layouts/{layoutNumber:int}/versions")]
        public async Task<IActionResult> DuplicateLayoutVersion(int layoutNumber)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            var source = await dbContext.Layouts
                .OrderByDescending(l => l.Version ?? 0)
                .FirstOrDefaultAsync(l => l.LayoutNumber == layoutNumber);

            if (source == null)
            {
                return NotFound();
            }

            var versionCount = await dbContext.Layouts
                .CountAsync(l => l.LayoutNumber == layoutNumber);

            var newLayout = new Layout
            {
                Id = Guid.NewGuid(),
                LayoutNumber = source.LayoutNumber,
                Version = versionCount + 1,
                LayoutName = source.LayoutName,
                Notes = source.Notes,
                Head = source.Head,
                HtmlHeader = source.HtmlHeader,
                BodyHtmlAttributes = source.BodyHtmlAttributes,
                FooterHtmlContent = source.FooterHtmlContent,
                IsDefault = false,
                Published = null,
                CommunityLayoutId = source.CommunityLayoutId,
                LastModified = DateTimeOffset.UtcNow,
            };

            dbContext.Layouts.Add(newLayout);
            await dbContext.SaveChangesAsync();

            return Ok(new
            {
                layoutNumber = newLayout.LayoutNumber,
                version = newLayout.Version,
            });
        }

        private static DateTimeOffset? ParsePublishedValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (DateTimeOffset.TryParse(value, out var parsed))
            {
                return parsed.ToUniversalTime();
            }

            return null;
        }

        private async Task<Layout?> GetEditableLayout(int layoutNumber)
        {
            if (layoutNumber <= 0)
            {
                // Legacy tenants can still have layout families persisted with LayoutNumber=0.
                // Keep VS Code field access working by resolving/editing that family directly.
                var legacyFamily = await dbContext.Layouts
                    .Where(l => l.LayoutNumber == 0)
                    .ToListAsync();

                var legacyLatest = legacyFamily
                    .OrderByDescending(l => l.Version ?? 0)
                    .FirstOrDefault();

                if (legacyLatest == null)
                {
                    return null;
                }

                if (!legacyLatest.Published.HasValue)
                {
                    return legacyLatest;
                }

                var legacyNewLayout = new Layout
                {
                    Id = Guid.NewGuid(),
                    LayoutNumber = legacyLatest.LayoutNumber,
                    Version = legacyFamily.Count + 1,
                    LayoutName = legacyLatest.LayoutName,
                    Notes = legacyLatest.Notes,
                    Head = legacyLatest.Head,
                    HtmlHeader = legacyLatest.HtmlHeader,
                    BodyHtmlAttributes = legacyLatest.BodyHtmlAttributes,
                    FooterHtmlContent = legacyLatest.FooterHtmlContent,
                    IsDefault = false,
                    Published = null,
                    CommunityLayoutId = legacyLatest.CommunityLayoutId,
                    LastModified = DateTimeOffset.UtcNow,
                };

                dbContext.Layouts.Add(legacyNewLayout);
                await dbContext.SaveChangesAsync();

                return legacyNewLayout;
            }

            var result = await mediator.SendAsync(new GetEditableLayoutForEditCommand
            {
                LayoutNumber = layoutNumber,
            });

            if (!result.IsSuccess)
            {
                return null;
            }

            return result.Data?.Layout;
        }

        private async Task<Article?> GetEditableArticle(int articleNumber)
        {
            var result = await mediator.SendAsync(new GetEditableArticleForEditCommand
            {
                ArticleNumber = articleNumber,
            });

            if (!result.IsSuccess)
            {
                return null;
            }

            return result.Data?.Article;
        }

        private async Task<Article?> GetLatestArticleVersion(int articleNumber)
        {
            // Fetch all articles with the given articleNumber, then sort client-side
            // to avoid Cosmos DB ORDER BY limitations
            var articles = await dbContext.Articles
                .Where(a => a.ArticleNumber == articleNumber)
                .ToListAsync();

            return articles
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefault();
        }

        private IActionResult? EnsureVsCodeRequestAuthorized()
        {
            if (TryGetBearerIdentity(out var bearerIdentity))
            {
                if (bearerIdentity != null && IsAllowedRole(bearerIdentity.Role))
                {
                    return null;
                }

                return Forbid();
            }

            var role = ResolveUserRole();
            if (User.Identity?.IsAuthenticated == true && role is not null)
            {
                return null;
            }

            return Unauthorized();
        }

        private bool TryGetBearerIdentity(out BearerTokenCacheEntry? identity)
        {
            identity = null;
            var token = ExtractBearerToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (!memoryCache.TryGetValue(TokenCachePrefix + token, out BearerTokenCacheEntry? entry) || entry == null)
            {
                return false;
            }

            identity = entry;
            return true;
        }

        private string? ResolveUserRole()
        {
            if (User.IsInRole("Administrators"))
            {
                return "Administrators";
            }

            if (User.IsInRole("Editors"))
            {
                return "Editors";
            }

            return null;
        }

        private static bool IsAllowedRole(string? role)
        {
            return string.Equals(role, "Administrators", StringComparison.Ordinal)
                || string.Equals(role, "Editors", StringComparison.Ordinal);
        }

        private string? ExtractBearerToken()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var values))
            {
                return null;
            }

            var header = values.ToString();
            const string prefix = "Bearer ";
            if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return header[prefix.Length..].Trim();
        }

        private static string GenerateOneTimeCode()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            Span<byte> bytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(bytes);

            Span<char> chars = stackalloc char[8];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = alphabet[bytes[i] % alphabet.Length];
            }

            return new string(chars);
        }

        private static string GenerateBearerToken()
        {
            Span<byte> bytes = stackalloc byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static string BuildVsCodeCallbackUri(string code, string state, string websiteTitle, string publicUrl)
        {
            return $"vscode://cwalabs.skycms-explorer/auth/callback"
                + $"?code={Uri.EscapeDataString(code)}"
                + $"&state={Uri.EscapeDataString(state)}"
                + $"&websiteTitle={Uri.EscapeDataString(websiteTitle)}"
                + $"&publicUrl={Uri.EscapeDataString(publicUrl)}";
        }

        private static string BuildVsCodeErrorUri(string error, string errorDescription)
        {
            return $"vscode://cwalabs.skycms-explorer/auth/callback"
                + $"?error={Uri.EscapeDataString(error)}"
                + $"&error_description={Uri.EscapeDataString(errorDescription)}";
        }

        /// <summary>
        /// Browser auth exchange payload.
        /// </summary>
        public class AuthExchangeRequest
        {
            /// <summary>
            /// Gets or sets correlation state.
            /// </summary>
            public string? State { get; set; }

            /// <summary>
            /// Gets or sets user-provided exchange code.
            /// </summary>
            public string Code { get; set; } = string.Empty;
        }

        private sealed class BrowserStateCacheEntry
        {
            public string State { get; set; } = string.Empty;
        }

        private sealed class OneTimeCodeCacheEntry
        {
            public string Code { get; set; } = string.Empty;

            public string State { get; set; } = string.Empty;

            public string Username { get; set; } = string.Empty;

            public string Role { get; set; } = string.Empty;

            public string DisplayName { get; set; } = string.Empty;

            public string WebsiteTitle { get; set; } = string.Empty;

            public string PublicUrl { get; set; } = string.Empty;
        }

        private sealed class BearerTokenCacheEntry
        {
            public string Token { get; set; } = string.Empty;

            public string Username { get; set; } = string.Empty;

            public string Role { get; set; } = string.Empty;

            public string DisplayName { get; set; } = string.Empty;
        }

        /// <summary>
        /// Lists files and folders in a directory at the specified path.
        /// </summary>
        /// <param name="pathHash">Base64-encoded path. If empty, lists root (/).</param>
        /// <returns>List of file and folder names with metadata.</returns>
        [HttpGet("files/{pathHash?}")]
        public async Task<IActionResult> GetFilesList(string? pathHash = null)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            string path;
            try
            {
                path = string.IsNullOrEmpty(pathHash) ? "/" : PublicFileEntryHelper.DecodePathHash(pathHash);
                path = PublicFileEntryHelper.NormalizePath(path);
            }
            catch
            {
                return BadRequest(new { message = "Invalid path hash." });
            }

            try
            {
                var tenantDomain = this.configProvider.GetTenantDomainNameFromRequest();
                var entries = await this.folderListingService.GetEntriesAsync(path, this.memoryCache, tenantDomain);

                var articleTitlesByNumber = await this.titleResolver.GetArticleTitlesByNumberAsync(entries);
                var templateTitlesById = await this.titleResolver.GetTemplateTitlesByIdAsync(entries);

                var result = entries.Select(e => new
                {
                    name = PublicFileEntryHelper.ResolveFriendlyDisplayName(path, e, articleTitlesByNumber, templateTitlesById),
                    path = PublicFileEntryHelper.ResolveEntryPath(path, e),
                    displayPath = PublicFileEntryHelper.ResolveFriendlyDisplayPath(
                        PublicFileEntryHelper.ResolveEntryPath(path, e),
                        articleTitlesByNumber),
                    isDir = e.IsDirectory,
                    mimeType = e.IsDirectory ? "directory" : (string.IsNullOrWhiteSpace(e.ContentType) ? "application/octet-stream" : e.ContentType),
                    size = e.Size,
                }).ToList();

                return Ok(result);
            }
            catch (Cosmos.BlobService.Exceptions.StorageException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error listing files at path {Path}", path);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Gets metadata for a file or folder at the specified path.
        /// </summary>
        /// <param name="pathHash">Base64-encoded path.</param>
        /// <returns>File metadata (size, modified date, type).</returns>
        [HttpGet("files/{pathHash}/stat")]
        public async Task<IActionResult> GetFileStat(string pathHash)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            string path;
            try
            {
                path = PublicFileEntryHelper.DecodePathHash(pathHash);
            }
            catch
            {
                return BadRequest(new { message = "Invalid path hash." });
            }

            try
            {
                var entry = await storageContext.GetFileAsync(path);
                if (entry == null)
                {
                    return NotFound();
                }

                return Ok(new
                {
                    size = entry.Size,
                    mtime = new DateTimeOffset(entry.ModifiedUtc).ToUnixTimeMilliseconds(),
                    isDir = entry.IsDirectory,
                    mimeType = entry.ContentType,
                });
            }
            catch (Cosmos.BlobService.Exceptions.StorageException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting stat for path {Path}", path);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Reads the content of a file at the specified path.
        /// </summary>
        /// <param name="pathHash">Base64-encoded path.</param>
        /// <returns>File content as base64 or bytes.</returns>
        [HttpGet("files/{pathHash}/read")]
        public async Task<IActionResult> GetFileContent(string pathHash)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            string path;
            try
            {
                path = PublicFileEntryHelper.DecodePathHash(pathHash);
            }
            catch
            {
                return BadRequest(new { message = "Invalid path hash." });
            }

            try
            {
                using (var stream = await storageContext.GetStreamAsync(path))
                {
                    if (stream == null)
                    {
                        return NotFound();
                    }

                    // We need to get the content type.
                    var metaData = await storageContext.GetFileAsync(path);
                    var contentType = string.IsNullOrWhiteSpace(metaData?.ContentType)
                        ? "application/octet-stream"
                        : metaData.ContentType;

                    // Return as bytes for small files
                    using (var reader = new System.IO.MemoryStream())
                    {
                        await stream.CopyToAsync(reader);
                        return File(reader.ToArray(), contentType);
                    }
                }
            }
            catch (Cosmos.BlobService.Exceptions.StorageException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading file at path {Path}", path);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Deletes a file at the specified path.
        /// </summary>
        /// <param name="pathHash">Base64-encoded path of the file to delete.</param>
        /// <returns>204 No Content on success.</returns>
        [HttpDelete("files/{pathHash}")]
        public async Task<IActionResult> DeleteFile(string pathHash)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            string path;
            try
            {
                path = PublicFileEntryHelper.DecodePathHash(pathHash);
            }
            catch
            {
                return BadRequest(new { message = "Invalid path hash." });
            }

            try
            {
                await storageContext.DeleteFileAsync(path);
                return NoContent();
            }
            catch (Cosmos.BlobService.Exceptions.StorageException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting file at path {Path}", path);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Deletes a folder (and all its contents) at the specified path.
        /// </summary>
        /// <param name="pathHash">Base64-encoded path of the folder to delete.</param>
        /// <returns>204 No Content on success.</returns>
        [HttpDelete("folders/{pathHash}")]
        public async Task<IActionResult> DeleteFolder(string pathHash)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            string path;
            try
            {
                path = PublicFileEntryHelper.DecodePathHash(pathHash);
            }
            catch
            {
                return BadRequest(new { message = "Invalid path hash." });
            }

            try
            {
                await storageContext.DeleteFolderAsync(path);
                return NoContent();
            }
            catch (Cosmos.BlobService.Exceptions.StorageException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting folder at path {Path}", path);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Creates a folder at the specified path.
        /// </summary>
        /// <param name="pathHash">Base64-encoded path of the folder to create.</param>
        /// <returns>201 Created with the folder entry on success.</returns>
        [HttpPost("folders/{pathHash}")]
        public async Task<IActionResult> CreateFolder(string pathHash)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            string path;
            try
            {
                path = PublicFileEntryHelper.DecodePathHash(pathHash);
            }
            catch
            {
                return BadRequest(new { message = "Invalid path hash." });
            }

            try
            {
                var entry = await storageContext.CreateFolder(path);
                return StatusCode(201, new
                {
                    name = entry.Name,
                    isDir = entry.IsDirectory,
                    path = PublicFileEntryHelper.EncodePathHash(entry.Path),
                });
            }
            catch (Cosmos.BlobService.Exceptions.StorageException)
            {
                return Conflict(new { message = "Folder already exists or path is invalid." });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating folder at path {Path}", path);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Uploads a file to the specified path. Accepts raw bytes in the request body.
        /// </summary>
        /// <param name="pathHash">Base64-encoded destination path (including file name).</param>
        /// <returns>204 No Content on success.</returns>
        [HttpPost("files/{pathHash}")]
        [RequestSizeLimit(52_428_800)] // 50 MB
        public async Task<IActionResult> UploadFile(string pathHash)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            string path;
            try
            {
                path = PublicFileEntryHelper.DecodePathHash(pathHash);
            }
            catch
            {
                return BadRequest(new { message = "Invalid path hash." });
            }

            if (Request.ContentLength is null or 0)
            {
                return BadRequest(new { message = "Request body is empty." });
            }

            if (!PublicFileEntryHelper.IsUploadPathSafe(path))
            {
                return BadRequest(new { message = "Uploads must target a path within the /pub directory." });
            }

            if (PublicFileEntryHelper.IsDangerousExtension(System.IO.Path.GetFileName(path)))
            {
                return BadRequest(new { message = "This file type is not allowed for upload." });
            }

            try
            {
                var fileName = System.IO.Path.GetFileName(path);
                var extension = System.IO.Path.GetExtension(fileName);
                var contentType = !string.IsNullOrWhiteSpace(Request.ContentType)
                    ? Request.ContentType
                    : MimeTypeMap.GetMimeType(extension);

                using var memoryStream = new MemoryStream();
                await Request.Body.CopyToAsync(memoryStream);

                var metaData = new Cosmos.BlobService.Models.FileUploadMetaData
                {
                    UploadUid = Guid.NewGuid().ToString(),
                    FileName = fileName,
                    RelativePath = path.TrimStart('/'),
                    ContentType = contentType,
                    ChunkIndex = 0,
                    TotalChunks = 1,
                    TotalFileSize = memoryStream.Length,
                };

                await storageContext.AppendBlob(memoryStream, metaData, Cosmos.BlobService.StorageConstants.UploadModeBlock);
                return NoContent();
            }
            catch (Cosmos.BlobService.Exceptions.StorageException)
            {
                return BadRequest(new { message = "Upload failed. Check the path is valid." });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error uploading file at path {Path}", path);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Moves a file from the specified source path to a new destination path.
        /// </summary>
        /// <param name="pathHash">Base64-encoded source path.</param>
        /// <param name="request">Move request containing the destination path.</param>
        /// <returns>204 No Content on success.</returns>
        [HttpPost("files/{pathHash}/move")]
        public async Task<IActionResult> MoveFile(string pathHash, [FromBody] MoveRequest request)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            string sourcePath;
            try
            {
                sourcePath = PublicFileEntryHelper.DecodePathHash(pathHash);
            }
            catch (ArgumentException)
            {
                return BadRequest(new { message = "Invalid path hash." });
            }

            if (string.IsNullOrWhiteSpace(request?.Destination))
            {
                return BadRequest(new { message = "Destination path is required." });
            }

            try
            {
                await storageContext.MoveFileAsync(sourcePath, request.Destination);
                return NoContent();
            }
            catch (Cosmos.BlobService.Exceptions.StorageException)
            {
                return NotFound(new { message = "Source file not found or destination is invalid." });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error moving file from {Source} to {Destination}", sourcePath, request.Destination);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Moves a folder from the specified source path to a new destination path.
        /// </summary>
        /// <param name="pathHash">Base64-encoded source path.</param>
        /// <param name="request">Move request containing the destination path.</param>
        /// <returns>204 No Content on success.</returns>
        [HttpPost("folders/{pathHash}/move")]
        public async Task<IActionResult> MoveFolder(string pathHash, [FromBody] MoveRequest request)
        {
            var authResult = EnsureVsCodeRequestAuthorized();
            if (authResult != null)
            {
                return authResult;
            }

            string sourcePath;
            try
            {
                sourcePath = PublicFileEntryHelper.DecodePathHash(pathHash);
            }
            catch (ArgumentException)
            {
                return BadRequest(new { message = "Invalid path hash." });
            }

            if (string.IsNullOrWhiteSpace(request?.Destination))
            {
                return BadRequest(new { message = "Destination path is required." });
            }

            try
            {
                await storageContext.MoveFolderAsync(sourcePath, request.Destination);
                return NoContent();
            }
            catch (Cosmos.BlobService.Exceptions.StorageException)
            {
                return NotFound(new { message = "Source folder not found or destination is invalid." });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error moving folder from {Source} to {Destination}", sourcePath, request.Destination);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Gets the layout for editing - creates a new version if the current one is default.
        /// </summary>
        /// <returns>Layout for editing.</returns>
        private async Task<Layout> GetLayoutForEdit()
        {
            // Fetch all layouts and sort client-side to avoid Cosmos DB ORDER BY limitations
            var layouts = await dbContext.Layouts.ToListAsync();
            var layout = layouts.OrderByDescending(o => o.Version).FirstOrDefault();

            if (layout == null)
            {
                layout = new Layout
                {
                    Id = Guid.NewGuid(),
                    IsDefault = true,
                    LayoutName = DefaultLayoutName,
                    Notes = DefaultLayoutNotes,
                    LayoutNumber = 1,
                    Version = 1,
                    LastModified = DateTimeOffset.UtcNow
                };

                dbContext.Layouts.Add(layout);

                await dbContext.SaveChangesAsync();

                logger.LogInformation("Created default layout {LayoutId} with LayoutNumber=1", layout.Id);

                return layout;
            }

            if (layout.IsDefault)
            {
                var newVersion = await layoutVersioningService.CreateNewVersionAsync(layout);
                if (newVersion != null)
                {
                    return newVersion;
                }

                logger.LogWarning("Layout versioning service returned null for default layout {LayoutId}; using current layout.", layout.Id);
                return layout;
            }

            return layout;
        }

        /// <summary>
        /// Generic field update payload.
        /// </summary>
        public class FieldUpdateRequest
        {
            /// <summary>
            /// Gets or sets document-oriented content value.
            /// </summary>
            public string? Content { get; set; }

            /// <summary>
            /// Gets or sets short-form value.
            /// </summary>
            public string? Value { get; set; }
        }

        /// <summary>
        /// Payload for creating a new article.
        /// </summary>
        public class CreateArticleRequest
        {
            /// <summary>
            /// Gets or sets the title of the new article.
            /// </summary>
            public string? Title { get; set; }

            /// <summary>
            /// Gets or sets the optional article type integer.
            /// </summary>
            public int? ArticleType { get; set; }
        }

        /// <summary>
        /// Payload for a move (rename) operation.
        /// </summary>
        public class MoveRequest
        {
            /// <summary>
            /// Gets or sets the destination path for the move operation.
            /// </summary>
            public string? Destination { get; set; }
        }
    }
}
