// <copyright file="RedirectCreationResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Titles
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents the result of a redirect creation operation, tracking successes and failures.
    /// </summary>
    /// <remarks>
    /// This class is used to provide detailed feedback about redirect creation during title
    /// change operations. It allows callers to determine which redirects were successfully
    /// created and which failed, along with error details for troubleshooting.
    /// </remarks>
    public sealed class RedirectCreationResult
    {
        /// <summary>
        /// Gets or sets the number of redirects successfully created.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the number of redirect operations that were skipped.
        /// </summary>
        /// <remarks>
        /// Redirects may be skipped for several reasons:
        /// <list type="bullet">
        /// <item><description>The old and new URLs are identical (identity redirect)</description></item>
        /// <item><description>Duplicate old URLs where only the last one was processed</description></item>
        /// </list>
        /// </remarks>
        public int SkippedCount { get; set; }

        /// <summary>
        /// Gets the list of redirects that failed to be created.
        /// </summary>
        /// <remarks>
        /// Each tuple contains:
        /// <list type="bullet">
        /// <item><description>Item1: The article number for diagnostic purposes</description></item>
        /// <item><description>Item2: The old URL that was supposed to redirect</description></item>
        /// <item><description>Item3: The new URL (redirect target)</description></item>
        /// <item><description>Item4: The error message describing why it failed</description></item>
        /// </list>
        /// </remarks>
        public List<(int ArticleNumber, string OldUrl, string NewUrl, string Error)> FailedRedirects { get; } = new();

        /// <summary>
        /// Gets a value indicating whether all redirects were created successfully.
        /// </summary>
        public bool AllSucceeded => FailedRedirects.Count == 0;

        /// <summary>
        /// Gets the total number of redirect operations attempted.
        /// </summary>
        public int TotalAttempted => SuccessCount + SkippedCount + FailedRedirects.Count;
    }
}
