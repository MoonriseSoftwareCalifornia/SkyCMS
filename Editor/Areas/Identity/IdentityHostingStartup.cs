// <copyright file="IdentityHostingStartup.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Microsoft.AspNetCore.Hosting;
using Sky.Cms.Areas.Identity;

[assembly: HostingStartup(typeof(IdentityHostingStartup))]

namespace Sky.Cms.Areas.Identity
{
    /// <summary>
    /// Identity hosting startup class.
    /// </summary>
    public class IdentityHostingStartup : IHostingStartup
    {
        /// <summary>
        /// Configure method.
        /// </summary>
        /// <param name="builder">Web host builder service.</param>
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) => { });
        }
    }
}
