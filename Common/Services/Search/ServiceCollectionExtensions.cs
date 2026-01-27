// <copyright file="ServiceCollectionExtensions.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Services.Search;

using Cosmos.Common.Services.Search.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

/// <summary>
/// Service collection extensions for search services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Lucene.Net search services using a preset configuration.
    /// </summary>
    public static IServiceCollection AddLuceneSearch(
        this IServiceCollection services,
        LuceneSearchOptions preset)
    {
        services.Configure<LuceneSearchOptions>(options =>
        {
            options.IndexPath = preset.IndexPath;
            options.UseRamDirectory = preset.UseRamDirectory;
            options.MaxResults = preset.MaxResults;
            options.HighlightFragmentCount = preset.HighlightFragmentCount;
            options.HighlightFragmentSize = preset.HighlightFragmentSize;
            options.AutoCommit = preset.AutoCommit;
            options.CommitIntervalMs = preset.CommitIntervalMs;
            options.Boosts = preset.Boosts;
        });

        services.AddSingleton<ISearchService, LuceneSearchService>();

        return services;
    }

    /// <summary>
    /// Adds Lucene.Net search services using configuration from appsettings.json.
    /// Falls back to Default preset if configuration section doesn't exist.
    /// </summary>
    public static IServiceCollection AddLuceneSearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(LuceneSearchOptions.SectionName);
        
        if (section.Exists())
        {
            services.Configure<LuceneSearchOptions>(section);
        }
        else
        {
            services.AddLuceneSearch(LuceneSearchPresets.Default);
            return services;
        }

        services.AddSingleton<ISearchService, LuceneSearchService>();

        return services;
    }

    /// <summary>
    /// Adds Lucene.Net search services using auto-detected environment preset.
    /// </summary>
    public static IServiceCollection AddLuceneSearchForEnvironment(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        var preset = LuceneSearchPresets.GetPresetForEnvironment(environment.EnvironmentName);
        return services.AddLuceneSearch(preset);
    }

    /// <summary>
    /// Adds Lucene.Net search services with custom configuration action.
    /// </summary>
    public static IServiceCollection AddLuceneSearch(
        this IServiceCollection services,
        LuceneSearchOptions preset,
        Action<LuceneSearchOptions> configure)
    {
        services.Configure<LuceneSearchOptions>(options =>
        {
            options.IndexPath = preset.IndexPath;
            options.UseRamDirectory = preset.UseRamDirectory;
            options.MaxResults = preset.MaxResults;
            options.HighlightFragmentCount = preset.HighlightFragmentCount;
            options.HighlightFragmentSize = preset.HighlightFragmentSize;
            options.AutoCommit = preset.AutoCommit;
            options.CommitIntervalMs = preset.CommitIntervalMs;
            options.Boosts = preset.Boosts;

            configure(options);
        });

        services.AddSingleton<ISearchService, LuceneSearchService>();

        return services;
    }
}