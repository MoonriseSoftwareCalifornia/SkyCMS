// <copyright file="AzureStorageConfig.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Common.Services.Configurations.Storage
{
    using System;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    ///     Azure storage config.
    /// </summary>
    /// <remarks>
    /// This class is marked as obsolete and will be removed in a future version.
    /// Storage configuration has been moved to Cosmos.BlobService project.
    /// Please use the storage configuration provided by Cosmos.BlobService instead.
    /// </remarks>
    [Obsolete("This class is unused and superseded by Cosmos.BlobService configuration. It will be removed in a future version.", false)]
    public class AzureStorageConfig
    {
        /// <summary>
        ///     Gets or sets connection string.
        /// </summary>
        [Display(Name = "Conn. String")]
        public string AzureBlobStorageConnectionString { get; set; }

        /// <summary>
        ///     Gets or sets container name.
        /// </summary>
        [Display(Name = "Container")]
        public string AzureBlobStorageContainerName { get; set; } = "$web";

        /// <summary>
        ///     Gets or sets storage end point.
        /// </summary>
        [Display(Name = "Website URL")]
        public string AzureBlobStorageEndPoint { get; set; } = "/";
    }
}
