// <copyright file="TenantContextTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using Cosmos.DynamicConfig;

namespace Sky.Tests.DynamicConfig
{
    /// <summary>
    /// Tests for TenantContext - Priority 1 multi-tenant core infrastructure.
    /// Tests ambient tenant context management for background operations.
    /// </summary>
    [TestClass]
    public class TenantContextTests
    {
        [TestInitialize]
        public void Setup()
        {
            // Clear context before each test to ensure isolation
            TenantContext.Clear();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clear context after each test
            TenantContext.Clear();
        }

        [TestMethod]
        public void CurrentDomain_SetAndGet_ReturnsCorrectValue()
        {
            // Arrange
            const string domain = "test-domain.com";

            // Act
            TenantContext.CurrentDomain = domain;

            // Assert
            Assert.AreEqual(domain, TenantContext.CurrentDomain);
        }

        [TestMethod]
        public void CurrentDomain_NormalizesToLowercase()
        {
            // Arrange
            const string upperDomain = "UPPERCASE-DOMAIN.COM";

            // Act
            TenantContext.CurrentDomain = upperDomain;

            // Assert
            Assert.AreEqual("uppercase-domain.com", TenantContext.CurrentDomain);
        }

        [TestMethod]
        public void HasContext_WithNoDomain_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(TenantContext.HasContext);
        }

        [TestMethod]
        public void HasContext_WithDomain_ReturnsTrue()
        {
            // Arrange
            TenantContext.CurrentDomain = "has-context-domain.com";

            // Act & Assert
            Assert.IsTrue(TenantContext.HasContext);
        }

        [TestMethod]
        public void HasContext_AfterClear_ReturnsFalse()
        {
            // Arrange
            TenantContext.CurrentDomain = "clear-test-domain.com";
            Assert.IsTrue(TenantContext.HasContext);

            // Act
            TenantContext.Clear();

            // Assert
            Assert.IsFalse(TenantContext.HasContext);
            Assert.IsNull(TenantContext.CurrentDomain);
        }

        [TestMethod]
        public void Clear_SetsCurrentDomainToNull()
        {
            // Arrange
            TenantContext.CurrentDomain = "clear-domain.com";

            // Act
            TenantContext.Clear();

            // Assert
            Assert.IsNull(TenantContext.CurrentDomain);
        }

        [TestMethod]
        public void Execute_SetsAndRestoresDomain()
        {
            // Arrange
            const string initialDomain = "initial-domain.com";
            const string executionDomain = "execution-domain.com";
            TenantContext.CurrentDomain = initialDomain;
            string capturedDomain = null;

            // Act
            TenantContext.Execute(executionDomain, () =>
            {
                capturedDomain = TenantContext.CurrentDomain;
            });

            // Assert
            Assert.AreEqual(executionDomain, capturedDomain);
            Assert.AreEqual(initialDomain, TenantContext.CurrentDomain);
        }

        [TestMethod]
        public void Execute_WithNullInitialDomain_RestoresToNull()
        {
            // Arrange
            const string executionDomain = "execution-domain.com";
            string capturedDomain = null;

            // Act
            TenantContext.Execute(executionDomain, () =>
            {
                capturedDomain = TenantContext.CurrentDomain;
            });

            // Assert
            Assert.AreEqual(executionDomain, capturedDomain);
            Assert.IsNull(TenantContext.CurrentDomain);
        }

        [TestMethod]
        public void Execute_WithException_RestoresDomain()
        {
            // Arrange
            const string initialDomain = "initial-domain.com";
            const string executionDomain = "exception-domain.com";
            TenantContext.CurrentDomain = initialDomain;

            // Act & Assert
            try
            {
                TenantContext.Execute(executionDomain, () =>
                {
                    throw new InvalidOperationException("Test exception");
                });
                Assert.Fail("Expected InvalidOperationException was not thrown");
            }
            catch (InvalidOperationException)
            {
                // Expected exception
            }

            // Domain should be restored even after exception
            Assert.AreEqual(initialDomain, TenantContext.CurrentDomain);
        }

        [TestMethod]
        public void Execute_NestedExecution_HandlesCorrectly()
        {
            // Arrange
            const string outerDomain = "outer-domain.com";
            const string innerDomain = "inner-domain.com";
            string outerCaptured = null;
            string innerCaptured = null;

            // Act
            TenantContext.Execute(outerDomain, () =>
            {
                outerCaptured = TenantContext.CurrentDomain;

                TenantContext.Execute(innerDomain, () =>
                {
                    innerCaptured = TenantContext.CurrentDomain;
                });

                // After inner execution, should restore to outer
                Assert.AreEqual(outerDomain, TenantContext.CurrentDomain);
            });

            // Assert
            Assert.AreEqual(outerDomain, outerCaptured);
            Assert.AreEqual(innerDomain, innerCaptured);
            Assert.IsNull(TenantContext.CurrentDomain);
        }

        [TestMethod]
        public async Task ExecuteAsync_SetsAndRestoresDomain()
        {
            // Arrange
            const string initialDomain = "initial-async-domain.com";
            const string executionDomain = "execution-async-domain.com";
            TenantContext.CurrentDomain = initialDomain;
            string capturedDomain = null;

            // Act
            await TenantContext.ExecuteAsync(executionDomain, async () =>
            {
                await Task.Delay(10);
                capturedDomain = TenantContext.CurrentDomain;
            });

            // Assert
            Assert.AreEqual(executionDomain, capturedDomain);
            Assert.AreEqual(initialDomain, TenantContext.CurrentDomain);
        }

