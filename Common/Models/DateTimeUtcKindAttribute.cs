// <copyright file="DateTimeUtcKindAttribute.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    ///     Ensures that a DateTime object is of kind UTC.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DateTimeUtcKindAttribute : ValidationAttribute
    {
        /// <summary>
        ///     Determines if value is valid.
        /// </summary>
        /// <param name="value">Value to test.</param>
        /// <param name="validationContext">Validation context.</param>
        /// <returns>Test results returned as a <see cref="ValidationResult"/>.</returns>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            var t = value.GetType();

            if (t == typeof(DateTimeOffset))
            {
                return ValidationResult.Success;
            }

            if (t == typeof(DateTime?) || t == typeof(DateTime))
            {
                var dateTime = (DateTime?)value;

                if (dateTime.HasValue && dateTime.Value.Kind != DateTimeKind.Utc)
                {
                    return new ValidationResult($"Must be DateTimeKind.Utc, not {dateTime.Value.Kind}.");
                }
            }

            return ValidationResult.Success;
        }
    }
}
