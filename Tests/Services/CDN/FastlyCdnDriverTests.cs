// <copyright file="FastlyCdnDriverTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.CDN
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Moq.Protected;
    using Newtonsoft.Json;
    using Sky.Editor.Services.CDN;
    using JsonSerializer = System.Text.Json.JsonSerializer;

    /// <summary>
    /// Unit tests for <see cref="FastlyCdnDriver"/> Fastly CDN integration.
    /// Tests are designed to execute in parallel where independent of one another.
    /// </summary>
    [TestClass]
    public class FastlyCdnDriverTests
    {
        private Mock<ILogger> mockLogger;

        [TestInitialize]
        public void Setup()
        {
            mockLogger = new Mock<ILogger>();
        }

        #region Constructor Tests

        /// <summary>
        /// Test: FastlyCdnDriver initializes with valid settings.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.Construction")]
        public void Constructor_WithValidSettings_InitializesDriver()
        {
            // Arrange
            var config = new FastlyCdnConfig
            {
                ServiceId = "test-service-123",
                ApiToken = "test-token-456",
                Domain = "www.example.com",
                SoftPurge = false
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = JsonConvert.SerializeObject(config)
            };

            // Act
            var driver = new FastlyCdnDriver(setting, mockLogger.Object);

            // Assert
            Assert.IsNotNull(driver);
            Assert.AreEqual("Fastly", driver.ProviderName);
        }

        /// <summary>
        /// Test: FastlyCdnDriver initializes with soft purge enabled.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.Construction")]
        public void Constructor_WithSoftPurgeEnabled_PreservesConfiguration()
        {
            // Arrange
            var config = new FastlyCdnConfig
            {
                ServiceId = "service-abc",
                ApiToken = "token-xyz",
                Domain = "cdn.example.org",
                SoftPurge = true
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = JsonConvert.SerializeObject(config)
            };

            // Act
            var driver = new FastlyCdnDriver(setting, mockLogger.Object);

            // Assert
            Assert.IsNotNull(driver);
            Assert.AreEqual("Fastly", driver.ProviderName);
        }

        /// <summary>
        /// Test: FastlyCdnDriver throws on invalid JSON configuration.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.Construction")]
        public void Constructor_WithInvalidJson_ThrowsException()
        {
            // Arrange
            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = "invalid json {{"
            };

            // Act & Assert
            try
            {
                _ = new FastlyCdnDriver(setting, mockLogger.Object);
                Assert.Fail("Should have thrown exception for invalid JSON");
            }
            catch (System.Text.Json.JsonException)
            {
                // Expected
            }
        }

        #endregion

        #region Single URL Purge Tests

        /// <summary>
        /// Test: PurgeCdn with single URL succeeds on successful response.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.PurgeSingleUrl")]
        public async Task PurgeCdn_SingleUrl_SuccessfulPurge()
        {
            // Arrange
            var config = new FastlyCdnConfig
            {
                ServiceId = "service-123",
                ApiToken = "token-456",
                Domain = "www.example.com",
                SoftPurge = false
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = JsonConvert.SerializeObject(config)
            };

            var mockResponse = new FastlyPurgeResponse { status = "ok", id = "purge-id-123" };
            var mockHandler = CreateMockHttpMessageHandler(
                HttpStatusCode.OK,
                JsonSerializer.Serialize(mockResponse));

            var driver = new FastlyCdnDriver(setting, mockLogger.Object);
            driver.GetType()
                .GetField("httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(driver, new HttpClient(mockHandler.Object));

            var urls = new List<string> { "/index.html" };

            // Act
            var results = await driver.PurgeCdn(urls);

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].IsSuccessStatusCode);
            Assert.AreEqual("Fastly", results[0].ProviderName);
            Assert.IsTrue(results[0].Message.Contains("Successfully purged"));
        }

        /// <summary>
        /// Test: PurgeCdn with multiple unique URLs processes all.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.PurgeSingleUrl")]
        public async Task PurgeCdn_MultipleUrls_AllProcessed()
        {
            // Arrange
            var config = new FastlyCdnConfig
            {
                ServiceId = "service-789",
                ApiToken = "token-xyz",
                Domain = "cdn.example.com",
                SoftPurge = false
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = JsonConvert.SerializeObject(config)
            };

            var mockResponse = new FastlyPurgeResponse { status = "ok", id = "purge-id-456" };
            var mockHandler = CreateMockHttpMessageHandler(
                HttpStatusCode.OK,
                JsonSerializer.Serialize(mockResponse));

            var driver = new FastlyCdnDriver(setting, mockLogger.Object);
            driver.GetType()
                .GetField("httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(driver, new HttpClient(mockHandler.Object));

            var urls = new List<string> { "/page1.html", "/page2.html", "/assets/style.css" };

            // Act
            var results = await driver.PurgeCdn(urls);

            // Assert
            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(results.All(r => r.IsSuccessStatusCode));
            Assert.IsTrue(results.All(r => r.ProviderName == "Fastly"));
        }

        /// <summary>
        /// Test: PurgeCdn with duplicate URLs deduplicates them.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.PurgeSingleUrl")]
        public async Task PurgeCdn_DuplicateUrls_Deduplicated()
        {
            // Arrange
            var config = new FastlyCdnConfig
            {
                ServiceId = "service-dup",
                ApiToken = "token-dup",
                Domain = "www.test.com",
                SoftPurge = false
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = JsonConvert.SerializeObject(config)
            };

            var mockResponse = new FastlyPurgeResponse { status = "ok", id = "purge-id-dup" };
            var mockHandler = CreateMockHttpMessageHandler(
                HttpStatusCode.OK,
                JsonSerializer.Serialize(mockResponse));

            var driver = new FastlyCdnDriver(setting, mockLogger.Object);
            driver.GetType()
                .GetField("httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(driver, new HttpClient(mockHandler.Object));

            var urls = new List<string> { "/index.html", "/index.html", "/index.html", "/page.html", "/page.html" };

            // Act
            var results = await driver.PurgeCdn(urls);

            // Assert - Should only process 2 unique URLs
            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.IsSuccessStatusCode));
        }

        /// <summary>
        /// Test: PurgeCdn with null URL list returns empty results.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.PurgeSingleUrl")]
        public async Task PurgeCdn_NullUrlList_ReturnsEmpty()
        {
            // Arrange
            var config = new FastlyCdnConfig
            {
                ServiceId = "service-null",
                ApiToken = "token-null",
                Domain = "www.null.com",
                SoftPurge = false
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new FastlyCdnDriver(setting, mockLogger.Object);

            // Act
            var results = await driver.PurgeCdn((List<string>)null);

            // Assert
            Assert.AreEqual(0, results.Count);
        }

        /// <summary>
        /// Test: PurgeCdn with empty URL list returns empty results.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.PurgeSingleUrl")]
        public async Task PurgeCdn_EmptyUrlList_ReturnsEmpty()
        {
            // Arrange
            var config = new FastlyCdnConfig
            {
                ServiceId = "service-empty",
                ApiToken = "token-empty",
                Domain = "www.empty.com",
                SoftPurge = false
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = JsonConvert.SerializeObject(config)
            };

            var driver = new FastlyCdnDriver(setting, mockLogger.Object);

            // Act
            var results = await driver.PurgeCdn(new List<string>());

            // Assert
            Assert.AreEqual(0, results.Count);
        }

        #endregion

        #region Purge All Tests

        /// <summary>
        /// Test: PurgeCdn() purges all content successfully.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.PurgeAll")]
        public async Task PurgeCdn_NoParameters_SuccessfulPurgeAll()
        {
            // Arrange
            var config = new FastlyCdnConfig
            {
                ServiceId = "service-all",
                ApiToken = "token-all",
                Domain = "www.purgeall.com",
                SoftPurge = false
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = JsonConvert.SerializeObject(config)
            };

            var mockResponse = new FastlyPurgeAllResponse { status = "ok" };
            var mockHandler = CreateMockHttpMessageHandler(
                HttpStatusCode.OK,
                JsonSerializer.Serialize(mockResponse));

            var driver = new FastlyCdnDriver(setting, mockLogger.Object);
            driver.GetType()
                .GetField("httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(driver, new HttpClient(mockHandler.Object));

            // Act
            var results = await driver.PurgeCdn();

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].IsSuccessStatusCode);
            Assert.IsTrue(results[0].Message.Contains("Successfully purged all content"));
            Assert.AreEqual("Fastly", results[0].ProviderName);
        }

        /// <summary>
        /// Test: PurgeCdn() includes correct API headers for purge all.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.PurgeAll")]
        public async Task PurgeCdn_NoParameters_IncludesCorrectHeaders()
        {
            // Arrange
            var config = new FastlyCdnConfig
            {
                ServiceId = "service-headers",
                ApiToken = "token-headers-secret",
                Domain = "www.headers.com",
                SoftPurge = false
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = JsonConvert.SerializeObject(config)
            };

            var mockResponse = new FastlyPurgeAllResponse { status = "ok" };
            var capturedRequest = (HttpRequestMessage)null;

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<System.Threading.CancellationToken>())
                .Callback<HttpRequestMessage, System.Threading.CancellationToken>((request, token) => capturedRequest = request)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(mockResponse))
                });

            var driver = new FastlyCdnDriver(setting, mockLogger.Object);
            driver.GetType()
                .GetField("httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(driver, new HttpClient(mockHandler.Object));

            // Act
            var results = await driver.PurgeCdn();

            // Assert
            Assert.IsNotNull(capturedRequest);
            Assert.AreEqual(HttpMethod.Post, capturedRequest.Method);
            Assert.IsTrue(capturedRequest.Headers.Contains("Fastly-Key"));
            Assert.IsTrue(capturedRequest.Headers.Contains("Accept"));
        }

        #endregion

        #region Error Handling Tests

        /// <summary>
        /// Test: PurgeCdn handles HTTP failure gracefully.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.ErrorHandling")]
        public async Task PurgeCdn_HttpFailure_ReturnsFailureResult()
        {
            // Arrange
            var config = new FastlyCdnConfig
            {
                ServiceId = "service-fail",
                ApiToken = "token-fail",
                Domain = "www.fail.com",
                SoftPurge = false
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = JsonConvert.SerializeObject(config)
            };

            var mockHandler = CreateMockHttpMessageHandler(
                HttpStatusCode.Unauthorized,
                "{ \"error\": \"Unauthorized\" }");

            var driver = new FastlyCdnDriver(setting, mockLogger.Object);
            driver.GetType()
                .GetField("httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(driver, new HttpClient(mockHandler.Object));

            var urls = new List<string> { "/test.html" };

            // Act
            var results = await driver.PurgeCdn(urls);

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].IsSuccessStatusCode);
            Assert.IsTrue(results[0].Message.Contains("Failed to purge"));
        }

        /// <summary>
        /// Test: PurgeCdn catches and logs exceptions.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.ErrorHandling")]
        public async Task PurgeCdn_RequestThrows_CatchesExceptionReturnsFailure()
        {
            // Arrange
            var config = new FastlyCdnConfig
            {
                ServiceId = "service-exception",
                ApiToken = "token-exception",
                Domain = "www.exception.com",
                SoftPurge = false
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = JsonConvert.SerializeObject(config)
            };

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<System.Threading.CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            var driver = new FastlyCdnDriver(setting, mockLogger.Object);
            driver.GetType()
                .GetField("httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(driver, new HttpClient(mockHandler.Object));

            var urls = new List<string> { "/error.html" };

            // Act
            var results = await driver.PurgeCdn(urls);

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].IsSuccessStatusCode);
            Assert.IsTrue(results[0].Message.Contains("Error"));
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        /// <summary>
        /// Test: PurgeCdn handles mixed success and failure URLs.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.ErrorHandling")]
        public async Task PurgeCdn_MixedResults_ProcessesAllReturnsResults()
        {
            // Arrange
            var config = new FastlyCdnConfig
            {
                ServiceId = "service-mixed",
                ApiToken = "token-mixed",
                Domain = "www.mixed.com",
                SoftPurge = false
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = JsonConvert.SerializeObject(config)
            };

            var successCount = 0;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<System.Threading.CancellationToken>())
                .Returns<HttpRequestMessage, System.Threading.CancellationToken>((request, token) =>
                {
                    successCount++;
                    var statusCode = successCount % 2 == 0 ? HttpStatusCode.BadRequest : HttpStatusCode.OK;
                    var mockResponse = new FastlyPurgeResponse { status = "ok", id = $"id-{successCount}" };
                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = statusCode,
                        Content = new StringContent(JsonSerializer.Serialize(mockResponse))
                    });
                });

            var driver = new FastlyCdnDriver(setting, mockLogger.Object);
            driver.GetType()
                .GetField("httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(driver, new HttpClient(mockHandler.Object));

            var urls = new List<string> { "/url1.html", "/url2.html", "/url3.html" };

            // Act
            var results = await driver.PurgeCdn(urls);

            // Assert
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(2, results.Count(r => r.IsSuccessStatusCode));
            Assert.AreEqual(1, results.Count(r => !r.IsSuccessStatusCode));
        }

        #endregion

        #region Soft Purge Tests

        /// <summary>
        /// Test: PurgeCdn includes soft purge header when configured.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.SoftPurge")]
        public async Task PurgeCdn_SoftPurgeEnabled_IncludesSoftPurgeHeader()
        {
            // Arrange
            var config = new FastlyCdnConfig
            {
                ServiceId = "service-soft",
                ApiToken = "token-soft",
                Domain = "www.soft.com",
                SoftPurge = true
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = JsonConvert.SerializeObject(config)
            };

            var mockResponse = new FastlyPurgeResponse { status = "ok", id = "purge-soft" };
            var capturedRequest = (HttpRequestMessage)null;

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<System.Threading.CancellationToken>())
                .Callback<HttpRequestMessage, System.Threading.CancellationToken>((request, token) => capturedRequest = request)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(mockResponse))
                });

            var driver = new FastlyCdnDriver(setting, mockLogger.Object);
            driver.GetType()
                .GetField("httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(driver, new HttpClient(mockHandler.Object));

            var urls = new List<string> { "/soft.html" };

            // Act
            var results = await driver.PurgeCdn(urls);

            // Assert
            Assert.IsNotNull(capturedRequest);
            Assert.IsTrue(capturedRequest.Headers.Contains("Fastly-Soft-Purge"));
            var softPurgeHeader = capturedRequest.Headers.GetValues("Fastly-Soft-Purge").FirstOrDefault();
            Assert.AreEqual("1", softPurgeHeader);
        }

        /// <summary>
        /// Test: PurgeCdn excludes soft purge header when disabled.
        /// </summary>
        [TestMethod]
        [TestCategory("FastlyCdnDriver.SoftPurge")]
        public async Task PurgeCdn_SoftPurgeDisabled_ExcludesSoftPurgeHeader()
        {
            // Arrange
            var config = new FastlyCdnConfig
            {
                ServiceId = "service-hard",
                ApiToken = "token-hard",
                Domain = "www.hard.com",
                SoftPurge = false
            };

            var setting = new CdnSetting
            {
                CdnProvider = CdnProviderEnum.Fastly,
                Value = JsonConvert.SerializeObject(config)
            };

            var mockResponse = new FastlyPurgeResponse { status = "ok", id = "purge-hard" };
            var capturedRequest = (HttpRequestMessage)null;

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<System.Threading.CancellationToken>())
                .Callback<HttpRequestMessage, System.Threading.CancellationToken>((request, token) => capturedRequest = request)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(mockResponse))
                });

            var driver = new FastlyCdnDriver(setting, mockLogger.Object);
            driver.GetType()
                .GetField("httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(driver, new HttpClient(mockHandler.Object));

            var urls = new List<string> { "/hard.html" };

            // Act
            var results = await driver.PurgeCdn(urls);

            // Assert
            Assert.IsNotNull(capturedRequest);
            Assert.IsFalse(capturedRequest.Headers.Contains("Fastly-Soft-Purge"));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Helper to create a mock HTTP message handler with specified status and content.
        /// </summary>
        private Mock<HttpMessageHandler> CreateMockHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
        {
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<System.Threading.CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(responseContent)
                });

            return mockHandler;
        }

        /// <summary>
        /// Internal response model for Fastly single URL purge.
        /// </summary>
        private class FastlyPurgeResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("status")]
            public string status { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string id { get; set; }
        }

        /// <summary>
        /// Internal response model for Fastly purge all.
        /// </summary>
        private class FastlyPurgeAllResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("status")]
            public string status { get; set; }
        }

        #endregion
    }
}
