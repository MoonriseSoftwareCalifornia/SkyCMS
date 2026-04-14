// <copyright file="Index.cshtml.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Pages.Diagnostics
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Services.Diagnostics;

    /// <summary>
    /// Diagnostic page model for configuration validation.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<IndexModel> logger;
        private readonly ConfigurationValidator validator;

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexModel"/> class.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="validator">Configuration validator service.</param>
        public IndexModel(
            IConfiguration configuration,
            ILogger<IndexModel> logger,
            ConfigurationValidator validator)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.validator = validator;
        }

        /// <summary>
        /// Gets the validation result.
        /// </summary>
        public ValidationResult Result { get; private set; }

        /// <summary>
        /// Handles GET requests.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task OnGetAsync()
        {
            Result = await validator.ValidateAsync();

            logger.LogInformation(
                "Configuration validation completed. Mode: {Mode}, Valid: {IsValid}",
                Result.Mode,
                Result.IsValid);
        }
    }
}