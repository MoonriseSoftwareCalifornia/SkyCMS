// <copyright file="ArticleLogicUtilitiesTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// </copyright>

namespace Cosmos.Common.Tests.Utilities
{
    using Cosmos.Common.Utilities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Text;

    /// <summary>
    /// Comprehensive tests for ArticleLogicUtilities.
    /// Target: 100% code coverage.
    /// </summary>
    [TestClass]
    public class ArticleLogicUtilitiesTests
    {
        #region Serialize Tests

        [TestMethod]
        public void Serialize_WithNull_ReturnsNull()
        {
            // Arrange
            object obj = null;

            // Act
            var result = ArticleLogicUtilities.Serialize(obj);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Serialize_WithSimpleObject_ReturnsUtf32Bytes()
        {
            // Arrange
            var obj = new { Name = "Test", Value = 123 };

            // Act
            var result = ArticleLogicUtilities.Serialize(obj);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);

            // Verify it's UTF-32 encoded JSON
            var json = Encoding.UTF32.GetString(result);
            Assert.IsTrue(json.Contains("\"Name\""));
            Assert.IsTrue(json.Contains("\"Test\""));
            Assert.IsTrue(json.Contains("\"Value\""));
            Assert.IsTrue(json.Contains("123"));
        }

        [TestMethod]
        public void Serialize_WithString_ReturnsUtf32Bytes()
        {
            // Arrange
            string obj = "Test String";

            // Act
            var result = ArticleLogicUtilities.Serialize(obj);

            // Assert
            Assert.IsNotNull(result);
            var json = Encoding.UTF32.GetString(result);
            Assert.AreEqual("\"Test String\"", json);
        }

        [TestMethod]
        public void Serialize_WithInteger_ReturnsUtf32Bytes()
        {
            // Arrange
            int obj = 42;

            // Act
            var result = ArticleLogicUtilities.Serialize(obj);

            // Assert
            Assert.IsNotNull(result);
            var json = Encoding.UTF32.GetString(result);
            Assert.AreEqual("42", json);
        }

        [TestMethod]
        public void Serialize_WithBoolean_ReturnsUtf32Bytes()
        {
            // Arrange
            bool obj = true;

            // Act
            var result = ArticleLogicUtilities.Serialize(obj);

            // Assert
            Assert.IsNotNull(result);
            var json = Encoding.UTF32.GetString(result);
            Assert.AreEqual("true", json);
        }

        [TestMethod]
        public void Serialize_WithArray_ReturnsUtf32Bytes()
        {
            // Arrange
            var obj = new[] { 1, 2, 3, 4, 5 };

            // Act
            var result = ArticleLogicUtilities.Serialize(obj);

            // Assert
            Assert.IsNotNull(result);
            var json = Encoding.UTF32.GetString(result);
            Assert.IsTrue(json.Contains("["));
            Assert.IsTrue(json.Contains("1"));
            Assert.IsTrue(json.Contains("5"));
            Assert.IsTrue(json.Contains("]"));
        }

        [TestMethod]
        public void Serialize_WithComplexObject_ReturnsUtf32Bytes()
        {
            // Arrange
            var obj = new TestClass
            {
                Id = Guid.NewGuid(),
                Name = "Complex Object",
                Count = 99,
                IsActive = true,
                Tags = new[] { "tag1", "tag2", "tag3" }
            };

            // Act
            var result = ArticleLogicUtilities.Serialize(obj);

            // Assert
            Assert.IsNotNull(result);
            var json = Encoding.UTF32.GetString(result);
            Assert.IsTrue(json.Contains(obj.Id.ToString()));
            Assert.IsTrue(json.Contains("Complex Object"));
            Assert.IsTrue(json.Contains("99"));
            Assert.IsTrue(json.Contains("true"));
            Assert.IsTrue(json.Contains("tag1"));
        }

        [TestMethod]
        public void Serialize_WithEmptyString_ReturnsUtf32Bytes()
        {
            // Arrange
            string obj = string.Empty;

            // Act
            var result = ArticleLogicUtilities.Serialize(obj);

            // Assert
            Assert.IsNotNull(result);
            var json = Encoding.UTF32.GetString(result);
            Assert.AreEqual("\"\"", json);
        }

