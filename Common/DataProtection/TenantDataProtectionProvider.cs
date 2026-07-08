using System;
using System.Collections.Generic;
using System.Text;
using Cosmos.DynamicConfig;
using Microsoft.AspNetCore.DataProtection;

namespace Cosmos.Common.DataProtection
{
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
