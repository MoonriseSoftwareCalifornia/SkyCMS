// <copyright file="ViewRenderingService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Services
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.Razor;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Routing;

    /// <summary>
    /// View render service interface.
    /// </summary>
    /// <remarks>
    /// Credits for this work go to the members of the thread found on
    /// <see href="https://stackoverflow.com/questions/40912375/return-view-as-string-in-net-core">Stack Overflow</see>.
    /// </remarks>
    public interface IViewRenderService
    {
        /// <summary>
        /// Render view as a string.
        /// </summary>
        /// <param name="viewName"></param>
        /// <param name="model"></param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<string> RenderToStringAsync(string viewName, object model);
    }

    /// <summary>
    /// View rendering service.
    /// </summary>
    /// <remarks>
    /// Credits for this work go to the members of the thread found on
    /// <see href="https://stackoverflow.com/questions/40912375/return-view-as-string-in-net-core">Stack Overflow</see>.
    /// </remarks>
    public class ViewRenderService : IViewRenderService
    {
        private readonly IRazorViewEngine razorViewEngine;
        private readonly ITempDataProvider tempDataProvider;
        private readonly IServiceProvider serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewRenderService"/> class.
        /// Constructor.
        /// </summary>
        /// <param name="razorViewEngine">View engine.</param>
        /// <param name="tempDataProvider">Temp data provider.</param>
        /// <param name="serviceProvider">Services provider.</param>
        public ViewRenderService(
            IRazorViewEngine razorViewEngine,
            ITempDataProvider tempDataProvider,
            IServiceProvider serviceProvider)
        {
            this.razorViewEngine = razorViewEngine;
            this.tempDataProvider = tempDataProvider;
            this.serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Render view as a string.
        /// </summary>
        /// <param name="viewPath">Path to view.</param>
        /// <param name="model">Page model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Null argument exception.</exception>
        public async Task<string> RenderToStringAsync(string viewPath, object model)
        {
            if (string.IsNullOrWhiteSpace(viewPath))
            {
                throw new ArgumentNullException(nameof(viewPath));
            }

            var actionContext = GetActionContext();

            // First try direct path resolution (works for physical/absolute views).
            var directViewResult = razorViewEngine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: true);
            var viewResult = directViewResult;

            // Fallback to MVC discovery for shared/RCL views.
            if (!viewResult.Success)
            {
                var lookupName = viewPath;
                if (lookupName.StartsWith("~/", StringComparison.Ordinal))
                {
                    lookupName = lookupName[2..];
                }

                if (lookupName.StartsWith("/", StringComparison.Ordinal))
                {
                    lookupName = lookupName[1..];
                }

                if (lookupName.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                {
                    lookupName = lookupName[..^".cshtml".Length];
                }

                // FindView expects a view name/path relative to MVC discovery roots.
                // '~/Views/Home/Index.cshtml' should become 'Home/Index' (not 'Views/Home/Index').
                if (lookupName.StartsWith("Views/", StringComparison.OrdinalIgnoreCase))
                {
                    lookupName = lookupName["Views/".Length..];
                }

                if (lookupName.StartsWith("Pages/", StringComparison.OrdinalIgnoreCase))
                {
                    lookupName = lookupName["Pages/".Length..];
                }

                viewResult = razorViewEngine.FindView(actionContext, lookupName, isMainPage: true);

                // Final RCL guard: explicit rooted path used by shared Razor class libraries.
                if (!viewResult.Success)
                {
                    var rclPath = "/Views/" + lookupName + ".cshtml";
                    viewResult = razorViewEngine.GetView(executingFilePath: null, viewPath: rclPath, isMainPage: true);
                }
            }

            if (!viewResult.Success)
            {
                var searchedLocations = directViewResult.SearchedLocations
                    .Concat(viewResult.SearchedLocations)
                    .Distinct()
                    .ToArray();

                var searchedLocationsText = searchedLocations.Length == 0
                    ? "(no locations reported by Razor view engine)"
                    : string.Join(Environment.NewLine, searchedLocations);

                Debug.WriteLine($"View resolution failed for '{viewPath}'. Searched locations:{Environment.NewLine}{searchedLocationsText}");

                throw new ArgumentNullException($"{viewPath} does not match any available view. Searched locations:{Environment.NewLine}{searchedLocationsText}");
            }

            var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model
            };

            await using var output = new StringWriter();
            var viewContext = new ViewContext(
                actionContext,
                viewResult.View,
                viewDictionary,
                new TempDataDictionary(actionContext.HttpContext, tempDataProvider),
                output,
                new HtmlHelperOptions());

            await viewResult.View.RenderAsync(viewContext);
            return output.ToString();
        }

        private ActionContext GetActionContext()
        {
            var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return actionContext;
        }
    }
}
