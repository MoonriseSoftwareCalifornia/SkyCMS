// <copyright file="ConnectionStringParserTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

using Cosmos.BlobService;
using Cosmos.BlobService.Exceptions;
using Cosmos.BlobService.Models;

namespace Sky.Tests.BlobStorage
{
    [TestClass]
    public class ConnectionStringParserTests
    {
        [TestMethod]
        public void ParseAzureConnectionString_WithEqualsInAccountKey_ParsesAccountName()
        {
            // Arrange
            var connectionString = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=abc123==;EndpointSuffix=core.windows.net";

            // Act
            var result = ConnectionStringParser.ParseAzureConnectionString(connectionString);

            // Assert
            Assert.AreEqual("myaccount", result.AccountName);
            Assert.IsFalse(result.UsesAccessToken);
        }

        [TestMethod]
        public void ParseAzureConnectionString_MissingAccountName_ThrowsInvalidConnectionStringException()
        {
            // Arrange
            var connectionString = "DefaultEndpointsProtocol=https;AccountKey=abc123==;EndpointSuffix=core.windows.net";

            // Act + Assert
            try
            {
                _ = ConnectionStringParser.ParseAzureConnectionString(connectionString);
                Assert.Fail("Expected InvalidConnectionStringException was not thrown.");
            }
            catch (InvalidConnectionStringException exception)
            {
                Assert.AreEqual(CloudStorageProvider.Azure, exception.AttemptedProvider);
            }
        }

        [TestMethod]
        public void ParseAmazonConnectionString_WithEqualsInSecret_ParsesRequiredFields()
        {
            // Arrange
            var connectionString = "Bucket=test-bucket;Region=us-east-1;KeyId=my-key;Key=secret==";

            // Act
            var result = ConnectionStringParser.ParseAmazonConnectionString(connectionString);

            // Assert
            Assert.AreEqual("test-bucket", result.BucketName);
            Assert.AreEqual("us-east-1", result.Region);
            Assert.AreEqual("my-key", result.KeyId);
            Assert.AreEqual("secret==", result.Key);
        }

        [TestMethod]
        public void UtilitiesGetContentType_WithNullMetadata_ReturnsDefaultType()
        {
            // Act
            var contentType = Utilities.GetContentType((FileUploadMetaData)null);

            // Assert
            Assert.AreEqual("application/octet-stream", contentType);
        }
    }
}
