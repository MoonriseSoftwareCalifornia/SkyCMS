// <copyright file="CloudFrontXmlGenerationTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.Services.CDN
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for XML generation and validation.
    /// </summary>
    [TestClass]
    public class CloudFrontXmlGenerationTests
    {
        [TestMethod]
        public void XmlPayload_WithSpecialCharacters_EscapesCorrectly()
        {
            // Arrange
            var paths = new List<string> { "/test<file>.html", "/test&page.html", "/test\"quote\".html" };

            // Act
            var pathItems = string.Join(string.Empty, paths.Select(p => 
                $"<Path>{System.Security.SecurityElement.Escape(p)}</Path>"));

            var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<InvalidationBatch>
    <Paths>
        <Quantity>{paths.Count}</Quantity>
        <Items>
            {pathItems}
        </Items>
    </Paths>
    <CallerReference>test-ref</CallerReference>
</InvalidationBatch>";

            // Assert
            // Parse to ensure valid XML
            var doc = XDocument.Parse(xml);
            Assert.IsNotNull(doc);
            
            var pathElements = doc.Descendants("Path").ToList();
            Assert.AreEqual(3, pathElements.Count);
            
            // Verify escaping
            Assert.IsFalse(xml.Contains("<file>"));
            Assert.IsFalse(xml.Contains("&page"));
            Assert.IsTrue(xml.Contains("&lt;") || xml.Contains("&amp;") || xml.Contains("&quot;"));
        }

        [TestMethod]
        public void XmlPayload_WithUnicodeCharacters_HandlesCorrectly()
        {
            // Arrange
            var paths = new List<string> { "/tëst.html", "/文件.html", "/тест.html" };

            // Act
            var pathItems = string.Join(string.Empty, paths.Select(p => 
                $"<Path>{System.Security.SecurityElement.Escape(p)}</Path>"));

            var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<InvalidationBatch>
    <Paths>
        <Quantity>{paths.Count}</Quantity>
        <Items>
            {pathItems}
        </Items>
    </Paths>
    <CallerReference>test-ref</CallerReference>
</InvalidationBatch>";

            // Assert
            var doc = XDocument.Parse(xml);
            Assert.IsNotNull(doc);
            Assert.AreEqual(3, doc.Descendants("Path").Count());
        }

        [TestMethod]
        public void XmlPayload_WithMaxPaths_GeneratesValidXml()
        {
            // Arrange - CloudFront allows up to 3000 paths per invalidation
            var paths = Enumerable.Range(1, 3000).Select(i => $"/path{i}.html").ToList();

            // Act
            var pathItems = string.Join(string.Empty, paths.Select(p => 
                $"<Path>{System.Security.SecurityElement.Escape(p)}</Path>"));

            var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<InvalidationBatch>
    <Paths>
        <Quantity>{paths.Count}</Quantity>
        <Items>
            {pathItems}
        </Items>
    </Paths>
    <CallerReference>test-ref</CallerReference>
</InvalidationBatch>";

            // Assert
            var doc = XDocument.Parse(xml);
            Assert.IsNotNull(doc);
            
            var quantity = doc.Descendants("Quantity").First().Value;
            Assert.AreEqual("3000", quantity);
            
            var pathCount = doc.Descendants("Path").Count();
            Assert.AreEqual(3000, pathCount);
        }
    }
}