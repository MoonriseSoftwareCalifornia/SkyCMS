// <copyright file="ArticlePermission.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data
{
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Article permission for a role or user.
    /// </summary>
    public class ArticlePermission
    {
        /// <summary>
        /// Gets or sets the article number.
        /// </summary>
        [Key]
        public int ArticleId { get; set; }

        /// <summary>
        /// Gets or sets role or user ID.
        /// </summary>
        public string IdentityObjectId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets permission (Read or Upload).
        /// </summary>
        public string Permission { get; set; } = "Read";

        /// <summary>
        /// Gets or sets a value indicating whether gets or sets if this is a role permission object.
        /// </summary>
        public bool IsRoleObject { get; set; } = true;
    }
}