        [TestMethod]
        public void Serialize_WithNestedObject_ReturnsUtf32Bytes()
        {
            // Arrange
            var obj = new
            {
                Outer = new
                {
                    Inner = new
                    {
                        Value = "Nested"
                    }
                }
            };

            // Act
            var result = ArticleLogicUtilities.Serialize(obj);

            // Assert
            Assert.IsNotNull(result);
            var json = Encoding.UTF32.GetString(result);
            Assert.IsTrue(json.Contains("Outer"));
            Assert.IsTrue(json.Contains("Inner"));
            Assert.IsTrue(json.Contains("Nested"));
        }

        [TestMethod]
        public void Serialize_WithSpecialCharacters_ReturnsUtf32Bytes()
        {
            // Arrange
            var obj = new { Text = "Special chars: \"quotes\", \\backslash\\, /forward/, \r\n newline" };

            // Act
            var result = ArticleLogicUtilities.Serialize(obj);

            // Assert
            Assert.IsNotNull(result);
            var json = Encoding.UTF32.GetString(result);
            // JSON should escape special characters
            Assert.IsTrue(json.Contains("\\\""));
            Assert.IsTrue(json.Contains("\\\\"));
        }

        #endregion

        #region Deserialize Tests

        [TestMethod]
        public void Deserialize_WithSimpleObject_ReturnsDeserializedObject()
        {
            // Arrange
            var original = new TestClass
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                Count = 42,
                IsActive = true
            };
            var bytes = ArticleLogicUtilities.Serialize(original);

            // Act
            var result = ArticleLogicUtilities.Deserialize<TestClass>(bytes);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(original.Id, result.Id);
            Assert.AreEqual(original.Name, result.Name);
            Assert.AreEqual(original.Count, result.Count);
            Assert.AreEqual(original.IsActive, result.IsActive);
        }

        [TestMethod]
        public void Deserialize_WithString_ReturnsString()
        {
            // Arrange
            string original = "Test String Value";
            var bytes = ArticleLogicUtilities.Serialize(original);

            // Act
            var result = ArticleLogicUtilities.Deserialize<string>(bytes);

            // Assert
            Assert.AreEqual(original, result);
        }

        [TestMethod]
        public void Deserialize_WithInteger_ReturnsInteger()
        {
            // Arrange
            int original = 999;
            var bytes = ArticleLogicUtilities.Serialize(original);

            // Act
            var result = ArticleLogicUtilities.Deserialize<int>(bytes);

            // Assert
            Assert.AreEqual(original, result);
        }

        [TestMethod]
        public void Deserialize_WithBoolean_ReturnsBoolean()
        {
            // Arrange
            bool original = false;
            var bytes = ArticleLogicUtilities.Serialize(original);

            // Act
            var result = ArticleLogicUtilities.Deserialize<bool>(bytes);

            // Assert
            Assert.AreEqual(original, result);
        }

        [TestMethod]
        public void Deserialize_WithArray_ReturnsArray()
        {
            // Arrange
            var original = new[] { "apple", "banana", "cherry" };
            var bytes = ArticleLogicUtilities.Serialize(original);

            // Act
            var result = ArticleLogicUtilities.Deserialize<string[]>(bytes);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual("apple", result[0]);
            Assert.AreEqual("banana", result[1]);
            Assert.AreEqual("cherry", result[2]);
        }

        [TestMethod]
        public void Deserialize_WithComplexObject_PreservesAllProperties()
        {
            // Arrange
            var original = new TestClass
            {
                Id = Guid.NewGuid(),
                Name = "Complex",
                Count = 100,
                IsActive = false,
                Tags = new[] { "a", "b", "c" }
            };
            var bytes = ArticleLogicUtilities.Serialize(original);

            // Act
            var result = ArticleLogicUtilities.Deserialize<TestClass>(bytes);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(original.Id, result.Id);
            Assert.AreEqual(original.Name, result.Name);
            Assert.AreEqual(original.Count, result.Count);
            Assert.AreEqual(original.IsActive, result.IsActive);
            Assert.IsNotNull(result.Tags);
            CollectionAssert.AreEqual(original.Tags, result.Tags);
        }

