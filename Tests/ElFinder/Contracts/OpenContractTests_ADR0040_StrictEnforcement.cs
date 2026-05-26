// <copyright file="OpenContractTests_ADR0040_StrictEnforcement.cs" company="Moonrise Software, LLC">
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

    /// <summary>
    /// ADR 0040 strict enforcement tests for the <c>open</c> command.
    /// These tests ensure that displayPath and name fields NEVER contain article IDs (integers)
    /// and always show article titles instead.
    ///
    /// DIFFERENCE FROM EXISTING TESTS:
    /// - Existing tests validate that the correct title IS present (positive assertion).
    /// - These tests validate that integers are NOT present (negative assertion).
    /// - Both are necessary for complete ADR 0040 contract enforcement.
    /// </summary>
    [TestClass]
    public class OpenContractTests_ADR0040_StrictEnforcement : ElFinderContractTestBase
    {
        [TestMethod]
        [Description("ADR 0040 STRICT: When opening /pub/articles, NO child 'name' field may contain only digits.")]
        public async Task Open_ArticlesRoot_NoChildNameMayBeInteger()
        {
            var adapter = BuildAdapterWithArticles();
            var resolver = BuildTitleNameResolver(ArticleNumber, ArticleTitle, TemplateId, TemplateTitle);
            var handler = new OpenCommandHandler(adapter.Object, resolver);

            var response = await handler.HandleAsync(new OpenCommand(target: ArticlesRootHash), default);
            using var doc = SerializeResponse(response);

            var files = AssertArrayProperty(doc.RootElement, "files", minLength: 1);
            var index = 0;
            foreach (var entry in files.EnumerateArray())
            {
                if (!entry.TryGetProperty("name", out var nameProp))
                {
                    continue;
                }

                var name = nameProp.GetString();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                // Check if this is an article-related entry by looking at the path structure
                var isArticleEntry = false;
                if (entry.TryGetProperty("realPath", out var realPathProp))
                {
                    var realPath = realPathProp.GetString();
                    if (!string.IsNullOrEmpty(realPath))
                    {
                        var segments = realPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                        isArticleEntry = segments.Length >= 3
                            && segments[0].Equals("pub", StringComparison.OrdinalIgnoreCase)
                            && segments[1].Equals("articles", StringComparison.OrdinalIgnoreCase);
                    }
                }

                // If this is an article entry, its name must NOT be purely numeric
                if (isArticleEntry && int.TryParse(name, out var parsedId))
                {
                    Assert.Fail(
                        $"ADR 0040 VIOLATION: files[{index}].name is an integer ('{name}') instead of an article title. " +
                        $"When listing /pub/articles, all article folder names must show article titles, not numeric IDs. " +
                        $"Expected: article title like 'My Great Article'. " +
                        $"Got: numeric ID '{parsedId}'. " +
                        $"This indicates ArticleTitleNameResolver failed to resolve the article title.");
                }

                index++;
            }
        }

        [TestMethod]
        [Description("ADR 0040 STRICT: When opening /pub/articles, NO child 'displayPath' may contain /pub/articles/{integer}.")]
        public async Task Open_ArticlesRoot_NoChildDisplayPathMayContainInteger()
        {
            var adapter = BuildAdapterWithArticles();
            var resolver = BuildTitleNameResolver(ArticleNumber, ArticleTitle, TemplateId, TemplateTitle);
            var handler = new OpenCommandHandler(adapter.Object, resolver);

            var response = await handler.HandleAsync(new OpenCommand(target: ArticlesRootHash), default);
            using var doc = SerializeResponse(response);

            var files = AssertArrayProperty(doc.RootElement, "files", minLength: 1);
            var index = 0;
            foreach (var entry in files.EnumerateArray())
            {
                if (entry.TryGetProperty("displayPath", out var displayPathProp))
                {
                    var displayPath = displayPathProp.GetString();
                    AssertDisplayPathDoesNotContainArticleInteger(displayPath, $"files[{index}].displayPath");
                }

                index++;
            }
        }

        [TestMethod]
        [Description("ADR 0040 STRICT: When opening an article folder /pub/articles/42, cwd 'name' must NOT be the integer.")]
        public async Task Open_ArticleFolder_CwdNameMustNotBeInteger()
        {
            var adapter = BuildAdapterWithArticles();
            var resolver = BuildTitleNameResolver(ArticleNumber, ArticleTitle, TemplateId, TemplateTitle);
            var handler = new OpenCommandHandler(adapter.Object, resolver);

            var response = await handler.HandleAsync(new OpenCommand(target: ArticleFolderHash), default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(doc.RootElement.TryGetProperty("cwd", out var cwd), "Response must contain 'cwd'.");
            var name = AssertStringProperty(cwd, "name");

            // The name must NOT be the article number
            Assert.IsFalse(
                string.Equals(name, ArticleNumber.ToString(), StringComparison.Ordinal),
                $"ADR 0040 VIOLATION: cwd.name is the article ID ('{ArticleNumber}') instead of the article title. " +
                $"Expected: '{ArticleTitle}'. Got: '{name}'. " +
                $"Article folder names must show titles, not numeric IDs.");

            // Extra validation: the name should not be ONLY digits
            Assert.IsFalse(
                int.TryParse(name, out _),
                $"ADR 0040 VIOLATION: cwd.name is a pure integer ('{name}'). " +
                $"Expected article title '{ArticleTitle}', not a numeric ID.");
        }

        [TestMethod]
        [Description("ADR 0040 STRICT: When opening an article folder, cwd 'displayPath' must NOT contain /pub/articles/{integer}.")]
        public async Task Open_ArticleFolder_CwdDisplayPathMustNotContainInteger()
        {
            var adapter = BuildAdapterWithArticles();
            var resolver = BuildTitleNameResolver(ArticleNumber, ArticleTitle, TemplateId, TemplateTitle);
            var handler = new OpenCommandHandler(adapter.Object, resolver);

            var response = await handler.HandleAsync(new OpenCommand(target: ArticleFolderHash), default);
            using var doc = SerializeResponse(response);

            Assert.IsTrue(doc.RootElement.TryGetProperty("cwd", out var cwd), "Response must contain 'cwd'.");
            if (cwd.TryGetProperty("displayPath", out var displayPathProp))
            {
                var displayPath = displayPathProp.GetString();
                AssertDisplayPathDoesNotContainArticleInteger(displayPath, "cwd.displayPath");
            }
        }

        [TestMethod]
        [Description("ADR 0040 STRICT: Deep article paths must NOT have integers in displayPath third segment.")]
        public async Task Open_DeepArticlePath_DisplayPathMustNotContainInteger()
        {
            var adapter = BuildAdapterWithArticles();
            var resolver = BuildTitleNameResolver(ArticleNumber, ArticleTitle, TemplateId, TemplateTitle);
            var handler = new OpenCommandHandler(adapter.Object, resolver);

            var response = await handler.HandleAsync(new OpenCommand(target: ArticleDeepFileHash), default);
            using var doc = SerializeResponse(response);

            // Check cwd.displayPath
            Assert.IsTrue(doc.RootElement.TryGetProperty("cwd", out var cwd), "Response must contain 'cwd'.");
            if (cwd.TryGetProperty("displayPath", out var cwdDisplayPath))
            {
                var displayPath = cwdDisplayPath.GetString();
                AssertDisplayPathDoesNotContainArticleInteger(displayPath, "cwd.displayPath");
            }

            // Check all files[].displayPath
            if (doc.RootElement.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var file in files.EnumerateArray())
                {
                    if (file.TryGetProperty("displayPath", out var fileDisplayPath))
                    {
                        var displayPath = fileDisplayPath.GetString();
                        AssertDisplayPathDoesNotContainArticleInteger(displayPath, $"files[{index}].displayPath");
                    }

                    index++;
                }
            }
        }

        [TestMethod]
        [Description("ADR 0040 STRICT: Article entries in files[] must have BOTH name as title AND displayPath with title.")]
        public async Task Open_ArticlesRoot_ArticleEntriesMustHaveTitleInBothNameAndDisplayPath()
        {
            var adapter = BuildAdapterWithArticles();
            var resolver = BuildTitleNameResolver(ArticleNumber, ArticleTitle, TemplateId, TemplateTitle);
            var handler = new OpenCommandHandler(adapter.Object, resolver);

            var response = await handler.HandleAsync(new OpenCommand(target: ArticlesRootHash), default);
            using var doc = SerializeResponse(response);

            var files = AssertArrayProperty(doc.RootElement, "files", minLength: 1);
            var foundArticleEntry = false;

            foreach (var entry in files.EnumerateArray())
            {
                // Check if this is the article entry by looking at realPath
                if (!entry.TryGetProperty("realPath", out var realPathProp))
                {
                    continue;
                }

                var realPath = realPathProp.GetString();
                if (string.IsNullOrEmpty(realPath) || !realPath.Equals(ArticleRealPath, StringComparison.Ordinal))
                {
                    continue;
                }

                foundArticleEntry = true;

                // This is the article entry - validate both name and displayPath
                var name = AssertStringProperty(entry, "name");
                Assert.AreEqual(ArticleTitle, name,
                    $"Article entry 'name' must be the article title '{ArticleTitle}', not '{name}'.");

                // Validate name is NOT the integer
                Assert.AreNotEqual(ArticleNumber.ToString(), name,
                    $"Article entry 'name' must NOT be the numeric ID '{ArticleNumber}'.");

                if (entry.TryGetProperty("displayPath", out var displayPathProp))
                {
                    var displayPath = displayPathProp.GetString();
                    Assert.AreEqual(ArticleDisplayPath, displayPath,
                        $"Article entry 'displayPath' must be '{ArticleDisplayPath}', not '{displayPath}'.");

                    // Validate displayPath does NOT contain the integer
                    AssertDisplayPathDoesNotContainArticleInteger(displayPath, "article entry displayPath");
                }
            }

            Assert.IsTrue(foundArticleEntry,
                $"Could not find article entry with realPath='{ArticleRealPath}' in files array.");
        }

        /// <summary>
        /// Helper method that enforces ADR 0040 by checking if a displayPath contains /pub/articles/{integer}.
        /// Throws Assert.Fail if a violation is detected.
        /// </summary>
        /// <param name="displayPath">The display path to validate.</param>
        /// <param name="fieldName">The field name for error reporting (e.g., "cwd.displayPath").</param>
        private static void AssertDisplayPathDoesNotContainArticleInteger(string? displayPath, string fieldName)
        {
            if (string.IsNullOrEmpty(displayPath))
            {
                return;
            }

            var segments = displayPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3
                && segments[0].Equals("pub", StringComparison.OrdinalIgnoreCase)
                && segments[1].Equals("articles", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(segments[2], out var articleId))
            {
                Assert.Fail(
                    $"ADR 0040 VIOLATION: {fieldName} contains article ID instead of title: '{displayPath}'. " +
                    $"The third segment '{segments[2]}' is an integer ({articleId}). " +
                    $"Expected format: /pub/articles/{{ArticleTitle}}, not /pub/articles/{{ArticleId}}. " +
                    $"This indicates ArticleTitleNameResolver failed to resolve the article title, or " +
                    $"OpenCommandHandler.BuildElFinderObjectAsync is not calling BuildDisplayPathAsync correctly.");
            }
        }
    }
}
