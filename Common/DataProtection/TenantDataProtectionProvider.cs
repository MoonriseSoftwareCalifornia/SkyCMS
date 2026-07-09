// <copyright file="TenantDataProtectionProvider.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.DataProtection
{
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.DataProtection;

    public class TenantDataProtectionProvider : IDataProtectionProvider
    {
        private readonly IDataProtectionProvider dataProtectionProvider;
        private readonly IDynamicConfigurationProvider dynamicConfigurationProvider;

        public TenantDataProtectionProvider(
            IDataProtectionProvider inner,
            IDynamicConfigurationProvider tenant)
        {
            dataProtectionProvider = inner;
            dynamicConfigurationProvider = tenant;
        }

        public IDataProtector CreateProtector(string purpose)
        {
            var domainName = dynamicConfigurationProvider.GetTenantDomainNameFromRequest();
            return dataProtectionProvider.CreateProtector(
                domainName,
                purpose);
        }
    }
}
