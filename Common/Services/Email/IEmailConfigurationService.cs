// <copyright file="IEmailConfigurationService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.Email;

using System.Threading.Tasks;

/// <summary>
/// Service interface for retrieving email configuration.
/// </summary>
public interface IEmailConfigurationService
{
    /// <summary>
    /// Gets email settings from environment variables or database.
    /// </summary>
    /// <returns>Email settings.</returns>
    Task<EmailSettings> GetEmailSettingsAsync();
}