// <copyright file="ServiceCollectionExtensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService
{
    using Azure.Identity;
    using Azure.Storage.Blobs;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
    using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using System;

    /// <summary>
    /// Adds the Cosmos Storage Context to the Services Collection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the storage context to the services collection.
        /// </summary>
        /// <param name="services">Services collection.</param>
        /// <param name="config">Startup configuration.</param>
        public static void AddCosmosStorageContext(this IServiceCollection services, IConfiguration config)
        {
            services.AddTransient<StorageContext>();
        }

        /// <summary>
        /// Adds the data protection service for Cosmos CMS to the services collection.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="config">Configuration.</param>
        /// <param name="defaultAzureCredential">Default azure credential.</param>
        /// <exception cref="ArgumentNullException">Returns error if no connection string found.</exception>
        public static void AddCosmosCmsDataProtection(this IServiceCollection services, IConfiguration config, DefaultAzureCredential defaultAzureCredential)
        {
            var connectionString = GetDataProtectionConnectionString(config);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(
                    nameof(connectionString),
                    "'DataProtectionStorage' or 'StorageConnectionString' connection string is not set.");
            }

            var blobClient = GetDataProtectionBlobClient(connectionString, defaultAzureCredential);

            services.AddDataProtection()
                .UseCryptographicAlgorithms(
                new AuthenticatedEncryptorConfiguration
                {
                    EncryptionAlgorithm = EncryptionAlgorithm.AES_256_CBC,
                    ValidationAlgorithm = ValidationAlgorithm.HMACSHA256
                })
                .PersistKeysToAzureBlobStorage(blobClient);
        }

        /// <summary>
        /// Adds the storage context to the services collection.
        /// </summary>
        /// <param name="config">Startup configuration.</param>
        /// <param name="defaultAzureCredential">Default Azure token credential.</param>
        /// <param name="container">The container to use.</param>
        /// <returns>Blob service client.</returns>
        public static BlobContainerClient GetBlobContainerClient(IConfiguration config, DefaultAzureCredential defaultAzureCredential, string container = StorageConstants.DefaultWebContainer)
        {
            var connectionString = GetConnectionString(config);
            var blobServiceClient = ConnectionStringParser.CreateBlobServiceClient(connectionString, defaultAzureCredential);
            return blobServiceClient.GetBlobContainerClient(container);
        }

        /// <summary>
        /// Sets the application discriminator for the data protection keys based on the domain name.
        /// </summary>
        /// <param name="app">Application builder.</param>
        /// <returns>IApplicationBuilder.</returns>
        public static IApplicationBuilder UseCosmosCmsDataProtection(this IApplicationBuilder app)
        {
            app.Use(async (context, next) =>
            {
                var dataProtectionOptions = context.RequestServices.GetRequiredService<IOptions<DataProtectionOptions>>().Value;

                // Set the ApplicationDiscriminator based on the domain name
                var domainName = context.Request.Host.Host;
                dataProtectionOptions.ApplicationDiscriminator = domainName;

                await next();
            });

            return app;
        }

        private static string GetConnectionString(IConfiguration config)
        {
            return config.GetConnectionString(StorageConstants.ConnectionStringKey_Storage) ??
                   config.GetConnectionString(StorageConstants.ConnectionStringKey_AzureBlob);
        }

        /// <summary>
        /// Gets the connection string for data protection storage, checking multi-tenant configuration first.
        /// </summary>
        /// <param name="config">Configuration.</param>
        /// <returns>The connection string for data protection storage.</returns>
        private static string GetDataProtectionConnectionString(IConfiguration config)
        {
            var isMultiTenant = config.GetValue<bool?>("MultiTenantEditor") ?? false;

            return isMultiTenant
                ? config.GetConnectionString("DataProtectionStorage")
                : GetConnectionString(config);
        }

        /// <summary>
        /// Creates and initializes a blob client for data protection keys.
        /// </summary>
        /// <param name="connectionString">The storage connection string.</param>
        /// <param name="defaultAzureCredential">Default Azure credential.</param>
        /// <returns>A configured <see cref="BlobClient"/> for the data protection keys file.</returns>
        private static BlobClient GetDataProtectionBlobClient(
            string connectionString,
            DefaultAzureCredential defaultAzureCredential)
        {
            var blobServiceClient = ConnectionStringParser.CreateBlobServiceClient(connectionString, defaultAzureCredential);
            var containerClient = blobServiceClient.GetBlobContainerClient(StorageConstants.DataProtectionContainer);
            containerClient.CreateIfNotExists();
            return containerClient.GetBlobClient(StorageConstants.DataProtectionKeysFile);
        }
    }
}