        [TestMethod]
        public void Deserialize_WithManualUtf32JsonBytes_ReturnsCorrectObject()
        {
            // Arrange - manually create UTF-32 encoded JSON
            var json = "{\"Name\":\"Manual\",\"Value\":777}";
            var bytes = Encoding.UTF32.GetBytes(json);

            // Act
            var result = ArticleLogicUtilities.Deserialize<Dictionary<string, object>>(bytes);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Manual", result["Name"].ToString());
            Assert.AreEqual(777L, result["Value"]); // Newtonsoft returns long for numbers
        }

        [TestMethod]
        public void Deserialize_WithEmptyString_ReturnsEmptyString()
        {
            // Arrange
            var original = string.Empty;
            var bytes = ArticleLogicUtilities.Serialize(original);

            // Act
            var result = ArticleLogicUtilities.Deserialize<string>(bytes);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Deserialize_WithNull_ReturnsNull()
        {
            // Arrange
            var json = "null";
            var bytes = Encoding.UTF32.GetBytes(json);

            // Act
            var result = ArticleLogicUtilities.Deserialize<TestClass>(bytes);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Deserialize_WithUnicodeCharacters_PreservesUnicode()
        {
            // Arrange
            var original = new { Text = "Unicode: 你好世界 🚀 émojis" };
            var bytes = ArticleLogicUtilities.Serialize(original);

            // Act
            var result = ArticleLogicUtilities.Deserialize<Dictionary<string, object>>(bytes);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Unicode: 你好世界 🚀 émojis", result["Text"].ToString());
        }

        #endregion

        #region Round-Trip Tests

        [TestMethod]
        public void SerializeDeserialize_RoundTrip_PreservesData()
        {
            // Arrange
            var original = new TestClass
            {
                Id = Guid.NewGuid(),
                Name = "RoundTrip Test",
                Count = 123,
                IsActive = true,
                Tags = new[] { "one", "two", "three" }
            };

            // Act
            var bytes = ArticleLogicUtilities.Serialize(original);
            var deserialized = ArticleLogicUtilities.Deserialize<TestClass>(bytes);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.Id, deserialized.Id);
            Assert.AreEqual(original.Name, deserialized.Name);
            Assert.AreEqual(original.Count, deserialized.Count);
            Assert.AreEqual(original.IsActive, deserialized.IsActive);
            CollectionAssert.AreEqual(original.Tags, deserialized.Tags);
        }

        [TestMethod]
        public void SerializeDeserialize_MultipleTypes_AllPreserved()
        {
            // Test multiple primitive types
            var stringOriginal = "test";
            var intOriginal = 42;
            var boolOriginal = true;
            var doubleOriginal = 3.14159;

            var stringBytes = ArticleLogicUtilities.Serialize(stringOriginal);
            var intBytes = ArticleLogicUtilities.Serialize(intOriginal);
            var boolBytes = ArticleLogicUtilities.Serialize(boolOriginal);
            var doubleBytes = ArticleLogicUtilities.Serialize(doubleOriginal);

            var stringResult = ArticleLogicUtilities.Deserialize<string>(stringBytes);
            var intResult = ArticleLogicUtilities.Deserialize<int>(intBytes);
            var boolResult = ArticleLogicUtilities.Deserialize<bool>(boolBytes);
            var doubleResult = ArticleLogicUtilities.Deserialize<double>(doubleBytes);

            Assert.AreEqual(stringOriginal, stringResult);
            Assert.AreEqual(intOriginal, intResult);
            Assert.AreEqual(boolOriginal, boolResult);
            Assert.AreEqual(doubleOriginal, doubleResult);
        }

        #endregion

        #region GetPublisherHealth Tests

        [TestMethod]
        public void GetPublisherHealth_AlwaysReturnsTrue()
        {
            // Act
            var result = ArticleLogicUtilities.GetPublisherHealth();

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void GetPublisherHealth_MultipleInvocations_AlwaysReturnsTrue()
        {
            // Act & Assert
            for (int i = 0; i < 100; i++)
            {
                Assert.IsTrue(ArticleLogicUtilities.GetPublisherHealth(), 
                    $"Health check failed on iteration {i}");
            }
        }

        #endregion

        #region Test Helper Classes

        /// <summary>
        /// Test class for serialization/deserialization tests.
        /// </summary>
        private class TestClass
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public int Count { get; set; }
            public bool IsActive { get; set; }
            public string[] Tags { get; set; }
        }

        #endregion
    }
}
