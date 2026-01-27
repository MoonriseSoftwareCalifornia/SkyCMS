// <copyright file="Program.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Cosmos.Common.Services.Search;
using Cosmos.Common.Services.Search.Configuration;

var builder = WebApplication.CreateBuilder(args);

var isStaticWebsite =
    builder.Configuration.GetValue<bool?>("CosmosStaticWebPages") ?? false;

// Register Lucene Search Service (before Boot calls)
if (isStaticWebsite)
{
    // Static sites might not need search, but register anyway
    builder.Services.AddLuceneSearch(LuceneSearchPresets.Production);
    await Cosmos.Publisher.Boot.StaticWebsiteProxy.Boot(builder);
}
else
{
    // Dynamic publisher needs search for content
    builder.Services.AddLuceneSearch(LuceneSearchPresets.Production);
    await Cosmos.Publisher.Boot.DynamicPublisherWebsite.Boot(builder);
}
