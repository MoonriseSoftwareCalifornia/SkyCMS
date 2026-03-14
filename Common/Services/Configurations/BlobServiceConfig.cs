// <copyright file="BlobServiceConfig.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Cms.Common.Services.Configurations
{
    using System;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    ///     Azure blob storage config.
    /// </summary>
    /// <remarks>
    /// This class is marked as obsolete and will be removed in a future version.
    /// Storage configuration has been moved to Cosmos.BlobService project.
    /// Please use the storage configuration provided by Cosmos.BlobService instead.
    /// </remarks>
    [Obsolete("This class is unused and superseded by Cosmos.BlobService configuration. It will be removed in a future version.", false)]
    public class BlobServiceConfig
    {
        /// <summary>
        ///     Gets or sets id of the provider.
        /// </summary>
        [Key]
        [Display(Name = "Id")]
        public int Id { get; set; }

        /// <summary>
        ///     Gets or sets cloud provider.
        /// </summary>
        [Required]
        [UIHint("CloudProvider")]
        [Display(Name = "Cloud Provider")]
        public string CloudName { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether is primary storage for this website.
        /// </summary>
        public bool IsPrimary { get; set; }

        /// <summary>
        ///     Gets or sets blob storage connection string.
        /// </summary>
        [Required]
        [Display(Name = "Blob storage connection string")]
        public string ConnectionString { get; set; }
    }
}
