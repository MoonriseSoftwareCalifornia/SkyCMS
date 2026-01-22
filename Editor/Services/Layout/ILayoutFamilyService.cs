// <copyright file="ILayoutFamilyService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Layout
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cosmos.Common.Data;

    /// <summary>
    /// Service for managing layout families and their versions.
    /// </summary>
    public interface ILayoutFamilyService
    {
        Task<List<Cosmos.Common.Data.Layout>> GetLayoutFamilyAsync(int layoutNumber);
        Task<Cosmos.Common.Data.Layout?> GetLatestVersionAsync(int layoutNumber);
        Task<Cosmos.Common.Data.Layout?> GetPublishedVersionAsync(int layoutNumber);
        Task<List<int>> GetAllLayoutNumbersAsync();
        Task<LayoutFamilyInfo?> GetFamilyInfoAsync(int layoutNumber);
        Task<Cosmos.Common.Data.Layout> CreateNewVersionAsync(int layoutNumber, string? userId = null);
        Task<bool> PublishVersionAsync(Guid layoutId);
        Task<bool> DeleteVersionAsync(Guid layoutId);
        Task<List<LayoutFamilyGroup>> GetLayoutsGroupedByFamilyAsync();
    }
}