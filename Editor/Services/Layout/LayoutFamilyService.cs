// <copyright file="LayoutFamilyService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Layout
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Sky.Editor.Extensions;

    /// <summary>
    /// Service for managing layout families and their versions.
    /// </summary>
    public class LayoutFamilyService : ILayoutFamilyService
    {
        private readonly ApplicationDbContext dbContext;
        private readonly ILogger<LayoutFamilyService> logger;

        public LayoutFamilyService(
            ApplicationDbContext dbContext,
            ILogger<LayoutFamilyService> logger)
        {
            this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<Cosmos.Common.Data.Layout>> GetLayoutFamilyAsync(int layoutNumber)
        {
            try
            {
                return await dbContext.Layouts
                    .ByLayoutNumber(layoutNumber)
                    .OrderByDescending(l => l.Version)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving layout family {LayoutNumber}", layoutNumber);
                throw;
            }
        }

        public async Task<Cosmos.Common.Data.Layout?> GetLatestVersionAsync(int layoutNumber)
        {
            try
            {
                return await dbContext.Layouts
                    .ByLayoutNumber(layoutNumber)
                    .OrderByDescending(l => l.Version)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving latest version for layout family {LayoutNumber}", layoutNumber);
                throw;
            }
        }

        public async Task<Cosmos.Common.Data.Layout?> GetPublishedVersionAsync(int layoutNumber)
        {
            try
            {
                return await dbContext.Layouts
                    .ByLayoutNumber(layoutNumber)
                    .Published()
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving published version for layout family {LayoutNumber}", layoutNumber);
                throw;
            }
        }

        public async Task<List<int>> GetAllLayoutNumbersAsync()
        {
            try
            {
                return await dbContext.Layouts
                    .WithLayoutNumber()
                    .Select(l => l.LayoutNumber)
                    .Distinct()
                    .OrderBy(ln => ln)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving all layout numbers");
                throw;
            }
        }

        public async Task<LayoutFamilyInfo?> GetFamilyInfoAsync(int layoutNumber)
        {
            try
            {
                var layouts = await GetLayoutFamilyAsync(layoutNumber);
                if (!layouts.Any()) return null;

                var publishedVersion = layouts.FirstOrDefault(l => l.IsDefault);
                var latestVersion = layouts.OrderByDescending(l => l.Version).First();

                return new LayoutFamilyInfo
                {
                    LayoutNumber = layoutNumber,
                    FamilyName = latestVersion.LayoutName ?? $"Layout {layoutNumber}",
                    TotalVersions = layouts.Count,
                    LatestVersion = latestVersion,
                    PublishedVersion = publishedVersion,
                    AllVersions = layouts
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving family info for layout {LayoutNumber}", layoutNumber);
                throw;
            }
        }

        public async Task<Cosmos.Common.Data.Layout> CreateNewVersionAsync(int layoutNumber, string? userId = null)
        {
            try
            {
                var latestVersion = await GetLatestVersionAsync(layoutNumber);
                if (latestVersion == null)
                {
                    throw new InvalidOperationException($"No layouts found with LayoutNumber {layoutNumber}");
                }

                var newVersion = new Cosmos.Common.Data.Layout
                {
                    Id = Guid.NewGuid(),
                    LayoutNumber = layoutNumber,
                    LayoutName = latestVersion.LayoutName,
                    Notes = latestVersion.Notes,
                    Head = latestVersion.Head,
                    HtmlHeader = latestVersion.HtmlHeader,
                    BodyHtmlAttributes = latestVersion.BodyHtmlAttributes,
                    FooterHtmlContent = latestVersion.FooterHtmlContent,
                    CommunityLayoutId = latestVersion.CommunityLayoutId,
                    IsDefault = false,
                    Version = (latestVersion.Version ?? 0) + 1,
                    LastModified = DateTimeOffset.UtcNow,
                    Published = null
                };

                dbContext.Layouts.Add(newVersion);
                await dbContext.SaveChangesAsync();

                logger.LogInformation(
                    "Created new version {Version} for layout family {LayoutNumber} (ID: {LayoutId})",
                    newVersion.Version, layoutNumber, newVersion.Id);

                return newVersion;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating new version for layout family {LayoutNumber}", layoutNumber);
                throw;
            }
        }

        public async Task<bool> PublishVersionAsync(Guid layoutId)
        {
            try
            {
                var layout = await dbContext.Layouts.FindAsync(layoutId);
                if (layout == null)
                {
                    logger.LogWarning("Layout {LayoutId} not found for publishing", layoutId);
                    return false;
                }

                if (layout.IsDefault && layout.Published.HasValue) return true;

                var familyLayouts = await GetLayoutFamilyAsync(layout.LayoutNumber);
                foreach (var familyLayout in familyLayouts.Where(l => l.Id != layoutId))
                {
                    familyLayout.IsDefault = false;
                    familyLayout.Published = null;
                }

                layout.IsDefault = true;
                layout.Published = DateTimeOffset.UtcNow;

                await dbContext.SaveChangesAsync();

                logger.LogInformation(
                    "Published layout {LayoutId} (LayoutNumber: {LayoutNumber}, Version: {Version})",
                    layoutId, layout.LayoutNumber, layout.Version);

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing layout {LayoutId}", layoutId);
                throw;
            }
        }

        public async Task<bool> DeleteVersionAsync(Guid layoutId)
        {
            try
            {
                var layout = await dbContext.Layouts.FindAsync(layoutId);
                if (layout == null)
                {
                    logger.LogWarning("Layout {LayoutId} not found for deletion", layoutId);
                    return false;
                }

                if (layout.IsDefault)
                {
                    logger.LogWarning(
                        "Cannot delete published layout {LayoutId} (LayoutNumber: {LayoutNumber})",
                        layoutId, layout.LayoutNumber);
                    return false;
                }

                var templates = await dbContext.Templates
                    .Where(t => t.LayoutId == layoutId)
                    .ToListAsync();

                dbContext.Templates.RemoveRange(templates);
                dbContext.Layouts.Remove(layout);

                await dbContext.SaveChangesAsync();

                logger.LogInformation(
                    "Deleted layout {LayoutId} (LayoutNumber: {LayoutNumber}, Version: {Version}) and {TemplateCount} templates",
                    layoutId, layout.LayoutNumber, layout.Version, templates.Count);

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting layout {LayoutId}", layoutId);
                throw;
            }
        }

        public async Task<List<LayoutFamilyGroup>> GetLayoutsGroupedByFamilyAsync()
        {
            try
            {
                var layouts = await dbContext.Layouts
                    .WithLayoutNumber()
                    .OrderByFamilyAndVersion()
                    .ToListAsync();

                return layouts
                    .GroupBy(l => l.LayoutNumber)
                    .Select(g => new LayoutFamilyGroup
                    {
                        LayoutNumber = g.Key,
                        FamilyName = g.First().LayoutName ?? $"Layout {g.Key}",
                        IsActive = g.Any(l => l.IsDefault),
                        Versions = g.Select(v => new LayoutVersionOption
                        {
                            Id = v.Id,
                            Version = v.Version ?? 0,
                            DisplayName = $"{v.LayoutName} (v{v.Version})",
                            IsPublished = v.IsDefault,
                            LastModified = v.LastModified ?? DateTimeOffset.MinValue
                        }).ToList()
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving grouped layouts");
                throw;
            }
        }
    }
}