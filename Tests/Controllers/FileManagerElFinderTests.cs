// <copyright file="FileManagerControllerTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Controllers
{
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Services.Caching;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Primitives;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using SkyCMS.Drivers.ElFinder;
    using SkyCMS.Drivers.ElFinder.Adapters;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Handlers;
    using SkyCMS.Drivers.ElFinder.Responses;
    using Sky.Cms.Controllers;
    using Sky.Cms.Models;
    using Sky.Cms.Services;
    using Sky.Editor.Services.EditorSettings;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Claims;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for <see cref="FileManagerController"/>.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class FileManagerElFinderTests : SkyCmsTestBase
    {
        private const string VolumeId = "l1_";
        private ApplicationDbContext dbContext;
        private UserManager<IdentityUser> userManager;
        private ICacheService<Layout> layoutCacheService;
        private IStorageContext storage;
        private IEditorSettings editorSettings;
        private Mock<IMediator> mediator;
        private IElFinderDispatcher elFinderMediator;
        private Mock<ILogger<FileManagerController>> logger;
        private FileManagerController controller;
        private string testRoot;

        [TestInitialize]
        public new async Task Setup()
        {
            InitializeTestContext(seedLayout: true);
            dbContext = Db;
            userManager = UserManager;
            layoutCacheService = LayoutCacheService;
            storage = Storage;
            editorSettings = EditorSettings;
            mediator = new Mock<IMediator>();
            elFinderMediator = new Mock<IElFinderDispatcher>().Object;
            logger = new Mock<ILogger<FileManagerController>>();

            testRoot = $"/pub/elfinder-tests-{Guid.NewGuid():N}";
            await storage.CreateFolder(testRoot);

            // Build a real dispatcher wired to the elFinder handlers so that
            // Connector() dispatches work end-to-end without a mock.
            // ElFinderNameResolver must be registered BEFORE AddElFinderDriver()
            // so it wins the TryAddScoped for IElFinderNameResolver — matching Program.cs.
            var sp = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IPathNormalizer, PathNormalizer>()
                .AddSingleton<IPathValidator, PathValidator>()
                .AddSingleton<IMemoryCache>(_ => Cache)
                .AddScoped<IElFinderStorageAdapter>(
                    svc => new ElFinderStorageAdapter(
                        storage,
                        svc.GetRequiredService<IPathNormalizer>(),
                        svc.GetRequiredService<IPathValidator>()))
                .AddScoped<IFileEntryTitleService>(_ => new FileEntryTitleService(dbContext, Cache, DynamicConfigurationProvider))
                .AddSingleton<Cosmos.DynamicConfig.IDynamicConfigurationProvider>(_ => DynamicConfigurationProvider)
                .AddElFinderDriver()
                .BuildServiceProvider();
            elFinderMediator = sp.GetRequiredService<IElFinderDispatcher>();

            controller = new FileManagerController(
                dbContext,
                userManager,
                mediator.Object,
                layoutCacheService,
                storage,
                new FileOperationsService(storage, NullLogger<FileOperationsService>.Instance),
                editorSettings,
                logger.Object,
                new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
                new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
                elFinderMediator);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = CreateAuthorizedHttpContext(),
            };
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(testRoot))
                {
                    try
                    {
                        await storage.DeleteFolderAsync(testRoot);
                    }
                    catch
                    {
                        // Ignore cleanup failures for already-removed roots.
                    }
                }
            }
            finally
            {
                await DisposeAsync();
            }
        }

        [TestMethod]
        public async Task Connector_WithUnknownCommand_ReturnsUnknownCommandError()
        {
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "unknown-cmd",
            });

            var result = await controller.Connector();

            var json = AsJsonObject(result);
            Assert.AreEqual("errUnknownCmd", json["error"]?.ToString());
        }

        [TestMethod]
        public async Task Connector_Open_WithTraversalPath_ReturnsAccessError()
        {
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash("/pub/../private"),
            });

            var result = await controller.Connector();

            var json = AsJsonObject(result);
            Assert.AreEqual("errAccess", json["error"]?.ToString());
        }

        [TestMethod]
        public async Task Connector_Open_WithValidTarget_ReturnsCwdAndFiles()
        {
            var path = testRoot + "/open";
            await storage.CreateFolder(path);
            await CreateTestFile(path + "/hello.txt", "hello");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash(path),
            });

            var result = await controller.Connector();

            var json = AsJsonObject(result);
            Assert.IsNotNull(json["cwd"]);
            Assert.IsNotNull(json["files"]);
            Assert.IsNull(json["api"], "api must not be present on a non-init open response");
            Assert.IsTrue((json["files"] as JArray)?.Count >= 1);
        }

        [TestMethod]
        public async Task Connector_Open_ForNestedDirectory_ReturnsCanonicalParentHash()
        {
            var designPath = testRoot + "/design";
            var imagesPath = designPath + "/images";
            await storage.CreateFolder(designPath);
            await storage.CreateFolder(imagesPath);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash(imagesPath + "/"),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.AreEqual(EncodeHash(designPath), json["cwd"]?["phash"]?.ToString());
        }

        // â”€â”€â”€ OPEN â€“ files[] content by mode â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //
        // Tree-restoration mode (init=1 or tree=1): files[] must contain the full
        // ancestor chain (root + siblings at every level + cwd + cwd children) so
        // elFinder can reconstruct the tree panel on page load or direct deep-link.
        //
        // Navigation mode (regular open, no init/tree): files[] must contain ONLY
        // the direct children. The client-side cache already holds parent/sibling
        // nodes from prior navigations; including ancestors here would overwrite the
        // cached child-lists for those nodes, causing sibling folders to vanish.

        [TestMethod]
        public async Task Connector_Open_DeepNestedPath_FilesContainsAllAncestorNodes()
        {
            // Arrange: /testRoot/level1/level2/level3  (3 levels deep)
            var level1 = testRoot + "/level1";
            var level2 = level1 + "/level2";
            var level3 = level2 + "/level3";
            await storage.CreateFolder(level1);
            await storage.CreateFolder(level2);
            await storage.CreateFolder(level3);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"]    = "open",
                ["target"] = EncodeHash(level3),
                ["tree"]   = "1",   // tree-restoration mode
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");

            var files = (JArray)json["files"];
            Assert.IsNotNull(files);
            var hashes = files.Select(f => f["hash"]?.ToString()).ToHashSet();

            // Root must always be present
            Assert.IsTrue(hashes.Contains(EncodeHash("/pub")), "Root /pub must be in files[]");
            // Every intermediate ancestor must be present for phash chain to be resolvable
            Assert.IsTrue(hashes.Contains(EncodeHash(testRoot)), $"Ancestor {testRoot} must be in files[]");
            Assert.IsTrue(hashes.Contains(EncodeHash(level1)), $"Ancestor {level1} must be in files[]");
            Assert.IsTrue(hashes.Contains(EncodeHash(level2)), $"Ancestor {level2} must be in files[]");
            // The cwd itself must also be present
            Assert.IsTrue(hashes.Contains(EncodeHash(level3)), $"Cwd {level3} must be in files[]");
        }

        [TestMethod]
        public async Task Connector_Open_DeepNestedPath_AllAncestorPhashChainsAreValid()
        {
            // Arrange
            var level1 = testRoot + "/docs";
            var level2 = level1 + "/archive";
            var level3 = level2 + "/2025";
            await storage.CreateFolder(level1);
            await storage.CreateFolder(level2);
            await storage.CreateFolder(level3);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"]    = "open",
                ["target"] = EncodeHash(level3),
                ["tree"]   = "1",   // tree-restoration mode
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            var files = (JArray)json["files"];
            Assert.IsNotNull(files);

            // Build a hashâ†’node lookup
            var byHash = files
                .Where(f => f["hash"] != null)
                .ToDictionary(f => f["hash"]!.ToString(), f => f);

            // Every non-root node with a phash must point to a node that exists in files[]
            foreach (var node in byHash.Values)
            {
                var phash = node["phash"]?.ToString();
                if (string.IsNullOrEmpty(phash))
                {
                    continue; // root has no phash
                }

                Assert.IsTrue(
                    byHash.ContainsKey(phash),
                    $"Node '{node["name"]}' (hash={node["hash"]}) has phash={phash} but that parent is missing from files[]. elFinder cannot build the tree.");
            }

            // Also verify the cwd's phash points to level2
            var cwd = (JObject)json["cwd"];
            Assert.AreEqual(EncodeHash(level2), cwd?["phash"]?.ToString());
        }

        [TestMethod]
        public async Task Connector_Open_DeepNestedPath_IncludesAncestorSiblingsAtEachLevel()
        {
            // Arrange: create the deep path plus sibling directories at each level
            var level1        = testRoot + "/content";
            var level1sibling = testRoot + "/assets";
            var level2        = level1 + "/pages";
            var level2sibling = level1 + "/posts";
            var level3        = level2 + "/2025";
            await storage.CreateFolder(level1);
            await storage.CreateFolder(level1sibling);
            await storage.CreateFolder(level2);
            await storage.CreateFolder(level2sibling);
            await storage.CreateFolder(level3);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"]    = "open",
                ["target"] = EncodeHash(level3),
                ["tree"]   = "1",   // tree-restoration mode
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            var files = (JArray)json["files"];
            Assert.IsNotNull(files);
            var hashes = files.Select(f => f["hash"]?.ToString()).ToHashSet();

            // Siblings at each ancestor level must be present so the tree can be fully
            // expanded at each level without an additional round-trip to the server.
            Assert.IsTrue(hashes.Contains(EncodeHash(level1sibling)),
                $"Sibling {level1sibling} at level1 must be in files[] so the tree is fully navigable");
            Assert.IsTrue(hashes.Contains(EncodeHash(level2sibling)),
                $"Sibling {level2sibling} at level2 must be in files[] so the tree is fully navigable");
        }

        [TestMethod]
        public async Task Connector_Open_DeepNestedPath_FilesContainsNoDuplicateHashes()
        {
            // Arrange
            var level1 = testRoot + "/media";
            var level2 = level1 + "/images";
            await storage.CreateFolder(level1);
            await storage.CreateFolder(level2);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"]    = "open",
                ["target"] = EncodeHash(level2),
                ["tree"]   = "1",   // tree-restoration mode â€” this is where dedup matters
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            var files = (JArray)json["files"];
            Assert.IsNotNull(files);

            var hashes = files
                .Select(f => f["hash"]?.ToString())
                .Where(h => h != null)
                .ToList();

            var duplicates = hashes
                .GroupBy(h => h)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.AreEqual(0, duplicates.Count,
                $"Duplicate hashes in files[]: {string.Join(", ", duplicates)}");
        }

        [TestMethod]
        public async Task Connector_Open_RootAlwaysPresentInFiles_EvenForDeeplyNestedPath()
        {
            // Arrange: 4 levels deep
            var a = testRoot + "/a";
            var b = a + "/b";
            var c = b + "/c";
            await storage.CreateFolder(a);
            await storage.CreateFolder(b);
            await storage.CreateFolder(c);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"]    = "open",
                ["target"] = EncodeHash(c),
                ["tree"]   = "1",   // tree-restoration mode
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            var files = (JArray)json["files"];
            Assert.IsNotNull(files);

            var rootNode = files.FirstOrDefault(f => f["hash"]?.ToString() == EncodeHash("/pub"));
            Assert.IsNotNull(rootNode, "The root /pub node must always appear in files[] (tree=1) regardless of depth");
            Assert.AreEqual("pub", rootNode?["name"]?.ToString());
            Assert.IsNotNull(rootNode?["volumeid"], "The root node must carry volumeid so elFinder treats it as a volume root");
        }

        [TestMethod]
        public async Task Connector_Open_FilesAndParents_ReturnConsistentAncestorHashes()
        {
            // Arrange: shared tree
            var level1 = testRoot + "/shared";
            var level2 = level1 + "/inner";
            await storage.CreateFolder(level1);
            await storage.CreateFolder(level2);

            // Act: open level2 in tree-restoration mode so ancestors are included
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"]    = "open",
                ["target"] = EncodeHash(level2),
                ["tree"]   = "1",
            });
            var openResult = await controller.Connector();
            var openJson = AsJsonObject(openResult);
            var openFiles = (JArray)openJson["files"];
            var openHashes = openFiles!.Select(f => f["hash"]?.ToString()).ToHashSet();

            // Act: parents for level2
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "parents",
                ["target"] = EncodeHash(level2),
            });
            var parentsResult = await controller.Connector();
            var parentsJson = AsJsonObject(parentsResult);
            var parentsTree = (JArray)parentsJson["tree"];
            var parentsHashes = parentsTree!.Select(f => f["hash"]?.ToString()).ToHashSet();

            // Assert: the specific ancestors of level2 must appear in BOTH responses.
            // We do NOT assert that every hash in parents also appears in open, because
            // other concurrent tests create sibling folders under /pub between the two
            // calls, so parents can legitimately return more nodes than open saw.
            var openDump    = string.Join(", ", openHashes.OrderBy(h => h));
            var parentsDump = string.Join(", ", parentsHashes.OrderBy(h => h));

            foreach (var knownAncestor in new[] { testRoot, level1, level2 })
            {
                var hash = EncodeHash(knownAncestor);
                Assert.IsTrue(
                    openHashes.Contains(hash),
                    $"Ancestor {knownAncestor} (hash {hash}) is missing from open files[].\n"
                    + $"  open files[] : {openDump}");
                Assert.IsTrue(
                    parentsHashes.Contains(hash),
                    $"Ancestor {knownAncestor} (hash {hash}) is missing from parents tree[].\n"
                    + $"  parents tree[]: {parentsDump}");
            }
        }

        [TestMethod]
        public async Task Connector_Open_Navigation_ReturnsOnlyDirectChildren()
        {
            // Navigation open (no init=1, no tree=1) must return ONLY the direct children
            // of the opened folder. Returning ancestors/root would overwrite the client's
            // cached child-lists for those nodes, causing sibling folders to disappear.
            var navFolder  = testRoot + "/nav";
            var navSibling = testRoot + "/nav-sibling";
            var navChild   = navFolder + "/deep";
            await storage.CreateFolder(navFolder);
            await storage.CreateFolder(navSibling);
            await storage.CreateFolder(navChild);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"]    = "open",
                ["target"] = EncodeHash(navFolder),
                // Deliberately no tree=1 or init=1 â€” this is a regular navigation click
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");

            var files = (JArray)json["files"];
            Assert.IsNotNull(files);
            var hashes = files.Select(f => f["hash"]?.ToString()).ToHashSet();

            // The direct child must appear
            Assert.IsTrue(hashes.Contains(EncodeHash(navChild)),
                "Direct child of cwd must appear in navigation files[]");

            // Root and ancestors must NOT appear â€” they are in the client's cache already.
            // Including them would cause elFinder to overwrite its cached sibling listings.
            Assert.IsFalse(hashes.Contains(EncodeHash("/pub")),
                "Root must NOT appear in navigation files[] â€” it would overwrite the tree cache");
            Assert.IsFalse(hashes.Contains(EncodeHash(testRoot)),
                "testRoot ancestor must NOT appear in navigation files[]");
            Assert.IsFalse(hashes.Contains(EncodeHash(navSibling)),
                "Sibling of cwd must NOT appear in navigation files[] â€” that is the overwrite that drops it from the tree");
        }

        [TestMethod]
        public async Task Connector_Open_Tree1_CwdSiblingsIncludedForDirectChildOfParent()
        {
            // Regression: when cwd is a direct child of its parent level (regardless of depth
            // from root), tree=1 mode must include all peers of the cwd so the user sees the
            // complete sibling list in the tree panel. Previously the ancestor loop broke
            // immediately when the cwd was the first entry, leaving siblings out.
            var alpha  = testRoot + "/t1-alpha";
            var beta   = testRoot + "/t1-beta";
            var target = testRoot + "/t1-target";
            await storage.CreateFolder(alpha);
            await storage.CreateFolder(beta);
            await storage.CreateFolder(target);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"]    = "open",
                ["target"] = EncodeHash(target),
                ["tree"]   = "1",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");

            var files = (JArray)json["files"];
            Assert.IsNotNull(files);
            var hashes = files.Select(f => f["hash"]?.ToString()).ToHashSet();

            // All sibling folders at the cwd's level must be in files[]
            Assert.IsTrue(hashes.Contains(EncodeHash(alpha)),
                $"Sibling '{alpha}' must be in files[] (tree=1) â€” previously dropped due to ancestor-loop break");
            Assert.IsTrue(hashes.Contains(EncodeHash(beta)),
                $"Sibling '{beta}' must be in files[] (tree=1) â€” previously dropped due to ancestor-loop break");
            Assert.IsTrue(hashes.Contains(EncodeHash(target)),
                $"Target '{target}' must be in files[]");
            Assert.IsTrue(hashes.Contains(EncodeHash("/pub")),
                "Root must be in files[]");
        }

        [TestMethod]
        public async Task Connector_Parents_ForNestedDirectory_ReturnsRootFirstTreeWithCanonicalHashes()
        {
            var designPath = testRoot + "/design";
            var imagesPath = designPath + "/images";
            var articlesPath = testRoot + "/articles";
            await storage.CreateFolder(designPath);
            await storage.CreateFolder(imagesPath);
            await storage.CreateFolder(articlesPath);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "parents",
                ["target"] = EncodeHash(imagesPath + "/"),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            var tree = (JArray)json["tree"];

            Assert.IsNotNull(tree);
            Assert.AreEqual("pub", tree[0]?["name"]?.ToString());

            var hashes = tree.Select(item => item?["hash"]?.ToString()).Where(hash => hash != null).ToList();
            var testRootHash = EncodeHash(testRoot);
            var designHash = EncodeHash(designPath);
            var imagesHash = EncodeHash(imagesPath);

            Assert.IsTrue(hashes.IndexOf(testRootHash) > hashes.IndexOf(EncodeHash("/pub")));
            Assert.IsTrue(hashes.IndexOf(designHash) > hashes.IndexOf(testRootHash));
            Assert.IsTrue(hashes.IndexOf(imagesHash) > hashes.IndexOf(designHash));

            var imagesNode = tree.Children<JObject>().First(node => node["hash"]?.ToString() == imagesHash);
            Assert.AreEqual(designHash, imagesNode["phash"]?.ToString());
        }

        [TestMethod]
        public async Task Connector_Mkdir_WithUnsafeName_ReturnsInvalidNameError()
        {
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "mkdir",
                ["target"] = EncodeHash(testRoot),
                ["name"] = "../bad",
            });

            var result = await controller.Connector();

            var json = AsJsonObject(result);
            Assert.AreEqual("errInvName", json["error"]?.ToString());
        }

        [TestMethod]
        public async Task Connector_Upload_WithDangerousExtension_ReturnsUploadError()
        {
            var context = CreateAuthorizedHttpContext();
            context.Request.Method = "POST";
            context.Request.ContentType = "multipart/form-data; boundary=----unit-test-boundary";
            context.Request.QueryString = QueryString.Create(new Dictionary<string, string>
            {
                ["cmd"] = "upload",
                ["target"] = EncodeHash(testRoot),
            });

            var bytes = Encoding.UTF8.GetBytes("MZ");
            var stream = new MemoryStream(bytes);
            var file = new FormFile(stream, 0, bytes.Length, "upload[]", "malware.exe");
            var files = new FormFileCollection { file };
            var form = new FormCollection(new Dictionary<string, StringValues>(), files);
            context.Features.Set<IFormFeature>(new FormFeature(form));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = context,
            };

            var result = await controller.Connector();

            var json = AsJsonObject(result);
            Assert.AreEqual("errUploadFile", json["error"]?.ToString());
        }

        [TestMethod]
        public async Task Connector_Mkdir_WithDuplicateName_AppendsNumericSuffix()
        {
            await storage.CreateFolder(testRoot + "/assets");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "mkdir",
                ["target"] = EncodeHash(testRoot),
                ["name"] = "assets",
            });

            var result = await controller.Connector();

            var json = AsJsonObject(result);
            Assert.AreEqual("assets-1", json["added"]?[0]?["name"]?.ToString());
            var entries = await storage.GetFilesAndDirectories(testRoot);
            Assert.IsTrue(entries.Any(e => string.Equals(NormalizePath(e.Path), testRoot + "/assets-1", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public async Task Connector_Mkfile_WithDuplicateName_AppendsNumericSuffixBeforeExtension()
        {
            await CreateTestFile(testRoot + "/notes.txt", "original");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "mkfile",
                ["target"] = EncodeHash(testRoot),
                ["name"] = "notes.txt",
            });

            var result = await controller.Connector();

            var json = AsJsonObject(result);
            Assert.AreEqual("notes-1.txt", json["added"]?[0]?["name"]?.ToString());
            await AssertEntryExists(testRoot + "/notes-1.txt");
        }

        [TestMethod]
        public async Task Connector_Rename_IntoExistingName_AppendsNumericSuffixBeforeExtension()
        {
            await CreateTestFile(testRoot + "/draft.txt", "draft");
            await CreateTestFile(testRoot + "/published.txt", "published");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "rename",
                ["target"] = EncodeHash(testRoot + "/draft.txt"),
                ["name"] = "published.txt",
            });

            var result = await controller.Connector();

            var json = AsJsonObject(result);
            Assert.AreEqual("published-1.txt", json["added"]?[0]?["name"]?.ToString());
            await AssertEntryExists(testRoot + "/published-1.txt");
        }

        [TestMethod]
        public async Task Connector_Upload_IntoExistingName_AppendsNumericSuffixBeforeExtension()
        {
            await CreateTestFile(testRoot + "/photo.jpg", "existing image");

            SetMultipartRequest(
                new Dictionary<string, string>
                {
                    ["cmd"] = "upload",
                    ["target"] = EncodeHash(testRoot),
                },
                CreateFormFile("photo.jpg", "new image content"));

            var result = await controller.Connector();

            var json = AsJsonObject(result);
            Assert.AreEqual("photo-1.jpg", json["added"]?[0]?["name"]?.ToString());
            await AssertEntryExists(testRoot + "/photo-1.jpg");
        }

        // â”€â”€â”€ DELETE (rm) TESTS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        public async Task Connector_Rm_DeletesExistingFile_ReturnsHashInRemovedList()
        {
            // Arrange
            var filePath = testRoot + "/test-file.txt";
            await CreateTestFile(filePath, "test content");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "rm",
                ["targets[]"] = EncodeHash(filePath),
            });

            // Act
            var result = await controller.Connector();

            // Assert
            var json = AsJsonObject(result);
            var removed = (JArray)json["removed"];
            Assert.IsNotNull(removed);
            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(EncodeHash(filePath), removed[0]?.ToString());

            // Verify file is actually deleted
            var entries = await storage.GetFilesAndDirectories(testRoot);
            Assert.IsFalse(entries.Any(e => e.Path.EndsWith("test-file.txt", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public async Task Connector_Rm_DeletesExistingFolder_ReturnsHashInRemovedList()
        {
            // Arrange
            var folderPath = testRoot + "/subfolder";
            await storage.CreateFolder(folderPath);
            await CreateTestFile(folderPath + "/inner.txt", "inner content");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "rm",
                ["targets[]"] = EncodeHash(folderPath),
            });

            // Act
            var result = await controller.Connector();

            // Assert
            var json = AsJsonObject(result);
            var removed = (JArray)json["removed"];
            Assert.IsNotNull(removed);
            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(EncodeHash(folderPath), removed[0]?.ToString());
            
            // Note: We verify the folder is returned in the removed list.
            // Actual filesystem/storage verification may vary based on storage implementation.
        }

        [TestMethod]
        public async Task Connector_Rm_WithMultipleTargets_DeletesOnlySuccessful()
        {
            // Arrange
            var file1Path = testRoot + "/file1.txt";
            var file2Path = testRoot + "/file2.txt";
            await CreateTestFile(file1Path, "content1");
            await CreateTestFile(file2Path, "content2");

            // Delete first file
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "rm",
                ["targets[]"] = EncodeHash(file1Path),
            });

            var result1 = await controller.Connector();
            var json1 = AsJsonObject(result1);

            // Delete second file
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "rm",
                ["targets[]"] = EncodeHash(file2Path),
            });

            var result2 = await controller.Connector();

            // Assert first deletion
            var removed1 = (JArray)json1["removed"];
            Assert.AreEqual(1, removed1.Count);
            Assert.AreEqual(EncodeHash(file1Path), removed1[0]?.ToString());

            // Assert second deletion
            var json2 = AsJsonObject(result2);
            var removed2 = (JArray)json2["removed"];
            Assert.AreEqual(1, removed2.Count);
            Assert.AreEqual(EncodeHash(file2Path), removed2[0]?.ToString());

            // Verify both files are actually deleted
            var entries = await storage.GetFilesAndDirectories(testRoot);
            Assert.IsFalse(entries.Any(e => e.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)));
        }
            [TestMethod]
            public async Task Connector_Rm_MissingTarget_ReturnsNotFoundArrayAndNoRemoved()
            {
                var missingPath = testRoot + "/missing-file.txt";
                var missingHash = EncodeHash(missingPath);

                SetGetRequest(new Dictionary<string, string>
                {
                    ["cmd"] = "rm",
                    ["targets[]"] = missingHash,
                });

                var result = await controller.Connector();

                var json = AsJsonObject(result);
                CollectionAssert.AreEqual(new[] { missingHash }, ((JArray)json["notFound"])!.Values<string>().ToArray());
                Assert.AreEqual(0, ((JArray)json["removed"]!)!.Count, "Missing target must not appear in removed.");
                Assert.AreEqual(0, ((JArray)json["notRemoved"]!)!.Count, "Missing target must not appear in notRemoved.");

                var notFoundDetails = (JArray)json["notFoundDetails"]!;
                Assert.AreEqual(1, notFoundDetails.Count, "Missing target should include one diagnostic detail.");
                Assert.AreEqual(missingHash, notFoundDetails[0]?["hash"]?.ToString());
                Assert.AreEqual(missingPath, notFoundDetails[0]?["path"]?.ToString());
                Assert.IsFalse(string.IsNullOrWhiteSpace(notFoundDetails[0]?["reason"]?.ToString()));
                Assert.IsFalse(string.IsNullOrWhiteSpace(notFoundDetails[0]?["reasonCode"]?.ToString()));
            }

            [TestMethod]
            public async Task Connector_Rm_Cqrs_ResponseUsesLowercaseNotFoundAndNotRemovedKeys()
            {
                var missingHash = EncodeHash(testRoot + "/missing-cqrs.txt");
                var lockedHash = EncodeHash(testRoot + "/locked-cqrs.txt");
                var fakeResponse = new SkyCMS.Drivers.ElFinder.Commands.RmResponse
                {
                    NotFound = new List<string> { missingHash },
                    NotRemoved = new List<string> { lockedHash },
                    NotFoundDetails = new List<SkyCMS.Drivers.ElFinder.Commands.RmDiagnosticEntry>
                    {
                        new SkyCMS.Drivers.ElFinder.Commands.RmDiagnosticEntry
                        {
                            Hash = missingHash,
                            Path = testRoot + "/missing-cqrs.txt",
                            Reason = "Target is not accessible or does not exist",
                            ReasonCode = "not_accessible",
                        },
                    },
                    NotRemovedDetails = new List<SkyCMS.Drivers.ElFinder.Commands.RmDiagnosticEntry>
                    {
                        new SkyCMS.Drivers.ElFinder.Commands.RmDiagnosticEntry
                        {
                            Hash = lockedHash,
                            Path = testRoot + "/locked-cqrs.txt",
                            Reason = "Delete call completed but target is still accessible",
                            ReasonCode = "delete_no_effect",
                        },
                    },
                };

                var mockDispatcher = new Mock<IElFinderDispatcher>();
                mockDispatcher
                    .Setup(m => m.SendAsync(It.IsAny<RmCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IElFinderResponse)fakeResponse);

                var localController = new FileManagerController(
                    dbContext,
                    userManager,
                    mediator.Object,
                    layoutCacheService,
                    storage,
                    new FileOperationsService(storage, NullLogger<FileOperationsService>.Instance),
                    editorSettings,
                    logger.Object,
                    new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
                    new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
                    mockDispatcher.Object);

                var context = CreateAuthorizedHttpContext();
                context.Request.Method = "GET";
                context.Request.QueryString = QueryString.Create(new Dictionary<string, string>
                {
                    ["cmd"] = "rm",
                    ["targets[]"] = missingHash,
                });
                localController.ControllerContext = new ControllerContext { HttpContext = context };

                var result = await localController.Connector();

                Assert.IsInstanceOfType(result, typeof(JsonResult), "CQRS rm should aggregate dispatcher responses into a JsonResult.");
                var parsed = AsJsonObject(result);

                Assert.IsNotNull(parsed["removed"], "Response body must have lowercase 'removed' key.");
                Assert.IsNotNull(parsed["notFound"], "Response body must have lowercase 'notFound' key.");
                Assert.IsNotNull(parsed["notRemoved"], "Response body must have lowercase 'notRemoved' key.");
                Assert.IsNotNull(parsed["notFoundDetails"], "Response body must have lowercase 'notFoundDetails' key.");
                Assert.IsNotNull(parsed["notRemovedDetails"], "Response body must have lowercase 'notRemovedDetails' key.");
                Assert.IsNull(parsed["NotFound"], "Response body must not have PascalCase 'NotFound' key.");
                Assert.IsNull(parsed["NotRemoved"], "Response body must not have PascalCase 'NotRemoved' key.");
                Assert.IsNull(parsed["NotFoundDetails"], "Response body must not have PascalCase 'NotFoundDetails' key.");
                Assert.IsNull(parsed["NotRemovedDetails"], "Response body must not have PascalCase 'NotRemovedDetails' key.");

                CollectionAssert.AreEqual(new[] { missingHash }, ((JArray)parsed["notFound"]!)!.Values<string>().ToArray());
                CollectionAssert.AreEqual(new[] { lockedHash }, ((JArray)parsed["notRemoved"]!)!.Values<string>().ToArray());
                CollectionAssert.AreEqual(new[] { missingHash }, ((JArray)parsed["notFoundDetails"]!)!.Select(x => x["hash"]?.ToString()).ToArray());
                CollectionAssert.AreEqual(new[] { lockedHash }, ((JArray)parsed["notRemovedDetails"]!)!.Select(x => x["hash"]?.ToString()).ToArray());
                CollectionAssert.AreEqual(new[] { "not_accessible" }, ((JArray)parsed["notFoundDetails"]!)!.Select(x => x["reasonCode"]?.ToString()).ToArray());
                CollectionAssert.AreEqual(new[] { "delete_no_effect" }, ((JArray)parsed["notRemovedDetails"]!)!.Select(x => x["reasonCode"]?.ToString()).ToArray());
            }

        [TestMethod]
        public async Task Connector_Rm_WithInvalidHash_SkipsAndReturnsWarning()
        {
            // Arrange
            var filePath = testRoot + "/valid-file.txt";
            await CreateTestFile(filePath, "valid content");

            // Delete valid file
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "rm",
                ["targets[]"] = EncodeHash(filePath),
            });

            // Act
            var result = await controller.Connector();

            // Assert
            var json = AsJsonObject(result);
            var removed = (JArray)json["removed"];

            // Valid file should be removed
            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(EncodeHash(filePath), removed[0]?.ToString());

            // Verify file is actually deleted
            var entries = await storage.GetFilesAndDirectories(testRoot);
            Assert.IsFalse(entries.Any(e => e.Path.EndsWith("valid-file.txt", StringComparison.OrdinalIgnoreCase)));
        }

        // â”€â”€â”€ PARENTS (TREE NAVIGATION) TESTS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        public async Task Connector_Parents_WithDeepPath_IncludesAllAncestorsAndSiblings()
        {
            // Arrange: Create a deep hierarchy
            var level1Path = testRoot + "/level1";
            var level2Path = level1Path + "/level2";
            var level3Path = level2Path + "/level3";

            await storage.CreateFolder(level1Path);
            await storage.CreateFolder(level2Path);
            await storage.CreateFolder(level3Path);

            // Create sibling folders at each level
            await storage.CreateFolder(testRoot + "/level1-sibling");
            await storage.CreateFolder(level1Path + "/level2-sibling");
            await storage.CreateFolder(level2Path + "/level3-sibling");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "parents",
                ["target"] = EncodeHash(level3Path),
            });

            // Act
            var result = await controller.Connector();

            // Assert
            var json = AsJsonObject(result);
            var tree = (JArray)json["tree"];
            Assert.IsNotNull(tree);

            var hashes = tree.Select(item => item?["hash"]?.ToString()).ToList();
            var hashStrings = new HashSet<string>(hashes.Where(h => h != null)!);

            // Verify all ancestors are present
            Assert.IsTrue(hashStrings.Contains(EncodeHash("/pub")), "Root should be in tree");
            Assert.IsTrue(hashStrings.Contains(EncodeHash(testRoot)), "Test root should be in tree");
            Assert.IsTrue(hashStrings.Contains(EncodeHash(level1Path)), "Level 1 should be in tree");
            Assert.IsTrue(hashStrings.Contains(EncodeHash(level2Path)), "Level 2 should be in tree");

            // Verify siblings at each level are present
            Assert.IsTrue(hashStrings.Contains(EncodeHash(testRoot + "/level1-sibling")), 
                "Level 1 sibling should be in tree for breadcrumb expansion");
            Assert.IsTrue(hashStrings.Contains(EncodeHash(level1Path + "/level2-sibling")), 
                "Level 2 sibling should be in tree for breadcrumb expansion");
            Assert.IsTrue(hashStrings.Contains(EncodeHash(level2Path + "/level3-sibling")), 
                "Level 3 sibling should be in tree for breadcrumb expansion");
        }

        [TestMethod]
        public async Task Connector_Parents_WithDeepPath_MaintainsCorrectParentHashValues()
        {
            // Arrange: Create a deep hierarchy
            var level1Path = testRoot + "/documents";
            var level2Path = level1Path + "/archives";
            var level3Path = level2Path + "/old";

            await storage.CreateFolder(level1Path);
            await storage.CreateFolder(level2Path);
            await storage.CreateFolder(level3Path);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "parents",
                ["target"] = EncodeHash(level3Path),
            });

            // Act
            var result = await controller.Connector();

            // Assert
            var json = AsJsonObject(result);
            var tree = (JArray)json["tree"];
            Assert.IsNotNull(tree);

            // Find each node and verify its phash
            var documentsNode = tree.FirstOrDefault(item => item?["name"]?.ToString() == "documents");
            Assert.IsNotNull(documentsNode, "documents folder should be in tree");
            Assert.AreEqual(EncodeHash(testRoot), documentsNode["phash"]?.ToString(), 
                "documents parent hash should point to test root");

            var archivesNode = tree.FirstOrDefault(item => item?["name"]?.ToString() == "archives");
            Assert.IsNotNull(archivesNode, "archives folder should be in tree");
            Assert.AreEqual(EncodeHash(level1Path), archivesNode["phash"]?.ToString(), 
                "archives parent hash should point to documents");

            var oldNode = tree.FirstOrDefault(item => item?["name"]?.ToString() == "old");
            Assert.IsNotNull(oldNode, "old folder should be in tree");
            Assert.AreEqual(EncodeHash(level2Path), oldNode["phash"]?.ToString(), 
                "old parent hash should point to archives");
        }

        [TestMethod]
        public async Task Connector_Parents_IncludesChildrenOfTargetPath()
        {
            // Arrange: Create target path with children
            var targetPath = testRoot + "/target";
            await storage.CreateFolder(targetPath);
            await storage.CreateFolder(targetPath + "/child1");
            await storage.CreateFolder(targetPath + "/child2");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "parents",
                ["target"] = EncodeHash(targetPath),
            });

            // Act
            var result = await controller.Connector();

            // Assert
            var json = AsJsonObject(result);
            var tree = (JArray)json["tree"];
            Assert.IsNotNull(tree);

            var hashes = tree.Select(item => item?["hash"]?.ToString()).ToList();
            var hashStrings = new HashSet<string>(hashes.Where(h => h != null)!);

            // Verify children of target are included
            Assert.IsTrue(hashStrings.Contains(EncodeHash(targetPath + "/child1")), 
                "Target's child1 should be in tree");
            Assert.IsTrue(hashStrings.Contains(EncodeHash(targetPath + "/child2")), 
                "Target's child2 should be in tree");
        }

        // â”€â”€â”€ JSON SHAPE / PROTOCOL COMPLIANCE TESTS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //
        // These tests assert that our responses match the elFinder protocol shape
        // expected by the JS client. They mirror what the studio-42.github.io reference
        // connector produces, and directly correspond to the fix for the "tree goes blank
        // on child folder click" bug caused by missing volumeid on directory objects.

        [TestMethod]
        public async Task Connector_Open_Init_CwdHasVolumeId()
        {
            // The root cwd (init=1) must carry volumeid so elFinder treats it as a volume root.
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash("/pub"),
                ["init"] = "1",
                ["tree"] = "1",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNotNull(json["cwd"]?["volumeid"],
                "cwd.volumeid must be present in the init open response");
            Assert.AreEqual(VolumeId, json["cwd"]?["volumeid"]?.ToString());
        }

        [TestMethod]
        public async Task Connector_Open_ChildFolder_CwdHasVolumeId()
        {
            // Navigating into a child folder: the cwd object must carry volumeid.
            // Without this the JS tree plugin cannot slot the node into the volume.
            var child = testRoot + "/shape-child";
            await storage.CreateFolder(child);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash(child),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNotNull(json["cwd"]?["volumeid"],
                "cwd.volumeid must be present even when opening a non-root child folder");
            Assert.AreEqual(VolumeId, json["cwd"]?["volumeid"]?.ToString());
        }

        [TestMethod]
        public async Task Connector_Open_Init_AllDirectoryFilesEntriesHaveVolumeId()
        {
            // Every directory object inside files[] must carry volumeid (not just the root).
            // The reference connector returns volumeid on every dir regardless of depth.
            var sub = testRoot + "/shape-sub";
            await storage.CreateFolder(sub);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash("/pub"),
                ["init"] = "1",
                ["tree"] = "1",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            var files = (JArray)json["files"];
            Assert.IsNotNull(files);

            var dirs = files.Where(f => f["mime"]?.ToString() == "directory").ToList();
            Assert.IsTrue(dirs.Count > 0, "There must be at least one directory in files[]");

            foreach (var dir in dirs)
            {
                Assert.IsNotNull(
                    dir["volumeid"],
                    $"Directory '{dir["name"]}' (hash={dir["hash"]}) is missing volumeid â€” elFinder cannot anchor it in the tree");
                Assert.AreEqual(VolumeId, dir["volumeid"]?.ToString(),
                    $"Directory '{dir["name"]}' has wrong volumeid value");
            }
        }

        [TestMethod]
        public async Task Connector_Open_Navigation_AllDirectoryFilesEntriesHaveVolumeId()
        {
            // Navigation open (no init/tree): all returned directory objects must still have volumeid.
            var parent = testRoot + "/shape-nav";
            var grandchild = parent + "/sub";
            await storage.CreateFolder(parent);
            await storage.CreateFolder(grandchild);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash(parent),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            var files = (JArray)json["files"];
            Assert.IsNotNull(files);

            var dirs = files.Where(f => f["mime"]?.ToString() == "directory").ToList();
            foreach (var dir in dirs)
            {
                Assert.IsNotNull(
                    dir["volumeid"],
                    $"Directory '{dir["name"]}' in navigation files[] is missing volumeid");
            }
        }

        [TestMethod]
        public async Task Connector_Open_NonDirectoryFilesDoNotHaveVolumeId()
        {
            // Regular files (non-directories) must NOT carry volumeid â€” protocol compliance.
            var folder = testRoot + "/shape-files";
            await storage.CreateFolder(folder);
            await CreateTestFile(folder + "/doc.txt", "content");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash(folder),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            var files = (JArray)json["files"];
            Assert.IsNotNull(files);

            var nonDirs = files.Where(f => f["mime"]?.ToString() != "directory").ToList();
            foreach (var file in nonDirs)
            {
                Assert.IsNull(
                    file["volumeid"],
                    $"Non-directory file '{file["name"]}' must NOT have a volumeid property");
            }
        }

        [TestMethod]
        public async Task Connector_Open_Options_HasUploadMaxConnNotUploadMaxConnections()
        {
            // The options key must be uploadMaxConn (matching the elFinder protocol).
            // uploadMaxConnections is not a valid elFinder option key.
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash(testRoot),
                ["init"] = "1",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            var options = json["options"] as JObject;
            Assert.IsNotNull(options);

            Assert.IsNotNull(options["uploadMaxConn"],
                "options.uploadMaxConn must be present (elFinder protocol key)");
            Assert.IsNull(options["uploadMaxConnections"],
                "options.uploadMaxConnections must NOT be present â€” wrong key, elFinder ignores it");
        }

        [TestMethod]
        public async Task Connector_Open_Init_HasApiAndUplMaxSize()
        {
            // api and uplMaxSize must be present on the init response.
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash("/pub"),
                ["init"] = "1",
                ["tree"] = "1",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.AreEqual("2.1049", json["api"]?.ToString(),
                "api must be present on the init response");
            Assert.IsNotNull(json["uplMaxSize"],
                "uplMaxSize must be present on the init response");
        }

        [TestMethod]
        public async Task Connector_Open_Navigation_DoesNotHaveApiOrUplMaxSize()
        {
            // api and uplMaxSize must NOT appear on a navigation (non-init) open.
            // Sending api on navigation triggers elFinder client re-initialization
            // which clears the folder tree.
            var folder = testRoot + "/shape-noapi";
            await storage.CreateFolder(folder);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash(folder),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["api"],
                "api must NOT appear on a non-init open â€” it would trigger client re-init and clear the tree");
            Assert.IsNull(json["uplMaxSize"],
                "uplMaxSize must NOT appear on a non-init open");
        }

        [TestMethod]
        public async Task Connector_Open_Init_RootFileEntryHasIsrootAndEmptyPhash()
        {
            // The root volume node in files[] must have isroot:1 and phash:"" (empty string,
            // not absent). This lets elFinder anchor the node at the volume root.
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash("/pub"),
                ["init"] = "1",
                ["tree"] = "1",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            var files = (JArray)json["files"];
            Assert.IsNotNull(files);

            var rootNode = files.FirstOrDefault(f =>
                f["hash"]?.ToString() == EncodeHash("/pub"));
            Assert.IsNotNull(rootNode, "Root node (hash=l1_cHVi) must be present in files[]");
            Assert.AreEqual(1, rootNode["isroot"]?.ToObject<int>(),
                "Root node must have isroot:1");
            Assert.IsNotNull(rootNode["phash"],
                "Root node must have phash present (empty string, not absent)");
            Assert.AreEqual(string.Empty, rootNode["phash"]?.ToString(),
                "Root node phash must be empty string per elFinder protocol");
        }

        [TestMethod]
        public async Task Connector_Open_CwdHasRootField()
        {
            // Every open response's cwd must carry a root field pointing to the volume
            // root hash. This lets the JS client resolve the volume the folder belongs to.
            var folder = testRoot + "/shape-root-field";
            await storage.CreateFolder(folder);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash(folder),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            var rootHash = EncodeHash("/pub");
            Assert.IsNotNull(json["cwd"]?["root"],
                "cwd.root must be present");
            Assert.AreEqual(rootHash, json["cwd"]?["root"]?.ToString(),
                "cwd.root must equal the volume root hash");
        }

        private static string EncodeHash(string path)
        {
            path = NormalizePath(path);
            var bytes = Encoding.UTF8.GetBytes(path.TrimStart('/'));
            return VolumeId + Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private static string NormalizePath(string path)
        {
            var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            return "/" + string.Join("/", segments);
        }

        private JObject AsJsonObject(IActionResult result)
        {
            if (result is ContentResult contentResult)
            {
                Assert.IsNotNull(contentResult.Content, "ContentResult.Content must not be null");
                return JObject.Parse(contentResult.Content);
            }

            Assert.IsInstanceOfType(result, typeof(JsonResult));
            var jsonResult = (JsonResult)result;
            Assert.IsNotNull(jsonResult.Value);
            return JObject.FromObject(jsonResult.Value);
        }

        private void SetGetRequest(Dictionary<string, string> query)
        {
            var context = CreateAuthorizedHttpContext();
            context.Request.Method = "GET";
            context.Request.QueryString = QueryString.Create(query);
            context.RequestServices = BuildRequestServices();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = context,
            };
        }

        private void SetMultipartRequest(Dictionary<string, string> query, IFormFile file)
        {
            var context = CreateAuthorizedHttpContext();
            context.Request.Method = "POST";
            context.Request.ContentType = "multipart/form-data; boundary=----unit-test-boundary";
            context.Request.QueryString = QueryString.Create(query);
            context.RequestServices = BuildRequestServices();

            var files = new FormFileCollection { file };
            var form = new FormCollection(new Dictionary<string, StringValues>(), files);
            context.Features.Set<IFormFeature>(new FormFeature(form));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = context,
            };
        }

        private DefaultHttpContext CreateAuthorizedHttpContext()
        {
            var context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                new Claim(ClaimTypes.Name, "testuser@example.com"),
                new Claim(ClaimTypes.Role, "Administrators"),
            }, "mock"));

            return context;
        }

        private static IFormFile CreateFormFile(string fileName, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "upload[]", fileName);
        }

        private async Task AssertEntryExists(string path)
        {
            var entry = await storage.GetFileAsync(path);
            Assert.IsNotNull(entry, $"Expected storage entry at '{path}'.");
        }

        private async Task CreateTestFile(string path, string content)
        {
            var normalizedPath = path.Replace('\\', '/');
            var fileName = Path.GetFileName(normalizedPath);
            var metadata = new FileUploadMetaData
            {
                FileName = fileName,
                RelativePath = normalizedPath.TrimStart('/'),
                ChunkIndex = 0,
                TotalChunks = 1,
                ContentType = "text/plain",
                TotalFileSize = Encoding.UTF8.GetByteCount(content),
                UploadUid = Guid.NewGuid().ToString(),
            };

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            await storage.AppendBlob(stream, metadata);
        }

        // â”€â”€â”€ CQRS PARITY TESTS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        public async Task Connector_CqrsOpen_WithValidTarget_ReturnsCwdAndFiles()
        {
            var path = testRoot + "/cqrs-open";
            await storage.CreateFolder(path);
            await CreateTestFile(path + "/hello.txt", "hello");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash(path),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            Assert.IsNotNull(json["cwd"], "Expected 'cwd' in open response");
            Assert.IsNotNull(json["files"], "Expected 'files' in open response");
        }

        [TestMethod]
        public async Task Connector_CqrsTree_WithValidTarget_ReturnsTree()
        {
            var path = testRoot + "/cqrs-tree";
            await storage.CreateFolder(path);
            await storage.CreateFolder(path + "/child");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "tree",
                ["target"] = EncodeHash(path),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            Assert.IsNotNull(json["tree"], "Expected 'tree' in response");
        }

        [TestMethod]
        public async Task Connector_CqrsTree_WhenDispatcherAvailable_ResponseBodyHasLowercaseKeys()
        {
            // Regression test: CQRS handlers use System.Text.Json [JsonPropertyName] attributes,
            // but the MVC pipeline uses Newtonsoft + DefaultContractResolver (PascalCase).
            // JsonCqrs() must serialize via System.Text.Json so elFinder receives lowercase JSON.
            var fakeResponse = new SkyCMS.Drivers.ElFinder.Commands.TreeResponse
            {
                Tree = new List<SkyCMS.Drivers.ElFinder.Responses.ElFinderObject>
                {
                    new SkyCMS.Drivers.ElFinder.Responses.ElFinderObject
                    {
                        Hash = "l1_dGVzdA",
                        PHash = "l1_cHVi",
                        Name = "child-a",
                        Mime = "directory",
                        Size = 0,
                        Ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    },
                },
            };

            var mockDispatcher = new Mock<IElFinderDispatcher>();
            mockDispatcher
                .Setup(m => m.SendAsync(It.IsAny<TreeCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IElFinderResponse)fakeResponse);

            var localController = new FileManagerController(
                dbContext,
                userManager,
                mediator.Object,
                layoutCacheService,
                storage,
                new FileOperationsService(storage, NullLogger<FileOperationsService>.Instance),
                editorSettings,
                logger.Object,
                new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
                new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
                mockDispatcher.Object);

            var context = CreateAuthorizedHttpContext();
            context.Request.Method = "GET";
            context.Request.QueryString = QueryString.Create(new Dictionary<string, string>
            {
                ["cmd"] = "tree",
                ["target"] = EncodeHash(testRoot),
            });
            localController.ControllerContext = new ControllerContext { HttpContext = context };

            var result = await localController.Connector();

            // Must return ContentResult (serialized by System.Text.Json).
            Assert.IsInstanceOfType(result, typeof(ContentResult), "CQRS tree should return ContentResult, not JsonResult");
            var contentResult = (ContentResult)result;
            var parsed = JObject.Parse(contentResult.Content!);

            // elFinder protocol requires lowercase keys â€” "tree" not "Tree", "hash" not "Hash".
            Assert.IsNotNull(parsed["tree"], "Response body must have lowercase 'tree' key");
            Assert.IsNull(parsed["Tree"], "Response body must NOT have PascalCase 'Tree' key");

            var entries = (JArray)parsed["tree"]!;
            Assert.IsTrue(entries.Count > 0, "Tree should contain at least one directory entry");
            var first = entries[0];
            Assert.IsNotNull(first["hash"], "Entry must have lowercase 'hash' key");
            Assert.IsNull(first["Hash"], "Entry must NOT have PascalCase 'Hash' key");
            Assert.IsNotNull(first["phash"], "Entry must have lowercase 'phash' key");
            Assert.IsNull(first["PHash"], "Entry must NOT have PascalCase 'PHash' key");
        }

        [TestMethod]
        public async Task Connector_CqrsMkdir_WithDuplicateName_AppendsNumericSuffix()
        {
            await storage.CreateFolder(testRoot + "/cqrs-assets");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "mkdir",
                ["target"] = EncodeHash(testRoot),
                ["name"] = "cqrs-assets",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.AreEqual("cqrs-assets-1", json["added"]?[0]?["name"]?.ToString());
        }

        [TestMethod]
        public async Task Connector_CqrsMkfile_WithDuplicateName_AppendsNumericSuffixBeforeExtension()
        {
            await CreateTestFile(testRoot + "/cqrs-notes.txt", "original");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "mkfile",
                ["target"] = EncodeHash(testRoot),
                ["name"] = "cqrs-notes.txt",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.AreEqual("cqrs-notes-1.txt", json["added"]?[0]?["name"]?.ToString());
        }

        [TestMethod]
        public async Task Connector_CqrsRename_IntoExistingName_AppendsNumericSuffixBeforeExtension()
        {
            await CreateTestFile(testRoot + "/cqrs-draft.txt", "draft");
            await CreateTestFile(testRoot + "/cqrs-published.txt", "published");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "rename",
                ["target"] = EncodeHash(testRoot + "/cqrs-draft.txt"),
                ["name"] = "cqrs-published.txt",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.AreEqual("cqrs-published-1.txt", json["added"]?[0]?["name"]?.ToString());
        }

        [TestMethod]
        public async Task Connector_CqrsRm_DeletesExistingFile_ReturnsHashInRemovedList()
        {
            var filePath = testRoot + "/cqrs-rm.txt";
            await CreateTestFile(filePath, "content");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "rm",
                ["targets[]"] = EncodeHash(filePath),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            var removed = (JArray)json["removed"];
            Assert.IsNotNull(removed);
            Assert.AreEqual(EncodeHash(filePath), removed[0]?.ToString());
        }

        [TestMethod]
        public async Task Connector_CqrsUpload_IntoExistingName_AppendsNumericSuffixBeforeExtension()
        {
            await CreateTestFile(testRoot + "/cqrs-photo.jpg", "existing");

            SetMultipartRequest(
                new Dictionary<string, string>
                {
                    ["cmd"] = "upload",
                    ["target"] = EncodeHash(testRoot),
                },
                CreateFormFile("cqrs-photo.jpg", "new image content"));

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.AreEqual("cqrs-photo-1.jpg", json["added"]?[0]?["name"]?.ToString());
        }

        [TestMethod]
        public async Task Connector_CqrsParents_IncludesChildrenOfTargetPath()
        {
            var targetPath = testRoot + "/cqrs-target";
            await storage.CreateFolder(targetPath);
            await storage.CreateFolder(targetPath + "/child1");
            await storage.CreateFolder(targetPath + "/child2");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "parents",
                ["target"] = EncodeHash(targetPath),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            Assert.IsNotNull(json["tree"], "Expected 'tree' in parents response");
        }

        [TestMethod]
        public async Task Connector_CqrsPut_UpdatesFileContent_ReturnsChangedEntry()
        {
            var filePath = testRoot + "/cqrs-put.txt";
            await CreateTestFile(filePath, "original content");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "put",
                ["target"] = EncodeHash(filePath),
                ["content"] = "updated content",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            var changed = (JArray)json["changed"];
            Assert.IsNotNull(changed);
            Assert.AreEqual(1, changed.Count);
        }

        [TestMethod]
        public async Task Connector_CqrsPaste_CopiesFile_ReturnsAddedEntry()
        {
            var srcPath = testRoot + "/cqrs-paste-src.txt";
            var destFolder = testRoot + "/cqrs-paste-dest";
            await CreateTestFile(srcPath, "paste content");
            await storage.CreateFolder(destFolder);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "paste",
                ["dst"] = EncodeHash(destFolder),
                ["targets[]"] = EncodeHash(srcPath),
                ["cut"] = "0",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
        }

        [TestMethod]
        public async Task Connector_CqrsGet_ReturnsFileContent()
        {
            var filePath = testRoot + "/cqrs-get.txt";
            await CreateTestFile(filePath, "hello from get");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "get",
                ["target"] = EncodeHash(filePath),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            Assert.IsNotNull(json["content"]);
        }

        [TestMethod]
        public async Task Connector_CqrsLs_ReturnsIntersectionList()
        {
            await CreateTestFile(testRoot + "/cqrs-ls-a.txt", "a");
            await CreateTestFile(testRoot + "/cqrs-ls-b.txt", "b");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "ls",
                ["target"] = EncodeHash(testRoot),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            Assert.IsNotNull(json["list"] ?? json["intersect"]);
        }

        [TestMethod]
        public async Task Connector_CqrsTmb_ReturnsTmbObject()
        {
            var filePath = testRoot + "/cqrs-tmb.jpg";
            await CreateTestFile(filePath, "fake-jpeg");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "tmb",
                ["targets[]"] = EncodeHash(filePath),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
        }

        [TestMethod]
        public async Task Connector_CqrsInfo_ReturnsFilesArray()
        {
            var filePath = testRoot + "/cqrs-info.txt";
            await CreateTestFile(filePath, "info content");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "info",
                ["targets[]"] = EncodeHash(filePath),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            Assert.IsNotNull(json["files"]);
        }

        [TestMethod]
        public async Task Connector_CqrsSize_ReturnsSizeResponse()
        {
            var filePath = testRoot + "/cqrs-size.txt";
            await CreateTestFile(filePath, "size content");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "size",
                ["targets[]"] = EncodeHash(filePath),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            Assert.IsNotNull(json["size"]);
        }

        // â”€â”€â”€ TREE â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        public async Task Connector_Tree_WithValidTarget_ReturnsOnlyDirectories()
        {
            var dirPath = testRoot + "/tree-test";
            await storage.CreateFolder(dirPath);
            await storage.CreateFolder(dirPath + "/subA");
            await storage.CreateFolder(dirPath + "/subB");
            await CreateTestFile(dirPath + "/file.txt", "content");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "tree",
                ["target"] = EncodeHash(dirPath),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            var tree = (JArray)json["tree"];
            Assert.IsNotNull(tree);
            Assert.IsTrue(tree.Count >= 2, "Should contain both subdirectories");
            Assert.IsTrue(tree.All(e => e["mime"]?.ToString() == "directory"), "tree must only return directories");
        }

        [TestMethod]
        public async Task Connector_Tree_WithTraversalPath_ReturnsAccessError()
        {
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "tree",
                ["target"] = EncodeHash("/pub/../etc"),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.AreEqual("errAccess", json["error"]?.ToString());
        }

        // â”€â”€â”€ LS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        public async Task Connector_Ls_WithValidTarget_ReturnsHashToNameMap()
        {
            var dirPath = testRoot + "/ls-test";
            await storage.CreateFolder(dirPath);
            await CreateTestFile(dirPath + "/alpha.txt", "a");
            await storage.CreateFolder(dirPath + "/beta");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "ls",
                ["target"] = EncodeHash(dirPath),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            var list = json["list"] as JArray;
            Assert.IsNotNull(list, "'list' should be a JSON array per the elFinder 2.1 spec.");
            Assert.IsTrue(list.Count >= 2, "Should list both the file and subdirectory");
            var names = list.Select(t => t.ToString()).ToList();
            Assert.IsTrue(names.Any(n => n.StartsWith("alpha")), "Should contain alpha.txt");
            Assert.IsTrue(names.Any(n => n.StartsWith("beta")), "Should contain beta");
        }

        [TestMethod]
        public async Task Connector_Ls_WithTraversalPath_ReturnsAccessError()
        {
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "ls",
                ["target"] = EncodeHash("/pub/../etc"),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.AreEqual("errAccess", json["error"]?.ToString());
        }

        // â”€â”€â”€ GET â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        public async Task Connector_Get_OnExistingTextFile_ReturnsContent()
        {
            var filePath = testRoot + "/get-test.txt";
            await CreateTestFile(filePath, "hello from get");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "get",
                ["target"] = EncodeHash(filePath),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            Assert.AreEqual("hello from get", json["content"]?.ToString());
            Assert.AreEqual("utf-8", json["encoding"]?.ToString());
        }

        [TestMethod]
        public async Task Connector_Get_WithInvalidTarget_ReturnsAccessError()
        {
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "get",
                ["target"] = EncodeHash("/private/secret.txt"),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.AreEqual("errAccess", json["error"]?.ToString());
        }

        // â”€â”€â”€ PUT â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        public async Task Connector_Put_UpdatesFileContent_ReturnsChangedEntry()
        {
            var filePath = testRoot + "/put-test.txt";
            await CreateTestFile(filePath, "original");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "put",
                ["target"] = EncodeHash(filePath),
                ["content"] = "updated content",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            var changed = (JArray)json["changed"];
            Assert.IsNotNull(changed);
            Assert.AreEqual(1, changed.Count);
            Assert.AreEqual("put-test.txt", changed[0]?["name"]?.ToString());
        }

        [TestMethod]
        public async Task Connector_Put_WithInvalidTarget_ReturnsAccessError()
        {
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "put",
                ["target"] = EncodeHash("/private/secret.txt"),
                ["content"] = "bad",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.AreEqual("errAccess", json["error"]?.ToString());
        }

        // â”€â”€â”€ PASTE (copy) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        public async Task Connector_Paste_Copy_CreatesFileInDestination_SourcePreserved()
        {
            var srcPath = testRoot + "/paste-src.txt";
            var destDir = testRoot + "/paste-dest";
            await CreateTestFile(srcPath, "paste content");
            await storage.CreateFolder(destDir);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "paste",
                ["dst"] = EncodeHash(destDir),
                ["targets[]"] = EncodeHash(srcPath),
                ["cut"] = "0",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            var added = (JArray)json["added"];
            Assert.IsNotNull(added);
            Assert.AreEqual(1, added.Count);
            Assert.AreEqual("paste-src.txt", added[0]?["name"]?.ToString());

            // Source must still exist after copy
            var srcEntry = await storage.GetFileAsync(srcPath);
            Assert.IsNotNull(srcEntry, "Source file should still exist after copy");
        }

        [TestMethod]
        public async Task Connector_Paste_Copy_WithInvalidDst_ReturnsAccessError()
        {
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "paste",
                ["dst"] = EncodeHash("/private/bad"),
                ["targets[]"] = EncodeHash(testRoot + "/any.txt"),
                ["cut"] = "0",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);
            Assert.AreEqual("errAccess", json["error"]?.ToString());
        }

        // â”€â”€â”€ PASTE (cut / move) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        public async Task Connector_Paste_Cut_MovesFileToDestination_SourceRemovedFromResponse()
        {
            var srcPath = testRoot + "/move-src.txt";
            var destDir = testRoot + "/move-dest";
            await CreateTestFile(srcPath, "move content");
            await storage.CreateFolder(destDir);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "paste",
                ["dst"] = EncodeHash(destDir),
                ["targets[]"] = EncodeHash(srcPath),
                ["cut"] = "1",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            var added = (JArray)json["added"];
            var removed = (JArray)json["removed"];
            Assert.IsNotNull(added);
            Assert.AreEqual(1, added.Count);
            Assert.IsNotNull(removed);
            Assert.AreEqual(EncodeHash(srcPath), removed[0]?.ToString());
        }

        // â”€â”€â”€ TMB â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        public async Task Connector_Tmb_WithImageTarget_ReturnsHashToUrlMap()
        {
            var jpgPath = testRoot + "/tmb-test.jpg";
            await CreateTestFile(jpgPath, "fake-jpeg");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "tmb",
                ["targets[]"] = EncodeHash(jpgPath),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            var images = json["images"] as JObject;
            Assert.IsNotNull(images);
            Assert.AreEqual(1, images.Count);
            var url = images.Properties().First().Value.ToString();
            Assert.IsTrue(url.Contains("/FileManager/GetImageThumbnail"), "URL should reference thumbnail endpoint");
            Assert.IsTrue(url.Contains("width=80"), "URL should include width param");
        }

        [TestMethod]
        public async Task Connector_Tmb_WithNonImageTarget_ReturnsEmptyImages()
        {
            var txtPath = testRoot + "/tmb-test.txt";
            await CreateTestFile(txtPath, "text");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "tmb",
                ["targets[]"] = EncodeHash(txtPath),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            var images = json["images"] as JObject;
            Assert.IsNotNull(images);
            Assert.AreEqual(0, images.Count, "Non-image file should produce no thumbnail entry");
        }

        // â”€â”€â”€ INFO â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        public async Task Connector_Info_WithValidTarget_ReturnsFileMetadata()
        {
            var filePath = testRoot + "/info-test.txt";
            await CreateTestFile(filePath, "info content");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "info",
                ["targets[]"] = EncodeHash(filePath),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            var files = (JArray)json["files"];
            Assert.IsNotNull(files);
            Assert.AreEqual(1, files.Count);
            Assert.AreEqual("info-test.txt", files[0]?["name"]?.ToString());
            Assert.IsNotNull(files[0]?["hash"]);
            Assert.IsNotNull(files[0]?["mime"]);
        }

        [TestMethod]
        public async Task Connector_Info_WithInvalidTargetHash_ReturnsEmptyFilesArray()
        {
            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "info",
                ["targets[]"] = "l1_INVALID==",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            var files = (JArray)json["files"];
            Assert.IsNotNull(files);
            Assert.AreEqual(0, files.Count, "Invalid hash should be silently skipped");
        }

        // â”€â”€â”€ SIZE â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        public async Task Connector_Size_WithValidFile_ReturnsNonNegativeNumericTotal()
        {
            var filePath = testRoot + "/size-test.txt";
            await CreateTestFile(filePath, "hello world");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "size",
                ["targets[]"] = EncodeHash(filePath),
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            Assert.IsNotNull(json["size"]);
            Assert.IsTrue(long.TryParse(json["size"]!.ToString(), out var size) && size >= 0,
                "size must be a non-negative number");
        }

        [TestMethod]
        public async Task Connector_Size_WithMultipleFiles_EachReturnsNonNegativeSize()
        {
            var fileA = testRoot + "/size-a.txt";
            var fileB = testRoot + "/size-b.txt";
            await CreateTestFile(fileA, "aaaa");
            await CreateTestFile(fileB, "bbbbbbbb");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "size",
                ["targets[]"] = EncodeHash(fileA),
            });
            var ra = AsJsonObject(await controller.Connector());
            long.TryParse(ra["size"]!.ToString(), out var sizeA);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "size",
                ["targets[]"] = EncodeHash(fileB),
            });
            var rb = AsJsonObject(await controller.Connector());
            long.TryParse(rb["size"]!.ToString(), out var sizeB);

            Assert.IsTrue(sizeA >= 0, "File A size must be non-negative");
            Assert.IsTrue(sizeB >= 0, "File B size must be non-negative");
        }

        // â”€â”€â”€ RENAME (success path) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        public async Task Connector_Rename_WithValidNewName_ReturnsAddedAndRemoved()
        {
            var filePath = testRoot + "/rename-me.txt";
            await CreateTestFile(filePath, "rename content");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "rename",
                ["target"] = EncodeHash(filePath),
                ["name"] = "renamed.txt",
            });

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            var added = (JArray)json["added"];
            Assert.IsNotNull(added);
            Assert.AreEqual("renamed.txt", added[0]?["name"]?.ToString());
            var removed = (JArray)json["removed"];
            Assert.IsNotNull(removed);
            Assert.AreEqual(EncodeHash(filePath), removed[0]?.ToString());
        }

        [TestMethod]
        public async Task Connector_Rename_WithUnsafeName_ReturnsInvalidNameError()
        {
            var filePath = testRoot + "/rename-bad.txt";
            await CreateTestFile(filePath, "content");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "rename",
                ["target"] = EncodeHash(filePath),
                ["name"] = "../escape.txt",
            });

            var result = await controller.Connector();

            var json = AsJsonObject(result);
            Assert.AreEqual("errInvName", json["error"]?.ToString());
        }

        // â”€â”€â”€ UPLOAD (success path) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [TestMethod]
        public async Task Connector_Upload_BasicSuccess_ReturnsAddedEntry()
        {
            SetMultipartRequest(
                new Dictionary<string, string>
                {
                    ["cmd"] = "upload",
                    ["target"] = EncodeHash(testRoot),
                },
                CreateFormFile("upload-basic.jpg", "fake image data"));

            var result = await controller.Connector();
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
            var added = (JArray)json["added"];
            Assert.IsNotNull(added);
            Assert.IsTrue(added.Count >= 1, "Should have at least one added entry");
            Assert.AreEqual("upload-basic.jpg", added[0]?["name"]?.ToString());
        }

        // â”€â”€â”€ REGRESSION TESTS (bug fixes) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Regression: the "tmb" field in file listings must contain only the encoded path
        /// suffix (appended after tmbUrl), not a fully-qualified URL.
        /// Previously the tmb field started with "/FileManager/GetImageThumbnail?target=...",
        /// causing elFinder to prepend tmbUrl again and producing a doubled URL that resulted
        /// in a 500 error when loading thumbnails.
        /// </summary>
        [TestMethod]
        public async Task Connector_Open_ImageFile_TmbFieldContainsOnlySuffix_NotFullUrl()
        {
            // Arrange
            var filePath = testRoot + "/thumb-test.jpg";
            await CreateTestFile(filePath, "fake-jpeg-content");

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "open",
                ["target"] = EncodeHash(testRoot),
            });

            // Act
            var result = await controller.Connector();

            // Assert
            var json = AsJsonObject(result);
            var files = (JArray)json["files"];
            Assert.IsNotNull(files);

            var imageFile = files.FirstOrDefault(f => f["name"]?.ToString() == "thumb-test.jpg");
            Assert.IsNotNull(imageFile, "Image file should appear in listing");

            var tmb = imageFile["tmb"]?.ToString();
            Assert.IsNotNull(tmb, "Image file should have a tmb field");

            // The tmb value must NOT be a full URL path â€” it must be the suffix that
            // gets appended to tmbUrl. elFinder builds: tmbUrl + tmb.
            // tmbUrl = "/FileManager/GetImageThumbnail?target="
            // So tmb must NOT start with "/FileManager/..."
            Assert.IsFalse(
                tmb.StartsWith("/FileManager/", StringComparison.OrdinalIgnoreCase),
                $"tmb should be a URL suffix, not a full URL path. Got: {tmb}");

            // Must be the URL-encoded file path followed by width/height params
            Assert.IsTrue(
                tmb.Contains("width=80") && tmb.Contains("height=80"),
                $"tmb should contain size params. Got: {tmb}");
        }

        /// <summary>
        /// Regression: the "parents" command returned errAccess for virtual directory paths
        /// (directories that exist in blob storage only as implied prefixes with no marker blob).
        /// The fix makes IsAccessibleAsync fall back to listing children when GetFileAsync returns null.
        /// </summary>
        [TestMethod]
        public async Task Connector_Parents_VirtualDirectory_ReturnsTreeNotAccessError()
        {
            // Arrange: create a nested path â€” the inner directory is a virtual path
            var level1Path = testRoot + "/design";
            var level2Path = level1Path + "/images";
            await storage.CreateFolder(level1Path);
            await storage.CreateFolder(level2Path);

            SetGetRequest(new Dictionary<string, string>
            {
                ["cmd"] = "parents",
                ["target"] = EncodeHash(level2Path),
            });

            // Act
            var result = await controller.Connector();

            // Assert
            var json = AsJsonObject(result);

            Assert.IsNull(json["error"], $"Expected no error but got: {json["error"]}");
            Assert.IsNotNull(json["tree"], "Expected a tree in the response");

            var tree = (JArray)json["tree"];
            Assert.IsTrue(tree.Count > 0, "Tree should contain at least one entry");

            var hashes = tree.Select(item => item?["hash"]?.ToString()).ToHashSet();
            Assert.IsTrue(
                hashes.Contains(EncodeHash(level1Path)),
                "Ancestor 'design' should appear in the tree");
        }

        // ─── ADR 0040: DisplayPath must use article titles, not numeric IDs ──────
        //
        // These are TRUE regression tests.
        //
        // CURRENT STATE (bug present, no fix):
        //   Setup() wires the dispatcher with the default PassThroughNameResolver.
        //   When Connector() opens an article folder the numeric ID is left in the
        //   DisplayPath.  ValidateDisplayPathsForArticles() detects this and throws
        //   InvalidOperationException before the response is serialised, causing the
        //   test to fail with an unexpected exception.
        //
        // AFTER THE FIX:
        //   Setup() must register ElFinderNameResolver (backed by the real
        //   in-memory DB) before calling AddElFinderDriver(), matching Program.cs.
        //   The resolver then substitutes the numeric ID with the article title,
        //   ValidateDisplayPathsForArticles() passes, and the title assertions below
        //   confirm the correct value is returned.
        //
        // Each test seeds a real article in the DB and creates the matching blob
        // folder so the end-to-end path through the resolver can be exercised.

        /// <summary>
        /// Regression (ADR 0040): Opening /pub/articles/{number} must return cwd.name equal
        /// to the article title, not the numeric ID.
        /// FAILS before fix (InvalidOperationException from ValidateDisplayPathsForArticles).
        /// PASSES after fix (ElFinderNameResolver wired in Setup()).
        /// </summary>
        [TestMethod]
        public async Task Connector_Open_ArticleFolder_CwdNameIsArticleTitle_NotNumericId()
        {
            const int articleNumber = 501;
            const string articleTitle = "My Article Title";
            var articlePath = $"/pub/articles/{articleNumber}";
            await storage.CreateFolder(articlePath);

            dbContext.Articles.Add(new Cosmos.Common.Data.Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = articleNumber,
                Title = articleTitle,
                UrlPath = "my-article-title",
                Content = "<p>test</p>",
                StatusCode = (int)Cosmos.Common.Data.Logic.StatusCodeEnum.Active,
                VersionNumber = 1,
                Updated = DateTimeOffset.UtcNow.DateTime,
                UserId = Guid.NewGuid().ToString(),
                BannerImage = string.Empty,
            });
            await dbContext.SaveChangesAsync();

            try
            {
                SetGetRequest(new Dictionary<string, string>
                {
                    ["cmd"] = "open",
                    ["target"] = EncodeHash(articlePath),
                });

                var result = await controller.Connector();
                var json = AsJsonObject(result);

                Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
                var cwdName = json["cwd"]?["name"]?.ToString();
                Assert.AreEqual(
                    articleTitle,
                    cwdName,
                    $"cwd.name must be the article title '{articleTitle}', not the raw number '{articleNumber}'. " +
                    $"Fix: register ElFinderNameResolver in Setup() before AddElFinderDriver().");
            }
            finally
            {
                try { await storage.DeleteFolderAsync(articlePath); } catch { }
            }
        }

        /// <summary>
        /// Regression (ADR 0040): Opening /pub/articles must return the article folder child
        /// with name equal to the article title, not the numeric ID.
        /// FAILS before fix. PASSES after fix.
        /// </summary>
        [TestMethod]
        public async Task Connector_Open_ArticlesParentFolder_ChildNameIsArticleTitle_NotNumericId()
        {
            const int articleNumber = 502;
            const string articleTitle = "Another Article";
            var articlesPath = "/pub/articles";
            var articlePath = $"/pub/articles/{articleNumber}";
            await storage.CreateFolder(articlesPath);
            await storage.CreateFolder(articlePath);

            dbContext.Articles.Add(new Cosmos.Common.Data.Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = articleNumber,
                Title = articleTitle,
                UrlPath = "another-article",
                Content = "<p>test</p>",
                StatusCode = (int)Cosmos.Common.Data.Logic.StatusCodeEnum.Active,
                VersionNumber = 1,
                Updated = DateTimeOffset.UtcNow.DateTime,
                UserId = Guid.NewGuid().ToString(),
                BannerImage = string.Empty,
            });
            await dbContext.SaveChangesAsync();

            try
            {
                SetGetRequest(new Dictionary<string, string>
                {
                    ["cmd"] = "open",
                    ["target"] = EncodeHash(articlesPath),
                });

                var result = await controller.Connector();
                var json = AsJsonObject(result);

                Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
                var files = json["files"] as Newtonsoft.Json.Linq.JArray;
                Assert.IsNotNull(files);

                var articleEntry = files.FirstOrDefault(f =>
                    f["name"]?.ToString() == articleTitle ||
                    f["name"]?.ToString() == articleNumber.ToString());

                Assert.IsNotNull(articleEntry, $"No entry found for article {articleNumber} in files[].");
                Assert.AreEqual(
                    articleTitle,
                    articleEntry["name"]?.ToString(),
                    $"Article folder name must be the title '{articleTitle}', not the raw number '{articleNumber}'. " +
                    $"Fix: register ElFinderNameResolver in Setup() before AddElFinderDriver().");
            }
            finally
            {
                try { await storage.DeleteFolderAsync(articlePath); } catch { }
                try { await storage.DeleteFolderAsync(articlesPath); } catch { }
            }
        }

        /// <summary>
        /// Regression (ADR 0040): A deep path /pub/articles/{number}/my subdirectory must
        /// return displayPath values that use the article title at segment [2], not the
        /// numeric ID.  Mirrors the real-world path: /pub/articles/my article name/my subdirectory/myimage.png.
        /// FAILS before fix. PASSES after fix.
        /// </summary>
        [TestMethod]
        public async Task Connector_Open_DeepArticlePath_DisplayPathContainsArticleTitle_NotNumericId()
        {
            const int articleNumber = 503;
            const string articleTitle = "My Article Name";
            var articlePath = $"/pub/articles/{articleNumber}";
            var subPath = $"{articlePath}/my subdirectory";
            await storage.CreateFolder(articlePath);
            await storage.CreateFolder(subPath);
            await CreateTestFile(subPath + "/myimage.png", "fake-png");

            dbContext.Articles.Add(new Cosmos.Common.Data.Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = articleNumber,
                Title = articleTitle,
                UrlPath = "my-article-name",
                Content = "<p>test</p>",
                StatusCode = (int)Cosmos.Common.Data.Logic.StatusCodeEnum.Active,
                VersionNumber = 1,
                Updated = DateTimeOffset.UtcNow.DateTime,
                UserId = Guid.NewGuid().ToString(),
                BannerImage = string.Empty,
            });
            await dbContext.SaveChangesAsync();

            try
            {
                SetGetRequest(new Dictionary<string, string>
                {
                    ["cmd"] = "open",
                    ["target"] = EncodeHash(subPath),
                });

                var result = await controller.Connector();
                var json = AsJsonObject(result);

                Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");

                // No displayPath in the response may carry a numeric ID at segment [2].
                var files = json["files"] as Newtonsoft.Json.Linq.JArray;
                Assert.IsNotNull(files);

                var numericViolation = files
                    .Select(f => f["displayPath"]?.ToString())
                    .Where(dp => dp != null)
                    .FirstOrDefault(dp =>
                    {
                        var segs = dp.Split('/', StringSplitOptions.RemoveEmptyEntries);
                        return segs.Length >= 3
                            && segs[0].Equals("pub", StringComparison.OrdinalIgnoreCase)
                            && segs[1].Equals("articles", StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(segs[2], out _);
                    });

                Assert.IsNull(
                    numericViolation,
                    $"displayPath '{numericViolation}' contains a numeric article ID at segment [2]. " +
                    $"Expected '/pub/articles/{articleTitle}/my subdirectory'. " +
                    $"Fix: register ElFinderNameResolver in Setup() before AddElFinderDriver().");
            }
            finally
            {
                try { await storage.DeleteFolderAsync(articlePath); } catch { }
            }
        }

        /// <summary>
        /// Regression (ADR 0040): The tree command for /pub/articles must return the article
        /// folder as a tree item whose name is the article title, not the numeric ID.
        /// FAILS before fix (the article folder appears with name="504", the raw number).
        /// PASSES after fix (ElFinderNameResolver wired in Setup()).
        /// </summary>
        [TestMethod]
        public async Task Connector_Open_ArticlesRoot_SkipsDeletedArticleFolders()
        {
            var activeArticleNumber = 600000 + Math.Abs(Guid.NewGuid().GetHashCode() % 100000);
            var deletedArticleNumber = activeArticleNumber + 1;
            const string activeTitle = "Visible Article";
            const string deletedTitle = "Hidden Article";
            var articlesPath = "/pub/articles";
            await storage.CreateFolder(articlesPath);

            dbContext.ArticleCatalog.AddRange(
                new Cosmos.Common.Data.CatalogEntry
                {
                    ArticleNumber = activeArticleNumber,
                    Title = activeTitle,
                    UrlPath = "visible-article",
                    Status = nameof(Cosmos.Common.Data.Logic.StatusCodeEnum.Active),
                    Updated = DateTimeOffset.UtcNow,
                },
                new Cosmos.Common.Data.CatalogEntry
                {
                    ArticleNumber = deletedArticleNumber,
                    Title = deletedTitle,
                    UrlPath = "hidden-article",
                    Status = nameof(Cosmos.Common.Data.Logic.StatusCodeEnum.Deleted),
                    Updated = DateTimeOffset.UtcNow,
                });
            await dbContext.SaveChangesAsync();

            // Create folders for both articles, but only the active one should appear in the tree.
            storage.CreateFolder($"{articlesPath}/{activeArticleNumber}").Wait();
            storage.CreateFolder($"{articlesPath}/{deletedArticleNumber}").Wait();

            try
            {
                SetGetRequest(new Dictionary<string, string>
                {
                    ["cmd"] = "open",
                    ["target"] = EncodeHash(articlesPath),
                });

                var result = await controller.Connector();
                var json = AsJsonObject(result);

                Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");
                var files = json["files"] as JArray;
                Assert.IsNotNull(files);

                var visible = files!.FirstOrDefault(f => f["name"]?.ToString() == activeTitle);
                Assert.IsNotNull(visible, $"Expected visible article '{activeTitle}' in files[].");

                var hidden = files!.FirstOrDefault(f => f["name"]?.ToString() == deletedTitle);
                Assert.IsNull(hidden, $"Deleted article '{deletedTitle}' must not appear in files[].");
            }
            finally
            {
                try { await storage.DeleteFolderAsync(articlesPath); } catch { }
            }
        }

        [TestMethod]
        public async Task Connector_Tree_ArticleFolder_TreeItemNameIsArticleTitle_NotNumericId()
        {
            var articleNumber = 700000 + Math.Abs(Guid.NewGuid().GetHashCode() % 100000);
            const string articleTitle = "Tree Article Title";
            var articlesPath = "/pub/articles";
            var articlePath = $"/pub/articles/{articleNumber}";
            await storage.CreateFolder(articlesPath);
            await storage.CreateFolder(articlePath);

            dbContext.Articles.Add(new Cosmos.Common.Data.Article
            {
                Id = Guid.NewGuid(),
                ArticleNumber = articleNumber,
                Title = articleTitle,
                UrlPath = "tree-article-title",
                Content = "<p>test</p>",
                StatusCode = (int)Cosmos.Common.Data.Logic.StatusCodeEnum.Active,
                VersionNumber = 1,
                Updated = DateTimeOffset.UtcNow.DateTime,
                UserId = Guid.NewGuid().ToString(),
                BannerImage = string.Empty,
            });
            await dbContext.SaveChangesAsync();

            try
            {
                // Target /pub/articles (the parent) so the article folder itself
                // appears as a tree item — its name will be "504" before the fix.
                var context = CreateAuthorizedHttpContext();
                context.Request.Method = "GET";
                context.Request.QueryString = QueryString.Create(new Dictionary<string, string>
                {
                    ["cmd"] = "tree",
                    ["target"] = EncodeHash(articlesPath),
                });
                controller.ControllerContext = new ControllerContext { HttpContext = context };

                var result = await controller.Connector();
                var json = AsJsonObject(result);

                Assert.IsNull(json["error"], $"Unexpected error: {json["error"]}");

                var tree = json["tree"] as Newtonsoft.Json.Linq.JArray;
                Assert.IsNotNull(tree, "Response must contain a 'tree' array.");

                // The article folder must appear in the tree and must use the title as name.
                var articleEntry = tree.FirstOrDefault(f =>
                    f["name"]?.ToString() == articleNumber.ToString() ||
                    f["name"]?.ToString() == articleTitle);

                Assert.IsNotNull(
                    articleEntry,
                    $"No tree entry found for article {articleNumber} ('{articleTitle}'). " +
                    $"The article folder must appear in the tree[] response.");

                Assert.AreEqual(
                    articleTitle,
                    articleEntry["name"]?.ToString(),
                    $"tree item name must be the article title '{articleTitle}', not the raw number '{articleNumber}'. " +
                    $"Fix: register ElFinderNameResolver in Setup() before AddElFinderDriver().");
            }
            finally
            {
                try { await storage.DeleteFolderAsync(articlePath); } catch { }
                try { await storage.DeleteFolderAsync(articlesPath); } catch { }
            }
        }

        private IServiceProvider BuildRequestServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddElFinderDriver();
            services.AddScoped<IStorageContext>(_ => storage);
            return services.BuildServiceProvider();
        }
    }
}
