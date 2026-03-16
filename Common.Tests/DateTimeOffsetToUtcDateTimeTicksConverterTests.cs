// <copyright file="DateTimeOffsetToUtcDateTimeTicksConverterTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests
{
    using System;
    using Cosmos.Common;
    using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="DateTimeOffsetToUtcDateTimeTicksConverter"/>.
    /// </summary>
    [TestClass]
    public class DateTimeOffsetToUtcDateTimeTicksConverterTests
    {
        [TestMethod]
        public void Constructor_WithDefaultHints_ShouldSucceed()
        {
            var converter = new DateTimeOffsetToUtcDateTimeTicksConverter();

            Assert.IsNotNull(converter);
        }

        [TestMethod]
        public void ConvertToProvider_ShouldUseUtcTicks()
        {
            var converter = new DateTimeOffsetToUtcDateTimeTicksConverter();
            var toProvider = converter.ConvertToProviderExpression.Compile();
            var input = new DateTimeOffset(2024, 01, 02, 10, 30, 00, TimeSpan.FromHours(-5));

            var ticks = toProvider(input);

            Assert.AreEqual(input.UtcDateTime.Ticks, ticks);
        }

        [TestMethod]
        public void ConvertFromProvider_ShouldCreateUtcDateTimeOffset()
        {
            var converter = new DateTimeOffsetToUtcDateTimeTicksConverter();
            var fromProvider = converter.ConvertFromProviderExpression.Compile();
            var utcDateTime = new DateTime(2024, 01, 02, 15, 30, 00, DateTimeKind.Utc);
            var ticks = utcDateTime.Ticks;

            var result = fromProvider(ticks);

            Assert.AreEqual(TimeSpan.Zero, result.Offset);
            Assert.AreEqual(ticks, result.UtcDateTime.Ticks);
        }

        [TestMethod]
        public void ConvertRoundTrip_ShouldPreserveUtcInstant()
        {
            var converter = new DateTimeOffsetToUtcDateTimeTicksConverter();
            var toProvider = converter.ConvertToProviderExpression.Compile();
            var fromProvider = converter.ConvertFromProviderExpression.Compile();
            var input = new DateTimeOffset(2024, 08, 20, 12, 00, 00, TimeSpan.FromHours(3));

            var ticks = toProvider(input);
            var output = fromProvider(ticks);

            Assert.AreEqual(input.UtcDateTime.Ticks, output.UtcDateTime.Ticks);
            Assert.AreEqual(TimeSpan.Zero, output.Offset);
        }

        [TestMethod]
        public void DefaultInfo_ShouldDescribeDateTimeOffsetToLong()
        {
            var info = DateTimeOffsetToUtcDateTimeTicksConverter.DefaultInfo;

            Assert.IsNotNull(info);
            Assert.AreEqual(typeof(DateTimeOffset), info.ModelClrType);
            Assert.AreEqual(typeof(long), info.ProviderClrType);
        }

        [TestMethod]
        public void DefaultInfoFactory_ShouldCreateConverter()
        {
            var converter = DateTimeOffsetToUtcDateTimeTicksConverter.DefaultInfo
                .Create() as ValueConverter;

            Assert.IsNotNull(converter);
            Assert.IsInstanceOfType<DateTimeOffsetToUtcDateTimeTicksConverter>(converter);
        }
    }
}
