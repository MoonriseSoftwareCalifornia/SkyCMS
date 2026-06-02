// <copyright file="FileManagerController.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Articles.EditorQueries;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Services;
    using Cosmos.Common.Services.Caching;
    using Cosmos.DynamicConfig;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using MimeTypes;
    using Newtonsoft.Json;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Processing;
    using Sky.Cms.Models;
    using Sky.Cms.Services;
    using Sky.Editor.Data.Logic;
    using Sky.Editor.Features.Articles.Save;
    using Sky.Editor.Models;
    using Sky.Editor.Services.CDN;
    using Sky.Editor.Services.EditorSettings;
    using SkyCMS.Drivers.ElFinder;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Responses;

    /// <summary>
    /// Connector adapter controller that maps elFinder JSON protocol commands to SkyCMS
    /// storage operations. Business commands are handled here; cross-cutting concerns
    /// (tenancy, authentication) remain in middleware and the standard pipeline.
    /// See ADR 0035.
    /// </summary>
    [Authorize(Roles = "Administrators, Editors, Authors, Team Members")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class FileManagerController : BaseController
    {
        private const string VolumeId = ElFinderHashEncoder.VolumeId;
        private const string RootPath = "/pub";

        /// <summary>
        /// Gets the file extensions that are considered valid images.
        /// </summary>
        public static string[] ValidImageExtensions => FileStorageConstants.ValidImageExtensions;

        /// <summary>
        /// Gets the file extensions that are not allowed for upload due to security concerns.
        /// </summary>
        public static string[] DangerousFileExtensions => FileStorageConstants.DangerousFileExtensions;

        /// <summary>
        /// Fixes the path so it always starts with a forward slash.
        /// </summary>
        /// <param name="path">Path to fix.</param>
        /// <returns>Fixed path string.</returns>
        public static string FixPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "/";
            }

            if (path.StartsWith("http://") || path.StartsWith("https://"))
            {
                return path;
            }

            return "/" + path.TrimStart('/');
        }

        /// <summary>
        /// Gets an array of image asset URLs from the given storage path.
        /// </summary>
        /// <param name="storageContext">Storage context.</param>
        /// <param name="path">Path to retrieve images.</param>
        /// <param name="exclude">Path to exclude images.</param>
        /// <returns>Array of image URLs.</returns>
        public static async Task<string[]> GetImageAssetArray(IStorageContext storageContext, string path, string exclude)
        {
            var blobs = await storageContext.GetFilesAndDirectories(path);

            if (!string.IsNullOrEmpty(exclude))
            {
                return blobs.Where(w => ValidImageExtensions.Contains(Path.GetExtension(w.Name).ToLower()) && !w.Path.ToLower().StartsWith(exclude.TrimStart('/').ToLower())).Select(s => new
                {
                    src = FixPath(s.Path),
                }).ToList().Select(s => s.src).ToArray();
            }

            return blobs.Where(w => ValidImageExtensions.Contains(Path.GetExtension(w.Name).ToLower())).Select(s => new
            {
                src = FixPath(s.Path),
            }).ToList().Select(s => s.src).ToArray();
        }

        private readonly ApplicationDbContext dbContext;
        private readonly IStorageContext storageContext;
        private readonly IFileOperationsService fileOperations;
        private readonly IEditorSettings editorSettings;
        private readonly ILogger<FileManagerController> logger;
        private readonly IConfiguration configuration;
        private readonly IMemoryCache memoryCache;
        private readonly IDynamicConfigurationProvider configProvider;
        private readonly ArticleEditLogic articleLogic;
        private readonly IWebHostEnvironment hostEnvironment;
        private readonly IViewRenderService viewRenderService;
        private readonly UserManager<IdentityUser> userManager;
        private readonly IFolderListingService folderListingService;
        private readonly Cosmos.Common.Features.Shared.IMediator articleQueries;
        private readonly SkyCMS.Drivers.ElFinder.IElFinderDispatcher elFinderMediator;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileManagerController"/> class.
        /// </summary>
        /// <param name="dbContext">Database context (required by BaseController).</param>
        /// <param name="userManager">User manager (required by BaseController).</param>
        /// <param name="mediator">Mediator (required by BaseController).</param>
        /// <param name="layoutCache">Layout cache (required by BaseController).</param>
        /// <param name="storageContext">Storage context for file operations.</param>
        /// <param name="fileOperations">File operations service.</param>
        /// <param name="editorSettings">Editor settings (blob URL, flags).</param>
        /// <param name="logger">Logger.</param>
        /// <param name="configuration">Configuration.</param>
        /// <param name="memoryCache">Application memory cache for deleted-article filtering.</param>
        /// <param name="configProvider">Dynamic configuration provider for tenant-scoped cache keys.</param>
        [ActivatorUtilitiesConstructor]
        public FileManagerController(
            ApplicationDbContext dbContext,
            UserManager<IdentityUser> userManager,
            Cosmos.Common.Features.Shared.IMediator mediator,
            ICacheService<Layout> layoutCache,
            IStorageContext storageContext,
            IFileOperationsService fileOperations,
            IEditorSettings editorSettings,
            ILogger<FileManagerController> logger,
            IConfiguration configuration,
            IMemoryCache memoryCache,
            SkyCMS.Drivers.ElFinder.IElFinderDispatcher elFinderMediator,
            IDynamicConfigurationProvider configProvider = null,
            ArticleEditLogic articleLogic = null,
            IWebHostEnvironment hostEnvironment = null,
            IViewRenderService viewRenderService = null,
            IFolderListingService folderListingService = null)
            : base(dbContext, userManager, mediator, layoutCache)
        {
            this.dbContext = dbContext;
            this.storageContext = storageContext;
            this.fileOperations = fileOperations;
            this.editorSettings = editorSettings;
            this.logger = logger;
            this.configuration = configuration;
            this.memoryCache = memoryCache;
            this.configProvider = configProvider;
            this.articleLogic = articleLogic;
            this.hostEnvironment = hostEnvironment;
            this.viewRenderService = viewRenderService;
            this.userManager = userManager;
            this.articleQueries = mediator;
            this.folderListingService = folderListingService;
            this.elFinderMediator = elFinderMediator;
        }

        /// <summary>
        /// elFinder connector endpoint. Accepts all elFinder JSON protocol commands via
        /// GET or POST and dispatches to the appropriate storage operation.
        /// </summary>
        /// <returns>JSON response conforming to the elFinder 2.1 API.</returns>
        [HttpGet]
        [HttpPost]
        [Route("FileManager/ElFinderConnector")]
        public async Task<IActionResult> Connector()
        {
            var cmd = GetParam("cmd");

            if (string.IsNullOrEmpty(cmd))
            {
                return Json(ElFinderError("errUnknownCmd"));
            }

             try
            {
                return cmd switch
                {
                    "open" => await HandleOpenViaCqrsAsync(),
                    "tree" => await HandleTreeViaCqrsAsync(),
                    "ls" => await HandleLsViaCqrsAsync(),
                    "mkdir" => await HandleMkdirViaCqrsAsync(),
                    "mkfile" => await HandleMkfileViaCqrsAsync(),
                    "rename" => await HandleRenameViaCqrsAsync(),
                    "rm" => await HandleRmViaCqrsAsync(),
                    "upload" => await HandleUploadViaCqrsAsync(),
                    "get" => await HandleGetViaCqrsAsync(),
                    "put" => await HandlePutViaCqrsAsync(),
                    "paste" => await HandlePasteViaCqrsAsync(),
                    "tmb" => await HandleTmbViaCqrsAsync(),
                    "info" => await HandleInfoViaCqrsAsync(),
                    "size" => await HandleSizeViaCqrsAsync(),
                    "parents" => await HandleParentsViaCqrsAsync(),
                    "search" => await HandleSearchViaCqrsAsync(),
                    "file" => await HandleFileViaCqrsAsync(),
                    "duplicate" => await HandleDuplicateViaCqrsAsync(),
                    "resize" => await HandleResizeViaCqrsAsync(),
                    "url" => await HandleUrlViaCqrsAsync(),
                    "dim" => await HandleDimViaCqrsAsync(),
                    _ => Json(ElFinderError("errUnknownCmd"))
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ElFinder connector error handling command '{Cmd}'", cmd);
                return Json(ElFinderError(ex.Message));
            }
        }

        /// <summary>
        /// Validates that no display paths in the response contain article IDs (integers) instead of article titles.
        /// ADR 0040 requires that display paths NEVER show `/pub/articles/{integer}` - they must show `/pub/articles/{title}`.
        /// This method enforces that contract by throwing an exception if a violation is detected.
        /// </summary>
        /// <param name="response">The elFinder response to validate.</param>
        /// <exception cref="InvalidOperationException">Thrown if any display path contains `/pub/articles/{integer}`.</exception>
        private void ValidateDisplayPathsForArticles(IElFinderResponse response)
        {
            if (response == null)
            {
                return;
            }

            var displayPaths = new List<string>();

            // Extract displayPath fields from various response types
            if (response is OpenResponse openResponse)
            {
                if (openResponse.Cwd?.DisplayPath != null)
                {
                    displayPaths.Add(openResponse.Cwd.DisplayPath);
                }

                if (openResponse.Files != null)
                {
                    foreach (var file in openResponse.Files)
                    {
                        if (file?.DisplayPath != null)
                        {
                            displayPaths.Add(file.DisplayPath);
                        }
                    }
                }
            }
            else if (response is TreeResponse treeResponse)
            {
                if (treeResponse.Tree != null)
                {
                    foreach (var item in treeResponse.Tree)
                    {
                        if (item?.DisplayPath != null)
                        {
                            displayPaths.Add(item.DisplayPath);
                        }
                    }
                }
            }
            else if (response is InfoResponse infoResponse)
            {
                if (infoResponse.Files != null)
                {
                    foreach (var file in infoResponse.Files)
                    {
                        if (file?.DisplayPath != null)
                        {
                            displayPaths.Add(file.DisplayPath);
                        }
                    }
                }
            }

            // Check each display path for violations
            foreach (var displayPath in displayPaths)
            {
                if (string.IsNullOrEmpty(displayPath))
                {
                    continue;
                }

                // Check if path matches /pub/articles/{integer}
                // Pattern: /pub/articles/ followed by one or more digits, optionally followed by more path segments
                var segments = displayPath.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 3
                    && segments[0].Equals("pub", System.StringComparison.OrdinalIgnoreCase)
                    && segments[1].Equals("articles", System.StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(segments[2], out _))
                {
                    throw new InvalidOperationException(
                        $"VIOLATION: DisplayPath contains article ID instead of title: '{displayPath}'. " +
                        $"ADR 0040 requires display paths to show article titles, not numeric IDs. " +
                        $"Expected format: /pub/articles/{{ArticleTitle}}, not /pub/articles/{{ArticleId}}. " +
                        $"This indicates the ElFinderNameResolver failed to resolve the article title.");
                }
            }
        }

        /// <summary>
        /// Serializes a CQRS response using System.Text.Json so that
        /// <c>[JsonPropertyName]</c> attributes on response DTOs are honoured.
        /// The MVC pipeline is configured with Newtonsoft + DefaultContractResolver
        /// (PascalCase), which ignores those attributes; using System.Text.Json here
        /// ensures the elFinder client receives the expected lowercase keys.
        /// </summary>
        private ContentResult JsonCqrs(IElFinderResponse response)
        {
            // Validate ADR 0040 contract before serializing
            ValidateDisplayPathsForArticles(response);

            var json = System.Text.Json.JsonSerializer.Serialize(response, response.GetType());
            return Content(json, "application/json; charset=utf-8");
        }

        private static IActionResult MapCqrsError(Controller controller, IElFinderResponse response)
        {
            if (response is not ElFinderErrorResponse error)
            {
                return null;
            }

            var code = error.ErrorCode;
            var mapped = code switch
            {
                "errCmdParams" => "errAccess",
                "errNotFound" => "errOpen",
                _ => code
            };

            return controller.Json(new { error = mapped });
        }

        private async Task<IActionResult> HandleTreeViaCqrsAsync()
        {
            var target = GetParam("target");
            var targetPath = string.IsNullOrWhiteSpace(target) ? RootPath : DecodeHash(target);
            var blocked = await DenyDeletedArticlePathForCqrsAsync(targetPath);
            if (blocked != null)
            {
                return blocked;
            }

            var command = new TreeCommand
            {
                Target = target,
                Filter = GetParam("filter"),
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleMkdirViaCqrsAsync()
        {
            var target = GetParam("target");
            var name = NormalizeElFinderName(GetParam("name"));
            var path = DecodeHash(target);

            if (path == null || !IsAllowedPath(path))
            {
                return Json(ElFinderError("errAccess"));
            }

            var blocked = await DenyDeletedArticlePathForCqrsAsync(path);
            if (blocked != null)
            {
                return blocked;
            }

            var hasBatchDirs = Request.Method == "POST" && Request.HasFormContentType && Request.Form.ContainsKey("dirs[]");
            if (!hasBatchDirs && string.IsNullOrWhiteSpace(name))
            {
                return Json(ElFinderError("errAccess"));
            }

            if (!string.IsNullOrWhiteSpace(name) && !IsSafeName(name))
            {
                return Json(ElFinderError("errInvName"));
            }

            var uniqueName = (!string.IsNullOrWhiteSpace(name))
                ? await GetUniqueNameAsync(path, name)
                : null;

            var batchDirs = hasBatchDirs
                ? GetParams("dirs[]")
                    .Select(NormalizeElFinderName)
                    .Where(d => !string.IsNullOrWhiteSpace(d) && IsSafeName(d))
                    .ToList()
                : null;

            var command = new MkdirCommand
            {
                Target = target,
                Name = uniqueName,
                Dirs = batchDirs,
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleMkfileViaCqrsAsync()
        {
            var target = GetParam("target");
            var name = NormalizeElFinderName(GetParam("name"));
            var path = DecodeHash(target);

            if (path == null || !IsAllowedPath(path) || string.IsNullOrWhiteSpace(name))
            {
                return Json(ElFinderError("errAccess"));
            }

            var blocked = await DenyDeletedArticlePathForCqrsAsync(path);
            if (blocked != null)
            {
                return blocked;
            }

            if (!IsSafeName(name))
            {
                return Json(ElFinderError("errInvName"));
            }

            var ext = Path.GetExtension(name).ToLowerInvariant();
            if (FileStorageConstants.DangerousFileExtensions.Contains(ext))
            {
                return Json(ElFinderError("errUploadFile"));
            }

            var uniqueName = await GetUniqueNameAsync(path, name);

            var command = new MkfileCommand
            {
                Target = target,
                Name = uniqueName,
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleRenameViaCqrsAsync()
        {
            var target = GetParam("target");
            var name = NormalizeElFinderName(GetParam("name"));
            var path = DecodeHash(target);

            if (path == null || !IsAllowedPath(path) || string.IsNullOrWhiteSpace(name))
            {
                return Json(ElFinderError("errAccess"));
            }

            var blocked = await DenyDeletedArticlePathForCqrsAsync(path);
            if (blocked != null)
            {
                return blocked;
            }

            if (!IsSafeName(name))
            {
                return Json(ElFinderError("errInvName"));
            }

            var parentPath = GetParentPath(path);
            var uniqueName = await GetUniqueNameAsync(parentPath, name, path);

            var command = new RenameCommand
            {
                Target = target,
                Name = uniqueName,
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleRmViaCqrsAsync()
        {
            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            var blockedTargets = await DenyDeletedArticleHashesForCqrsAsync(targets);
            if (blockedTargets != null)
            {
                return blockedTargets;
            }

            var removed = new List<string>();
            var notFound = new List<string>();
            var notRemoved = new List<string>();
            var notFoundDetails = new List<RmDiagnosticEntry>();
            var notRemovedDetails = new List<RmDiagnosticEntry>();
            foreach (var t in targets)
            {
                var command = new RmCommand
                {
                    Target = t,
                    VolumeId = VolumeId,
                };

                var response = await elFinderMediator.SendAsync(command);
                if (response is RmResponse rm)
                {
                    removed.AddRange(rm.Removed ?? new List<string>());
                    notFound.AddRange(rm.NotFound ?? new List<string>());
                    notRemoved.AddRange(rm.NotRemoved ?? new List<string>());
                    notFoundDetails.AddRange(rm.NotFoundDetails ?? new List<RmDiagnosticEntry>());
                    notRemovedDetails.AddRange(rm.NotRemovedDetails ?? new List<RmDiagnosticEntry>());
                }
            }

            return Json(new
            {
                removed = removed.Distinct().ToList(),
                notFound = notFound.Distinct().ToList(),
                notRemoved = notRemoved.Distinct().ToList(),
                notFoundDetails = notFoundDetails.Select(d => new
                {
                    hash = d.Hash,
                    path = d.Path,
                    reason = d.Reason,
                    reasonCode = d.ReasonCode,
                }).ToList(),
                notRemovedDetails = notRemovedDetails.Select(d => new
                {
                    hash = d.Hash,
                    path = d.Path,
                    reason = d.Reason,
                    reasonCode = d.ReasonCode,
                }).ToList(),
            });
        }

        private async Task<IActionResult> HandleOpenViaCqrsAsync()
        {
            var target = GetParam("target");
            var targetPath = string.IsNullOrWhiteSpace(target) ? RootPath : DecodeHash(target);
            var blocked = await DenyDeletedArticlePathForCqrsAsync(targetPath);
            if (blocked != null)
            {
                return blocked;
            }

            var isInit = GetParam("init") == "1";
            var command = new OpenCommand(
                target: target,
                init: isInit,
                volumeId: VolumeId,
                tree: GetParam("tree") == "1",
                blobPublicUrl: editorSettings.BlobPublicUrl,
                tmbUrl: "/FileManager/GetImageThumbnail?target=",
                rootPath: RootPath);

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleUploadViaCqrsAsync()
        {
            var target = GetParam("target");
            var path = DecodeHash(target);
            if (path == null || !IsAllowedPath(path))
            {
                return Json(ElFinderError("errAccess"));
            }

            var blocked = await DenyDeletedArticlePathForCqrsAsync(path);
            if (blocked != null)
            {
                return blocked;
            }

            var files = Request.Form.Files;
            if (files == null || files.Count == 0)
            {
                return Json(ElFinderError("errUploadNoFiles"));
            }

            var added = new List<object>();
            foreach (var file in files)
            {
                var normalizedName = NormalizeElFinderName(Path.GetFileName(file.FileName));
                if (string.IsNullOrWhiteSpace(normalizedName))
                {
                    continue;
                }

                if (!IsSafeName(normalizedName))
                {
                    return Json(ElFinderError("errInvName"));
                }

                var ext = Path.GetExtension(normalizedName).ToLowerInvariant();
                if (FileStorageConstants.DangerousFileExtensions.Contains(ext))
                {
                    return Json(ElFinderError("errUploadFile"));
                }

                var uniqueName = await GetUniqueNameAsync(path, normalizedName);

                var command = new UploadCommand
                {
                    Target = target,
                    FileStream = file.OpenReadStream(),
                    Filename = uniqueName,
                    VolumeId = VolumeId,
                };

                var response = await elFinderMediator.SendAsync(command);
                var mappedError = MapCqrsError(this, response);
                if (mappedError != null)
                {
                    return mappedError;
                }

                if (response is UploadResponse upload)
                {
                    added.AddRange(upload.Added);
                }
            }

            return Content(
                System.Text.Json.JsonSerializer.Serialize(new { added }),
                "application/json; charset=utf-8");
        }

        private async Task<IActionResult> HandleGetViaCqrsAsync()
        {
            var target = GetParam("target");
            var targetPath = DecodeHash(target);
            var blocked = await DenyDeletedArticlePathForCqrsAsync(targetPath);
            if (blocked != null)
            {
                return blocked;
            }

            var command = new GetCommand
            {
                Target = target,
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandlePutViaCqrsAsync()
        {
            var target = GetParam("target");
            var targetPath = DecodeHash(target);
            var blocked = await DenyDeletedArticlePathForCqrsAsync(targetPath);
            if (blocked != null)
            {
                return blocked;
            }

            var command = new PutCommand
            {
                Target = target,
                Content = GetParam("content"),
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandlePasteViaCqrsAsync()
        {
            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            var destination = GetParam("dst") ?? GetParam("target");
            var destinationPath = DecodeHash(destination);
            var blockedDestination = await DenyDeletedArticlePathForCqrsAsync(destinationPath);
            if (blockedDestination != null)
            {
                return blockedDestination;
            }

            var blockedSources = await DenyDeletedArticleHashesForCqrsAsync(targets);
            if (blockedSources != null)
            {
                return blockedSources;
            }

            var command = new PasteCommand
            {
                Target = destination,
                Sources = string.Join(',', targets),
                Cut = GetParam("cut"),
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleParentsViaCqrsAsync()
        {
            var target = GetParam("target");
            var targetPath = DecodeHash(target);
            var blocked = await DenyDeletedArticlePathForCqrsAsync(targetPath);
            if (blocked != null)
            {
                return blocked;
            }

            var command = new ParentsCommand
            {
                Target = target,
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            if (mappedError != null)
            {
                return mappedError;
            }

            // Use System.Text.Json so [JsonPropertyName] / [JsonIgnore] attributes on
            // the CQRS response DTOs are honored (the app uses Newtonsoft with
            // DefaultContractResolver which would otherwise produce PascalCase keys).
            // Serialize using runtime type so properties from concrete response classes are included.
            var json = System.Text.Json.JsonSerializer.Serialize(response, response.GetType());
            return Content(json, "application/json");
        }

        private async Task<IActionResult> HandleSizeViaCqrsAsync()
        {
            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            if (targets.Length == 0)
            {
                return Json(new { size = 0L });
            }

            var blockedTargets = await DenyDeletedArticleHashesForCqrsAsync(targets);
            if (blockedTargets != null)
            {
                return blockedTargets;
            }

            long total = 0;
            foreach (var target in targets)
            {
                var command = new SizeCommand
                {
                    Target = target,
                    VolumeId = VolumeId,
                };

                var response = await elFinderMediator.SendAsync(command);
                if (response is SizeResponse sizeResponse)
                {
                    total += sizeResponse.Size;
                }
            }

            return Json(new { size = total });
        }

        private async Task<IActionResult> HandleLsViaCqrsAsync()
        {
            var intersect = GetParams("intersect[]");
            var target = GetParam("target");
            var targetPath = string.IsNullOrWhiteSpace(target) ? RootPath : DecodeHash(target);
            var blocked = await DenyDeletedArticlePathForCqrsAsync(targetPath);
            if (blocked != null)
            {
                return blocked;
            }

            var command = new LsCommand
            {
                Target = target,
                Intersect = intersect,
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleTmbViaCqrsAsync()
        {
            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            var blockedTargets = await DenyDeletedArticleHashesForCqrsAsync(targets);
            if (blockedTargets != null)
            {
                return blockedTargets;
            }

            var command = new TmbCommand
            {
                Targets = string.Join(',', targets),
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleInfoViaCqrsAsync()
        {
            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            var blockedTargets = await DenyDeletedArticleHashesForCqrsAsync(targets);
            if (blockedTargets != null)
            {
                return blockedTargets;
            }

            var command = new InfoCommand
            {
                Targets = string.Join(',', targets),
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleSearchViaCqrsAsync()
        {
            var mimes = GetParams("mimes[]");
            var target = GetParam("target");
            var targetPath = string.IsNullOrWhiteSpace(target) ? RootPath : DecodeHash(target);
            var blocked = await DenyDeletedArticlePathForCqrsAsync(targetPath);
            if (blocked != null)
            {
                return blocked;
            }

            var command = new SearchCommand
            {
                Query = GetParam("q"),
                Target = target,
                Mimes = mimes.Length > 0 ? mimes : null,
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            if (mappedError != null)
            {
                return mappedError;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(response);
            return Content(json, "application/json");
        }

        private async Task<IActionResult> HandleFileViaCqrsAsync()
        {
            var target = GetParam("target");
            var targetPath = DecodeHash(target);
            var blocked = await DenyDeletedArticlePathForCqrsAsync(targetPath);
            if (blocked != null)
            {
                return blocked;
            }

            var command = new FileCommand
            {
                Target = target,
                Download = GetParam("download"),
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);

            if (response is FileResponse fileResponse && fileResponse.Stream != null)
            {
                if (fileResponse.ForceDownload)
                {
                    return File(fileResponse.Stream, fileResponse.ContentType, fileResponse.FileName);
                }

                Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileResponse.FileName}\"";
                return File(fileResponse.Stream, fileResponse.ContentType);
            }

            var mappedError2 = MapCqrsError(this, response);
            return mappedError2 ?? Json(ElFinderError("errOpen"));
        }

        private async Task<IActionResult> HandleDuplicateViaCqrsAsync()
        {
            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            var blockedTargets = await DenyDeletedArticleHashesForCqrsAsync(targets);
            if (blockedTargets != null)
            {
                return blockedTargets;
            }

            var command = new DuplicateCommand
            {
                Targets = string.Join(',', targets),
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            if (mappedError != null)
            {
                return mappedError;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(response);
            return Content(json, "application/json");
        }

        private async Task<IActionResult> HandleResizeViaCqrsAsync()
        {
            _ = int.TryParse(GetParam("width"), out var width);
            _ = int.TryParse(GetParam("height"), out var height);
            _ = int.TryParse(GetParam("x"), out var x);
            _ = int.TryParse(GetParam("y"), out var y);
            _ = int.TryParse(GetParam("degree"), out var degree);
            _ = int.TryParse(GetParam("quality"), out var quality);
            var target = GetParam("target");
            var targetPath = DecodeHash(target);
            var blocked = await DenyDeletedArticlePathForCqrsAsync(targetPath);
            if (blocked != null)
            {
                return blocked;
            }

            var command = new ResizeCommand
            {
                Target = target,
                Mode = GetParam("mode"),
                Width = width,
                Height = height,
                X = x,
                Y = y,
                Degree = degree,
                Quality = quality > 0 ? quality : 100,
                CopyName = GetParam("copyname"),
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            if (mappedError != null)
            {
                return mappedError;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(response);
            return Content(json, "application/json");
        }

        private async Task<IActionResult> HandleUrlViaCqrsAsync()
        {
            var target = GetParam("target");
            var targetPath = DecodeHash(target);
            var blocked = await DenyDeletedArticlePathForCqrsAsync(targetPath);
            if (blocked != null)
            {
                return blocked;
            }

            var command = new UrlCommand
            {
                Target = target,
                BlobPublicUrl = editorSettings.BlobPublicUrl,
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            if (mappedError != null)
            {
                return mappedError;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(response);
            return Content(json, "application/json");
        }

        private async Task<IActionResult> HandleDimViaCqrsAsync()
        {
            var target = GetParam("target");
            var targetPath = DecodeHash(target);
            var blocked = await DenyDeletedArticlePathForCqrsAsync(targetPath);
            if (blocked != null)
            {
                return blocked;
            }

            var command = new DimCommand
            {
                Target = target,
                VolumeId = VolumeId,
            };

            var response = await elFinderMediator.SendAsync(command);
            var mappedError = MapCqrsError(this, response);
            if (mappedError != null)
            {
                return mappedError;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(response);
            return Content(json, "application/json");
        }

        // Helpers
        private async Task<string> GetUniqueNameAsync(string parentPath, string requestedName, string ignorePath = null)
        {
            var desired = NormalizeElFinderName(requestedName);
            if (string.IsNullOrWhiteSpace(desired))
            {
                return requestedName;
            }

            var entries = await storageContext.GetFilesAndDirectories(parentPath);
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalizedIgnore = string.IsNullOrWhiteSpace(ignorePath) ? null : NormalizePath(ignorePath);

            foreach (var entry in entries)
            {
                var entryPath = NormalizePath(entry.Path.StartsWith("/") ? entry.Path : "/" + entry.Path);
                if (normalizedIgnore != null && string.Equals(entryPath, normalizedIgnore, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.Name))
                {
                    existing.Add(entry.Name);
                }

                var ext = entry.Extension ?? string.Empty;
                if (!string.IsNullOrEmpty(ext) && !ext.StartsWith('.'))
                {
                    ext = "." + ext;
                }

                if (!entry.IsDirectory && !string.IsNullOrWhiteSpace(entry.Name) && !string.IsNullOrEmpty(ext))
                {
                    existing.Add(entry.Name + ext);
                }

                var fileNameFromPath = Path.GetFileName(entryPath);
                if (!string.IsNullOrWhiteSpace(fileNameFromPath))
                {
                    existing.Add(fileNameFromPath);
                }
            }

            if (!existing.Contains(desired))
            {
                return desired;
            }

            var desiredExt = Path.GetExtension(desired);
            var desiredBaseName = Path.GetFileNameWithoutExtension(desired);

            if (string.IsNullOrEmpty(desiredExt))
            {
                desiredBaseName = desired;
            }

            for (var i = 1; i < 10000; i++)
            {
                var candidate = $"{desiredBaseName}-{i}{desiredExt}";
                if (!existing.Contains(candidate))
                {
                    return candidate;
                }
            }

            return desired;
        }

        private static string EncodeHash(string path) =>
            ElFinderHashEncoder.Encode(NormalizePath(path));

        private static string DecodeHash(string hash) =>
            ElFinderHashEncoder.Decode(hash) is string decoded ? NormalizePath(decoded) : null;

        private static string NormalizePath(string path)
        {
            var normalized = FileEntryPathHelper.NormalizePath(path);
            return normalized == "/" ? RootPath : normalized;
        }

        private static string GetParentPath(string path)
        {
            var trimmed = NormalizePath(path);
            var idx = trimmed.LastIndexOf('/');
            if (idx <= 0)
            {
                return "/";
            }

            return trimmed.Substring(0, idx);
        }

        private static string GetParentHash(string path)
        {
            var parent = GetParentPath(path);
            if (parent == "/" || string.IsNullOrEmpty(parent))
            {
                return null;
            }

            return EncodeHash(parent);
        }

        private static bool IsAllowedPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return FileEntryPathHelper.IsPathWithinRoot(path, RootPath);
        }

        private static bool IsSafeName(string name)
        {
            return !name.Contains('/') && !name.Contains('\\') && !name.Contains("..") && !name.Contains('\0');
        }

        private static string NormalizeElFinderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var normalized = name.Trim().ToLowerInvariant().Replace(' ', '-');

            // Collapse repeated dashes to keep generated names readable.
            while (normalized.Contains("--", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
            }

            return normalized;
        }

        private object ToElFinderObject(FileManagerEntry entry, string parentHash)
        {
            var fullPath = NormalizePath(entry.Path.StartsWith("/") ? entry.Path : "/" + entry.Path);
            var hash = EncodeHash(fullPath);
            var displayName = GetDisplayName(entry);
            var mime = entry.IsDirectory ? "directory" : GetMimeType(entry.Extension);
            var ts = new DateTimeOffset(entry.ModifiedUtc == default ? DateTime.UtcNow : entry.ModifiedUtc, TimeSpan.Zero)
                         .ToUnixTimeSeconds();
            var isRoot = fullPath == RootPath;
            var displayPath = NormalizePath(!string.IsNullOrWhiteSpace(entry.DisplayPath) ? entry.DisplayPath : fullPath);

            var obj = new Dictionary<string, object>
            {
                ["hash"] = hash,
                ["name"] = displayName,
                ["size"] = entry.IsDirectory ? 0L : entry.Size,
                ["mime"] = mime,
                ["ts"] = ts,
                ["read"] = 1,
                ["write"] = 1,
                ["locked"] = 0,
                ["realPath"] = fullPath,
                ["displayPath"] = displayPath,
            };

            if (entry.IsDirectory && entry.HasDirectories)
            {
                obj["dirs"] = 1;
            }

            if (isRoot)
            {
                // Root volume node: isroot and an empty phash are required by the
                // elFinder protocol so the JS client anchors the node correctly.
                obj["isroot"] = 1;
                obj["phash"] = string.Empty;
            }
            else if (parentHash != null)
            {
                obj["phash"] = parentHash;
            }

            if (entry.IsDirectory)
            {
                obj["volumeid"] = VolumeId;
            }

            if (isRoot)
            {
                obj["dirs"] = 1;
            }

            if (!entry.IsDirectory)
            {
                var blobBase = editorSettings.BlobPublicUrl.TrimEnd('/');
                obj["url"] = $"{blobBase}/{fullPath.TrimStart("/")}";

                var ext = (entry.Extension ?? string.Empty).ToLowerInvariant();
                if (FileStorageConstants.ValidImageExtensions.Contains(ext))
                {
                    obj["tmb"] = $"{Uri.EscapeDataString(fullPath)}&width=80&height=80";
                }
            }

            return obj;
        }

        private object SyntheticDirObject(string path, string parentHash, bool isRoot)
        {
            path = NormalizePath(path);
            var hash = EncodeHash(path);
            var name = isRoot ? "pub" : path.TrimEnd('/').Split('/').Last();
            var obj = new Dictionary<string, object>
            {
                ["hash"] = hash,
                ["name"] = name,
                ["size"] = 0L,
                ["mime"] = "directory",
                ["ts"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["read"] = 1,
                ["write"] = 1,
                ["locked"] = 0,
                ["dirs"] = 1,
                ["realPath"] = path,
                ["displayPath"] = path,
            };

            obj["volumeid"] = VolumeId;
            if (isRoot)
            {
                // Root volume node: isroot and an empty phash are required by the
                // elFinder protocol so the JS client anchors the node correctly.
                obj["isroot"] = 1;
                obj["phash"] = string.Empty;
            }
            else if (parentHash != null)
            {
                obj["phash"] = parentHash;
            }

            return obj;
        }

        private static FileManagerEntry BuildSyntheticFileEntry(string path, string nameWithoutExt, string ext, long size, bool isDir = false)
        {
            return new FileManagerEntry
            {
                Path = path.StartsWith("/") ? path : "/" + path,
                Name = nameWithoutExt,
                Extension = ext ?? string.Empty,
                Size = size,
                IsDirectory = isDir,
                HasDirectories = false,
                Created = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow,
            };
        }

        private Dictionary<string, object> BuildOptions(string path, string displayPath = null)
        {
            var blobBase = editorSettings.BlobPublicUrl.TrimEnd('/');
            var humanPath = !string.IsNullOrEmpty(displayPath)
                ? displayPath.TrimStart('/')
                : NormalizePath(path).TrimStart('/');
            var canonicalPath = NormalizePath(path).TrimStart('/');

            return new Dictionary<string, object>
            {
                ["path"] = humanPath,
                ["url"] = $"{blobBase}/{canonicalPath.TrimEnd('/')}/",
                ["tmbUrl"] = "/FileManager/GetImageThumbnail?target=",
                ["separator"] = "/",
                ["copyOverwrite"] = 1,
                ["uploadOverwrite"] = 1,
                ["archivers"] = new { create = Array.Empty<string>(), extract = Array.Empty<string>() },
                ["disabled"] = new[] { "chmod", "zipdl", "archive", "extract" },
                ["uploadMaxConn"] = 3,
            };
        }

        private static object ElFinderError(string message)
        {
            return new { error = message };
        }

        private static string GetMimeType(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return "application/octet-stream";
            }

            try
            {
                return MimeTypeMap.GetMimeType(extension);
            }
            catch
            {
                return "application/octet-stream";
            }
        }

        private string GetParam(string key)
        {
            if (Request.Method == "POST" && Request.HasFormContentType)
            {
                var formVal = Request.Form[key].ToString();
                if (!string.IsNullOrEmpty(formVal))
                {
                    return formVal;
                }
            }

            return Request.Query[key].ToString();
        }

        private string[] GetParams(string key)
        {
            if (Request.Method == "POST" && Request.HasFormContentType)
            {
                var vals = Request.Form[key];
                if (vals.Count > 0)
                {
                    return vals.ToArray();
                }
            }

            return Request.Query[key].ToArray();
        }

        private async Task<List<FileManagerEntry>> GetEntriesWithFriendlyTitlesAsync(string parentPath)
        {
            var items = await storageContext.GetFilesAndDirectories(parentPath);
            var normalizedParent = NormalizePath(parentPath);

            // Only get friendly names for folders and files that are children of the /pub/articles folder.
            if (!normalizedParent.StartsWith("/pub/articles", StringComparison.OrdinalIgnoreCase) && normalizedParent.Split('/').Length < 3)
            {
                // Don't bother with further processing.
                return items;
            }

            /*
             * If we reach here, it means the path is a child of /pub/articles.
             * 
             *  IMPORTANT !!!!!
             *  All child entried be they directories or files must have their friendly titles resolved.
             *  This is because article entries can be either folders or files, and we want to ensure that
             *  all of them have friendly titles if they are article entries.
            */

            var titleResolver = new FileEntryTitleService(dbContext, memoryCache, configProvider);
            var tenantDomain = this.configProvider?.GetTenantDomainNameFromRequest() ?? string.Empty;
            await titleResolver.FilterDeletedArticleEntriesAsync(items, tenantDomain);
            var articleTitlesByNumber = await titleResolver.GetArticleTitlesByNumberAsync(items, tenantDomain);
            foreach (var item in items)
            {
                FileEntryPathHelper.TryGetArticleNumber(item, out var articleNumber);
                articleTitlesByNumber.TryGetValue(articleNumber, out var articleTitle);

                // Get the "friendly" display path for the entry, which will be used by the UI to display the entry path.
                if (item.IsDirectory && item.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).Length == 3)
                {
                    // This is a top-level article folder under /pub/articles/{integer}/ (note trailing backslash).
                    // we should use the article title as the display name if possible.
                    item.Title = articleTitle;
                }

                item.DisplayPath = FileEntryPathHelper.ResolveFriendlyDisplayPath(item.Path, articleNumber, articleTitle);
            }

            return items;
        }

        private async Task ApplyFriendlyTitleAsync(FileManagerEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            var normalizedPath = NormalizePath(entry.Path);
            if (!FileEntryPathHelper.TryGetArticleNumberFromPath(normalizedPath, out var articleNumber))
            {
                return;
            }

            var titleResolver = new FileEntryTitleService(dbContext, memoryCache, configProvider);
            var tenantDomain = this.configProvider?.GetTenantDomainNameFromRequest() ?? string.Empty;
            var titles = await titleResolver.GetArticleTitlesByNumberAsync(new[] { articleNumber }, tenantDomain);
            if (titles.TryGetValue(articleNumber, out var articleTitle) && !string.IsNullOrWhiteSpace(articleTitle))
            {
                entry.Title = articleTitle;
                entry.DisplayPath = FileEntryPathHelper.ResolveFriendlyDisplayPath(normalizedPath, articleNumber, articleTitle);
            }
            else
            {
                entry.DisplayPath = normalizedPath;
            }
        }

        private static string GetDisplayName(FileManagerEntry entry)
        {
            if (entry.IsDirectory)
            {
                return !string.IsNullOrWhiteSpace(entry.Title) ? entry.Title : (entry.Name ?? string.Empty);
            }

            var name = entry.Name ?? string.Empty;
            var ext = entry.Extension ?? string.Empty;

            if (!string.IsNullOrEmpty(ext) && !ext.StartsWith(".", StringComparison.Ordinal))
            {
                ext = "." + ext;
            }

            if (string.IsNullOrEmpty(ext))
            {
                return name;
            }

            return name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
                ? name
                : name + ext;
        }

        private async Task<IActionResult> DenyDeletedArticlePathForCqrsAsync(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var normalizedPath = NormalizePath(path);
            var tenantDomain = this.configProvider?.GetTenantDomainNameFromRequest() ?? string.Empty;
            var titleResolver = new FileEntryTitleService(this.dbContext, this.memoryCache, this.configProvider);
            if (!await titleResolver.IsArticlePathDeletedAsync(normalizedPath, tenantDomain))
            {
                return null;
            }

            return Json(ElFinderError("errAccess"));
        }

        private async Task<IActionResult> DenyDeletedArticleHashesForCqrsAsync(IEnumerable<string> hashes)
        {
            if (hashes == null)
            {
                return null;
            }

            foreach (var hash in hashes)
            {
                var path = DecodeHash(hash);
                var blocked = await DenyDeletedArticlePathForCqrsAsync(path);
                if (blocked != null)
                {
                    return blocked;
                }
            }

            return null;
        }

        /// <summary>
        /// File manager index page.
        /// </summary>
        /// <param name="target">Path to folder.</param>
        /// <param name="selectOne">Select one item triggered in UI.</param>
        /// <param name="sortOrder">Sort order.</param>
        /// <param name="currentSort">Current or selected sort.</param>
        /// <param name="pageNo">Page number to get.</param>
        /// <param name="isNewSession">s a new session.</param>
        /// <param name="directoryOnly">Only return directories.</param>
        /// <param name="imagesOnly">Show only images.</param>
        /// <param name="isNewSession">s a new session.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpGet]
        public async Task<IActionResult> Index(string? target, bool? selectOne, string sortOrder = "asc", string currentSort = "Name", int pageNo = 0, int pageSize = 10, bool directoryOnly = false, bool imagesOnly = false, bool isNewSession = false)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (selectOne == null)
            {
                selectOne = false;
            }

            if (string.IsNullOrEmpty(target) || target == "/")
            {
                return RedirectToAction("Index", new { target = "/pub" });
            }

            target = string.IsNullOrEmpty(target) ? string.Empty : HttpUtility.UrlDecode(target);

            ViewData["PathPrefix"] = target.StartsWith('/') ? target : "/" + target;

            var articleTitle = string.Empty;

            if (target.Trim('/').StartsWith("pub/articles"))
            {
                if (FileEntryPathHelper.TryGetArticleNumber(target, out var articleNumber))
                {
                    var article = await dbContext.ArticleCatalog
                        .Select(s => new { s.ArticleNumber, s.Title })
                        .FirstOrDefaultAsync(f => f.ArticleNumber == articleNumber);
                    if (article != null)
                    {
                        articleTitle = article.Title;
                    }
                }
            }

            if (target.Trim('/').StartsWith("pub/templates"))
            {
                if (FileEntryPathHelper.TryGetTemplateId(target, out var templateId))
                {
                    var template = await dbContext.Templates
                        .Select(s => new { s.Id, s.Title })
                        .FirstOrDefaultAsync(f => f.Id == templateId);
                    if (template != null)
                    {
                        articleTitle = template.Title;
                    }
                }
            }

            ViewData["ArticleTitle"] = articleTitle;
            ViewData["DirectoryOnly"] = directoryOnly;
            ViewData["Title"] = "Website File Manager";
            ViewData["StorageName"] = "Public File Storage";
            ViewData["TopDirectory"] = "/pub";
            ViewData["Controller"] = "FileManager";
            ViewData["Action"] = "Index";
            ViewData["SelectOne"] = selectOne;
            ViewData["ImagesOnly"] = imagesOnly;
            ViewData["isNewSession"] = isNewSession;

            // Grid pagination
            ViewData["sortOrder"] = sortOrder;
            ViewData["currentSort"] = currentSort;
            ViewData["pageNo"] = pageNo;
            ViewData["pageSize"] = pageSize;

            // GET FULL OR ABSOLUTE PATH ï¿½ delegated to the shared FolderListingService.
            var tenantDomain = this.configProvider.GetTenantDomainNameFromRequest();
            var entries = await this.folderListingService.GetEntriesAsync(target, tenantDomain);
            IQueryable<FileManagerEntry> query = entries.AsQueryable();

            if (imagesOnly)
            {
                query = query.Where(w => w.IsDirectory || ValidImageExtensions.Contains(w.Extension.ToLower()));
            }

            ViewData["RowCount"] = query.Count();

            if (string.IsNullOrEmpty(sortOrder))
            {
                // Default sort order
                query = query.OrderByDescending(o => o.Name);
            }

            if (sortOrder == "desc")
            {
                if (!string.IsNullOrEmpty(currentSort))
                {
                    switch (currentSort)
                    {
                        case "Name":
                            query = query.OrderByDescending(o => o.Name);
                            break;
                        case "IsDirectory":
                            query = query.OrderByDescending(o => o.IsDirectory);
                            break;
                        case "CreatedUtc":
                            query = query.OrderByDescending(o => o.CreatedUtc);
                            break;
                        case "Extension":
                            query = query.OrderByDescending(o => o.Extension);
                            break;
                        case "ModifiedUtc":
                            query = query.OrderByDescending(o => o.ModifiedUtc);
                            break;
                        case "Path":
                            query = query.OrderByDescending(o => o.Path);
                            break;
                        case "Size":
                            query = query.OrderByDescending(o => o.Size);
                            break;
                    }
                }
            }
            else if (sortOrder == "asc")
            {
                if (!string.IsNullOrEmpty(currentSort))
                {
                    switch (currentSort)
                    {
                        case "Name":
                            query = query.OrderBy(o => o.Name);
                            break;
                        case "IsDirectory":
                            query = query.OrderBy(o => o.IsDirectory);
                            break;
                        case "CreatedUtc":
                            query = query.OrderBy(o => o.CreatedUtc);
                            break;
                        case "Extension":
                            query = query.OrderBy(o => o.Extension);
                            break;
                        case "ModifiedUtc":
                            query = query.OrderBy(o => o.ModifiedUtc);
                            break;
                        case "Path":
                            query = query.OrderBy(o => o.Path);
                            break;
                        case "Size":
                            query = query.OrderBy(o => o.Size);
                            break;
                    }
                }
            }

            if (directoryOnly)
            {
                var ddata = query.Where(w => w.IsDirectory).ToList();
                return View("~/Views/Shared/FileExplorer/Index.cshtml", ddata);
            }

            var data = query.Skip(pageNo * pageSize).Take(pageSize).ToList();
            return View("~/Views/Shared/FileExplorer/Index.cshtml", data);
        }

        /// <summary>
        /// Gets images for the design editor.
        /// </summary>
        /// <param name="path">Path to folder to search.</param>
        /// <param name="exclude">Excluded paths.</param>
        /// <returns>JSON data.</returns>
        [HttpGet]
        public async Task<IActionResult> GetImageAssets(string path, string exclude = "")
        {
            return Json(await GetImageAssetArray(storageContext, path, exclude));
        }

        /// <summary>
        /// Gets a unique GUID for FilePond.
        /// </summary>
        /// <param name="files">Files being uploaded.</param>
        /// <returns>Returns an IActionResult.</returns>
        [HttpPost]
        public ActionResult Process([FromForm] string files)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var parsed = JsonConvert.DeserializeObject<FilePondMetadata>(files);

            var mime = MimeTypeMap.GetMimeType(Path.GetExtension(parsed.FileName));

            var uid = $"{parsed.Path.TrimEnd('/')}|{parsed.RelativePath.TrimStart('/')}|{Guid.NewGuid().ToString()}|{mime}|{parsed.ImageWidth}|{parsed.ImageHeight}";

            return Ok(uid);
        }

        /// <summary>
        /// This is used by filepond to upload a single image.
        /// </summary>
        /// <param name="files">File metadata.</param>
        /// <returns>IActionResult.</returns>
        [HttpPost]
        public async Task<IActionResult> UploadImage([FromForm] string files)
        {
            var parsed = JsonConvert.DeserializeObject<FilePondMetadata>(files);
            var mime = MimeTypeMap.GetMimeType(Path.GetExtension(parsed.FileName));

            // Gets the file being uploaded.
            var file = Request.Form.Files.FirstOrDefault();

            if (file.Length > (1048576 * 25))
            {
                return Json(ReturnSimpleErrorMessage("The image upload failed because the image was too big (max 25MB)."));
            }

            var extension = Path.GetExtension(file.FileName).ToLower();

            // Validate that the file is an image based on MIME type
            if (!mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return Json(ReturnSimpleErrorMessage($"The file '{file.FileName}' is not a valid image file."));
            }

            var blobEndPoint = this.editorSettings.BlobPublicUrl.TrimEnd('/');
            var directory = parsed.Path.TrimEnd('/');
            var fileName = file.FileName.ToLower();

            Image image;
            try
            {
                image = await Image.LoadAsync(file.OpenReadStream());
            }
            catch (UnknownImageFormatException)
            {
                return Json(ReturnSimpleErrorMessage($"The file '{file.FileName}' could not be loaded as an image."));
            }

            string relativePath = FileEntryPathHelper.UrlEncodePath($"{directory}/{fileName}");

            var contentType = MimeTypeMap.GetMimeType(extension);

            var metaData = new FileUploadMetaData()
            {
                ChunkIndex = 0,
                ContentType = contentType,
                FileName = fileName,
                RelativePath = relativePath,
                TotalChunks = 1,
                TotalFileSize = file.Length,
                UploadUid = Guid.NewGuid().ToString(),
                ImageHeight = image.Height.ToString(),
                ImageWidth = image.Width.ToString(),
            };

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);

            await storageContext.AppendBlob(memoryStream, metaData);

            return Content(blobEndPoint + "/" + relativePath);
        }

        /// <summary>
        /// Processes a chunked FilePond upload.
        /// Supports both query-style transfer ids (?patch=...) and
        /// path-style transfer ids (/FileManager/Process/{transferId}).
        /// </summary>
        /// <param name="patch">Transfer id supplied via query string.</param>
        /// <param name="options">Upload options.</param>
        /// <param name="patchRoute">Transfer id supplied via path segment.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPatch("FileManager/Process/{*patchRoute}")]
        public async Task<ActionResult> Process(string patch = "", string options = "", string patchRoute = "")
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var transferId = !string.IsNullOrWhiteSpace(patch) ? patch : patchRoute;
            if (string.IsNullOrWhiteSpace(transferId))
            {
                return BadRequest("Missing transfer id.");
            }

            var patchArray = transferId.Split('|');
            if (patchArray.Length < 6)
            {
                return BadRequest("Invalid transfer id format.");
            }

            // 0 based index
            var uploadOffset = long.Parse(Request.Headers["Upload-Offset"]);

            // File name being uploaded
            var uploadName = (string)Request.Headers["Upload-Name"];

            // Total size of the file in bytes
            var uploadLenth = long.Parse(Request.Headers["Upload-Length"]);

            // Size of the chunk
            var contentSize = long.Parse(Request.Headers["Content-Length"]);

            long chunk = 0;

            if (uploadOffset > 0)
            {
                chunk = DivideByAndRoundUp(uploadLenth, uploadOffset);
            }

            var totalChunks = DivideByAndRoundUp(uploadLenth, contentSize);

            var blobName = FileEntryPathHelper.UrlEncodePath(uploadName);

            var relativePath = FileEntryPathHelper.UrlEncodePath(patchArray[0].TrimEnd('/'));

            if (!string.IsNullOrEmpty(patchArray[1]))
            {
                var dpath = Path.GetDirectoryName(patchArray[1]).Replace('\\', '/'); // Convert windows paths to unix style.
                var epath = FileEntryPathHelper.UrlEncodePath(dpath);
                relativePath += "/" + FileEntryPathHelper.UrlEncodePath(epath);
            }

            var extension = Path.GetExtension(blobName).ToLower();

            // Mime type
            var contentType = MimeTypeMap.GetMimeType(extension);

            var metaData = new FileUploadMetaData()
            {
                ChunkIndex = chunk,
                ContentType = contentType,
                FileName = blobName,
                RelativePath = relativePath + "/" + blobName,
                TotalChunks = totalChunks,
                TotalFileSize = uploadLenth,
                UploadUid = patchArray[2],
                ImageWidth = patchArray[4],
                ImageHeight = patchArray[5],
            };

            // Make sure full folder path exists
            var pathParts = patchArray[0].Trim('/').Split('/');
            var part = string.Empty;

            for (int i = 0; i < pathParts.Length - 1; i++)
            {
                if (i == 0 && pathParts[i] != "pub")
                {
                    throw new ArgumentException("Must upload folders and files under /pub directory.");
                }

                part = $"{part}/{pathParts[i]}";
                if (part != "/pub")
                {
                    var folder = part.Trim('/');
                    await fileOperations.CreateFolderAsync(folder);
                }
            }

            using var memoryStream = new MemoryStream();
            await Request.Body.CopyToAsync(memoryStream);
            await storageContext.AppendBlob(memoryStream, metaData);

            if (metaData.TotalChunks - 1 == metaData.ChunkIndex)
            {
                await PurgeCdnPath(metaData);
            }

            return Ok();
        }

        /// <summary>
        /// Reverts (deletes) an already-uploaded file. Called by FilePond when the user
        /// clicks the undo button after a completed upload.
        /// </summary>
        /// <param name="fileName">The original file name, supplied as a query parameter by the FilePond revert function.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpDelete]
        [ActionName("Process")]
        public IActionResult ProcessRevert([FromQuery] string? fileName = null)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return BadRequest("fileName is required.");
            }

            // FilePond sends the server UID as the request body:
            // {path}|{relativePath}|{guid}|{mime}|{imageWidth}|{imageHeight}
            string uid;
            using (var reader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                uid = reader.ReadToEnd();
            }

            if (string.IsNullOrWhiteSpace(uid))
            {
                return BadRequest("Missing file id in request body.");
            }

            var parts = uid.Split('|');
            if (parts.Length < 2)
            {
                return BadRequest("Invalid file id format.");
            }

            // Reconstruct blob path the same way the PATCH action does.
            var basePath = FileEntryPathHelper.UrlEncodePath(parts[0].TrimEnd('/'));
            var subDir = parts.Length > 1 ? parts[1].TrimStart('/') : string.Empty;

            string blobPath;
            if (!string.IsNullOrEmpty(subDir))
            {
                var dpath = Path.GetDirectoryName(subDir)?.Replace('\\', '/') ?? string.Empty;
                if (!string.IsNullOrEmpty(dpath))
                {
                    blobPath = $"{basePath}/{FileEntryPathHelper.UrlEncodePath(dpath)}/{FileEntryPathHelper.UrlEncodePath(fileName)}";
                }
                else
                {
                    blobPath = $"{basePath}/{FileEntryPathHelper.UrlEncodePath(fileName)}";
                }
            }
            else
            {
                blobPath = $"{basePath}/{FileEntryPathHelper.UrlEncodePath(fileName)}";
            }

            storageContext.DeleteFile(blobPath);
            return Ok();
        }

        /// <summary>
        /// Simple file upload for live editor.
        /// </summary>
        /// <param name="id">Article or template id.</param>
        /// <param name="entityType">Where to upload the item (eg articles or templates).</param>
        /// <param name="editorType">Either ckeditor or grapesjs.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> SimpleUpload(string id, string entityType = "articles", string editorType = "ckeditor")
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Gets the file being uploaded.
            var file = Request.Form.Files.FirstOrDefault();

            if (file.Length > (1048576 * 25))
            {
                return Json(ReturnSimpleErrorMessage("The image upload failed because the image was too big (max 25MB)."));
            }

            var extension = Path.GetExtension(file.FileName).ToLower();
            var directory = $"/pub/{entityType}/{id}/";
            var blobEndPoint = this.editorSettings.BlobPublicUrl.TrimEnd('/');
            var fileName = $"{Guid.NewGuid().ToString().ToLower()}{extension}";

            var image = await Image.LoadAsync(file.OpenReadStream());

            string relativePath = FileEntryPathHelper.UrlEncodePath(directory + fileName);

            var contentType = MimeTypeMap.GetMimeType(Path.GetExtension(fileName));

            try
            {
                var metaData = new FileUploadMetaData()
                {
                    ChunkIndex = 0,
                    ContentType = contentType,
                    FileName = fileName,
                    RelativePath = relativePath,
                    TotalChunks = 1,
                    TotalFileSize = file.Length,
                    UploadUid = Guid.NewGuid().ToString(),
                    ImageHeight = image.Height.ToString(),
                    ImageWidth = image.Width.ToString(),
                };

                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);

                await storageContext.AppendBlob(memoryStream, metaData);

                try
                {
                    await PurgeCdnPath(metaData);
                }
                catch
                {
                    // Nothing to do.
                }

                if (editorType == "grapesjs")
                {
                    return Json(JsonConvert.DeserializeObject<dynamic>("{ data: [ \"" + blobEndPoint + "/" + relativePath + "\"] }"));
                }

                return Json(JsonConvert.DeserializeObject<dynamic>("{\"url\": \"" + blobEndPoint + "/" + relativePath + "\"}"));
            }
            catch (Exception e)
            {
                logger.LogError(e.Message, e);
                return Json(ReturnSimpleErrorMessage(e.Message));
            }
        }

        /// <summary>
        /// Imports a page.
        /// </summary>
        /// <param name="id">Page ID number.</param>
        /// <returns>IActionResult.</returns>
        [Authorize(Roles = "Administrators, Editors, Authors, Team Members")]
        public IActionResult ImportPage(int? id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id.HasValue)
            {
                ViewData["ArticleId"] = id.Value;
                return View();
            }

            return NotFound();
        }

        /// <summary>
        /// Import a view.
        /// </summary>
        /// <param name="files">Files.</param>
        /// <param name="metaData">Metadata.</param>
        /// <param name="id">Article ID.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        [Authorize(Roles = "Administrators, Editors, Authors, Team Members")]
        public async Task<IActionResult> ImportPage(
            IEnumerable<IFormFile> files,
            string metaData,
            string id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (files == null || !files.Any() || !Guid.TryParse(id, out Guid articleId))
            {
                return null;
            }

            if (string.IsNullOrEmpty(metaData))
            {
                return Unauthorized("metaData cannot be null or empty.");
            }

            // Get information about the chunk we are on.
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(metaData));

            var serializer = new Newtonsoft.Json.JsonSerializer();
            FileUploadMetaData fileMetaData;
            using (var streamReader = new StreamReader(ms))
            {
                fileMetaData =
                    (FileUploadMetaData)serializer.Deserialize(streamReader, typeof(FileUploadMetaData));
            }

            if (fileMetaData == null)
            {
                throw new Exception("Could not read the file's metadata");
            }

            // Validate against path traversal in metadata
            if (fileMetaData.RelativePath?.Contains("..") == true || fileMetaData.FileName?.Contains("..") == true)
            {
                return Unauthorized("Path traversal attempts are not allowed.");
            }

            var uploadResult = new PageImportResult
            {
                Uploaded = fileMetaData.TotalChunks - 1 <= fileMetaData.ChunkIndex,
                FileUid = fileMetaData.UploadUid
            };

            try
            {
                if (ModelState.IsValid)
                {
                    var article = await articleQueries.QueryAsync(new GetArticleByIdQuery
                    {
                        Id = articleId
                    });

                    var originalHtml = await articleLogic.ExportArticle(article, viewRenderService);
                    var originalHtmlDoc = new HtmlAgilityPack.HtmlDocument();
                    originalHtmlDoc.LoadHtml(originalHtml);

                    var file = files.FirstOrDefault();
                    using var memstream = new MemoryStream();
                    await file.CopyToAsync(memstream);
                    var html = Encoding.UTF8.GetString(memstream.ToArray());

                    // Load the HTML document.
                    var newHtmlDoc = new HtmlAgilityPack.HtmlDocument();
                    newHtmlDoc.LoadHtml(html);

                    var originalHeadNode = originalHtmlDoc.DocumentNode.SelectSingleNode("//head");
                    var originalBodyNode = originalHtmlDoc.DocumentNode.SelectSingleNode("//body");

                    var layoutHeadNodes =
                        SelectNodesBetweenComments(originalHeadNode, PageImportConstants.COSMOSHEADSTART, PageImportConstants.COSMOSHEADEND);
                    var layoutHeadScriptsNodes =
                        SelectNodesBetweenComments(originalHeadNode, PageImportConstants.COSMOSHEADSCRIPTSSTART, PageImportConstants.COSMOSHEADSCRIPTSEND);
                    var layoutBodyHeaderNodes =
                        SelectNodesBetweenComments(originalBodyNode, PageImportConstants.COSMOSBODYHEADERSTART, PageImportConstants.COSMOSBODYHEADEREND);
                    var layoutBodyFooterNodes =
                        SelectNodesBetweenComments(originalBodyNode, PageImportConstants.COSMOSBODYFOOTERSTART, PageImportConstants.COSMOSBODYFOOTEREND);
                    var layoutBodyGoogleTranslateNodes =
                        SelectNodesBetweenComments(originalBodyNode, PageImportConstants.COSMOSGOOGLETRANSLATESTART, PageImportConstants.COSMOSGOOGLETRANSLATEEND);
                    var layoutBodyEndScriptsNodes =
                        SelectNodesBetweenComments(originalBodyNode, PageImportConstants.COSMOSBODYENDSCRIPTSSTART, PageImportConstants.COSMOSBODYENDSCRIPTSEND);

                    // NOTES
                    // https://stackoverflow.com/questions/3844208/html-agility-pack-find-comment-node?msclkid=b885cfabc88011ecbf75531a66703f70
                    // https://html-agility-pack.net/knowledge-base/7275301/htmlagilitypack-select-nodes-between-comments?msclkid=b88685c7c88011ecbe703bfac7781d3c
                    var newHeadNode = newHtmlDoc.DocumentNode.SelectSingleNode("//head");
                    var newBodyNode = newHtmlDoc.DocumentNode.SelectSingleNode("//body");

                    // Now remove layout elements for the HEAD node
                    RemoveNodes(ref newHeadNode, layoutHeadNodes);
                    RemoveNodes(ref newHeadNode, layoutHeadScriptsNodes);

                    // Now remove layout elements for the BODY - Except layout footer
                    RemoveNodes(ref newBodyNode, layoutBodyHeaderNodes);
                    RemoveNodes(ref newBodyNode, layoutBodyGoogleTranslateNodes);
                    RemoveNodes(ref newBodyNode, layoutBodyEndScriptsNodes);

                    // Now capture nodes above and below footer within body
                    var exclude = new[] { HtmlAgilityPack.HtmlNodeType.Comment, HtmlAgilityPack.HtmlNodeType.Text };

                    var footerStartIndex = GetChildNodeIndex(newBodyNode, layoutBodyFooterNodes.FirstOrDefault(f => !exclude.Contains(f.NodeType)));
                    var footerEndIndex = GetChildNodeIndex(newBodyNode, layoutBodyFooterNodes.LastOrDefault(f => !exclude.Contains(f.NodeType)));

                    // Clean up the head inject
                    var headHtml = new StringBuilder();
                    foreach (var node in newHeadNode.ChildNodes)
                    {
                        if (node.NodeType != HtmlAgilityPack.HtmlNodeType.Comment &&
                           node.NodeType != HtmlAgilityPack.HtmlNodeType.Text)
                        {
                            headHtml.AppendLine(node.OuterHtml);
                        }
                    }

                    // Retrieve HTML above footer
                    var bodyHtmlAboveFooter = new StringBuilder();
                    for (int i = 0; i < footerStartIndex; i++)
                    {
                        if (newBodyNode.ChildNodes[i].NodeType != HtmlAgilityPack.HtmlNodeType.Comment &&
                            newBodyNode.ChildNodes[i].NodeType != HtmlAgilityPack.HtmlNodeType.Text)
                        {
                            bodyHtmlAboveFooter.AppendLine(newBodyNode.ChildNodes[i].OuterHtml);
                        }
                    }

                    // Retrieve HTML below footer
                    var bodyHtmlBelowFooter = new StringBuilder();
                    for (int i = footerEndIndex + 1; i < newBodyNode.ChildNodes.Count; i++)
                    {
                        if (newBodyNode.ChildNodes[i].NodeType != HtmlAgilityPack.HtmlNodeType.Comment &&
                               newBodyNode.ChildNodes[i].NodeType != HtmlAgilityPack.HtmlNodeType.Text)
                        {
                            bodyHtmlBelowFooter.AppendLine(newBodyNode.ChildNodes[i].OuterHtml);
                        }
                    }

                    var trims = new char[] { ' ', '\n', '\r' };

                    article.HeadJavaScript = headHtml.ToString().Trim(trims);
                    article.Content = bodyHtmlAboveFooter.ToString().Trim(trims);
                    article.FooterJavaScript = bodyHtmlBelowFooter.ToString().Trim(trims);

                    // Get the user's ID for logging.
                    var user = await userManager.GetUserAsync(User);

                    // Use SaveArticleCommand via mediator
                    var command = new SaveArticleCommand
                    {
                        ArticleNumber = article.ArticleNumber,
                        Title = article.Title,
                        Content = article.Content,
                        HeadJavaScript = article.HeadJavaScript,
                        FooterJavaScript = article.FooterJavaScript,
                        BannerImage = article.BannerImage,
                        UserId = Guid.Parse(user.Id),
                        ArticleType = (Cosmos.Cms.Common.ArticleType)article.ArticleType,
                        Category = article.Category,
                        Introduction = article.Introduction,
                        Published = article.Published,
                        UrlPath = article.UrlPath
                    };
                    var result = await articleQueries.SendAsync(command);
                    if (!result.IsSuccess)
                    {
                        uploadResult.Errors = result.ErrorMessage;
                    }
                }
                else
                {
                    uploadResult.Errors = SerializeErrors(ModelState);
                }
            }
            catch (Exception e)
            {
                ModelState.AddModelError("file", e.Message);
                logger.LogError(e, "Web page import failed.");
                uploadResult.Errors = SerializeErrors(ModelState);
            }

            return Json(uploadResult);
        }

        /// <summary>
        /// Download a file.
        /// </summary>
        /// <param name="path">Path to the file to retrieve.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> Download(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Check if blob exists before attempting to get metadata
                if (!await storageContext.BlobExistsAsync(path))
                {
                    return NotFound();
                }

                var blob = await fileOperations.GetFileAsync(path);

                if (blob == null)
                {
                    return NotFound();
                }

                if (blob.IsDirectory)
                {
                    return NotFound();
                }

                using var stream = await storageContext.GetStreamAsync(path);
                using var memStream = new MemoryStream();
                await stream.CopyToAsync(memStream);
                return File(memStream.ToArray(), "application/octet-stream", fileDownloadName: blob.Name);
            }
            catch (Cosmos.BlobService.Exceptions.StorageException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Edit code for a file.
        /// </summary>
        /// <param name="path">URL or path to code.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IActionResult> EditCode(string path)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var extension = Path.GetExtension(path.ToLower());

                var filter = this.editorSettings.AllowedFileTypes.Split(',');
                var editorField = new EditorField
                {
                    FieldId = "Content",
                    FieldName = Path.GetFileName(path)
                };

                if (!filter.Contains(extension))
                {
                    return new UnsupportedMediaTypeResult();
                }

                switch (extension)
                {
                    case ".js":
                        editorField.EditorMode = EditorMode.JavaScript;
                        editorField.IconUrl = "/images/seti-ui/icons/javascript.svg";
                        break;
                    case ".html":
                        editorField.EditorMode = EditorMode.Html;
                        editorField.IconUrl = "/images/seti-ui/icons/html.svg";
                        break;
                    case ".css":
                        editorField.EditorMode = EditorMode.Css;
                        editorField.IconUrl = "/images/seti-ui/icons/css.svg";
                        break;
                    case ".xml":
                        editorField.EditorMode = EditorMode.Xml;
                        editorField.IconUrl = "/images/seti-ui/icons/javascript.svg";
                        break;
                    case ".json":
                        editorField.EditorMode = EditorMode.Json;
                        editorField.IconUrl = "/images/seti-ui/icons/javascript.svg";
                        break;
                    default:
                        editorField.EditorMode = EditorMode.Html;
                        editorField.IconUrl = "/images/seti-ui/icons/html.svg";
                        break;
                }

                // Get the blob now, so we can determine the type, or use this client as-is
                //
                // var properties = blob.GetProperties();

                // Open a stream
                await using var memoryStream = new MemoryStream();

                await using (var stream = await storageContext.GetStreamAsync(path))
                {
                    // Load into memory and release the blob stream right away
                    await stream.CopyToAsync(memoryStream);
                }

                var metaData = await fileOperations.GetFileAsync(path);

                ViewData["PageTitle"] = metaData.Name;
                ViewData[" Published"] = DateTimeOffset.FromFileTime(metaData.ModifiedUtc.Ticks);

                return View(new FileManagerEditCodeViewModel
                {
                    Id = path,
                    Path = path,
                    EditorTitle = Path.GetFileName(Path.GetFileName(path)),
                    EditorFields = new List<EditorField>
                    {
                        editorField
                    },
                    Content = Encoding.UTF8.GetString(memoryStream.ToArray()),
                    EditingField = "Content",
                    CustomButtons = new List<string>()
                });
            }
            catch (Exception e)
            {
                logger.LogError(e.Message, e);
                throw;
            }
        }

        /// <summary>
        /// Save the file.
        /// </summary>
        /// <param name="model">Code post model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCode(FileManagerEditCodeViewModel model)
        {
            model.Content = CryptoJsDecryption.Decrypt(model.Content);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var extension = Path.GetExtension(model.Path.ToLower());

            var filter = this.editorSettings.AllowedFileTypes.Split(',');
            var editorField = new EditorField
            {
                FieldId = "Content",
                FieldName = Path.GetFileName(model.Path)
            };

            if (!filter.Contains(extension))
            {
                return new UnsupportedMediaTypeResult();
            }

            var contentType = string.Empty;

            switch (extension)
            {
                case ".js":
                    editorField.EditorMode = EditorMode.JavaScript;
                    editorField.IconUrl = "/images/seti-ui/icons/javascript.svg";
                    break;
                case ".html":
                    editorField.EditorMode = EditorMode.Html;
                    editorField.IconUrl = "/images/seti-ui/icons/html.svg";
                    break;
                case ".css":
                    editorField.EditorMode = EditorMode.Css;
                    editorField.IconUrl = "/images/seti-ui/icons/css.svg";
                    break;
                case ".xml":
                    editorField.EditorMode = EditorMode.Xml;
                    editorField.IconUrl = "/images/seti-ui/icons/javascript.svg";
                    break;
                case ".json":
                    editorField.EditorMode = EditorMode.Json;
                    editorField.IconUrl = "/images/seti-ui/icons/javascript.svg";
                    break;
                default:
                    editorField.EditorMode = EditorMode.Html;
                    editorField.IconUrl = "/images/seti-ui/icons/html.svg";
                    break;
            }

            // Save the blob now
            var bytes = Encoding.Default.GetBytes(model.Content);

            using var memoryStream = new MemoryStream(bytes, false);

            var formFile = new FormFile(memoryStream, 0, memoryStream.Length, Path.GetFileNameWithoutExtension(model.Path), Path.GetFileName(model.Path));

            var metaData = new FileUploadMetaData
            {
                ChunkIndex = 0,
                ContentType = contentType,
                FileName = Path.GetFileName(model.Path),
                RelativePath = Path.GetFileName(model.Path),
                TotalFileSize = memoryStream.Length,
                UploadUid = Guid.NewGuid().ToString(),
                TotalChunks = 1
            };

            var uploadPath = model.Path.TrimEnd(metaData.FileName.ToArray()).TrimEnd('/');

            var result = (JsonResult)await Upload(new IFormFile[] { formFile }, JsonConvert.SerializeObject(metaData), uploadPath);

            var resultMode = (FileUploadResult)result.Value;

            var jsonModel = new SaveCodeResultJsonModel
            {
                ErrorCount = ModelState.ErrorCount,
                IsValid = ModelState.IsValid
            };

            if (!resultMode.Uploaded)
            {
                ModelState.AddModelError(string.Empty, $"Error saving {Path.GetFileName(model.Path)}");
            }

            jsonModel.Errors.AddRange(ModelState.Values
                .Where(w => w.ValidationState == Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
                .ToList());
            jsonModel.ValidationState = ModelState.ValidationState;

            return Json(jsonModel);
        }

        /// <summary>
        /// Edit an image.
        /// </summary>
        /// <param name="target">Path to image.</param>
        /// <returns>IActionResult.</returns>
        public IActionResult EditImage(string target)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrEmpty(target))
            {
                return NotFound();
            }

            ViewData["ImageTarget"] = target;
            var extension = Path.GetExtension(target.ToLower());

            var filter = new[] { ".png", ".jpg", ".gif", ".jpeg", ".webp" };
            if (filter.Contains(extension))
            {
                return View();
            }

            return new UnsupportedMediaTypeResult();
        }

        /// <summary>
        /// Image editor post image back to storage.
        /// </summary>
        /// <param name="model">File robot post model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        public async Task<IActionResult> EditImage([FromBody] FileRobotImagePost model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // FileRobotImagePost model
            //     = new FileRobotImagePost()
            //     {
            //         extension = form["extension"],
            //          folder = form["folder"],
            //          fullName = form["fullName"],
            //         height = int.Parse(form["height"]),
            //         width  = int.Parse(form["width"]),
            //          imageBase64 = form["imageBase64"],
            //           mimeType = form["mimeType"],
            //            name = form["name"],
            //             quantity = form["quantity"]
            //     };
            // Convert base 64 string to byte[]
            var data = model.ImageBase64.Split(',')[1];

            byte[] imageBytes = Convert.FromBase64String(data);

            // Create a stream and build an image object
            using var ms = new MemoryStream(imageBytes);

            var image = await SixLabors.ImageSharp.Image.LoadAsync(ms);

            using var output = new MemoryStream();

            switch (model.Extension)
            {
                case "jpg":
                    await image.SaveAsJpegAsync(output);
                    break;
                case "png":
                    await image.SaveAsPngAsync(output);
                    break;
                case "gif":
                    await image.SaveAsGifAsync(output);
                    break;
                case "webp":
                    await image.SaveAsWebpAsync(output);
                    break;
            }

            var contentType = MimeTypeMap.GetMimeType(Path.GetExtension(model.FullName));

            try
            {
                var metaData = new FileUploadMetaData()
                {
                    ChunkIndex = 0,
                    ContentType = contentType,
                    FileName = model.FullName,
                    RelativePath = model.Folder + "/" + model.FullName,
                    TotalChunks = 1,
                    TotalFileSize = output.Length,
                    UploadUid = Guid.NewGuid().ToString(),
                    ImageHeight = model.Height,
                    ImageWidth = model.Width
                };

                await storageContext.AppendBlob(output, metaData);
            }
            catch (Exception e)
            {
                return Json(ReturnSimpleErrorMessage(e.Message));
            }

            return Ok();
        }

        /// <summary>
        /// Gets a thumbnail for the specified image.
        /// </summary>
        /// <param name="target">Path to file.</param>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        // [ResponseCache(NoStore = true)]
        public async Task<IActionResult> GetImageThumbnail(string target, int width = 120, int height = 120)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Use default values if negative dimensions are provided
            if (width <= 0)
            {
                width = 120;
            }

            if (height <= 0)
            {
                height = 120;
            }

            var extension = Path.GetExtension(target.ToLower());

            var filter = new[] { ".png", ".jpg", ".gif", ".jpeg", ".webp", ".svg" };

            if (!filter.Contains(extension))
            {
                throw new NotSupportedException($"Image type {extension} not supported.");
            }

            if (extension == ".svg")
            {
                // For SVGs, return the original file since they are vector-based and scale without losing quality
                using var svgstream = await storageContext.GetStreamAsync(target);
                using var memStream = new MemoryStream();
                await svgstream.CopyToAsync(memStream);
                return File(memStream.ToArray(), "image/svg+xml");
            }

            using var stream = await storageContext.GetStreamAsync(target);
            var image = await Image.LoadAsync(stream);
            var newImage = image.Clone(i => i.Resize(new ResizeOptions() { Mode = ResizeMode.Crop, Position = AnchorPositionMode.Center, Size = new Size(width, height) }));

            using var outStream = new MemoryStream();
            newImage.SaveAsWebp(outStream);

            return File(outStream.ToArray(), "image/webp");
        }

        /// <summary>
        ///     Used to upload files, one chunk at a time, and normalizes the blob name to lower case.
        /// </summary>
        /// <param name="files">Files being uploaded.</param>
        /// <param name="metaData">File metadata.</param>
        /// <param name="path">Path to where file should be uploaded.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        [RequestSizeLimit(
            6291456)] // AWS S3 multi part upload requires 5 MB parts--no more, no less so pad the upload size by a MB just in case
        public async Task<ActionResult> Upload(IEnumerable<IFormFile> files, string metaData, string path = "")
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (files == null || !files.Any())
            {
                return Json(string.Empty);
            }

            if (!FileEntryPathHelper.IsUploadPathSafe(path))
            {
                return Unauthorized("Cannot upload here. Please select the 'pub' folder first, or sub-folder below that, then try again.");
            }

            // Get information about the chunk we are on.
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(metaData));

            var serializer = new Newtonsoft.Json.JsonSerializer();
            FileUploadMetaData fileMetaData;
            using (var streamReader = new StreamReader(ms))
            {
                fileMetaData =
                    (FileUploadMetaData)serializer.Deserialize(streamReader, typeof(FileUploadMetaData));
            }

            if (fileMetaData == null)
            {
                throw new ArgumentException("Could not read the file's metadata");
            }

            // Validate against path traversal in metadata
            if (!FileEntryPathHelper.IsUploadPathSafe(fileMetaData.RelativePath) || fileMetaData.FileName?.Contains("..") == true)
            {
                return Unauthorized("Path traversal attempts are not allowed.");
            }

            // Validate against dangerous file extensions
            if (FileEntryPathHelper.IsDangerousExtension(fileMetaData.FileName))
            {
                return Json(new FileUploadResult
                {
                    Uploaded = false,
                    FileUid = fileMetaData.UploadUid
                });
            }

            var file = files.FirstOrDefault();

            if (file == null)
            {
                throw new ArgumentException("No file found to upload.");
            }

            var blobName = FileEntryPathHelper.UrlEncodePath(fileMetaData.FileName);
            fileMetaData.ContentType = MimeTypeMap.GetMimeType(Path.GetExtension(fileMetaData.FileName));

            fileMetaData.FileName = blobName;
            fileMetaData.RelativePath = path.TrimEnd('/') + "/" + fileMetaData.RelativePath;

            // Make sure full folder path exists
            var parts = fileMetaData.RelativePath.Trim('/').Split('/');
            var part = string.Empty;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (i == 0 && parts[i] != "pub")
                {
                    return Unauthorized("Must upload folders and files under /pub directory."); // guarded above by IsUploadPathSafe; defensive fallback
                }

                part = $"{part}/{parts[i]}";
                await fileOperations.CreateFolderAsync(part.Trim('/'));
            }

            await using (var stream = file.OpenReadStream())
            {
                await using (var memoryStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memoryStream);
                    await storageContext.AppendBlob(memoryStream, fileMetaData);
                }
            }

            if (fileMetaData.TotalChunks - 1 == fileMetaData.ChunkIndex)
            {
                await PurgeCdnPath(fileMetaData);
            }

            var fileBlob = new FileUploadResult
            {
                Uploaded = fileMetaData.TotalChunks - 1 <= fileMetaData.ChunkIndex,
                FileUid = fileMetaData.UploadUid
            };
            return Json(fileBlob);
        }

        private async Task PurgeCdnPath(FileUploadMetaData metaData)
        {
            if (metaData.TotalChunks - 1 == metaData.ChunkIndex)
            {
                // This is the last chunk, wrap things up here.  Flush the CDN if one is configured.
                var purgeUrls = new List<string>
                {
                    metaData.RelativePath
                };
                var cdnService = await CdnService.GetCdnServiceAsync(dbContext, logger, HttpContext);
                _ = await cdnService.PurgeCdn(purgeUrls);
            }
        }

        private long DivideByAndRoundUp(long number, long divideBy)
        {
            return (long)Math.Ceiling((float)number / (float)divideBy);
        }

        private dynamic ReturnSimpleErrorMessage(string message)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>("{ \"error\": { \"message\": \"" + message + "\"}}");
        }

        private int GetChildNodeIndex(HtmlAgilityPack.HtmlNode parent, HtmlAgilityPack.HtmlNode child)
        {
            var target = parent.ChildNodes.FirstOrDefault(f => NodesAreEqual(f, child));
            if (target == null)
            {
                return -1;
            }

            var index = parent.ChildNodes.IndexOf(target);
            return index;
        }

        /// <summary>
        /// Removes nodes from a parent node by XPath.
        /// </summary>
        /// <param name="originalNode">Origin node.</param>
        /// <param name="nodesToRemove">Nodes to remove.</param>
        private void RemoveNodes(ref HtmlAgilityPack.HtmlNode originalNode, IEnumerable<HtmlAgilityPack.HtmlNode> nodesToRemove)
        {
            foreach (var node in nodesToRemove)
            {
                var doomed = originalNode.ChildNodes.FirstOrDefault(w => NodesAreEqual(w, node));
                if (doomed != null)
                {
                    doomed.Remove();
                }
            }
        }

        /// <summary>
        /// Determines if nodes are equal.
        /// </summary>
        /// <param name="node1">HtmlNode.</param>
        /// <param name="node2">Compare to HtmlNode.</param>
        /// <returns>boolean.</returns>
        /// <remarks>Compares node name, node type, and attributes.</remarks>
        private bool NodesAreEqual(HtmlAgilityPack.HtmlNode node1, HtmlAgilityPack.HtmlNode node2)
        {
            if (node1.Name == node2.Name && node1.NodeType == node2.NodeType)
            {
                var attributeNames1 = node1.Attributes.Select(s => new
                {
                    Name = s.Name.ToLower(),
                    Value = s.Value
                }).OrderBy(o => o.Name).ToList();

                var attributeNames2 = node2.Attributes.Select(s => new
                {
                    Name = s.Name.ToLower(),
                    Value = s.Value
                }).OrderBy(o => o.Name).ToList();

                var firstNotInSecond = attributeNames1.Except(attributeNames2).ToList();
                var secondNotInFirst = attributeNames2.Except(attributeNames1).ToList();

                return firstNotInSecond.Count == 0 && secondNotInFirst.Count == 0;
            }

            return false;
        }

        /// <summary>
        /// Returns model state errors as serialization.
        /// </summary>
        /// <param name="modelState">Model state.</param>
        /// <returns>Errors.</returns>
        private string SerializeErrors(ModelStateDictionary modelState)
        {
            var errors = modelState.Values
                .Where(w => w.ValidationState == ModelValidationState.Invalid).Select(s => s.Errors)
                .ToList();

            return Newtonsoft.Json.JsonConvert.SerializeObject(errors);
        }

        /// <summary>
        /// Selects nodes between HTML comments.
        /// </summary>
        /// <param name="originalNode">Original node.</param>
        /// <param name="startComment">Start comment.</param>
        /// <param name="endComment">End comment.</param>
        /// <returns>HTML Node.</returns>
        private IEnumerable<HtmlAgilityPack.HtmlNode> SelectNodesBetweenComments(HtmlAgilityPack.HtmlNode originalNode, string startComment, string endComment)
        {
            var nodes = new List<HtmlAgilityPack.HtmlNode>();

            startComment = startComment.Replace("<!--", string.Empty).Replace("-->", string.Empty).Trim();
            endComment = endComment.Replace("<!--", string.Empty).Replace("-->", string.Empty).Trim();

            var startNode = originalNode.SelectSingleNode($"//comment()[contains(., '{startComment}')]");
            var endNode = originalNode.SelectSingleNode($"//comment()[contains(., '{endComment}')]");

            if (startNode != null && endNode != null)
            {
                int startNodeIndex = startNode.ParentNode.ChildNodes.IndexOf(startNode);
                int endNodeIndex = endNode.ParentNode.ChildNodes.IndexOf(endNode);

                for (int i = startNodeIndex; i < endNodeIndex + 1; i++)
                {
                    nodes.Add(originalNode.ChildNodes[i]);
                }
            }
            else if (startNode != null && endNode == null)
            {
                throw new Exception($"End comment: '{endComment}' not found.");
            }
            else if (startNode == null && endNode != null)
            {
                throw new Exception($"Start comment: '{startComment}' not found.");
            }

            return nodes;
        }
    }
}




