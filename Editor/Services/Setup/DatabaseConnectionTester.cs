// <copyright file="DatabaseConnectionTester.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Setup
{
    using System.Threading.Tasks;
    using Cosmos.Common.Data;

    /// <summary>
    /// Default runtime implementation for setup database connectivity validation.
    /// </summary>
    public class DatabaseConnectionTester : IDatabaseConnectionTester
    {
        /// <inheritdoc/>
        public async Task<TestResult> TestConnectionAsync(string connectionString)
        {
            using var context = new ApplicationDbContext(connectionString);
            var canConnect = await context.Database.CanConnectAsync();

            if (!canConnect)
            {
                return new TestResult
                {
                    Success = false,
                    Message = "Unable to connect to database"
                };
            }

            var dbStatus = ApplicationDbContext.EnsureDatabaseExists(connectionString);

            return new TestResult
            {
                Success = dbStatus == DbStatus.ExistsWithNoUsers,
                Message = $"Database connection successful. Status: {dbStatus}",
                Status = dbStatus
            };
        }
    }
}
