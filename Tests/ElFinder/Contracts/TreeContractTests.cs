// <copyright file="TreeContractTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// </copyright>

namespace Sky.Tests.ElFinder.Contracts
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Handlers;
    using SkyCMS.Drivers.ElFinder.Responses;

    /// <summary>
    /// Contract tests for the <c>tree</c> command.
    ///
    /// KEY CONTRACT RULES (from Docs/commands/tree.md):
    ///   - Response root key must be "tree" (lowercase array).
    ///   - Only directories are returned (mime = "directory").
    ///   - Every entry must be a valid elFinder file object.
    ///   - Entries must have "phash" pointing to the requested target.
    /// </summary>
    [TestClass]
    public class TreeContractTests : ElFinderContractTestBase
    {
        private TreeCommandHandler _handler = null!;

        [TestInitialize]
        public void Setup()
        {
            _handler = new TreeCommandHandler(BuildAdapter().Object, BuildPassThroughNameResolver());
        }

        [TestMethod]
        [Description("Response must have lowercase 'tree' key containing an array.")]
        public async Task Tree_ResponseKey_IsLowercaseTreeArray()
        {
            var command = new TreeCommand { Target = RootHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            AssertArrayProperty(doc.RootElement, "tree");
        }

        [TestMethod]
        [Description("All entries in 'tree' must be valid elFinder file objects.")]
        public async Task Tree_Entries_AreValidElFinderObjects()
        {
            var command = new TreeCommand { Target = RootHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var tree = AssertArrayProperty(doc.RootElement, "tree");
            var index = 0;
            foreach (var entry in tree.EnumerateArray())
            {
                AssertElFinderObject(entry, $"tree[{index}]");
                index++;
            }
        }

        [TestMethod]
        [Description("Tree command returns only directories — no files.")]
        public async Task Tree_Entries_AreDirectoriesOnly()
        {
            var command = new TreeCommand { Target = RootHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            var tree = AssertArrayProperty(doc.RootElement, "tree");
            var index = 0;
            foreach (var entry in tree.EnumerateArray())
            {
                var mime = AssertStringProperty(entry, "mime");
                Assert.AreEqual("directory", mime,
                    $"tree[{index}].mime must be 'directory'. Got '{mime}'. " +
                    $"The tree command must return only directories — files should not appear.");
                index++;
            }
        }

        [TestMethod]
        [Description("No PascalCase keys should appear in the response.")]
        public async Task Tree_NoPascalCaseKeysLeak()
        {
            var command = new TreeCommand { Target = RootHash };
            var response = await _handler.Handle(command, default);
            using var doc = SerializeResponse(response);

            foreach (var forbiddenKey in new[] { "Tree", "Hash", "Name", "Mime" })
            {
                Assert.IsFalse(
                    doc.RootElement.TryGetProperty(forbiddenKey, out _),
                    $"PascalCase key '{forbiddenKey}' found — STJ must be used, not Newtonsoft.");
            }
        }

        [TestMethod]
        [Description("Invalid hash returns an error response.")]
        public async Task Tree_InvalidHash_ReturnsErrorResponse()
        {
            var command = new TreeCommand { Target = "not_a_hash" };
            var response = await _handler.Handle(command, default);

            Assert.IsTrue(response is ElFinderErrorResponse,
                "Invalid hash must return ElFinderErrorResponse.");

            using var doc = SerializeResponse(response);
            Assert.IsTrue(doc.RootElement.TryGetProperty("error", out _),
                "Error response must contain 'error' key.");
        }

        // ------------------------------------------------------------------ //
        //  Article-path: title substitution and dual-path contract             //
        // ------------------------------------------------------------------ //

        [TestMethod]
        [Description("When expanding /pub/articles, the article-folder entry 'name' must be the article title.")]
        public async Task Tree_ArticleFolder_NameIsArticleTitle()
        {
            var adapter = BuildAdapterWithArticles();
            var resolver = BuildArticleTitleNameResolver(ArticleNumber, ArticleTitle);
            var handler = new TreeCommandHandler(adapter.Object, resolver);

            var response = await handler.Handle(new TreeCommand { Target = ArticlesRootHash }, default);
            using var doc = SerializeResponse(response);

            var tree = AssertArrayProperty(doc.RootElement, "tree", minLength: 1);
            var found = false;
            foreach (var entry in tree.EnumerateArray())
            {
                if (entry.TryGetProperty("name", out var nameProp) &&
                    string.Equals(nameProp.GetString(), ArticleTitle, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found,
                $"No tree entry with name='{ArticleTitle}' found. " +
                $"Article folders under /pub/articles/{{number}} must display the article title in 'name'.");
        }

        [TestMethod]
        [Description("Article folder entry in tree must carry 'realPath' = '/pub/articles/42'.")]
        public async Task Tree_ArticleFolder_RealPathIsCanonicalStoragePath()
        {
            var adapter = BuildAdapterWithArticles();
            var resolver = BuildArticleTitleNameResolver(ArticleNumber, ArticleTitle);
            var handler = new TreeCommandHandler(adapter.Object, resolver);

            var response = await handler.Handle(new TreeCommand { Target = ArticlesRootHash }, default);
            using var doc = SerializeResponse(response);

            var tree = AssertArrayProperty(doc.RootElement, "tree", minLength: 1);
            string? foundRealPath = null;
            foreach (var entry in tree.EnumerateArray())
            {
                if (!entry.TryGetProperty("name", out var nameProp) ||
                    !string.Equals(nameProp.GetString(), ArticleTitle, StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.IsTrue(entry.TryGetProperty("realPath", out var rp),
                    $"Article folder entry (name='{ArticleTitle}') in tree must contain 'realPath' so that " +
                    $"the SkyCMS Explorer can identify the canonical storage path alongside the friendly name.");
                foundRealPath = rp.GetString();
                break;
            }

            Assert.IsNotNull(foundRealPath,
                $"Could not find the article entry (name='{ArticleTitle}') in tree array.");
            Assert.AreEqual(ArticleRealPath, foundRealPath,
                $"realPath must equal the canonical storage path '{ArticleRealPath}', not '{foundRealPath}'.");
        }

        [TestMethod]
        [Description("Non-article folders must NOT carry 'realPath' in tree responses.")]
        public async Task Tree_NonArticleFolder_RealPathIsAbsent()
        {
            var adapter = BuildAdapterWithArticles();
            var resolver = BuildPassThroughNameResolver();
            var handler = new TreeCommandHandler(adapter.Object, resolver);

            // Expand root — children are images/, docs/, articles/ (no title substitution here).
            var response = await handler.Handle(new TreeCommand { Target = RootHash }, default);
            using var doc = SerializeResponse(response);

            var tree = AssertArrayProperty(doc.RootElement, "tree");
            foreach (var entry in tree.EnumerateArray())
            {
                if (!entry.TryGetProperty("name", out var nameProp))
                {
                    continue;
                }

                var entryName = nameProp.GetString() ?? string.Empty;
                if (entryName is "images" or "docs" or "articles")
                {
                    Assert.IsFalse(entry.TryGetProperty("realPath", out _),
                        $"Plain folder '{entryName}' must NOT have 'realPath'. " +
                        $"The field is omitted (WhenWritingNull) for entries whose name was not substituted.");
                }
            }
        }
    }
}