        [TestMethod]
        public async Task ExecuteAsync_WithException_RestoresDomain()
        {
            // Arrange
            const string initialDomain = "initial-async-exception-domain.com";
            const string executionDomain = "async-exception-domain.com";
            TenantContext.CurrentDomain = initialDomain;

            // Act & Assert
            try
            {
                await TenantContext.ExecuteAsync(executionDomain, async () =>
                {
                    await Task.Delay(10);
                    throw new InvalidOperationException("Async test exception");
                });
                Assert.Fail("Expected InvalidOperationException was not thrown");
            }
            catch (InvalidOperationException)
            {
                // Expected exception
            }

            // Domain should be restored even after exception
            Assert.AreEqual(initialDomain, TenantContext.CurrentDomain);
        }

        [TestMethod]
        public async Task ExecuteAsync_WithResult_ReturnsCorrectValue()
        {
            // Arrange
            const string executionDomain = "result-domain.com";
            const int expectedResult = 42;

            // Act
            var result = await TenantContext.ExecuteAsync(executionDomain, async () =>
            {
                await Task.Delay(10);
                return expectedResult;
            });

            // Assert
            Assert.AreEqual(expectedResult, result);
            Assert.IsNull(TenantContext.CurrentDomain);
        }

        [TestMethod]
        public async Task ExecuteAsync_WithResult_RestoresDomainAfterExecution()
        {
            // Arrange
            const string initialDomain = "initial-result-domain.com";
            const string executionDomain = "execution-result-domain.com";
            TenantContext.CurrentDomain = initialDomain;

            // Act
            var result = await TenantContext.ExecuteAsync(executionDomain, async () =>
            {
                await Task.Delay(10);
                return TenantContext.CurrentDomain;
            });

            // Assert
            Assert.AreEqual(executionDomain, result);
            Assert.AreEqual(initialDomain, TenantContext.CurrentDomain);
        }

        [TestMethod]
        public async Task ExecuteAsync_IsolatesContextBetweenConcurrentOperations()
        {
            // Arrange
            const string domain1 = "concurrent-domain-1.com";
            const string domain2 = "concurrent-domain-2.com";
            string captured1 = null;
            string captured2 = null;

            // Act
            var task1 = TenantContext.ExecuteAsync(domain1, async () =>
            {
                await Task.Delay(50);
                captured1 = TenantContext.CurrentDomain;
            });

            var task2 = TenantContext.ExecuteAsync(domain2, async () =>
            {
                await Task.Delay(20);
                captured2 = TenantContext.CurrentDomain;
            });

            await Task.WhenAll(task1, task2);

            // Assert
            Assert.AreEqual(domain1, captured1);
            Assert.AreEqual(domain2, captured2);
        }

        [TestMethod]
        public async Task ExecuteAsync_NestedAsyncExecution_HandlesCorrectly()
        {
            // Arrange
            const string outerDomain = "outer-async-domain.com";
            const string innerDomain = "inner-async-domain.com";
            string outerCaptured = null;
            string innerCaptured = null;

            // Act
            await TenantContext.ExecuteAsync(outerDomain, async () =>
            {
                await Task.Delay(10);
                outerCaptured = TenantContext.CurrentDomain;

                await TenantContext.ExecuteAsync(innerDomain, async () =>
                {
                    await Task.Delay(10);
                    innerCaptured = TenantContext.CurrentDomain;
                });

                // After inner execution, should restore to outer
                Assert.AreEqual(outerDomain, TenantContext.CurrentDomain);
            });

            // Assert
            Assert.AreEqual(outerDomain, outerCaptured);
            Assert.AreEqual(innerDomain, innerCaptured);
            Assert.IsNull(TenantContext.CurrentDomain);
        }

        [TestMethod]
        public void CurrentDomain_SetToNull_ClearsContext()
        {
            // Arrange
            TenantContext.CurrentDomain = "domain-to-clear.com";
            Assert.IsTrue(TenantContext.HasContext);

            // Act
            TenantContext.CurrentDomain = null;

            // Assert
            Assert.IsFalse(TenantContext.HasContext);
            Assert.IsNull(TenantContext.CurrentDomain);
        }

        [TestMethod]
        public void CurrentDomain_SetToEmptyString_ResultsInNoContext()
        {
            // Arrange
            TenantContext.CurrentDomain = "domain-before-empty.com";
            Assert.IsTrue(TenantContext.HasContext);

            // Act
            TenantContext.CurrentDomain = string.Empty;

            // Assert
            // Empty string is normalized to empty, so HasContext should be false
            Assert.IsFalse(TenantContext.HasContext);
        }

        [TestMethod]
        public void CurrentDomain_SetToWhitespace_ResultsInNoContext()
        {
            // Arrange
            TenantContext.CurrentDomain = "domain-before-whitespace.com";
            Assert.IsTrue(TenantContext.HasContext);

            // Act
            TenantContext.CurrentDomain = "   ";

            // Assert
            Assert.IsFalse(TenantContext.HasContext);
        }
    }
}
