// <copyright file="GoogleStorageConfig.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the GNU Public License, Version 3.0 (https://www.gnu.org/licenses/gpl-3.0.html)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.BlobService.Config
{
    using System;

    /// <summary>
    ///     Google configuration.
    /// </summary>
    [Obsolete("This class is not used and will be removed in a future version. Use connection strings instead.")]
    public class GoogleStorageConfig
    {
        /// <summary>
        ///     Gets or sets project Id.
        /// </summary>
        public string GoogleProjectId { get; set; }

        /// <summary>
        ///     Gets or sets jSON authorization path.
        /// </summary>
        public string GoogleJsonAuthPath { get; set; }

        /// <summary>
        ///     Gets or sets bucket name.
        /// </summary>
        public string GoogleBucketName { get; set; }
    }
}
