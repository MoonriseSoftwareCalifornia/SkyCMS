// <copyright file="ResilientEntityFrameworkCoreXmlRepository.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data
{
    using System;
    using System.Collections.Generic;
    using System.Xml.Linq;
    using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
    using Microsoft.AspNetCore.DataProtection.Repositories;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// A resilient XML repository wrapper that handles Cosmos DB conflicts gracefully.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    public class ResilientEntityFrameworkCoreXmlRepository<TContext> : IXmlRepository
        where TContext : DbContext, IDataProtectionKeyContext
    {
        private readonly IXmlRepository innerRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResilientEntityFrameworkCoreXmlRepository{TContext}"/> class.
        /// </summary>
        /// <param name="services">The service provider.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        public ResilientEntityFrameworkCoreXmlRepository(IServiceProvider services, ILoggerFactory loggerFactory)
        {
            this.innerRepository = new EntityFrameworkCoreXmlRepository<TContext>(services, loggerFactory);
        }

        /// <summary>
        /// Gets all XML elements from the repository.
        /// </summary>
        /// <returns>The XML elements.</returns>
        public IReadOnlyCollection<XElement> GetAllElements()
        {
            return this.innerRepository.GetAllElements();
        }

        /// <summary>
        /// Stores an XML element, handling conflicts gracefully.
        /// </summary>
        /// <param name="element">The element to store.</param>
        /// <param name="friendlyName">The friendly name.</param>
        public void StoreElement(XElement element, string friendlyName)
        {
            try
            {
                this.innerRepository.StoreElement(element, friendlyName);
            }
            catch (DbUpdateException ex)
            {
                // If the conflict is due to a duplicate key (409), it means the key already exists
                // from another concurrent request. This is safe to ignore since the key is already stored.
                if (ex.InnerException?.Message.Contains("409") == true ||
                    ex.Message.Contains("already exists") ||
                    ex.InnerException?.Message.Contains("already exists") == true)
                {
                    // Log this silently - the key was already created by another request
                    System.Diagnostics.Debug.WriteLine($"Data protection key already exists (conflict on {friendlyName}), skipping store operation");
                }
                else
                {
                    // Re-throw if it's a different kind of error
                    throw;
                }
            }
        }
    }
}
