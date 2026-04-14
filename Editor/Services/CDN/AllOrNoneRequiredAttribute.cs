// <copyright file="AllOrNoneRequiredAttribute.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>
namespace Sky.Editor.Services.CDN
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;

    /// <summary>
    ///  Checks if all specified properties are either all filled or all empty.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class AllOrNoneRequiredAttribute : ValidationAttribute
    {
        private readonly string[] propertyNames;

        /// <summary>
        ///  Initializes a new instance of the <see cref="AllOrNoneRequiredAttribute"/> class.
        /// </summary>
        /// <param name="propertyNames">Property names to check.</param>
        public AllOrNoneRequiredAttribute(params string[] propertyNames)
        {
            this.propertyNames = propertyNames;
        }

        /// <summary>
        ///  Validates that either all or none of the specified properties are filled.
        /// </summary>
        /// <param name="value">Object to check.</param>
        /// <param name="validationContext">Calidation context.</param>
        /// <returns>Result.</returns>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var values = propertyNames
                .Select(name => validationContext.ObjectType.GetProperty(name)?.GetValue(validationContext.ObjectInstance))
                .ToList();

            bool allFilled = values.All(v => v != null && !string.IsNullOrWhiteSpace(v.ToString()));
            bool allEmpty = values.All(v => v == null || string.IsNullOrWhiteSpace(v.ToString()));

            if (allFilled || allEmpty)
            {
                return ValidationResult.Success;
            }

            return new ValidationResult($"Either all or none of the fields [{string.Join(", ", propertyNames)}] must be filled.");
        }
    }
}
