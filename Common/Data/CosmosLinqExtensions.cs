// <copyright file="CosmosLinqExtensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Data
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Cosmos custom Any LINQ method.
    /// </summary>
    public static class CosmosLinqExtensions
    {
        /// <summary>
        /// Determines if any rows exist with the given query.
        /// </summary>
        /// <typeparam name="T">Dynamic type that maps to a table.</typeparam>
        /// <param name="query">Query.</param>
        /// <returns>Indicates the existence of any entities as a <see cref="bool"/>.</returns>
        public static async Task<bool> CosmosAnyAsync<T>(this IQueryable<T> query)
        {
            try
            {
                return (await query.CountAsync()) > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
