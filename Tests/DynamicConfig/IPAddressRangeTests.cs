// <copyright file="IPAddressRangeTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

using Cosmos.DynamicConfig;
using System.Net;

namespace Sky.Tests.DynamicConfig
{
    [TestClass]
    public class IPAddressRangeTests
    {
        [TestMethod]
        public void Parse_SingleIp_ContainsOnlyThatIp()
        {
            var r = IPAddressRange.Parse("127.0.0.1");
            Assert.IsTrue(r.Contains(IPAddress.Parse("127.0.0.1")));
            Assert.IsFalse(r.Contains(IPAddress.Parse("127.0.0.2")));
        }

        [TestMethod]
        public void Parse_CidrIpv4_ContainsExpectedAddresses()
        {
            var r = IPAddressRange.Parse("192.168.1.0/24");
            Assert.IsTrue(r.Contains(IPAddress.Parse("192.168.1.5")));
            Assert.IsTrue(r.Contains(IPAddress.Parse("192.168.1.0")));
            Assert.IsTrue(r.Contains(IPAddress.Parse("192.168.1.255")));
            Assert.IsFalse(r.Contains(IPAddress.Parse("192.168.2.1")));
        }

        [TestMethod]
        public void Parse_RangeStartEnd_ContainsExpectedAddresses()
        {
            var r = IPAddressRange.Parse("10.0.0.1-10.0.0.10");
            Assert.IsTrue(r.Contains(IPAddress.Parse("10.0.0.1")));
            Assert.IsTrue(r.Contains(IPAddress.Parse("10.0.0.5")));
            Assert.IsTrue(r.Contains(IPAddress.Parse("10.0.0.10")));
            Assert.IsFalse(r.Contains(IPAddress.Parse("10.0.0.11")));
        }

        [TestMethod]
        public void Parse_CidrIpv6_ContainsExpectedAddresses()
        {
            var r = IPAddressRange.Parse("2001:db8::/32");
            Assert.IsTrue(r.Contains(IPAddress.Parse("2001:db8::1")));
            Assert.IsFalse(r.Contains(IPAddress.Parse("2001:db9::1")));
        }

        [TestMethod]
        public void Parse_CidrIpv4_PrefixEdgeCases()
        {
            // /0 should include all IPv4
            var all = IPAddressRange.Parse("0.0.0.0/0");
            Assert.IsTrue(all.Contains(IPAddress.Parse("1.2.3.4")));

            // /32 should include only single address
            var single = IPAddressRange.Parse("203.0.113.5/32");
            Assert.IsTrue(single.Contains(IPAddress.Parse("203.0.113.5")));
            Assert.IsFalse(single.Contains(IPAddress.Parse("203.0.113.6")));
        }

        [TestMethod]
        public void Parse_CidrIpv6_PrefixEdgeCases()
        {
            // ::/0 includes everything IPv6
            var all6 = IPAddressRange.Parse("::/0");
            Assert.IsTrue(all6.Contains(IPAddress.Parse("2001:db8::1")));

            // /128 single address
            var single6 = IPAddressRange.Parse("2001:db8::1/128");
            Assert.IsTrue(single6.Contains(IPAddress.Parse("2001:db8::1")));
            Assert.IsFalse(single6.Contains(IPAddress.Parse("2001:db8::2")));
        }

        [TestMethod]
        public void Parse_StartEnd_Reversed_ThrowsArgumentException()
        {
            try
            {
                IPAddressRange.Parse("10.0.0.10-10.0.0.1");
                Assert.Fail();
            }
            catch (ArgumentException)
            {
                // Expected
            }

            try
            {
                IPAddressRange.Parse("2001:db8::5-2001:db8::1");
                Assert.Fail();
            }
            catch (ArgumentException)
            {
                // Expected
            }
        }

        [TestMethod]
        public void Parse_InvalidFormats_ThrowFormatException()
        {
            var invalids = new[]
            {
                "",
                "   ",
                "not-an-ip",
                "192.168.1.0 255.255.255.0", // dotted netmask not supported
                "[::1]", // bracketed IPv6 should be rejected by parser
                "2001:db8::/129", // invalid prefix
                "256.0.0.1",
            };

            foreach (var s in invalids)
            {
                try
                {
                    IPAddressRange.Parse(s);
                    Assert.Fail($"Expected FormatException for input: '{s}'");
                }
                catch (FormatException)
                {
                    // Expected
                }
            }
        }

        [TestMethod]
        public void Parse_WhitespaceTrimAccepted()
        {
            var r = IPAddressRange.Parse(" 192.0.2.1 ");
            Assert.IsTrue(r.Contains(IPAddress.Parse("192.0.2.1")));

            var cidr = IPAddressRange.Parse(" 10.0.0.0/8 ");
            Assert.IsTrue(cidr.Contains(IPAddress.Parse("10.1.2.3")));
        }

        [TestMethod]
        public void Contains_MixedAddressFamily_ReturnsFalse()
        {
            var r = IPAddressRange.Parse("192.168.0.0/24");
            // IPv6 address should not be considered contained
            Assert.IsFalse(r.Contains(IPAddress.Parse("::1")));
        }

        [TestMethod]
        public void Parse_Ipv6Range_StartEnd_Works()
        {
            var r = IPAddressRange.Parse("2001:db8::1-2001:db8::ffff");
            Assert.IsTrue(r.Contains(IPAddress.Parse("2001:db8::10")));
            Assert.IsFalse(r.Contains(IPAddress.Parse("2001:db9::1")));
        }
    }
}
