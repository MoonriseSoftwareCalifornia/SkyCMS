// <copyright file="TableOfContents.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Table of context entity.
    /// </summary>
    public class TableOfContents
    {
        /// <summary>
        /// Gets or sets current page number.
        /// </summary>
        public int PageNo { get; set; }

        /// <summary>
        /// Gets or sets page size.
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Gets or sets total number of items.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Gets or sets the publisher URL of the table of contents.
        /// </summary>
        public string PublisherUrl { get; set; }

        /// <summary>
        /// Gets or sets the static web URL.
        /// </summary>
        public string BlobPublicUrl { get; set; }

        /// <summary>
        /// Gets or sets items in the current page.
        /// </summary>
        public List<TableOfContentsItem> Items { get; set; } = new List<TableOfContentsItem>();
    }
}
