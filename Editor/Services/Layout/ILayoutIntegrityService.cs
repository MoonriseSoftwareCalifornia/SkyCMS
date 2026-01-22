// <copyright file="ILayoutIntegrityService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Layout
{
    using System.Threading.Tasks;

    /// <summary>
    /// Service for verifying data integrity of layouts and templates.
    /// </summary>
    public interface ILayoutIntegrityService
    {
        /// <summary>
        /// Performs a comprehensive integrity check on layouts and templates.
        /// </summary>
        /// <returns>Results of the integrity check.</returns>
        Task<IntegrityCheckResult> CheckIntegrityAsync();

        /// <summary>
        /// Checks if layouts have valid LayoutNumber assignments.
        /// </summary>
        /// <returns>Results of the layout number validation.</returns>
        Task<IntegrityCheckResult> ValidateLayoutNumbersAsync();

        /// <summary>
        /// Checks if templates reference valid layouts.
        /// </summary>
        /// <returns>Results of the template validation.</returns>
        Task<IntegrityCheckResult> ValidateTemplateReferencesAsync();

        /// <summary>
        /// Checks if layout families have consistent IsDefault flags.
        /// </summary>
        /// <returns>Results of the family consistency check.</returns>
        Task<IntegrityCheckResult> ValidateFamilyConsistencyAsync();
    }
}