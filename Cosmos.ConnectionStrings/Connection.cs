// <copyright file="ConnectionStringProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Cosmos.DynamicConfig.Validation;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.DynamicConfig
{
    public class Connection
    {
        [Key]
        [Display(Name = "ID")]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets a value indicating whether the website is allowed to perform setup tasks.
        /// </summary>
        public bool AllowSetup { get; set; } = true;

        /// <summary>
        /// Gets or sets the editor domain name of the connection.
        /// </summary>
        [Display(Name = "Editor Domain Names")]
        public string[] DomainNames { get; set; } = null!;

        /// <summary>
        /// Gets or sets the database connection string.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        [Display(Name = "Database Connection String")]
        public string DbConn { get; set; } = null!;

        /// <summary>
        /// Gets or sets the storage connection string.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        [Display(Name = "Storage Connection String")]
        public string StorageConn { get; set; } = null!;

        /// <summary>
        /// Gets or sets the customer name.
        /// </summary>
        [Display(Name = "Website Owner Name")]
        public string? Customer { get; set; } = null;

        /// <summary>
        /// Gets or sets the resrouce group where the customer's resources are kept.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        [Display(Name = "Customer Resource Group")]
        public string? ResourceGroup { get; set; } = null;

        /// <summary>
        /// Gets or sets the publisher mode.
        /// </summary>
        [AllowedValues("Static", "Decoupled", "Headless", "Hybrid", "Static-dynamic", "")]
        public string PublisherMode { get; set; } = "Static";

        /// <summary>
        /// Gets or sets the public URL for the storage account. (if a static website, this can be left blank).
        /// </summary>
        public string BlobPublicUrl { get; set; } = "/";

        /// <summary>
        /// Gets or sets the microsoft application ID used for application verification.
        /// </summary>
        public string MicrosoftAppId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the publisher requires authentication.
        /// </summary>
        public bool PublisherRequiresAuthentication { get; set; } = false;

        /// <summary>
        /// Gets or sets the website URL.
        /// </summary>
        [HttpUrl]
        [Required(AllowEmptyStrings = false)]
        [Display(Name = "Website URL")]
        public string WebsiteUrl { get; set; } = null!;

        [EmailAddress]
        [Display(Name = "Website Owner Email")]
        public string? OwnerEmail { get; set; } = null;
    }
}
