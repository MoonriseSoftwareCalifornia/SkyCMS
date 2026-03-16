// <copyright file="SQLiteUtilsTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Data.SQlite
{
    using System;
    using System.Linq;
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.SQlite;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Conventions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="SQLiteUtils"/>.
    /// </summary>
    [TestClass]
    public class SQLiteUtilsTests
    {
        [TestMethod]
        public void OnModelCreating_ShouldConfigureUniqueIndexForArticleId()
        {
            var modelBuilder = new ModelBuilder(new ConventionSet());

            SQLiteUtils.OnModelCreating(modelBuilder);

            var articleEntity = modelBuilder.Model.FindEntityType(typeof(Article));
            Assert.IsNotNull(articleEntity);

            var idIndex = articleEntity.GetIndexes()
                .FirstOrDefault(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(Article.Id));

            Assert.IsNotNull(idIndex);
            Assert.IsTrue(idIndex.IsUnique);
        }

        [TestMethod]
        public void OnModelCreating_ShouldConfigureIdentityUserRoleCompositeIndex()
        {
            var modelBuilder = new ModelBuilder(new ConventionSet());

            SQLiteUtils.OnModelCreating(modelBuilder);

            var entity = modelBuilder.Model.FindEntityType(typeof(IdentityUserRole<string>));
            Assert.IsNotNull(entity);

            var compositeIndex = entity.GetIndexes()
                .FirstOrDefault(i => i.Properties.Count == 2
                    && i.Properties.Any(p => p.Name == nameof(IdentityUserRole<string>.UserId))
                    && i.Properties.Any(p => p.Name == nameof(IdentityUserRole<string>.RoleId)));

            Assert.IsNotNull(compositeIndex);
        }

        [TestMethod]
        public void OnModelCreating_ShouldConfigureDateTimeOffsetConversionForArticlePublished()
        {
            var modelBuilder = new ModelBuilder(new ConventionSet());

            SQLiteUtils.OnModelCreating(modelBuilder);

            var articleEntity = modelBuilder.Model.FindEntityType(typeof(Article));
            Assert.IsNotNull(articleEntity);

            var publishedProperty = articleEntity.FindProperty(nameof(Article.Published));
            Assert.IsNotNull(publishedProperty);

            var converter = publishedProperty.GetValueConverter();
            Assert.IsNotNull(converter);
            Assert.IsInstanceOfType<Cosmos.Common.DateTimeOffsetToUtcDateTimeTicksConverter>(converter);
        }
    }
}
