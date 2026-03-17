// <copyright file="HomeController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>
using Cosmos.BlobService;
using Cosmos.Cms.Common.Services.Configurations;
using Cosmos.Cms.Publisher.Models;
using Cosmos.Common;
using Cosmos.Common.Data;
using Cosmos.Common.Data.Logic;
using Cosmos.Common.Features.Articles.Queries;
using Cosmos.Common.Features.Shared;
using Cosmos.Common.Models;
using Cosmos.Common.Services;
using Cosmos.Publisher.Configuration;
using Cosmos.Publisher.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web;

namespace Cosmos.Cms.Publisher.Controllers
{
    /// <summary>
    /// Home page controller.
    /// </summary>
    public class HomeController : HomeControllerBase
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<HomeController> logger;
        private readonly IMediator mediator;
        private readonly IOptions<SiteSettings> options;
        private readonly ApplicationDbContext dbContext;
        private readonly IGraphIntegrationService graphIntegrationService;
        private readonly IRequestContextProvider requestContextProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeController"/> class.
        /// </summary>
        /// <param name="configuration">Configuration.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="mediator">Mediator.</param>
        /// <param name="options">Cosmos options.</param>
        /// <param name="dbContext">Database Context.</param>
        /// <param name="storageContext">Storage context.</param>
        /// <param name="emailSender">Email services.</param>
        /// <param name="contactManagementService">Contact management service.</param>
        /// <param name="graphIntegrationService">Graph integration service.</param>
        /// <param name="requestContextProvider">Request context provider.</param>
        public HomeController(
            IConfiguration configuration,
            ILogger<HomeController> logger,
            IMediator mediator,
            IOptions<SiteSettings> options,
            ApplicationDbContext dbContext,
            IStorageContext storageContext,
            IEmailSender emailSender,
            IContactManagementService contactManagementService,
            IGraphIntegrationService graphIntegrationService,
            IRequestContextProvider requestContextProvider)
            : base(mediator, dbContext, storageContext, logger, emailSender, contactManagementService)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.graphIntegrationService = graphIntegrationService ?? throw new ArgumentNullException(nameof(graphIntegrationService));
            this.requestContextProvider = requestContextProvider ?? throw new ArgumentNullException(nameof(requestContextProvider));
        }

        /// <summary>
        /// Handles the head request.
        /// </summary>
        /// <returns>IActionResult.</returns>
        [HttpHead]
        [ActionName("Index")]
        public async Task<IActionResult> CCMS___Head()
        {
            if (!this.options.Value.CosmosRequiresAuthentication)
            {
                var article = await this.mediator.QueryAsync(new GetPublishedPageHeaderByUrlQuery
                {
                    UrlPath = this.requestContextProvider.GetPathValue()
                });

                if (article == null)
                {
                    return this.NotFound();
                }

                this.SetResponseCacheHeaders(article, isAuthenticated: false);
                return this.Ok("Ok");
            }

            return this.Unauthorized();
        }

        /// <summary>
        /// Index view.
        /// </summary>
        /// <param name="lang">Language code.</param>
        /// <param name="mode">json or nothing.</param>
        /// <returns>Returns an <see cref="IActionResult"/>.</returns>
        [HttpGet]
        public async Task<IActionResult> Index(string lang = "", string mode = "")
        {
            if (!this.ModelState.IsValid)
            {
                return this.BadRequest(this.ModelState);
            }

            try
            {
                var article = await this.mediator.QueryAsync(new GetPublishedPageByUrlQuery
                {
                    UrlPath = this.requestContextProvider.GetPathValue(),
                    Lang = lang,
                    CacheSpan = TimeSpan.FromSeconds(5),
                    LayoutCache = TimeSpan.FromSeconds(20),
                    IncludeLayout = true
                });

                if (article == null)
                {
                    return await this.HandleArticleNotFoundAsync();
                }

                if (this.options.Value.CosmosRequiresAuthentication)
                {
                    var authResult = await this.AuthorizeUserForArticleAsync(article);
                    if (authResult != null)
                    {
                        return authResult;
                    }

                    this.SetResponseCacheHeaders(article, isAuthenticated: true);
                }
                else
                {
                    this.SetResponseCacheHeaders(article, isAuthenticated: false);
                }

                if (mode == "json")
                {
                    article.Layout = null;
                    return this.Json(article);
                }

                if (article.StatusCode == StatusCodeEnum.Redirect)
                {
                    return this.View("~/Views/Home/Redirect.cshtml", new RedirectItemViewModel()
                    {
                        FromUrl = article.UrlPath,
                        ToUrl = article.Content,
                        Id = article.Id
                    });
                }

                return this.View(article);
            }
            catch (Microsoft.Azure.Cosmos.CosmosException ex)
            {
                this.logger.LogError(ex, "Cosmos exception while retrieving page");
                return this.HandlePageError(mode);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Unexpected exception while retrieving page");
                return this.HandlePageError(mode);
            }
        }

        /// <summary>
        /// Returns an error page.
        /// </summary>
        /// <returns>Returns an <see cref="IActionResult"/> with an <see cref="ErrorViewModel"/>.</returns>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return this.View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier });
        }

        /// <summary>
        /// Gets the application validation for Microsoft.
        /// </summary>
        /// <returns>Returns the microsoft-identity-association.json file as an <see cref="IActionResult"/>.</returns>
        [AllowAnonymous]
        public IActionResult GetMicrosoftIdentityAssociation()
        {
            var model = new MicrosoftValidationObject();
            model.associatedApplications.Add(new AssociatedApplication() { applicationId = this.options.Value.MicrosoftAppId });

            var data = Newtonsoft.Json.JsonConvert.SerializeObject(model);

            return this.File(Encoding.UTF8.GetBytes(data), "application/json", fileDownloadName: "microsoft-identity-association.json");
        }

        /// <summary>
        /// Sets appropriate response cache headers based on authentication status.
        /// </summary>
        /// <param name="article">The article being served.</param>
        /// <param name="isAuthenticated">Whether the user is authenticated.</param>
        private void SetResponseCacheHeaders(Cosmos.Common.Models.ArticleViewModel article, bool isAuthenticated)
        {
            if (isAuthenticated)
            {
                this.Response.Headers.Expires = DateTimeOffset.UtcNow.AddMinutes(-30).ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'");
                this.Response.Headers.ETag = Guid.NewGuid().ToString();
                this.Response.Headers.LastModified = DateTimeOffset.UtcNow.AddMinutes(-30).ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'");
                this.Response.Headers.CacheControl = PublisherConfigurationKeys.FileCache.PrivateCacheControl;
            }
            else
            {
                this.Response.Headers.Expires = article.Expires.HasValue ?
                    article.Expires.Value.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'") :
                    DateTimeOffset.UtcNow.AddMinutes(1).ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'");
                this.Response.Headers.ETag = article.Id.ToString();
                this.Response.Headers.LastModified = article.Updated.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'");
                this.Response.Headers.CacheControl = "max-age=20, stale-while-revalidate=119";
            }
        }

        /// <summary>
        /// Handles the case where an article is not found.
        /// </summary>
        /// <returns>An IActionResult representing the appropriate response.</returns>
        private async Task<IActionResult> HandleArticleNotFoundAsync()
        {
            if (!await this.dbContext.Pages.CosmosAnyAsync())
            {
                return this.View("__UnderConstruction");
            }

            this.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return this.View("__NotFound");
        }

        /// <summary>
        /// Authorizes the user for the requested article.
        /// </summary>
        /// <param name="article">The article being requested.</param>
        /// <returns>An IActionResult if authorization fails (redirect or permission denied), null if authorized.</returns>
        private async Task<IActionResult> AuthorizeUserForArticleAsync(Cosmos.Common.Models.ArticleViewModel article)
        {
            if (!this.requestContextProvider.IsUserAuthenticated())
            {
                var returnUrl = WebUtility.UrlEncode(this.requestContextProvider.GetPath());
                return this.Redirect($"~/Identity/Account/Login?returnUrl={returnUrl}");
            }

            var validGroups = this.configuration.GetValue<string>(PublisherConfigurationKeys.EntraIdValidUserGroups);

            if (string.IsNullOrWhiteSpace(validGroups))
            {
                // No group restrictions, user is authorized
                return null;
            }

            var groupArray = validGroups.Split(';', StringSplitOptions.RemoveEmptyEntries);
            if (groupArray.Length == 0)
            {
                return null;
            }

            var userEmail = this.requestContextProvider.GetUserEmail();
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                return this.View("__NeedPermission");
            }

            // Check Graph API first if available
            if (this.graphIntegrationService.IsAvailable)
            {
                var isInGroup = await this.graphIntegrationService.IsUserInGroupsAsync(userEmail, groupArray);
                if (isInGroup)
                {
                    return null; // Authorized
                }
                else
                {
                    return this.View("__NeedPermission");
                }
            }

            // Fallback to database authorization check
            if (!await this.mediator.QueryAsync(new Cosmos.Common.Features.Articles.Queries.AuthorizeUserForArticleQuery(this.requestContextProvider.GetUser(), article.ArticleNumber)))
            {
                return this.View("__NeedPermission");
            }

            return null; // Authorized
        }

        /// <summary>
        /// Handles errors during page retrieval.
        /// </summary>
        /// <param name="mode">The request mode (e.g., "json").</param>
        /// <returns>An IActionResult representing the error response.</returns>
        private IActionResult HandlePageError(string mode)
        {
            if (mode == "json")
            {
                return this.NotFound();
            }

            this.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return this.View("__NotFound");
        }
    }
}
