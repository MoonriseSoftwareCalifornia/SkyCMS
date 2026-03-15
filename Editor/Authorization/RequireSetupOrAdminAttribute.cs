// <copyright file="RequireSetupOrAdminAttribute.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Authorization
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Sky.Editor.Services.Setup;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Authorization attribute for setup wizard pages.
    /// Allows access during initial setup OR if user is an admin (for post-setup changes).
    /// Redirects to an error page if setup is complete and user is not an admin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireSetupOrAdminAttribute : Attribute, IAsyncAuthorizationFilter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequireSetupOrAdminAttribute"/> class.
        /// </summary>
        /// <param name="redirectIfBlocked">If true and unauthorized, redirect to /Setup/Unauthorized. If false, return 403 Forbidden.</param>
        public RequireSetupOrAdminAttribute(bool redirectIfBlocked = true)
        {
            this.RedirectIfBlocked = redirectIfBlocked;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to redirect to an unauthorized page if access is denied.
        /// </summary>
        public bool RedirectIfBlocked { get; set; }

        /// <inheritdoc/>
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var setupCheckService = context.HttpContext.RequestServices.GetService(typeof(ISetupCheckService)) as ISetupCheckService;

            if (setupCheckService == null)
            {
                // If service is not available, allow the request (services might not be configured in tests)
                return;
            }

            // Check if setup is complete
            var isSetupComplete = await setupCheckService.IsSetup();

            if (!isSetupComplete)
            {
                // Setup is in progress - allow access
                return;
            }

            // Setup is complete - check if user is admin
            var user = context.HttpContext.User;

            if (user?.Identity?.IsAuthenticated == true && user.IsInRole("Administrators"))
            {
                // User is authenticated and is an admin - allow access to post-setup wizard
                return;
            }

            // Setup is complete and user is not an admin - deny access
            if (this.RedirectIfBlocked)
            {
                context.Result = new RedirectToPageResult("/Setup/Unauthorized");
            }
            else
            {
                context.Result = new ForbidResult();
            }
        }
    }

    /// <summary>
    /// Authorization attribute that allows access ONLY during initial setup.
    /// Denies access once setup is complete, even for admins.
    /// Used for the initial setup wizard flow.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireSetupInProgressAttribute : Attribute, IAsyncAuthorizationFilter
    {
        /// <inheritdoc/>
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var setupCheckService = context.HttpContext.RequestServices.GetService(typeof(ISetupCheckService)) as ISetupCheckService;

            if (setupCheckService == null)
            {
                return;
            }

            var isSetupComplete = await setupCheckService.IsSetup();

            if (isSetupComplete)
            {
                // Setup is already complete - deny access
                context.Result = new RedirectToPageResult("/Index");
            }
        }
    }

    /// <summary>
    /// Authorization attribute that allows access ONLY after setup is complete and user is an admin.
    /// Used for post-setup wizard pages (re-running wizard to modify settings).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireSetupCompleteAndAdminAttribute : Attribute, IAsyncAuthorizationFilter
    {
        /// <inheritdoc/>
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var setupCheckService = context.HttpContext.RequestServices.GetService(typeof(ISetupCheckService)) as ISetupCheckService;

            if (setupCheckService == null)
            {
                return;
            }

            var isSetupComplete = await setupCheckService.IsSetup();
            var user = context.HttpContext.User;

            if (!isSetupComplete)
            {
                // Setup not complete yet
                context.Result = new RedirectToPageResult("/Setup/Index");
                return;
            }

            if (user?.Identity?.IsAuthenticated != true || !user.IsInRole("Administrators"))
            {
                // Setup is complete but user is not authenticated or not admin
                context.Result = new ForbidResult();
            }
        }
    }
}
