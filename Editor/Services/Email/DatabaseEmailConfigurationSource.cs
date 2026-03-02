// <copyright file="DatabaseEmailConfigurationSource.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Email
{
    using System;
    using System.Linq;
    using Cosmos.Common.Data;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Configuration source that loads email settings from the database Settings table.
    /// This allows email configuration saved by the setup wizard to be used at runtime.
    /// Falls back to environment variables/appsettings.json if database settings don't exist.
    /// </summary>
    public class DatabaseEmailConfigurationSource : IConfigurationSource
    {
        private readonly string connectionString;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseEmailConfigurationSource"/> class.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="logger">Logger.</param>
        public DatabaseEmailConfigurationSource(string connectionString, ILogger logger)
        {
            this.connectionString = connectionString;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new DatabaseEmailConfigurationProvider(this.connectionString, this.logger);
        }
    }

    /// <summary>
    /// Configuration provider that reads email settings from the database.
    /// Loads settings from the Settings table (Group: EMAIL) and maps them to
    /// the format expected by Cosmos.EmailServices.
    /// </summary>
    public class DatabaseEmailConfigurationProvider : ConfigurationProvider
    {
        private readonly string connectionString;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseEmailConfigurationProvider"/> class.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="logger">Logger.</param>
        public DatabaseEmailConfigurationProvider(string connectionString, ILogger logger)
        {
            this.connectionString = connectionString;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public override void Load()
        {
            try
            {
                using var context = new ApplicationDbContext(this.connectionString);

                // Load email settings from database (Group: EMAIL)
                var emailSettings = context.Settings
                    .Where(s => s.Group == "EMAIL")
                    .ToList();

                if (!emailSettings.Any())
                {
                    this.logger.LogInformation("No email settings found in database - will use environment variables");
                    return;
                }

                // Map database settings to IConfiguration format expected by Cosmos.EmailServices
                foreach (var setting in emailSettings)
                {
                    switch (setting.Name)
                    {
                        case "AdminEmail":
                            Data["AdminEmail"] = setting.Value;
                            break;

                        case "SendGridApiKey":
                            Data["CosmosSendGridApiKey"] = setting.Value;
                            break;

                        case "AzureEmailConnectionString":
                            Data["ConnectionStrings:AzureCommunicationConnection"] = setting.Value;
                            break;

                        case "SmtpHost":
                            Data["SmtpEmailProviderOptions:Host"] = setting.Value;
                            break;

                        case "SmtpPort":
                            Data["SmtpEmailProviderOptions:Port"] = setting.Value;
                            break;

                        case "SmtpUsername":
                            Data["SmtpEmailProviderOptions:UserName"] = setting.Value;
                            break;

                        case "SmtpPassword":
                            Data["SmtpEmailProviderOptions:Password"] = setting.Value;
                            break;
                    }
                }

                this.logger.LogInformation("Loaded {Count} email settings from database", emailSettings.Count);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to load email settings from database - will use environment variables");
            }
        }
    }
}
