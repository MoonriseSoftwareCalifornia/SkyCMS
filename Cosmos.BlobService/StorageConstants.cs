// <copyright file="StorageConstants.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService;

/// <summary>
/// Contains constant values used throughout the blob storage service.
/// </summary>
public static class StorageConstants
    {
        /// <summary>
        /// Default Azure blob container name for static websites.
        /// </summary>
        public const string DefaultWebContainer = "$web";

        /// <summary>
        /// Container name for data protection keys.
        /// </summary>
        public const string DataProtectionContainer = "dpkeys";

        /// <summary>
        /// Blob file name for data protection keys.
        /// </summary>
        public const string DataProtectionKeysFile = "keys.xml";

        /// <summary>
        /// Marker file name used to represent empty folders in blob storage.
        /// </summary>
        public const string FolderMarkerFile = "folder.stubxx";

        /// <summary>
        /// Metadata key for upload unique identifier.
        /// </summary>
        public const string MetadataUploadUid = "ccmsuploaduid";

        /// <summary>
        /// Metadata key for file size.
        /// </summary>
        public const string MetadataSize = "ccmssize";

        /// <summary>
        /// Metadata key for upload date/time (stored as ticks).
        /// </summary>
        public const string MetadataDateTime = "ccmsdatetime";

        /// <summary>
        /// Metadata key for image width.
        /// </summary>
        public const string MetadataImageWidth = "ccmsimagewidth";

        /// <summary>
        /// Metadata key for image height.
        /// </summary>
        public const string MetadataImageHeight = "ccmsimageheight";

        /// <summary>
        /// Upload mode: append chunks to an append blob.
        /// </summary>
        public const string UploadModeAppend = "append";

        /// <summary>
        /// Upload mode: upload as a block blob.
        /// </summary>
        public const string UploadModeBlock = "block";

        /// <summary>
        /// Cache key prefix for storage drivers in multi-tenant scenarios.
        /// </summary>
        internal const string DriverCacheKeyPrefix = "StorageDriver_";

        /// <summary>
        /// Connection string key for primary storage.
        /// </summary>
        public const string ConnectionStringKey_Storage = "StorageConnectionString";

        /// <summary>
        /// Connection string key for Azure blob storage.
        /// </summary>
        public const string ConnectionStringKey_AzureBlob = "AzureBlobStorageConnectionString";

            /// <summary>
            /// Connection string key for data protection storage.
            /// </summary>
            public const string ConnectionStringKey_DataProtection = "DataProtectionStorage";
        }
