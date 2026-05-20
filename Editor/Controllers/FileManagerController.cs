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
    using System.Text.Json;
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
    using MediatR;
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
        private string blobPublicAbsoluteUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="ElFinderConnectorController"/> class.
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
            this.blobPublicAbsoluteUrl = editorSettings?.BlobPublicUrl?.TrimStart('/') ?? string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ElFinderConnectorController"/> class
        /// for existing tests and call sites.
        /// </summary>
        public FileManagerController(
            ApplicationDbContext dbContext,
            UserManager<IdentityUser> userManager,
            Cosmos.Common.Features.Shared.IMediator mediator,
            ICacheService<Layout> layoutCache,
            IStorageContext storageContext,
            IEditorSettings editorSettings,
            ILogger<FileManagerController> logger)
            : this(
                dbContext,
                userManager,
                mediator,
                layoutCache,
                storageContext,
                new FileOperationsService(storageContext, new LoggerFactory().CreateLogger<FileOperationsService>()),
                editorSettings,
                logger,
                new ConfigurationBuilder().Build(),
                new MemoryCache(new MemoryCacheOptions()))
        {
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
                    "open" when UseCqrsForOpen() => await HandleOpenViaCqrsAsync(),
                    "open" => await HandleOpenAsync(),
                    "tree" when UseCqrsForCommand("tree") => await HandleTreeViaCqrsAsync(),
                    "tree" => await HandleTreeAsync(),
                    "ls" when UseCqrsForCommand("ls") => await HandleLsViaCqrsAsync(),
                    "ls" => await HandleLsAsync(),
                    "mkdir" when UseCqrsForCommand("mkdir") => await HandleMkdirViaCqrsAsync(),
                    "mkdir" => await HandleMkdirAsync(),
                    "mkfile" when UseCqrsForCommand("mkfile") => await HandleMkfileViaCqrsAsync(),
                    "mkfile" => await HandleMkfileAsync(),
                    "rename" when UseCqrsForCommand("rename") => await HandleRenameViaCqrsAsync(),
                    "rename" => await HandleRenameAsync(),
                    "rm" when UseCqrsForCommand("rm") => await HandleRmViaCqrsAsync(),
                    "rm" => await HandleRmAsync(),
                    "upload" when UseCqrsForCommand("upload") => await HandleUploadViaCqrsAsync(),
                    "upload" => await HandleUploadAsync(),
                    "get" when UseCqrsForCommand("get") => await HandleGetViaCqrsAsync(),
                    "get" => await HandleGetAsync(),
                    "put" when UseCqrsForCommand("put") => await HandlePutViaCqrsAsync(),
                    "put" => await HandlePutAsync(),
                    "paste" when UseCqrsForCommand("paste") => await HandlePasteViaCqrsAsync(),
                    "paste" => await HandlePasteAsync(),
                    "tmb" when UseCqrsForCommand("tmb") => await HandleTmbViaCqrsAsync(),
                    "tmb" => await HandleTmbAsync(),
                    "info" when UseCqrsForCommand("info") => await HandleInfoViaCqrsAsync(),
                    "info" => await HandleInfoAsync(),
                    "size" when UseCqrsForCommand("size") => await HandleSizeViaCqrsAsync(),
                    "size" => await HandleSizeAsync(),
                    "parents" when UseCqrsForCommand("parents") => await HandleParentsViaCqrsAsync(),
                    "parents" => await HandleParentsAsync(),
                    "search" when UseCqrsForCommand("search") => await HandleSearchViaCqrsAsync(),
                    "search" => await HandleSearchAsync(),
                    "file" when UseCqrsForCommand("file") => await HandleFileViaCqrsAsync(),
                    "file" => await HandleFileAsync(),
                    "duplicate" when UseCqrsForCommand("duplicate") => await HandleDuplicateViaCqrsAsync(),
                    "duplicate" => await HandleDuplicateAsync(),
                    "resize" when UseCqrsForCommand("resize") => await HandleResizeViaCqrsAsync(),
                    "resize" => await HandleResizeAsync(),
                    "url" when UseCqrsForCommand("url") => await HandleUrlViaCqrsAsync(),
                    "url" => await HandleUrlAsync(),
                    "dim" when UseCqrsForCommand("dim") => await HandleDimViaCqrsAsync(),
                    "dim" => await HandleDimAsync(),
                    _ => Json(ElFinderError("errUnknownCmd"))
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ElFinder connector error handling command '{Cmd}'", cmd);
                return Json(ElFinderError(ex.Message));
            }
        }

        private bool UseCqrsForOpen()
        {
            return UseCqrsForCommand("open");
        }

        private bool UseCqrsForCommand(string command)
        {
            // Backward compatibility with explicit query opt-in during migration.
            var legacyQueryOptIn = string.Equals(GetParam("__cqrs"), "1", StringComparison.Ordinal)
                || string.Equals(GetParam($"__cqrs_{command}"), "1", StringComparison.Ordinal);

            if (legacyQueryOptIn)
            {
                return true;
            }

            // Config-driven staged rollout.
            // Supported keys:
            // - ElFinder:Cqrs:Enabled=true|false
            // - ElFinder:Cqrs:Commands:open=true|false (per-command override)
            var globalEnabled = configuration.GetValue<bool?>("ElFinder:Cqrs:Enabled") ?? false;
            var commandEnabled = configuration.GetValue<bool?>($"ElFinder:Cqrs:Commands:{command}");

            return commandEnabled ?? globalEnabled;
        }

        private MediatR.IMediator GetElFinderMediatorOrNull()
        {
            return this.HttpContext?.RequestServices.GetService<MediatR.IMediator>();
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
            var json = System.Text.Json.JsonSerializer.Serialize(response, response.GetType());
            return Content(json, "application/json; charset=utf-8");
        }

        private static IActionResult TranslateCqrsErrorToLegacy(Controller controller, IElFinderResponse response)
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

        /// <summary>
        /// Executes a CQRS command via MediatR if available, otherwise falls back to the legacy handler.
        /// Centralizes the mediator null-check, logging, error translation, and JSON serialization pattern.
        /// </summary>
        /// <typeparam name="TCommand">The CQRS command type.</typeparam>
        /// <typeparam name="TResponse">The elFinder response type.</typeparam>
        /// <param name="command">The command to execute.</param>
        /// <param name="fallbackHandler">The legacy handler to call if MediatR is unavailable.</param>
        /// <param name="commandName">The command name for logging purposes.</param>
        /// <returns>An IActionResult with the command response or fallback result.</returns>
        private async Task<IActionResult> ExecuteCqrsCommandOrFallback<TCommand, TResponse>(
            TCommand command,
            Func<Task<IActionResult>> fallbackHandler,
            string commandName)
            where TCommand : MediatR.IRequest<TResponse>
            where TResponse : IElFinderResponse
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS {CommandName} requested but MediatR.IMediator is not registered; falling back to legacy handler.", commandName);
                return await fallbackHandler();
            }

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleTreeViaCqrsAsync()
        {
            var command = new TreeCommand
            {
                Target = GetParam("target"),
                Filter = GetParam("filter"),
                VolumeId = VolumeId,
            };

            return await ExecuteCqrsCommandOrFallback<TreeCommand, IElFinderResponse>(
                command,
                HandleTreeAsync,
                "tree");
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

            return await ExecuteCqrsCommandOrFallback<MkdirCommand, IElFinderResponse>(
                command,
                HandleMkdirAsync,
                "mkdir");
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

            return await ExecuteCqrsCommandOrFallback<MkfileCommand, IElFinderResponse>(
                command,
                HandleMkfileAsync,
                "mkfile");
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

            return await ExecuteCqrsCommandOrFallback<RenameCommand, IElFinderResponse>(
                command,
                HandleRenameAsync,
                "rename");
        }

        private async Task<IActionResult> HandleRmViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS rm requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleRmAsync();
            }

            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
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

                var response = await mediator.Send(command);
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
            var isInit = GetParam("init") == "1";
            var command = new OpenCommand(
                target: target,
                init: isInit,
                volumeId: VolumeId,
                tree: GetParam("tree") == "1",
                blobPublicUrl: editorSettings.BlobPublicUrl,
                tmbUrl: "/FileManager/GetImageThumbnail?target=",
                rootPath: RootPath);

            return await ExecuteCqrsCommandOrFallback<OpenCommand, IElFinderResponse>(
                command,
                HandleOpenAsync,
                "open");
        }

        private async Task<IActionResult> HandleUploadViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS upload requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleUploadAsync();
            }

            var target = GetParam("target");
            var path = DecodeHash(target);
            if (path == null || !IsAllowedPath(path))
            {
                return Json(ElFinderError("errAccess"));
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

                var response = await mediator.Send(command);
                var mappedError = TranslateCqrsErrorToLegacy(this, response);
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
            var command = new GetCommand
            {
                Target = GetParam("target"),
                VolumeId = VolumeId,
            };

            return await ExecuteCqrsCommandOrFallback<GetCommand, IElFinderResponse>(
                command,
                HandleGetAsync,
                "get");
        }

        private async Task<IActionResult> HandlePutViaCqrsAsync()
        {
            var command = new PutCommand
            {
                Target = GetParam("target"),
                Content = GetParam("content"),
                VolumeId = VolumeId,
            };

            return await ExecuteCqrsCommandOrFallback<PutCommand, IElFinderResponse>(
                command,
                HandlePutAsync,
                "put");
        }

        private async Task<IActionResult> HandlePasteViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS paste requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandlePasteAsync();
            }

            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            var command = new PasteCommand
            {
                Target = GetParam("dst") ?? GetParam("target"),
                Sources = string.Join(',', targets),
                Cut = GetParam("cut"),
                VolumeId = VolumeId,
            };

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleParentsViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS parents requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleParentsAsync();
            }

            var command = new ParentsCommand
            {
                Target = GetParam("target"),
                VolumeId = VolumeId,
            };

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            if (mappedError != null)
            {
                return mappedError;
            }

            // Use System.Text.Json so [JsonPropertyName] / [JsonIgnore] attributes on
            // the CQRS response DTOs are honored (the app uses Newtonsoft with
            // DefaultContractResolver which would otherwise produce PascalCase keys).
            var json = System.Text.Json.JsonSerializer.Serialize(response);
            return Content(json, "application/json");
        }

        private async Task<IActionResult> HandleSizeViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS size requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleSizeAsync();
            }

            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            if (targets.Length == 0)
            {
                return Json(new { size = 0L });
            }

            long total = 0;
            foreach (var target in targets)
            {
                var command = new SizeCommand
                {
                    Target = target,
                    VolumeId = VolumeId,
                };

                var response = await mediator.Send(command);
                if (response is SizeResponse sizeResponse)
                {
                    total += sizeResponse.Size;
                }
            }

            return Json(new { size = total });
        }

        private async Task<IActionResult> HandleLsViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS ls requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleLsAsync();
            }

            var intersect = GetParams("intersect[]");

            var command = new LsCommand
            {
                Target = GetParam("target"),
                Intersect = intersect,
                VolumeId = VolumeId,
            };

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleTmbViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS tmb requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleTmbAsync();
            }

            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            var command = new TmbCommand
            {
                Targets = string.Join(',', targets),
                VolumeId = VolumeId,
            };

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleInfoViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS info requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleInfoAsync();
            }

            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            var command = new InfoCommand
            {
                Targets = string.Join(',', targets),
                VolumeId = VolumeId,
            };

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleSearchViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS search requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleSearchAsync();
            }

            var mimes = GetParams("mimes[]");

            var command = new SearchCommand
            {
                Query = GetParam("q"),
                Target = GetParam("target"),
                Mimes = mimes.Length > 0 ? mimes : null,
                VolumeId = VolumeId,
            };

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            if (mappedError != null)
            {
                return mappedError;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(response);
            return Content(json, "application/json");
        }

        private async Task<IActionResult> HandleFileViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS file requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleFileAsync();
            }

            var command = new FileCommand
            {
                Target = GetParam("target"),
                Download = GetParam("download"),
                VolumeId = VolumeId,
            };

            var response = await mediator.Send(command);

            if (response is FileResponse fileResponse && fileResponse.Stream != null)
            {
                if (fileResponse.ForceDownload)
                {
                    return File(fileResponse.Stream, fileResponse.ContentType, fileResponse.FileName);
                }

                Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileResponse.FileName}\"";
                return File(fileResponse.Stream, fileResponse.ContentType);
            }

            var mappedError2 = TranslateCqrsErrorToLegacy(this, response);
            return mappedError2 ?? Json(ElFinderError("errOpen"));
        }

        private async Task<IActionResult> HandleDuplicateViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS duplicate requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleDuplicateAsync();
            }

            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            var command = new DuplicateCommand
            {
                Targets = string.Join(',', targets),
                VolumeId = VolumeId,
            };

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            if (mappedError != null)
            {
                return mappedError;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(response);
            return Content(json, "application/json");
        }

        private async Task<IActionResult> HandleResizeViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS resize requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleResizeAsync();
            }

            _ = int.TryParse(GetParam("width"), out var width);
            _ = int.TryParse(GetParam("height"), out var height);
            _ = int.TryParse(GetParam("x"), out var x);
            _ = int.TryParse(GetParam("y"), out var y);
            _ = int.TryParse(GetParam("degree"), out var degree);
            _ = int.TryParse(GetParam("quality"), out var quality);

            var command = new ResizeCommand
            {
                Target = GetParam("target"),
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

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            if (mappedError != null)
            {
                return mappedError;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(response);
            return Content(json, "application/json");
        }

        private async Task<IActionResult> HandleUrlViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS url requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleUrlAsync();
            }

            var command = new UrlCommand
            {
                Target = GetParam("target"),
                BlobPublicUrl = editorSettings.BlobPublicUrl,
                VolumeId = VolumeId,
            };

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            if (mappedError != null)
            {
                return mappedError;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(response);
            return Content(json, "application/json");
        }

        private async Task<IActionResult> HandleDimViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS dim requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleDimAsync();
            }

            var command = new DimCommand
            {
                Target = GetParam("target"),
                VolumeId = VolumeId,
            };

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            if (mappedError != null)
            {
                return mappedError;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(response);
            return Content(json, "application/json");
        }

        // ─── Command handlers ────────────────────────────────────────────────────
        private async Task<IActionResult> HandleOpenAsync()
        {
            var target = GetParam("target");
            var isInit = GetParam("init") == "1";

            var path = string.IsNullOrEmpty(target) ? RootPath : DecodeHash(target);
            if (path == null || !IsAllowedPath(path))
            {
                return Json(ElFinderError("errAccess"));
            }

            var items = await GetEntriesWithFriendlyTitlesAsync(path);
            var fileObjects = items.Select(e => ToElFinderObject(e, EncodeHash(path))).ToList();

            // Build the cwd object for the directory being opened
            FileManagerEntry cwdEntry;
            try
            {
                cwdEntry = await fileOperations.GetFileAsync(path);
                await ApplyFriendlyTitleAsync(cwdEntry);
            }
            catch
            {
                cwdEntry = null;
            }

            object cwdObject;
            if (cwdEntry != null)
            {
                cwdObject = ToElFinderObject(cwdEntry, GetParentHash(path));
            }
            else
            {
                // Synthesise a root entry when the storage provider does not return one
                var isRoot = path.TrimEnd('/') == RootPath;
                var parentHash = isRoot ? null : GetParentHash(path);
                cwdObject = SyntheticDirObject(path, parentHash, isRoot);
            }

            // elFinder maintains its tree panel entirely from a client-side cache built up over
            // successive responses. There are two distinct behaviours needed:
            //
            // • Tree-restoration mode (init=1 or tree=1): the client needs the full ancestor
            //   chain — root + siblings at every level down to and including the cwd's own
            //   peer level — so it can reconstruct the tree without extra round-trips. This is
            //   used on page load and when navigating directly to a deep path via a URL hash.
            //
            // • Navigation mode (regular open, no init/tree): the client already has parent/
            //   sibling nodes in its cache from prior navigations. Returning root or ancestors
            //   here causes elFinder to overwrite its cached child-list for those nodes with
            //   only the items present in this response — so siblings disappear. Return only
            //   the direct children.
            //
            // This matches the behaviour of the Studio-42 reference PHP connector.
            List<object> allFiles;
            var isTreeMode = isInit || GetParam("tree") == "1";

            if (isTreeMode)
            {
                var treeFiles = new List<object>();
                var seenHashes = new HashSet<string>();
                var rootHash = EncodeHash(RootPath);
                var cwdHash = ((Dictionary<string, object>)cwdObject)["hash"]?.ToString();

                // Always include the root volume node.
                treeFiles.Add(SyntheticDirObject(RootPath, null, isRoot: true));
                seenHashes.Add(rootHash);

                // Walk from the cwd up to root, collect ancestor paths (excluding root),
                // then process them outermost → innermost to load siblings at each level.
                var ancestors = new List<string>();
                var ancestorCursor = path;
                while (!string.IsNullOrEmpty(ancestorCursor) &&
                       ancestorCursor.StartsWith(RootPath, StringComparison.Ordinal) &&
                       !string.Equals(ancestorCursor, RootPath, StringComparison.Ordinal))
                {
                    ancestors.Add(ancestorCursor);
                    var p = GetParentPath(ancestorCursor);
                    if (string.IsNullOrEmpty(p) || string.Equals(p, ancestorCursor, StringComparison.Ordinal))
                    {
                        break;
                    }

                    ancestorCursor = p;
                }

                ancestors.Reverse(); // outermost (direct child of root) → innermost (cwd)

                foreach (var ancestor in ancestors)
                {
                    // Stop before the cwd itself — handled below after loading cwd siblings.
                    if (string.Equals(ancestor, path, StringComparison.Ordinal))
                    {
                        break;
                    }

                    // Add all directory siblings at this level (children of this ancestor's parent).
                    var ancestorParent = GetParentPath(ancestor);
                    try
                    {
                        var siblingItems = await GetEntriesWithFriendlyTitlesAsync(ancestorParent);
                        foreach (var sibling in siblingItems.Where(e => e.IsDirectory))
                        {
                            var sibObj = ToElFinderObject(sibling, EncodeHash(ancestorParent));
                            var sibHash = ((Dictionary<string, object>)sibObj)["hash"]?.ToString();
                            if (sibHash != null && seenHashes.Add(sibHash))
                            {
                                treeFiles.Add(sibObj);
                            }
                        }
                    }
                    catch
                    {
                        // Best-effort: skip this level if storage fails.
                    }
                }

                // Always load siblings of the cwd itself (children of cwd's parent).
                // The ancestor loop above stops before the cwd, so this handles:
                //   (a) cwd is a direct child of root — the loop ran zero useful iterations.
                //   (b) any depth — ensures the cwd's peer level is fully represented.
                if (!string.Equals(path, RootPath, StringComparison.Ordinal))
                {
                    var cwdParent = GetParentPath(path);
                    try
                    {
                        var cwdSiblings = await GetEntriesWithFriendlyTitlesAsync(cwdParent);
                        foreach (var sibling in cwdSiblings.Where(e => e.IsDirectory))
                        {
                            var sibObj = ToElFinderObject(sibling, EncodeHash(cwdParent));
                            var sibHash = ((Dictionary<string, object>)sibObj)["hash"]?.ToString();
                            if (sibHash != null && seenHashes.Add(sibHash))
                            {
                                treeFiles.Add(sibObj);
                            }
                        }
                    }
                    catch
                    {
                        // Best-effort: skip this level if storage fails.
                    }
                }

                // Include cwd if not already present as a sibling.
                if (!string.Equals(cwdHash, rootHash, StringComparison.Ordinal) &&
                    cwdHash != null && seenHashes.Add(cwdHash))
                {
                    treeFiles.Add(cwdObject);
                }

                treeFiles.AddRange(fileObjects);
                allFiles = treeFiles;
            }
            else
            {
                // Navigation mode: return only the direct children of the opened folder.
                // Do not include root, ancestors, or the cwd itself — adding those would
                // cause elFinder to overwrite its cached child-lists for those nodes and
                // drop sibling nodes that were loaded in prior responses.
                allFiles = new List<object>(fileObjects);
            }

            if (cwdObject is Dictionary<string, object> cwdDict)
            {
                cwdDict["root"] = EncodeHash(RootPath);
            }

            var articleTitlesByNumber = new Dictionary<int, string>();
            if (PublicFileEntryHelper.TryGetArticleNumberFromPath(NormalizePath(path), out var cwdArticleNumber))
            {
                var titleResolver = new PublicFileEntryTitleResolver(dbContext);
                var resolved = await titleResolver.GetArticleTitlesByNumberAsync(new[] { cwdArticleNumber });
                articleTitlesByNumber = new Dictionary<int, string>(resolved);
            }

            var displayPath = PublicFileEntryHelper.ResolveFriendlyDisplayPath(NormalizePath(path), articleTitlesByNumber);

            var response = new Dictionary<string, object>
            {
                ["cwd"] = cwdObject,
                ["files"] = allFiles,
                ["options"] = BuildOptions(path, displayPath),
            };

            if (isInit)
            {
                // api, uplMaxSize, and init are protocol fields that must only appear
                // on the init response. Sending api on a navigation response triggers
                // a full client re-initialization which clears the folder tree.
                response["api"] = "2.1";
                response["uplMaxSize"] = "64M";
                response["init"] = 1;
            }

            return Json(response);
        }

        private async Task<IActionResult> HandleTreeAsync()
        {
            var target = GetParam("target");
            var path = DecodeHash(target);
            if (path == null || !IsAllowedPath(path))
            {
                return Json(ElFinderError("errAccess"));
            }

            var items = await GetEntriesWithFriendlyTitlesAsync(path);
            var dirs = items.Where(e => e.IsDirectory)
                            .Select(e => ToElFinderObject(e, EncodeHash(path)))
                            .ToList();

            return Json(new { tree = dirs });
        }

        private async Task<IActionResult> HandleLsAsync()
        {
            var target = GetParam("target");
            var path = DecodeHash(target);
            if (path == null || !IsAllowedPath(path))
            {
                return Json(ElFinderError("errAccess"));
            }

            var intersect = GetParams("intersect[]");

            var items = await storageContext.GetFilesAndDirectories(path);
            var names = items
                .Select(e => e.IsDirectory ? e.Name : e.Name + e.Extension)
                .Where(n => !string.IsNullOrEmpty(n));

            if (intersect.Length > 0)
            {
                var intersectSet = new HashSet<string>(intersect, StringComparer.OrdinalIgnoreCase);
                names = names.Where(n => intersectSet.Contains(n));
            }

            return Json(new { list = names.ToList() });
        }

        private async Task<IActionResult> HandleMkdirAsync()
        {
            var target = GetParam("target");
            var name = NormalizeElFinderName(GetParam("name"));
            var path = DecodeHash(target);

            if (path == null || !IsAllowedPath(path) || string.IsNullOrWhiteSpace(name))
            {
                return Json(ElFinderError("errAccess"));
            }

            if (!IsSafeName(name))
            {
                return Json(ElFinderError("errInvName"));
            }

            var uniqueName = await GetUniqueNameAsync(path, name);
            var newPath = path.TrimEnd('/') + "/" + uniqueName;
            var entry = await fileOperations.CreateFolderAsync(newPath.TrimStart('/'));

            // Normalise path returned by the storage provider
            if (!entry.Path.StartsWith("/"))
            {
                entry.Path = "/" + entry.Path;
            }

            return Json(new { added = new[] { ToElFinderObject(entry, target) } });
        }

        private async Task<IActionResult> HandleMkfileAsync()
        {
            var target = GetParam("target");
            var name = NormalizeElFinderName(GetParam("name"));
            var path = DecodeHash(target);

            if (path == null || !IsAllowedPath(path) || string.IsNullOrWhiteSpace(name))
            {
                return Json(ElFinderError("errAccess"));
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
            var filePath = path.TrimEnd('/') + "/" + uniqueName;
            var uniqueExt = Path.GetExtension(uniqueName).ToLowerInvariant();
            var meta = new FileUploadMetaData
            {
                UploadUid = Guid.NewGuid().ToString(),
                FileName = uniqueName,
                RelativePath = filePath.TrimStart('/'),
                ContentType = MimeTypeMap.GetMimeType(uniqueExt),
                ChunkIndex = 0,
                TotalChunks = 1,
                TotalFileSize = 0,
            };

            await storageContext.AppendBlob(new MemoryStream(Array.Empty<byte>()), meta);

            var entry = BuildSyntheticFileEntry(filePath, Path.GetFileNameWithoutExtension(uniqueName), uniqueExt, 0);
            return Json(new { added = new[] { ToElFinderObject(entry, target) } });
        }

        private async Task<IActionResult> HandleRenameAsync()
        {
            var target = GetParam("target");
            var name = NormalizeElFinderName(GetParam("name"));
            var path = DecodeHash(target);

            if (path == null || !IsAllowedPath(path) || string.IsNullOrWhiteSpace(name))
            {
                return Json(ElFinderError("errAccess"));
            }

            if (!IsSafeName(name))
            {
                return Json(ElFinderError("errInvName"));
            }

            var parentPath = GetParentPath(path);
            var uniqueName = await GetUniqueNameAsync(parentPath, name, path);
            var newPath = parentPath.TrimEnd('/') + "/" + uniqueName;

            FileManagerEntry entry;
            try
            {
                entry = await fileOperations.GetFileAsync(path);
            }
            catch
            {
                entry = null;
            }

            var isDir = entry?.IsDirectory ?? path.EndsWith("/");

            if (isDir)
            {
                await fileOperations.MoveFolderAsync(path, newPath);
            }
            else
            {
                await fileOperations.MoveFileAsync(path, newPath);
            }

            var newEntry = BuildSyntheticFileEntry(newPath, Path.GetFileNameWithoutExtension(uniqueName), Path.GetExtension(uniqueName), entry?.Size ?? 0, isDir);
            return Json(new
            {
                added = new[] { ToElFinderObject(newEntry, EncodeHash(parentPath)) },
                removed = new[] { target },
            });
        }

        private async Task<IActionResult> HandleRmAsync()
        {
            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            var removed = new List<string>();
            var notFound = new List<string>();
            var notRemoved = new List<string>();
            var notFoundDetails = new List<object>();
            var notRemovedDetails = new List<object>();

            foreach (var t in targets)
            {
                var path = DecodeHash(t);
                if (path == null || !IsAllowedPath(path))
                {
                    notFound.Add(t);
                    notFoundDetails.Add(new
                    {
                        hash = t,
                        path,
                        reasonCode = path == null ? "hash_decode_failed" : "path_not_allowed",
                        reason = path == null
                            ? "Unable to decode target hash"
                            : "Decoded path is not allowed by server policy",
                    });
                    continue;
                }

                FileManagerEntry entry;
                try
                {
                    entry = await fileOperations.GetFileAsync(path);
                }
                catch
                {
                    entry = null;
                }

                var parentListingMatch = await GetEntryFromParentListingAsync(path);
                var existedBeforeDelete = entry != null || parentListingMatch != null;
                if (!existedBeforeDelete)
                {
                    notFound.Add(t);
                    notFoundDetails.Add(new
                    {
                        hash = t,
                        path,
                        reasonCode = "not_found_pre_delete",
                        reason = "Target was not found before delete",
                    });
                    continue;
                }

                var isDir = entry?.IsDirectory ?? parentListingMatch?.IsDirectory ?? false;

                if (isDir)
                {
                    await fileOperations.DeleteFolderAsync(path);
                }
                else
                {
                    await fileOperations.DeleteFileAsync(path);
                }

                if (!await PathExistsInParentListingAsync(path))
                {
                    removed.Add(t);
                }
                else
                {
                    notRemoved.Add(t);
                    notRemovedDetails.Add(new
                    {
                        hash = t,
                        path,
                        reasonCode = "delete_no_effect",
                        reason = "Delete call completed but target still appears in storage listing",
                    });
                }
            }

            return Json(new { removed, notFound, notRemoved, notFoundDetails, notRemovedDetails });
        }

        private async Task<FileManagerEntry?> GetEntryFromParentListingAsync(string path)
        {
            try
            {
                var normalizedPath = NormalizePath(path);
                if (string.IsNullOrEmpty(normalizedPath))
                {
                    return null;
                }

                var parent = GetParentPath(normalizedPath);
                var children = await storageContext.GetFilesAndDirectories(parent);
                return children.FirstOrDefault(c =>
                {
                    var childPath = NormalizePath(c.Path.StartsWith('/') ? c.Path : "/" + c.Path);
                    return string.Equals(childPath, normalizedPath, StringComparison.OrdinalIgnoreCase);
                });
            }
            catch
            {
                return null;
            }
        }

        private async Task<bool> PathExistsInParentListingAsync(string path)
        {
            try
            {
                var normalizedPath = NormalizePath(path);
                if (string.IsNullOrEmpty(normalizedPath))
                {
                    return false;
                }

                var direct = await fileOperations.GetFileAsync(normalizedPath);
                if (direct != null)
                {
                    return true;
                }

                var parentListingMatch = await GetEntryFromParentListingAsync(normalizedPath);
                return parentListingMatch != null;
            }
            catch
            {
                return false;
            }
        }

        private async Task<IActionResult> HandleUploadAsync()
        {
            var target = GetParam("target");
            var path = DecodeHash(target);

            if (path == null || !IsAllowedPath(path))
            {
                return Json(ElFinderError("errAccess"));
            }

            var files = Request.Form.Files;
            if (files == null || files.Count == 0)
            {
                return Json(ElFinderError("errUploadNoFiles"));
            }

            var added = new List<object>();

            foreach (var file in files)
            {
                var fileName = NormalizeElFinderName(Path.GetFileName(file.FileName));
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                if (!IsSafeName(fileName))
                {
                    return Json(ElFinderError("errInvName"));
                }

                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                if (FileStorageConstants.DangerousFileExtensions.Contains(ext))
                {
                    return Json(ElFinderError("errUploadFile"));
                }

                var uniqueName = await GetUniqueNameAsync(path, fileName);
                var uniqueExt = Path.GetExtension(uniqueName).ToLowerInvariant();
                var filePath = path.TrimEnd('/') + "/" + uniqueName;
                var meta = new FileUploadMetaData
                {
                    UploadUid = Guid.NewGuid().ToString(),
                    FileName = uniqueName,
                    RelativePath = filePath.TrimStart('/'),
                    ContentType = MimeTypeMap.GetMimeType(uniqueExt),
                    ChunkIndex = 0,
                    TotalChunks = 1,
                    TotalFileSize = file.Length,
                };

                await using (var stream = file.OpenReadStream())
                await using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms);
                    await storageContext.AppendBlob(ms, meta);
                }

                var entry = BuildSyntheticFileEntry(filePath, Path.GetFileNameWithoutExtension(uniqueName), uniqueExt, file.Length);
                added.Add(ToElFinderObject(entry, target));
            }

            return Json(new { added });
        }

        private async Task<IActionResult> HandleGetAsync()
        {
            var target = GetParam("target");
            var path = DecodeHash(target);

            if (path == null || !IsAllowedPath(path))
            {
                return Json(ElFinderError("errAccess"));
            }

            using var stream = await storageContext.GetStreamAsync(path);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var content = await reader.ReadToEndAsync();

            return Json(new { content, encoding = "utf-8", doconv = 0 });
        }

        private async Task<IActionResult> HandlePutAsync()
        {
            var target = GetParam("target");
            var content = GetParam("content");
            var path = DecodeHash(target);

            if (path == null || !IsAllowedPath(path))
            {
                return Json(ElFinderError("errAccess"));
            }

            var name = Path.GetFileName(path);
            var ext = Path.GetExtension(name).ToLowerInvariant();
            var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
            var meta = new FileUploadMetaData
            {
                UploadUid = Guid.NewGuid().ToString(),
                FileName = name,
                RelativePath = path.TrimStart('/'),
                ContentType = MimeTypeMap.GetMimeType(ext),
                ChunkIndex = 0,
                TotalChunks = 1,
                TotalFileSize = bytes.Length,
            };

            await storageContext.AppendBlob(new MemoryStream(bytes), meta);

            FileManagerEntry entry;
            try
            {
                entry = await fileOperations.GetFileAsync(path);
            }
            catch
            {
                entry = BuildSyntheticFileEntry(path, Path.GetFileNameWithoutExtension(name), ext, bytes.Length);
            }

            var parentHash = EncodeHash(GetParentPath(path));
            return Json(new { changed = new[] { ToElFinderObject(entry, parentHash) } });
        }

        private async Task<IActionResult> HandlePasteAsync()
        {
            var dst = GetParam("dst");
            var cut = GetParam("cut") == "1";
            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            var destPath = DecodeHash(dst);
            if (destPath == null || !IsAllowedPath(destPath))
            {
                return Json(ElFinderError("errAccess"));
            }

            var added = new List<object>();
            var removed = new List<string>();

            foreach (var t in targets)
            {
                var srcPath = DecodeHash(t);
                if (srcPath == null || !IsAllowedPath(srcPath))
                {
                    continue;
                }

                var name = Path.GetFileName(srcPath.TrimEnd('/'));
                var newPath = destPath.TrimEnd('/') + "/" + name;

                FileManagerEntry entry;
                try
                {
                    entry = await fileOperations.GetFileAsync(srcPath);
                }
                catch
                {
                    entry = null;
                }

                var isDir = entry?.IsDirectory ?? false;

                if (cut)
                {
                    if (isDir)
                    {
                        await fileOperations.MoveFolderAsync(srcPath, newPath);
                    }
                    else
                    {
                        await fileOperations.MoveFileAsync(srcPath, newPath);
                    }

                    removed.Add(t);
                }
                else
                {
                    await storageContext.CopyAsync(srcPath, newPath);
                }

                var newEntry = BuildSyntheticFileEntry(newPath, Path.GetFileNameWithoutExtension(name), Path.GetExtension(name), entry?.Size ?? 0, isDir);
                added.Add(ToElFinderObject(newEntry, dst));
            }

            return Json(new { added, removed });
        }

        private async Task<IActionResult> HandleTmbAsync()
        {
            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            var images = new Dictionary<string, string>();

            foreach (var t in targets)
            {
                var path = DecodeHash(t);
                if (path == null)
                {
                    continue;
                }

                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (FileStorageConstants.ValidImageExtensions.Contains(ext))
                {
                    images[t] = $"/FileManager/GetImageThumbnail?target={Uri.EscapeDataString(path)}&width=80&height=80";
                }
            }

            return await Task.FromResult(Json(new { images }));
        }

        private async Task<IActionResult> HandleInfoAsync()
        {
            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            var files = new List<object>();

            foreach (var t in targets)
            {
                var path = DecodeHash(t);
                if (path == null || !IsAllowedPath(path))
                {
                    continue;
                }

                try
                {
                    var entry = await fileOperations.GetFileAsync(path);
                    var parentHash = EncodeHash(GetParentPath(path));
                    files.Add(ToElFinderObject(entry, parentHash));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not fetch info for path '{Path}'", path);
                }
            }

            return Json(new { files });
        }

        private async Task<IActionResult> HandleSizeAsync()
        {
            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            long total = 0;

            foreach (var t in targets)
            {
                var path = DecodeHash(t);
                if (path == null)
                {
                    continue;
                }

                try
                {
                    var filePaths = await storageContext.GetFilesAsync(path);
                    foreach (var fp in filePaths)
                    {
                        try
                        {
                            var e = await fileOperations.GetFileAsync(fp);
                            total += e.Size;
                        }
                        catch
                        {
                            // Best-effort size accumulation
                        }
                    }
                }
                catch
                {
                    // Best-effort
                }
            }

            return Json(new { size = total });
        }

        private async Task<IActionResult> HandleParentsAsync()
        {
            var target = GetParam("target");
            var path = DecodeHash(target);

            if (path == null || !IsAllowedPath(path))
            {
                return Json(ElFinderError("errAccess"));
            }

            var ancestors = new List<string>();
            var current = path;

            while (!string.IsNullOrEmpty(current) && current.StartsWith(RootPath, StringComparison.Ordinal))
            {
                ancestors.Add(current);
                if (string.Equals(current, RootPath, StringComparison.Ordinal))
                {
                    break;
                }

                var parent = GetParentPath(current);
                if (string.IsNullOrEmpty(parent) || parent == current)
                {
                    break;
                }

                current = parent;
            }

            ancestors.Reverse();
            var tree = new List<object>();

            foreach (var ancestor in ancestors)
            {
                var isRoot = string.Equals(ancestor, RootPath, StringComparison.Ordinal);
                if (isRoot)
                {
                    tree.Add(SyntheticDirObject(RootPath, null, isRoot: true));
                    continue;
                }

                var parent = GetParentPath(ancestor);

                try
                {
                    var items = await GetEntriesWithFriendlyTitlesAsync(parent);
                    foreach (var item in items.Where(e => e.IsDirectory))
                    {
                        tree.Add(ToElFinderObject(item, EncodeHash(parent)));
                    }
                }
                catch
                {
                    // Best-effort: continue walking up even if this level fails.
                }
            }

            // Include children of the target path so the tree can expand current node.
            try
            {
                var targetChildren = await GetEntriesWithFriendlyTitlesAsync(path);
                foreach (var child in targetChildren.Where(e => e.IsDirectory))
                {
                    tree.Add(ToElFinderObject(child, EncodeHash(path)));
                }
            }
            catch
            {
                // Best-effort only.
            }

            var seen = new HashSet<string>();
            var deduped = new List<object>();
            foreach (var item in tree)
            {
                var itemDict = item as Dictionary<string, object>;
                if (itemDict != null && itemDict.ContainsKey("hash"))
                {
                    var hash = itemDict["hash"].ToString();
                    if (seen.Add(hash))
                    {
                        deduped.Add(item);
                    }
                }
            }

            return Json(new { tree = deduped });
        }

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

                // Storage providers can differ in how Name/Extension are populated.
                // Add all common display variants so duplicate detection is provider-agnostic.
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

        private async Task<IActionResult> HandleSearchAsync()
        {
            var q = GetParam("q");
            if (string.IsNullOrWhiteSpace(q))
            {
                return Json(ElFinderError("errCmdParams"));
            }

            var target = GetParam("target");
            var rootPath = string.IsNullOrEmpty(target) ? RootPath : DecodeHash(target);
            if (rootPath == null || !IsAllowedPath(rootPath))
            {
                return Json(ElFinderError("errAccess"));
            }

            var results = new List<object>();
            var queue = new Queue<string>();
            queue.Enqueue(rootPath);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                List<FileManagerEntry> entries;
                try
                {
                    entries = await storageContext.GetFilesAndDirectories(current);
                }
                catch
                {
                    continue;
                }

                foreach (var entry in entries)
                {
                    var entryPath = current.TrimEnd('/') + "/" + entry.Name;
                    if ((entry.Name ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(ToElFinderObject(entry, EncodeHash(current)));
                    }

                    if (entry.IsDirectory)
                    {
                        queue.Enqueue(entryPath);
                    }
                }
            }

            return Json(new { files = results });
        }

        private async Task<IActionResult> HandleFileAsync()
        {
            var target = GetParam("target");
            var path = DecodeHash(target);
            if (path == null || !IsAllowedPath(path))
            {
                return Json(ElFinderError("errAccess"));
            }

            var download = GetParam("download") == "1";
            Stream stream;
            try
            {
                stream = await storageContext.GetStreamAsync(path);
            }
            catch
            {
                return Json(ElFinderError("errOpen"));
            }

            if (stream == null)
            {
                return Json(ElFinderError("errOpen"));
            }

            var fileName = Path.GetFileName(path);
            var contentType = GetMimeType(Path.GetExtension(path));

            if (download)
            {
                return File(stream, contentType, fileName);
            }

            Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
            return File(stream, contentType);
        }

        private async Task<IActionResult> HandleDuplicateAsync()
        {
            var targets = GetParams("targets[]");
            if (targets.Length == 0)
            {
                targets = GetParams("targets");
            }

            var added = new List<object>();
            foreach (var t in targets)
            {
                var sourcePath = DecodeHash(t);
                if (sourcePath == null || !IsAllowedPath(sourcePath))
                {
                    continue;
                }

                var parentPath = GetParentPath(sourcePath);
                var originalName = Path.GetFileName(sourcePath.TrimEnd('/'));
                var ext = Path.GetExtension(originalName);
                var baseName = Path.GetFileNameWithoutExtension(originalName);
                var copyName = await GetUniqueNameAsync(parentPath, baseName + "~" + ext);
                var destPath = parentPath.TrimEnd('/') + "/" + copyName;

                try
                {
                    await storageContext.CopyAsync(sourcePath, destPath);
                    var newEntry = await fileOperations.GetFileAsync(destPath);
                    if (newEntry != null)
                    {
                        added.Add(ToElFinderObject(newEntry, EncodeHash(parentPath)));
                    }
                }
                catch
                {
                    // Skip failed copies.
                }
            }

            return Json(new { added });
        }

        private async Task<IActionResult> HandleResizeAsync()
        {
            // Legacy path delegates to the CQRS handler via feature flag default.
            return Json(ElFinderError("errCmdNoSupport"));
        }

        private Task<IActionResult> HandleUrlAsync()
        {
            var target = GetParam("target");
            var path = DecodeHash(target);
            if (path == null)
            {
                return Task.FromResult<IActionResult>(Json(ElFinderError("errCmdParams")));
            }

            var blobBase = (editorSettings.BlobPublicUrl ?? string.Empty).TrimEnd('/');
            var url = $"{blobBase}/{path.TrimStart('/')}";
            return Task.FromResult<IActionResult>(Json(new { url }));
        }

        private async Task<IActionResult> HandleDimAsync()
        {
            // Legacy path delegates to the CQRS handler via feature flag default.
            return Json(ElFinderError("errCmdNoSupport"));
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────
        private static string EncodeHash(string path) =>
            ElFinderHashEncoder.Encode(NormalizePath(path));

        private static string DecodeHash(string hash) =>
            ElFinderHashEncoder.Decode(hash) is string decoded ? NormalizePath(decoded) : null;

        private static string NormalizePath(string path)
        {
            var normalized = PublicFileEntryHelper.NormalizePath(path);
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

            return PublicFileEntryHelper.IsPathWithinRoot(path, RootPath);
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
            if (!normalizedParent.StartsWith("/pub/articles", StringComparison.OrdinalIgnoreCase))
            {
                // Don't bother with further processing.
                return items;
            }

            var titleResolver = new PublicFileEntryTitleResolver(dbContext);
            var tenantDomain = this.configProvider?.GetTenantDomainNameFromRequest() ?? string.Empty;
            await titleResolver.FilterDeletedArticleEntriesAsync(items, this.memoryCache, tenantDomain);
            var articleTitlesByNumber = await titleResolver.GetArticleTitlesByNumberAsync(items);
            foreach (var item in items)
            {
                if (item.IsDirectory
                    && PublicFileEntryHelper.TryGetArticleNumber(item, out var articleNumber)
                    && articleTitlesByNumber.TryGetValue(articleNumber, out var articleTitle)
                    && !string.IsNullOrWhiteSpace(articleTitle))
                {
                    item.Title = articleTitle;
                }
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
            if (!PublicFileEntryHelper.TryGetArticleNumberFromPath(normalizedPath, out var articleNumber))
            {
                return;
            }

            var titleResolver = new PublicFileEntryTitleResolver(dbContext);
            var titles = await titleResolver.GetArticleTitlesByNumberAsync(new[] { articleNumber });
            if (titles.TryGetValue(articleNumber, out var articleTitle) && !string.IsNullOrWhiteSpace(articleTitle))
            {
                entry.Title = articleTitle;
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
                if (PublicFileEntryHelper.TryGetArticleNumber(target, out var articleNumber))
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
                if (PublicFileEntryHelper.TryGetTemplateId(target, out var templateId))
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

            // GET FULL OR ABSOLUTE PATH � delegated to the shared FolderListingService.
            var tenantDomain = this.configProvider.GetTenantDomainNameFromRequest();
            var entries = await this.folderListingService.GetEntriesAsync(target, this.memoryCache, tenantDomain);
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

                if (this.editorSettings.UseModernFileExplorer)
                {
                    return View("~/Views/Shared/FileExplorer/index.cshtml", ddata);
                }

                return View(ddata);
            }

            var data = query.Skip(pageNo * pageSize).Take(pageSize).ToList();

            if (this.editorSettings.UseModernFileExplorer)
            {
                return View("~/Views/Shared/FileExplorer/index.cshtml", data);
            }

            return View("~/Views/Shared/FileExplorer/Index.cshtml", data);
        }

        /// <summary>
        /// Moves items to a new folder.
        /// </summary>
        /// <param name="model">Post model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        public async Task<IActionResult> Copy(MoveFilesViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Validate that the destination folder exists
                var destinationExists = await storageContext.BlobExistsAsync(model.Destination + "/folder.stubxx");
                if (!destinationExists)
                {
                    return BadRequest($"Destination folder '{model.Destination}' does not exist.");
                }

                foreach (var item in model.Items)
                {
                    string dest;

                    if (item.EndsWith("/"))
                    {
                        // copying a directory
                        var folderExists = await storageContext.BlobExistsAsync(item + "folder.stubxx");
                        if (!folderExists)
                        {
                            return BadRequest($"Source folder '{item}' does not exist.");
                        }

                        dest = model.Destination + item.TrimEnd('/').Split('/').LastOrDefault();
                    }
                    else
                    {
                        // copying a file
                        var fileExists = await storageContext.BlobExistsAsync(item);
                        if (!fileExists)
                        {
                            return BadRequest($"Source file '{item}' does not exist.");
                        }

                        var fileName = Path.GetFileName(item);
                        dest = model.Destination + "/" + fileName;
                    }

                    await storageContext.CopyAsync(item, dest);
                }
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }

            return Ok();
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
        /// Moves items to a new folder.
        /// </summary>
        /// <param name="model">Move file post model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        public async Task<IActionResult> Move(MoveFilesViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Validate that the destination folder exists
                var destinationExists = await storageContext.BlobExistsAsync(model.Destination + "/folder.stubxx");
                if (!destinationExists)
                {
                    return BadRequest($"Destination folder '{model.Destination}' does not exist.");
                }

                foreach (var item in model.Items)
                {
                    string dest;

                    if (item.EndsWith("/"))
                    {
                        // moving a directory
                        var folderExists = await storageContext.BlobExistsAsync(item + "folder.stubxx");
                        if (!folderExists)
                        {
                            return BadRequest($"Source folder '{item}' does not exist.");
                        }

                        dest = model.Destination + item.TrimEnd('/').Split('/').LastOrDefault();
                        await fileOperations.MoveFolderAsync(item, dest);
                    }
                    else
                    {
                        // moving a file
                        var fileExists = await storageContext.BlobExistsAsync(item);
                        if (!fileExists)
                        {
                            return BadRequest($"Source file '{item}' does not exist.");
                        }

                        var fileName = Path.GetFileName(item);
                        dest = model.Destination + "/" + fileName;
                        await fileOperations.MoveFileAsync(item, dest);
                    }
                }
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }

            return Ok();
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

            string relativePath = PublicFileEntryHelper.UrlEncodePath($"{directory}/{fileName}");

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

            var blobName = PublicFileEntryHelper.UrlEncodePath(uploadName);

            var relativePath = PublicFileEntryHelper.UrlEncodePath(patchArray[0].TrimEnd('/'));

            if (!string.IsNullOrEmpty(patchArray[1]))
            {
                var dpath = Path.GetDirectoryName(patchArray[1]).Replace('\\', '/'); // Convert windows paths to unix style.
                var epath = PublicFileEntryHelper.UrlEncodePath(dpath);
                relativePath += "/" + PublicFileEntryHelper.UrlEncodePath(epath);
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
            var basePath = PublicFileEntryHelper.UrlEncodePath(parts[0].TrimEnd('/'));
            var subDir = parts.Length > 1 ? parts[1].TrimStart('/') : string.Empty;

            string blobPath;
            if (!string.IsNullOrEmpty(subDir))
            {
                var dpath = Path.GetDirectoryName(subDir)?.Replace('\\', '/') ?? string.Empty;
                if (!string.IsNullOrEmpty(dpath))
                {
                    blobPath = $"{basePath}/{PublicFileEntryHelper.UrlEncodePath(dpath)}/{PublicFileEntryHelper.UrlEncodePath(fileName)}";
                }
                else
                {
                    blobPath = $"{basePath}/{PublicFileEntryHelper.UrlEncodePath(fileName)}";
                }
            }
            else
            {
                blobPath = $"{basePath}/{PublicFileEntryHelper.UrlEncodePath(fileName)}";
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

            string relativePath = PublicFileEntryHelper.UrlEncodePath(directory + fileName);

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
        /// Creates a new file in a given folder.
        /// </summary>
        /// <param name="model">New file post model.</param>
        /// <returns>IActionResult?</returns>
        public async Task<IActionResult> NewFile(NewFileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!FileStorageConstants.ValidEditorExtensions.Contains(Path.GetExtension(model.FileName).ToLower()))
            {
                return BadRequest("Invalid file extension.");
            }

            var relativePath = string.Join('/', PublicFileEntryHelper.ParsePath(model.ParentFolder, model.FileName));
            relativePath = PublicFileEntryHelper.UrlEncodePath(relativePath);

            // Check for duplicate entries
            var existingEntries = await storageContext.GetFilesAndDirectories(model.ParentFolder);

            if (!existingEntries.Exists(f => f.Name.Equals(model.FileName)))
            {
                using var memoryStream = new MemoryStream();
                await memoryStream.WriteAsync(Encoding.UTF8.GetBytes(string.Empty));
                await storageContext.AppendBlob(memoryStream, new FileUploadMetaData()
                {
                    ChunkIndex = 0,
                    ContentType = MimeTypeMap.GetMimeType(Path.GetExtension(model.FileName)),
                    FileName = model.FileName,
                    RelativePath = relativePath,
                    TotalChunks = 1,
                    TotalFileSize = memoryStream.Length,
                    UploadUid = Guid.NewGuid().ToString()
                });
            }

            return Ok();
        }

        /// <summary>
        /// New folder action.
        /// </summary>
        /// <param name="model">New folder model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewFolder(NewFolderViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(model.FolderName))
            {
                return BadRequest("Folder name is required.");
            }

            // Validate against path traversal attempts
            if (model.FolderName.Contains("..") || model.ParentFolder?.Contains("..") == true)
            {
                return BadRequest("Path traversal attempts are not allowed.");
            }

            var relativePath = string.Join('/', PublicFileEntryHelper.ParsePath(model.ParentFolder, model.FolderName));
            relativePath = PublicFileEntryHelper.UrlEncodePath(relativePath);

            // Check for duplicate entries
            var existingEntries = await storageContext.GetFilesAndDirectories(model.ParentFolder);

            if (!existingEntries.Exists(f => f.Name.Equals(model.FolderName)))
            {
                _ = storageContext.CreateFolder(relativePath);
            }

            return Ok();
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
        ///     Creates a new entry, using relative path-ing, and normalizes entry name to lower case.
        /// </summary>
        /// <param name="target">File or folder target.</param>
        /// <param name="entry">File manager entry model.</param>
        /// <returns><see cref="JsonResult" />(<see cref="FileManagerEntry" />).</returns>
        public async Task<ActionResult> Create(string target, FileManagerEntry entry)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            target = target == null ? string.Empty : target;
            entry.Path = target;
            entry.Name = PublicFileEntryHelper.UrlEncodePath(entry.Name);
            entry.Extension = entry.Extension;

            if (!entry.Path.StartsWith("/pub", StringComparison.CurrentCultureIgnoreCase))
            {
                return Unauthorized("New folders can't be created here using this tool. Please select the 'pub' folder and try again.");
            }

            // Check for duplicate entries
            var existingEntries = await storageContext.GetFilesAndDirectories(target);

            if (existingEntries != null && existingEntries.Any())
            {
                var results = existingEntries.FirstOrDefault(f => f.Name.Equals(entry.Name));

                if (results != null)
                {
                    // var i = 1;
                    var originalName = entry.Name;
                    for (var i = 0; i < existingEntries.Count; i++)
                    {
                        entry.Name = originalName + "-" + (i + 1);
                        if (!existingEntries.Any(f => f.Name.Equals(entry.Name)))
                        {
                            break;
                        }

                        i++;
                    }
                }
            }

            var relativePath = string.Join('/', PublicFileEntryHelper.ParsePath(entry.Path, entry.Name));
            relativePath = PublicFileEntryHelper.UrlEncodePath(relativePath);

            var fileManagerEntry = storageContext.CreateFolder(relativePath);

            return Json(fileManagerEntry);
        }

        /// <summary>
        ///     Deletes a folder, normalizes entry to lower case.
        /// </summary>
        /// <param name="model">Item to delete using relative path.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        public async Task<ActionResult> Delete(DeleteBlobItemsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            foreach (var item in model.Paths)
            {
                if (item.EndsWith('/'))
                {
                    await fileOperations.DeleteFolderAsync(item.TrimEnd('/'));
                }
                else
                {
                    storageContext.DeleteFile(item);
                }
            }

            return Ok();
        }

        /// <summary>
        /// Rename a blob item.
        /// </summary>
        /// <param name="model">Post view model.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rename(RenameBlobViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!string.IsNullOrEmpty(model.ToBlobName))
            {
                // Note rules:
                // 1. New folder names must end with slash.
                // 2. New file names must never end with a slash.
                if (model.FromBlobName.EndsWith("/"))
                {
                    if (!model.ToBlobName.EndsWith("/"))
                    {
                        model.ToBlobName = model.ToBlobName + "/";
                    }
                }
                else
                {
                    model.ToBlobName = model.ToBlobName.TrimEnd('/');
                }

                var target = $"{model.BlobRootPath.TrimEnd('/')}/{model.FromBlobName}";

                var dest = $"{model.BlobRootPath.TrimEnd('/')}/{PublicFileEntryHelper.UrlEncodePath(model.ToBlobName)}";

                // Skip move operation if source and destination are the same
                if (!target.Equals(dest, StringComparison.OrdinalIgnoreCase))
                {
                    await fileOperations.MoveFileAsync(target, dest);
                }
            }

            return Ok();
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

            var filter = new[] { ".png", ".jpg", ".gif", ".jpeg", ".webp" };

            if (!filter.Contains(extension))
            {
                throw new NotSupportedException($"Image type {extension} not supported.");
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

            if (!PublicFileEntryHelper.IsUploadPathSafe(path))
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
            if (!PublicFileEntryHelper.IsUploadPathSafe(fileMetaData.RelativePath) || fileMetaData.FileName?.Contains("..") == true)
            {
                return Unauthorized("Path traversal attempts are not allowed.");
            }

            // Validate against dangerous file extensions
            if (PublicFileEntryHelper.IsDangerousExtension(fileMetaData.FileName))
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

            var blobName = PublicFileEntryHelper.UrlEncodePath(fileMetaData.FileName);
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




