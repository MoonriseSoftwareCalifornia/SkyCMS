// <copyright file="StorageContext.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Azure.Identity;
    using Cosmos.BlobService.Config;
    using Cosmos.BlobService.Drivers;
    using Cosmos.BlobService.Exceptions;
    using Cosmos.BlobService.Models;
    using Cosmos.DynamicConfig;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    ///     Multi cloud blob service context.
    /// </summary>
    public sealed class StorageContext : IStorageContext
    {
        /// <summary>
        /// Cache expiration time for storage drivers (1 hour).
        /// </summary>
        private static readonly TimeSpan DriverCacheExpiration = TimeSpan.FromHours(1);

        /// <summary>
        /// Singleton instance of <see cref="IPathValidator"/> used for all path validation.
        /// </summary>
        private static readonly IPathValidator PathValidator = new PathValidator();

        /// <summary>
        /// Used to brefly store chuk data while uploading.
        /// </summary>
        private readonly IMemoryCache memoryCache;

        /// <summary>
        /// Represents a provider for dynamic configuration settings.
        /// </summary>
        /// <remarks>This field holds a reference to an implementation of <see
        /// cref="IDynamicConfigurationProvider"/>. It is used to access configuration settings that can change at
        /// runtime.</remarks>
        private readonly IDynamicConfigurationProvider dynamicConfigurationProvider;
        private readonly object tenantDriverLock = new object();

        /// <summary>
        /// Multi-tenant editor flag.
        /// </summary>
        private bool isMultiTenant;

        private ICosmosStorage primaryDriver;
        private string cachedTenantDomain = string.Empty;
        private ICosmosStorage cachedTenantDriver;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageContext"/> class for multitenant instances.
        /// </summary>
        /// <param name="configuration">App configuration.</param>
        /// <param name="cache"><see cref="IMemoryCache"/>Memory cache.</param>
        /// <param name="serviceProvider">Services provider.</param>
        public StorageContext(
            IConfiguration configuration,
            IMemoryCache cache,
            IServiceProvider serviceProvider)
        {
            memoryCache = cache;
            isMultiTenant = configuration.GetValue<bool>("MultiTenantEditor", defaultValue: false);
            if (isMultiTenant)
            {
                // ✅ Multi-tenant: Use dynamic configuration provider (resolved per request)
                dynamicConfigurationProvider = serviceProvider.GetRequiredService<IDynamicConfigurationProvider>();

                // DON'T set primaryDriver here - it's resolved per request in GetPrimaryDriver()
            }
            else
            {
                var connectionString = configuration.GetConnectionString("StorageConnectionString")
                        ?? configuration.GetConnectionString("AzureBlobStorageConnectionString");

                primaryDriver = GetDriverFromConnectionString(connectionString);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageContext"/> class.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        /// <param name="cache">Memory cache.</param>
        public StorageContext(string connectionString, IMemoryCache cache)
        {
            isMultiTenant = false;
            memoryCache = cache;
            primaryDriver = GetDriverFromConnectionString(connectionString);
        }

        /// <summary>
        ///     Determines if a blob exists.
        /// </summary>
        /// <param name="path">Path check for a blob.</param>
        /// <returns><see cref="bool"/> indicating existence.</returns>
        /// <exception cref="StorageException">Thrown if the path fails validation.</exception>
        public async Task<bool> BlobExistsAsync(string path)
        {
            path = PathUtilities.NormalizePath(path);
            ValidatePathOrThrow(path);
            var driver = await this.GetPrimaryDriverAsync().ConfigureAwait(false);
            return await driver.BlobExistsAsync(path).ConfigureAwait(false);
        }

        /// <summary>
        ///     Copies a file or folder.
        /// </summary>
        /// <param name="target">Path to the file or folder to copy.</param>
        /// <param name="destination">Path to where to make the copy.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task CopyAsync(string target, string destination)
        {
            await this.CopyObjectsAsync(target, destination, false).ConfigureAwait(false);
        }

        /// <summary>
        ///     Delete a folder.
        /// </summary>
        /// <param name="path">Path to folder.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="StorageException">Thrown if the path fails validation.</exception>
        public async Task DeleteFolderAsync(string path)
        {
            // Ensure leading slash is removed.
            path = PathUtilities.NormalizePath(path);
            ValidatePathOrThrow(path);
            var driver = await GetPrimaryDriverAsync().ConfigureAwait(false);
            await driver.DeleteFolderAsync(path).ConfigureAwait(false);
        }

        /// <summary>
        ///     Deletes a file asynchronously.
        /// </summary>
        /// <param name="path">Path to file.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="StorageException">Thrown if the path fails validation.</exception>
        public async Task DeleteFileAsync(string path)
        {
            // Ensure leading slash is removed.
            path = PathUtilities.NormalizePath(path);
            ValidatePathOrThrow(path);

            var driver = await this.GetPrimaryDriverAsync().ConfigureAwait(false);
            await driver.DeleteIfExistsAsync(path).ConfigureAwait(false);
        }

        /// <summary>
        ///     Deletes a file synchronously.
        /// </summary>
        /// <param name="path">Path to file.</param>
        /// <remarks>
        /// This is a synchronous wrapper around <see cref="DeleteFileAsync"/>.
        /// Consider using <see cref="DeleteFileAsync"/> instead to avoid blocking.
        /// </remarks>
        [Obsolete("Use DeleteFileAsync instead to avoid blocking. This method will be removed in a future version.")]
        public void DeleteFile(string path)
        {
            DeleteFileAsync(path).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Enables the Azure BLOB storage static website.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task EnableAzureStaticWebsite()
        {
            var driver = await this.GetPrimaryDriverAsync().ConfigureAwait(false);
            if (driver != null && driver.GetType() == typeof(AzureStorage))
            {
                var azureStorage = (AzureStorage)driver;
                await azureStorage.EnableStaticWebsite().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Disables the static website (when login is required for example).
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task DisableAzureStaticWebsite()
        {
            var driver = await this.GetPrimaryDriverAsync().ConfigureAwait(false);
            if (driver != null && driver.GetType() == typeof(AzureStorage))
            {
                var azureStorage = (AzureStorage)driver;
                await azureStorage.DisableStaticWebsite().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets all the file names for a given path, including files in subfolders.
        /// </summary>
        /// <param name="path">Path to search.</param>
        /// <returns>List of files found including full path.</returns>
        /// <exception cref="StorageException">Thrown if the path fails validation.</exception>
        public async Task<List<string>> GetFilesAsync(string path)
        {
            var driver = await this.GetPrimaryDriverAsync().ConfigureAwait(false);
            path = PathUtilities.NormalizePath(path);
            ValidatePathOrThrow(path);
            var blobNames = await driver.GetBlobNamesByPath(path).ConfigureAwait(false);

            return blobNames.Where(w => !w.EndsWith(StorageConstants.FolderMarkerFile)).ToList();
        }

        /// <summary>
        ///     Gets the metadata for a file.
        /// </summary>
        /// <param name="path">Path to file.</param>
        /// <returns>File metadata as a <see cref="FileManagerEntry"/>.</returns>
        /// <exception cref="StorageException">Thrown if the path fails validation.</exception>
        public async Task<FileManagerEntry> GetFileAsync(string path)
        {
            // Ensure leading slash is removed.
            path = PathUtilities.NormalizePath(path);
            ValidatePathOrThrow(path);

            var driver = await this.GetPrimaryDriverAsync().ConfigureAwait(false);

            // Check if blob exists first to avoid exceptions
            if (!await driver.BlobExistsAsync(path).ConfigureAwait(false))
            {
                return null;
            }

            var metadata = await driver.GetFileMetadataAsync(path).ConfigureAwait(false);

            if (metadata == null)
            {
                return null;
            }

            var isDirectory = metadata.FileName.EndsWith(StorageConstants.FolderMarkerFile);
            var fileName = Path.GetFileName(metadata.FileName);
            var blobName = metadata.FileName;
            var hasDirectories = false;

            if (isDirectory)
            {
                var children = await driver.GetBlobNamesByPath(path).ConfigureAwait(false);
                hasDirectories = children.Any(c => c.EndsWith(StorageConstants.FolderMarkerFile));
            }

            var fileManagerEntry = new FileManagerEntry
            {
                Created = metadata.Created.UtcDateTime,
                CreatedUtc = metadata.Created.UtcDateTime,
                Extension = isDirectory ? string.Empty : Path.GetExtension(metadata.FileName),
                HasDirectories = hasDirectories,
                IsDirectory = isDirectory,
                Modified = metadata.LastModified.DateTime,
                ModifiedUtc = metadata.LastModified.UtcDateTime,
                Name = fileName,
                Path = blobName,
                Size = metadata.ContentLength,
                ETag = metadata.ETag,
                ContentType = metadata.ContentType
            };

            return fileManagerEntry;
        }

        /// <summary>
        ///     Returns a response stream from the primary blob storage provider.
        /// </summary>
        /// <param name="path">Path to blob to open read stream from.</param>
        /// <returns>Data as a <see cref="Stream"/>.</returns>
        /// <exception cref="StorageException">Thrown if the path fails validation.</exception>
        public async Task<Stream> GetStreamAsync(string path)
        {
            // Ensure leading slash is removed.
            path = PathUtilities.NormalizePath(path);
            ValidatePathOrThrow(path);

            // Get the primary driver based on the configuration.
            var driver = await GetPrimaryDriverAsync().ConfigureAwait(false);
            return await driver.GetStreamAsync(path).ConfigureAwait(false);
        }

        /// <summary>
        ///     Renames a file.
        /// </summary>
        /// <param name="sourceFile">Path to file.</param>
        /// <param name="destinationFile">Destination file name.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="StorageException">Thrown if paths fail validation.</exception>
        public async Task MoveFileAsync(string sourceFile, string destinationFile)
        {
            sourceFile = PathUtilities.NormalizePath(sourceFile);
            destinationFile = PathUtilities.NormalizePath(destinationFile);
            ValidatePathOrThrow(sourceFile);
            ValidatePathOrThrow(destinationFile);
            var driver = await GetPrimaryDriverAsync().ConfigureAwait(false);
            await driver.MoveFileAsync(sourceFile, destinationFile).ConfigureAwait(false);
        }

        /// <summary>
        ///     Moves a folder.
        /// </summary>
        /// <param name="sourceFolder">Source folder.</param>
        /// <param name="destinationFolder">Destination folder.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="StorageException">Thrown if paths fail validation.</exception>
        public async Task MoveFolderAsync(string sourceFolder, string destinationFolder)
        {
            sourceFolder = PathUtilities.NormalizePath(sourceFolder);
            destinationFolder = PathUtilities.NormalizePath(destinationFolder);
            ValidatePathOrThrow(sourceFolder);
            ValidatePathOrThrow(destinationFolder);
            var driver = await GetPrimaryDriverAsync().ConfigureAwait(false);
            await driver.MoveFolderAsync(sourceFolder, destinationFolder).ConfigureAwait(false);
        }

        /// <summary>
        ///     Append bytes to blob(s).
        /// </summary>
        /// <param name="stream"><see cref="MemoryStream"/> containing data being appended.</param>
        /// <param name="fileMetaData"><see cref="FileUploadMetaData"/> containing metadata about the data 'chunk' and blob.</param>
        /// <param name="mode">Is either append or block.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task AppendBlob(MemoryStream stream, FileUploadMetaData fileMetaData, string mode = StorageConstants.UploadModeAppend)
        {
            var mark = DateTimeOffset.UtcNow;

            // Gets the primary driver based on the configuration.
            var driver = await this.GetPrimaryDriverAsync().ConfigureAwait(false);

            await driver.AppendBlobAsync(stream.ToArray(), fileMetaData, mark, mode).ConfigureAwait(false);
        }

        /// <summary>
        ///     Creates a folder in all the cloud storage accounts.
        /// </summary>
        /// <param name="path">Path to the folder to create.</param>
        /// <returns>Folder metadata as a <see cref="FileManagerEntry"/>.</returns>
        /// <remarks>Creates the folder if it does not already exist.</remarks>
        /// <exception cref="StorageException">Thrown if the path fails validation.</exception>
        public async Task<FileManagerEntry> CreateFolder(string path)
        {
            path = PathUtilities.NormalizePath(path);
            ValidatePathOrThrow(path);
            var driver = await this.GetPrimaryDriverAsync().ConfigureAwait(false);
            var folderMarkerPath = path + "/" + StorageConstants.FolderMarkerFile;

            // Check if folder already exists using proper async/await
            var exists = await driver.BlobExistsAsync(folderMarkerPath).ConfigureAwait(false);
            if (!exists)
            {
                await driver.CreateFolderAsync(path).ConfigureAwait(false);
            }

            var parts = path.TrimEnd('/').Split('/');

            return new FileManagerEntry
            {
                Name = parts.Last(),
                Path = path,
                Created = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
                Extension = string.Empty,
                HasDirectories = false,
                Modified = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow,
                IsDirectory = true,
                Size = 0
            };
        }

        /// <summary>
        ///     Gets files and subfolders for a given path.
        /// </summary>
        /// <param name="path">Path to objects.</param>
        /// <returns>Returns metadata for the objects as a <see cref="FileManagerEntry"/> <see cref="List{T}"/>.</returns>
        /// <exception cref="StorageException">Thrown if the path fails validation.</exception>
        public async Task<List<FileManagerEntry>> GetFilesAndDirectories(string path)
        {
            path = PathUtilities.NormalizePath(path);
            ValidatePathOrThrow(path);

            var driver = await this.GetPrimaryDriverAsync().ConfigureAwait(false);
            var entries = await driver.GetFilesAndDirectories(path).ConfigureAwait(false);

            return entries;
        }

        /// <summary>
        /// Asynchronously copies objects from a source path to a destination path, with an option to delete the source
        /// objects after copying.
        /// </summary>
        /// <remarks>This method ensures that the leading slashes are removed from both the source and
        /// destination paths before processing. It checks for the existence of destination objects before copying and
        /// throws an exception if any destination object already exists. If the copy operation is successful and
        /// <paramref name="deleteSource"/> is <see langword="true"/>, the source objects are deleted.</remarks>
        /// <param name="target">The source path from which objects are to be copied. Must not be null or empty, and cannot be the root
        /// folder.</param>
        /// <param name="destination">The destination path to which objects are to be copied. Must not be null or empty.</param>
        /// <param name="deleteSource">A boolean value indicating whether to delete the source objects after a successful copy. If <see
        /// langword="true"/>, the source objects will be deleted; otherwise, they will be retained.</param>
        /// <returns>Task.</returns>
        /// <exception cref="StorageException">Thrown if the <paramref name="target"/> is null or empty, if the root folder is specified as the target, or
        /// if a destination object already exists.</exception>
        private async Task CopyObjectsAsync(string target, string destination, bool deleteSource)
        {
            // Make sure leading slashes are removed.
            target = PathUtilities.NormalizePath(target);
            destination = PathUtilities.NormalizePath(destination);

            if (string.IsNullOrEmpty(target))
            {
                throw new StorageException("Cannot move the root folder.");
            }

            // Get the blob storage drivers.
            var driver = await this.GetPrimaryDriverAsync().ConfigureAwait(false);
            var blobNames = await driver.GetBlobNamesByPath(target).ConfigureAwait(false);

            // Work through the list here.
            foreach (var srcBlobName in blobNames)
            {
                var destBlobName = srcBlobName.Replace(target, destination);

                if (await driver.BlobExistsAsync(destBlobName).ConfigureAwait(false))
                {
                    throw new StorageException($"Could not copy {srcBlobName} as {destBlobName} already exists.");
                }

                await driver.CopyBlobAsync(srcBlobName, destBlobName).ConfigureAwait(false);

                // Now check to see if files were copied
                var success = await driver.BlobExistsAsync(destBlobName).ConfigureAwait(false);

                if (success)
                {
                    // Deleting the source is in the case of RENAME.
                    // Copying things does not delete the source
                    if (deleteSource)
                    {
                        await driver.DeleteIfExistsAsync(srcBlobName).ConfigureAwait(false);
                    }
                }
                else
                {
                    // The copy was NOT successfull, delete any copied files and halt, throw an error.
                    await driver.DeleteIfExistsAsync(destBlobName).ConfigureAwait(false);
                    throw new StorageException($"Could not copy: {srcBlobName} to {destBlobName}");
                }
            }
        }

        /// <summary>
        /// Gets the primary driver based on the configuration.
        /// </summary>
        /// <returns>Storage driver.</returns>
        private async Task<ICosmosStorage> GetPrimaryDriverAsync()
        {
            if (this.isMultiTenant == true)
            {
                var tenantDomain = this.dynamicConfigurationProvider.GetTenantDomainNameFromRequest();

                if (!string.IsNullOrWhiteSpace(tenantDomain))
                {
                    lock (tenantDriverLock)
                    {
                        if (cachedTenantDriver != null &&
                            tenantDomain.Equals(cachedTenantDomain, StringComparison.OrdinalIgnoreCase))
                        {
                            return cachedTenantDriver;
                        }
                    }
                }

                var connectionString = await this.dynamicConfigurationProvider
                    .GetStorageConnectionStringAsync(tenantDomain)
                    .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new TenantResolutionException(
                        "Cannot resolve tenant storage connection. Ensure HttpContext is available or provide domain explicitly. " +
                        "For background jobs, consider storing domain context before invoking storage operations.");
                }

                var driver = GetOrCreateCachedDriver(connectionString);

                if (!string.IsNullOrWhiteSpace(tenantDomain))
                {
                    lock (tenantDriverLock)
                    {
                        cachedTenantDomain = tenantDomain;
                        cachedTenantDriver = driver;
                    }
                }

                return driver;
            }

            return primaryDriver;
        }

        /// <summary>
        /// Gets or creates a cached storage driver for the given connection string.
        /// </summary>
        /// <param name="connectionString">The storage connection string.</param>
        /// <returns>A cached or newly created <see cref="ICosmosStorage"/> instance.</returns>
        /// <remarks>
        /// This method uses memory cache to store driver instances, preventing repeated instantiation
        /// for the same connection string. Cached drivers expire after the configured timeout.
        /// </remarks>
        private ICosmosStorage GetOrCreateCachedDriver(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidConnectionStringException("Connection string cannot be null or empty.");
            }

            var cacheKey = CreateDriverCacheKey(connectionString);

            // Try to get the driver from cache
            if (!memoryCache.TryGetValue(cacheKey, out ICosmosStorage driver))
            {
                // Driver not in cache, create new instance
                driver = GetDriverFromConnectionString(connectionString);

                // Set cache options
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(DriverCacheExpiration);

                // Save driver in cache
                memoryCache.Set(cacheKey, driver, cacheEntryOptions);
            }

            return driver;
        }

        private string CreateDriverCacheKey(string connectionString)
        {
            var bytes = Encoding.UTF8.GetBytes(connectionString);
            var hashBytes = SHA256.HashData(bytes);
            var hash = Convert.ToHexString(hashBytes);
            return StorageConstants.DriverCacheKeyPrefix + hash;
        }

        /// <summary>
        /// Creates a storage driver from a connection string.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        /// <returns>ICosmosStorage driver instance.</returns>
        /// <exception cref="InvalidConnectionStringException">Thrown when connection string format is invalid or missing required parameters.</exception>
        /// <exception cref="UnsupportedStorageProviderException">Thrown when the storage provider is not supported.</exception>
        private ICosmosStorage GetDriverFromConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return null;
            }

            var provider = ConnectionStringParser.DetermineProvider(connectionString);

            switch (provider)
            {
                case CloudStorageProvider.Azure:
                    var isAzurite = ConnectionStringParser.IsAzurite(connectionString);
                    var credential = isAzurite ? null : new DefaultAzureCredential();
                    return new AzureStorage(connectionString, credential);

                case CloudStorageProvider.CloudflareR2:
                case CloudStorageProvider.AmazonS3:
                    var amazonComponents = ConnectionStringParser.ParseAmazonConnectionString(connectionString);
                    return new AmazonStorage(
                        new AmazonStorageConfig
                        {
                            AccountId = amazonComponents.AccountId,
                            AmazonRegion = amazonComponents.Region,
                            BucketName = amazonComponents.BucketName,
                            KeyId = amazonComponents.KeyId,
                            Key = amazonComponents.Key
                        },
                        memoryCache);

                default:
                    throw new UnsupportedStorageProviderException(
                        "No valid storage connection string found. Please check your configuration. " +
                        "Supported formats: Azure Blob Storage (DefaultEndpointsProtocol=...), " +
                        "Amazon S3 (Bucket=...;Region=... or AccountId=...;Bucket=...).")
                    {
                        Provider = provider
                    };
            }
        }

        /// <summary>
        /// Validates a normalized path and throws a StorageException if validation fails.
        /// </summary>
        /// <param name="path">The normalized path to validate.</param>
        /// <exception cref="StorageException">Thrown if the path fails validation.</exception>
        private static void ValidatePathOrThrow(string path)
        {
            var validationResult = PathValidator.ValidatePath(path);
            if (!validationResult.IsValid)
            {
                throw new StorageException($"Invalid path: {validationResult.ErrorMessage}");
            }
        }
    }
}
