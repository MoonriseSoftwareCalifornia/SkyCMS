// <copyright file="ProxySettings.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.DynamicConfig.Configurations
{
    using System.Collections.Generic;

    /// <summary>
    /// Configuration for proxy trust and x-origin-hostname handling.
    /// </summary>
    public class ProxySettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether to trust the x-origin-hostname header from trusted proxies.
        /// </summary>
        public bool TrustXOriginHostname { get; set; } = false;

        /// <summary>
        /// Gets or sets a list of trusted proxy IP addresses (IPv4 or IPv6).
        /// </summary>
        public List<string> TrustedProxyIPs { get; set; } = new();
    }
}