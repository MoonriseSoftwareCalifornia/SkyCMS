// <copyright file="Contact.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data
{
    using System;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Contact record.
    /// </summary>
    public class Contact
    {
        /// <summary>
        ///     Gets or sets unique article entity primary key number (not to be confused with article number).
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets customer first name.
        /// </summary>
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets customer last name.
        /// </summary>
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a customer's email address.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the customer's phone number.
        /// </summary>
        [Required(AllowEmptyStrings = true)]
        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when this record was created.
        /// </summary>
        public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the record update date and time.
        /// </summary>
        public DateTimeOffset Updated { get; set; } = DateTimeOffset.UtcNow;
    }
}
