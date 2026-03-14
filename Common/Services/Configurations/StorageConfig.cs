// <copyright file="StorageConfig.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Common.Services.Configurations
{
    using System;
    using System.Collections.Generic;
    using Cosmos.Cms.Common.Services.Configurations.Storage;

    /// <summary>
    ///     Storage provider configuration.
    /// </summary>
    /// <remarks>
    /// This class is marked as obsolete and will be removed in a future version.
    /// Storage configuration has been moved to Cosmos.BlobService project.
    /// Please use the storage configuration provided by Cosmos.BlobService instead.
    /// </remarks>
    [Obsolete("This class is unused and superseded by Cosmos.BlobService configuration. It will be removed in a future version.", false)]
    public class StorageConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StorageConfig"/> class.
        /// </summary>
        public StorageConfig()
        {
            AzureConfigs = new List<AzureStorageConfig>();
        }

        /// <summary>
        ///     Gets or sets azure configuration.
        /// </summary>
        public List<AzureStorageConfig> AzureConfigs { get; set; }
    }
}
