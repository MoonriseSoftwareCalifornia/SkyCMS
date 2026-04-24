// <copyright file="ElFinderConnectorControllerTests.cs" company="Moonrise Software, LLC">
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
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using Sky.Cms.Controllers;
    using Sky.Cms.Models;
    using Sky.Editor.Services.EditorSettings;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Claims;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Unit tests for <see cref="ElFinderConnectorController"/>.
    /// </summary>
    [TestClass]
    public class ElFinderConnectorControllerTests : SkyCmsTestBase
    {
        private const string VolumeId = "l1_";
        private ApplicationDbContext dbContext;
        private UserManager<IdentityUser> userManager;
        private ICacheService<Layout> layoutCacheService;
        private IStorageContext storage;
        private IEditorSettings editorSettings;
        private Mock<IMediator> mediator;
        private Mock<ILogger<ElFinderConnectorController>> logger;
        private ElFinderConnectorController controller;
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
            logger = new Mock<ILogger<ElFinderConnectorController>>();

            testRoot = $"/pub/elfinder-tests-{Guid.NewGuid():N}";
            await storage.CreateFolder(testRoot);

            controller = new ElFinderConnectorController(
                dbContext,
                userManager,
                mediator.Object,
                layoutCacheService,
                storage,
                editorSettings,
                logger.Object);

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
            Assert.AreEqual("2.1", json["api"]?.ToString());
            Assert.IsTrue((json["files"] as JArray)?.Count >= 1);
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

        private static string EncodeHash(string path)
        {
            var bytes = Encoding.UTF8.GetBytes(path.TrimStart('/'));
            return VolumeId + Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private JObject AsJsonObject(IActionResult result)
        {
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
    }
}
