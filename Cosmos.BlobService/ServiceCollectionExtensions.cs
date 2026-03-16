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
    using System;

    /// <summary>
    /// Adds the Cosmos Storage Context to the Services Collection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        private const string DefaultDataProtectionApplicationName = "SkyCMS";

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
                .SetApplicationName(GetDataProtectionApplicationName(config))
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
        /// Deprecated middleware hook retained for backward compatibility.
        /// </summary>
        /// <param name="app">Application builder.</param>
        /// <returns>IApplicationBuilder.</returns>
        [Obsolete("Per-request data protection discriminator mutation is not supported. This method is now a no-op and will be removed in a future version.")]
        public static IApplicationBuilder UseCosmosCmsDataProtection(this IApplicationBuilder app)
        {
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
                ? config.GetConnectionString(StorageConstants.ConnectionStringKey_DataProtection)
                : GetConnectionString(config);
        }

        /// <summary>
        /// Gets a stable application name used to scope data protection keys.
        /// </summary>
        /// <param name="config">Configuration.</param>
        /// <returns>Data protection application name.</returns>
        private static string GetDataProtectionApplicationName(IConfiguration config)
        {
            return config.GetValue<string>("DataProtection:ApplicationName") ?? DefaultDataProtectionApplicationName;
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
