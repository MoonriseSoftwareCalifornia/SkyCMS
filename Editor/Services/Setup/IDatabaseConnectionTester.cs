// <copyright file="IDatabaseConnectionTester.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using System.Threading.Tasks;

    /// <summary>
    /// Tests whether a setup database connection string is valid.
    /// </summary>
    public interface IDatabaseConnectionTester
    {
        /// <summary>
        /// Validates that the provided connection string can be used for setup.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <returns>Connection test result.</returns>
        Task<TestResult> TestConnectionAsync(string connectionString);
    }
}
