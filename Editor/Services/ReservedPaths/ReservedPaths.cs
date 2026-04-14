// <copyright file="ReservedPaths.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.ReservedPaths
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Newtonsoft.Json;
    using Sky.Cms.Models;

    /// <summary>
    /// Manages reserved paths that cannot be used for articles, pages, or other content.
    /// </summary>
    public class ReservedPaths : IReservedPaths
    {
        private const string RESERVEDPATHSSETTING = "ReservedPaths";
        private readonly ApplicationDbContext db;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReservedPaths"/> class.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        public ReservedPaths(ApplicationDbContext dbContext)
        {
            db = dbContext;
        }

        /// <inheritdoc cref="IReservedPaths.Upsert"/>
        public async Task Upsert(ReservedPath path)
        {
            var paths = await GetReservedPaths();

            // Check if the path already exists
            var existing = paths.FirstOrDefault(p => p.Path.ToLower() == path.Path.ToLower());

            if (existing == null)
            {
                paths.Add(path);
            }
            else if (existing.CosmosRequired)
            {
                throw new Exception("Cannot update a system required path.");
            }
            else
            {
                // Update existing path
                existing.CosmosRequired = path.CosmosRequired;
                existing.Notes = path.Notes;
                existing.CosmosRequired = false;
            }

            // Save updated list back to settings
            var settings = db.Settings.FirstOrDefault(f => f.Name == RESERVEDPATHSSETTING);
            settings.Value = JsonConvert.SerializeObject(paths);
        }

        /// <summary>
        ///  Gets the reserved paths.
        /// </summary>
        /// <returns>Reserved path list.</returns>
        public async Task<List<ReservedPath>> GetReservedPaths()
        {
            var settings = db.Settings.FirstOrDefault(f => f.Name == RESERVEDPATHSSETTING);

            if (settings == null)
            {
                var staticReserved = new List<ReservedPath>
                    {
                        new () { Path = "root", CosmosRequired = true, Notes = "Home page alias" },

                        // controllers
                        new () { Path = "blog/*", CosmosRequired = true, Notes = "Blog root" },
                        new () { Path = "editor/*", CosmosRequired = true, Notes = "Editor page alias" },
                        new () { Path = "home/*", CosmosRequired = true, Notes = "Home page alias" },
                        new () { Path = "layouts/*", CosmosRequired = true, Notes = "Layouts page alias" },
                        new () { Path = "filemanager/*", CosmosRequired = true, Notes = "File Manager page alias" },
                        new () { Path = "pub/*", CosmosRequired = true, Notes = "Public assets" },
                        new () { Path = "roles/*", CosmosRequired = true, Notes = "Roles management" },
                        new () { Path = "templates/*", CosmosRequired = true, Notes = "Templates management" },
                        new () { Path = "users/*", CosmosRequired = true, Notes = "User management" },

                        new () { Path = "admin", CosmosRequired = true, Notes = "Admin path" },
                        new () { Path = "account", CosmosRequired = true, Notes = "Identity path" },
                        new () { Path = "login", CosmosRequired = true, Notes = "Identity path" },
                        new () { Path = "logout", CosmosRequired = true, Notes = "Identity path" },
                        new () { Path = "register", CosmosRequired = true, Notes = "Identity path" },
                        new () { Path = "blog/rss", CosmosRequired = true, Notes = "Blog RSS" },
                        new () { Path = "api", CosmosRequired = true, Notes = "API route" },
                        new () { Path = "rss", CosmosRequired = true, Notes = "RSS" },
                        new () { Path = "sitemap.xml", CosmosRequired = true, Notes = "Sitemap" },
                        new () { Path = "toc.json", CosmosRequired = true, Notes = "Table of contents" }
                    };

                var json = JsonConvert.SerializeObject(staticReserved);
                settings = new Setting
                {
                    Name = RESERVEDPATHSSETTING,
                    Value = json,
                    Group = "System",
                    Description = "Reserved paths that cannot be used for articles, pages, or other content.",
                    IsRequired = true,
                    Id = System.Guid.NewGuid()
                };
                db.Settings.Add(settings);
                await db.SaveChangesAsync();
            }

            var reservedPaths = JsonConvert.DeserializeObject<List<ReservedPath>>(settings.Value);
            return reservedPaths;
        }

        /// <inheritdoc cref="IReservedPaths.IsReserved"/>
        public async Task<bool> IsReserved(string path)
        {
            var reservedPaths = await GetReservedPaths();
            return reservedPaths.Any(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc cref="IReservedPaths.Remove"/>
        public async Task Remove(string path)
        {
            var reservedPaths = await GetReservedPaths();
            var pathToRemove = reservedPaths.FirstOrDefault(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

            if (pathToRemove != null)
            {
                if (pathToRemove.CosmosRequired)
                {
                    throw new Exception("Cannot remove a system required path.");
                }

                reservedPaths.Remove(pathToRemove);
                var settings = db.Settings.FirstOrDefault(f => f.Name == RESERVEDPATHSSETTING);
                settings.Value = JsonConvert.SerializeObject(reservedPaths);
                await db.SaveChangesAsync();
            }
        }
    }
}
