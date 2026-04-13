// <copyright file="EditorResponse.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Unified response model for all editor save operations.
    /// </summary>
    /// <typeparam name="T">Type of model returned on success.</typeparam>
    public class EditorResponse<T>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the save was successful.
        /// </summary>
        public bool ServerSideSuccess { get; set; }

        /// <summary>
        /// Gets or sets the updated article model (on success).
        /// </summary>
        public T Model { get; set; }

        /// <summary>
        /// Gets or sets CDN flush results if applicable.
        /// </summary>
        public List<CdnFlushResult> CdnResults { get; set; }

        /// <summary>
        /// Gets or sets validation errors keyed by field name.
        /// </summary>
        public Dictionary<string, string[]> Errors { get; set; }

        /// <summary>
        /// Gets or sets optional error message for non-validation errors.
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// CDN flush result.
    /// </summary>
    public class CdnFlushResult
    {
        /// <summary>
        /// Gets or sets provider name.
        /// </summary>
        public string ProviderName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this response represents a success status code.
        /// </summary>
        public bool IsSuccessStatusCode { get; set; }

        /// <summary>
        /// Gets or sets status message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets estimated flush date/time.
        /// </summary>
        public DateTime? EstimatedFlushDateTime { get; set; }
    }
}
