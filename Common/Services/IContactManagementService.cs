// <copyright file="IContactManagementService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services
{
    using System.Threading.Tasks;
    using Cosmos.Common.Models;

    /// <summary>
    /// Service for managing contacts, including adding and updating contact information, and integrating with MailChimp for email marketing.
    /// </summary>
    public interface IContactManagementService
    {
        /// <summary>
        /// Adds a new contact or updates an existing contact in the database and MailChimp list.
        /// </summary>
        /// <param name="model">Post model.</param>
        /// <returns>ContactViewModel.</returns>
        Task<ContactViewModel> AddContactAsync(ContactViewModel model);
    }
}