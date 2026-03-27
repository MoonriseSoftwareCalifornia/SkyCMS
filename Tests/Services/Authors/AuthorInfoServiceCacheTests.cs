// <copyright file="AuthorInfoServiceCacheTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.Authors
{
    using Cosmos.Common.Data;
    using Cosmos.Common.Data.Logic;
    using Cosmos.Common.Services.Caching;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Sky.Editor.Services.Authors;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests verifying that <see cref="AuthorInfoService"/> correctly reads from and writes to
    /// <see cref="ICacheService{AuthorInfo}"/> — tenant-aware cache — instead of the raw IMemoryCache.
    /// </summary>
    [TestClass]
    public class AuthorInfoServiceCacheTests
    {
        private ApplicationDbContext db;
        private Mock<ICacheService<AuthorInfo>> cacheMock;
        private AuthorInfoService service;

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            db = new ApplicationDbContext(options);

            cacheMock = new Mock<ICacheService<AuthorInfo>>(MockBehavior.Strict);
            service = new AuthorInfoService(db, cacheMock.Object);
        }

        [TestCleanup]
        public void Cleanup() => db.Dispose();

        #region Cache Hit

        /// <summary>
        /// When the cache already contains an entry for the user, GetOrCreateAsync must return it
        /// immediately without touching the database.
        /// </summary>
        [TestMethod]
        public async Task GetOrCreateAsync_CacheHit_ReturnsCachedValueWithoutDbAccess()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var key = userId.ToString();
            var expected = new AuthorInfo { Id = key, AuthorName = "Cached Author" };

            cacheMock
                .Setup(c => c.TryGet(key, out expected))
                .Returns(true);

            // Act
            var result = await service.GetOrCreateAsync(userId);

            // Assert — cache returned a value; DB was never consulted
            Assert.IsNotNull(result);
            Assert.AreEqual("Cached Author", result.AuthorName);
            cacheMock.Verify(c => c.TryGet(key, out expected), Times.Once);
            cacheMock.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<AuthorInfo>(), It.IsAny<TimeSpan>()), Times.Never);
            cacheMock.VerifyNoOtherCalls();
        }

        #endregion

        #region Cache Miss — existing DB record

        /// <summary>
        /// On a cache miss with an existing AuthorInfo record, the service fetches from the DB
        /// and stores the result in the cache.
        /// </summary>
        [TestMethod]
        public async Task GetOrCreateAsync_CacheMiss_ExistingAuthorInfo_FetchesFromDbAndSetsCache()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var key = userId.ToString();
            var dbRecord = new AuthorInfo { Id = key, AuthorName = "DB Author" };
            db.AuthorInfos.Add(dbRecord);
            await db.SaveChangesAsync();

            AuthorInfo nullOut = null;
            cacheMock
                .Setup(c => c.TryGet(key, out nullOut))
                .Returns(false);
            cacheMock
                .Setup(c => c.Set(key, It.Is<AuthorInfo>(a => a.Id == key), TimeSpan.FromMinutes(10)));

            // Act
            var result = await service.GetOrCreateAsync(userId);

            // Assert — result is the DB record, and it was written to the cache
            Assert.IsNotNull(result);
            Assert.AreEqual("DB Author", result.AuthorName);
            cacheMock.Verify(c => c.TryGet(key, out nullOut), Times.Once);
            cacheMock.Verify(c => c.Set(key, It.Is<AuthorInfo>(a => a.Id == key), TimeSpan.FromMinutes(10)), Times.Once);
            cacheMock.VerifyNoOtherCalls();
        }

        #endregion

        #region Cache Miss — identity user present, no AuthorInfo yet

        /// <summary>
        /// On a cache miss when there is no AuthorInfo record but an IdentityUser exists,
        /// the service creates a new AuthorInfo, persists it, and caches it.
        /// </summary>
        [TestMethod]
        public async Task GetOrCreateAsync_CacheMiss_NoAuthorInfo_CreatesFromIdentityAndSetsCache()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var key = userId.ToString();

            db.Users.Add(new IdentityUser { Id = key, UserName = "newuser", Email = "new@example.com" });
            await db.SaveChangesAsync();

            AuthorInfo nullOut = null;
            cacheMock
                .Setup(c => c.TryGet(key, out nullOut))
                .Returns(false);
            cacheMock
                .Setup(c => c.Set(key, It.Is<AuthorInfo>(a => a.Id == key && a.AuthorName == "newuser"), TimeSpan.FromMinutes(10)));

            // Act
            var result = await service.GetOrCreateAsync(userId);

            // Assert — new AuthorInfo created from identity and cached with correct key/value.
            // MockBehavior.Strict guarantees no calls beyond TryGet + Set were made.
            Assert.IsNotNull(result);
            Assert.AreEqual(key, result.Id);
            Assert.AreEqual("newuser", result.AuthorName);
            cacheMock.Verify(c => c.Set(key, It.Is<AuthorInfo>(a => a.Id == key && a.AuthorName == "newuser"), TimeSpan.FromMinutes(10)), Times.Once);
        }

        #endregion

        #region Unknown user

        /// <summary>
        /// When neither an AuthorInfo nor an IdentityUser record exists,
        /// GetOrCreateAsync returns null and does not write to the cache.
        /// </summary>
        [TestMethod]
        public async Task GetOrCreateAsync_UnknownUser_ReturnsNullAndDoesNotCache()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var key = userId.ToString();

            AuthorInfo nullOut = null;
            cacheMock
                .Setup(c => c.TryGet(key, out nullOut))
                .Returns(false);

            // Act
            var result = await service.GetOrCreateAsync(userId);

            // Assert — null returned; Set never called
            Assert.IsNull(result);
            cacheMock.Verify(c => c.TryGet(key, out nullOut), Times.Once);
            cacheMock.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<AuthorInfo>(), It.IsAny<TimeSpan>()), Times.Never);
            cacheMock.VerifyNoOtherCalls();
        }

        #endregion

        #region Cross-tenant key isolation

        /// <summary>
        /// Verifies that two different user IDs map to different cache keys,
        /// so one tenant's cached data cannot bleed into another user's result.
        /// The tenant-scoping itself is handled by CacheService; this test ensures
        /// AuthorInfoService passes the correct per-user key so CacheService can prefix it.
        /// </summary>
        [TestMethod]
        public async Task GetOrCreateAsync_TwoDifferentUsers_UseSeparateCacheKeys()
        {
            // Arrange — two users each with existing AuthorInfo in DB
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();
            var key1 = userId1.ToString();
            var key2 = userId2.ToString();

            db.AuthorInfos.Add(new AuthorInfo { Id = key1, AuthorName = "Author One" });
            db.AuthorInfos.Add(new AuthorInfo { Id = key2, AuthorName = "Author Two" });
            await db.SaveChangesAsync();

            AuthorInfo nullOut = null;
            cacheMock.Setup(c => c.TryGet(key1, out nullOut)).Returns(false);
            cacheMock.Setup(c => c.TryGet(key2, out nullOut)).Returns(false);
            cacheMock.Setup(c => c.Set(key1, It.Is<AuthorInfo>(a => a.Id == key1), TimeSpan.FromMinutes(10)));
            cacheMock.Setup(c => c.Set(key2, It.Is<AuthorInfo>(a => a.Id == key2), TimeSpan.FromMinutes(10)));

            // Act
            var result1 = await service.GetOrCreateAsync(userId1);
            var result2 = await service.GetOrCreateAsync(userId2);

            // Assert — each user gets their own result via their own cache key.
            // Distinct Set() keys prove isolation; MockBehavior.Strict ensures no extra calls.
            Assert.AreEqual("Author One", result1.AuthorName);
            Assert.AreEqual("Author Two", result2.AuthorName);
            cacheMock.Verify(c => c.Set(key1, It.Is<AuthorInfo>(a => a.Id == key1), TimeSpan.FromMinutes(10)), Times.Once);
            cacheMock.Verify(c => c.Set(key2, It.Is<AuthorInfo>(a => a.Id == key2), TimeSpan.FromMinutes(10)), Times.Once);
        }

        #endregion
    }
}
