// <copyright file="GetTemplateListQuery.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Features.Templates.GetList
{
    using Cosmos.Common.Features.Shared;

    /// <summary>
    /// Query to retrieve a paginated, sorted list of templates.
    /// </summary>
    public class GetTemplateListQuery : IQuery<CommandResult<GetTemplateListQueryResult>>
    {
        /// <summary>
        /// Gets or sets the sort order (asc or desc).
        /// </summary>
        public string SortOrder { get; set; } = "asc";

        /// <summary>
        /// Gets or sets the field to sort by (Title, Description, LayoutName).
        /// </summary>
        public string CurrentSort { get; set; } = "Title";

        /// <summary>
        /// Gets or sets the page number (0-based).
        /// </summary>
        public int PageNo { get; set; } = 0;

        /// <summary>
        /// Gets or sets the page size.
        /// </summary>
        public int PageSize { get; set; } = 10;

        /// <summary>
        /// Gets or sets optional layout ID filter.
        /// If null, gets templates for current layout.
        /// </summary>
        public System.Guid? LayoutId { get; set; }
    }
}
