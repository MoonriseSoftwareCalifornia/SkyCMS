// <copyright file="ElFinderConnectorController.cs" company="Moonrise Software, LLC">
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
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Services.Caching;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using MimeTypes;
    using Sky.Cms.Models;
    using Sky.Editor.Services.EditorSettings;
    using SkyCMS.Drivers.ElFinder.Commands;
    using SkyCMS.Drivers.ElFinder.Responses;
    using SkyCMS.Drivers.ElFinder;

    /// <summary>
    /// Connector adapter controller that maps elFinder JSON protocol commands to SkyCMS
    /// storage operations. Business commands are handled here; cross-cutting concerns
    /// (tenancy, authentication) remain in middleware and the standard pipeline.
    /// See ADR 0035.
    /// </summary>
    [Authorize(Roles = "Administrators, Editors, Authors, Team Members")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class ElFinderConnectorController : BaseController
    {
        private const string VolumeId = ElFinderHashEncoder.VolumeId;
        private const string RootPath = "/pub";

        private readonly IStorageContext storageContext;
        private readonly IEditorSettings editorSettings;
        private readonly ILogger<ElFinderConnectorController> logger;
        private readonly IConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="ElFinderConnectorController"/> class.
        /// </summary>
        /// <param name="dbContext">Database context (required by BaseController).</param>
        /// <param name="userManager">User manager (required by BaseController).</param>
        /// <param name="mediator">Mediator (required by BaseController).</param>
        /// <param name="layoutCache">Layout cache (required by BaseController).</param>
        /// <param name="storageContext">Storage context for file operations.</param>
        /// <param name="editorSettings">Editor settings (blob URL, flags).</param>
        /// <param name="logger">Logger.</param>
        /// <param name="configuration">Configuration.</param>
        [ActivatorUtilitiesConstructor]
        public ElFinderConnectorController(
            ApplicationDbContext dbContext,
            UserManager<IdentityUser> userManager,
            Cosmos.Common.Features.Shared.IMediator mediator,
            ICacheService<Layout> layoutCache,
            IStorageContext storageContext,
            IEditorSettings editorSettings,
            ILogger<ElFinderConnectorController> logger,
            IConfiguration configuration)
            : base(dbContext, userManager, mediator, layoutCache)
        {
            this.storageContext = storageContext;
            this.editorSettings = editorSettings;
            this.logger = logger;
            this.configuration = configuration;
        }

        /// <summary>
        /// Backward-compatible constructor for existing tests and call sites.
        /// </summary>
        public ElFinderConnectorController(
            ApplicationDbContext dbContext,
            UserManager<IdentityUser> userManager,
            Cosmos.Common.Features.Shared.IMediator mediator,
            ICacheService<Layout> layoutCache,
            IStorageContext storageContext,
            IEditorSettings editorSettings,
            ILogger<ElFinderConnectorController> logger)
            : this(
                dbContext,
                userManager,
                mediator,
                layoutCache,
                storageContext,
                editorSettings,
                logger,
                new ConfigurationBuilder().Build())
        {
        }

        // ─── Public endpoint ────────────────────────────────────────────────────

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
            var json = JsonSerializer.Serialize(response, response.GetType());
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

        private async Task<IActionResult> HandleTreeViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS tree requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleTreeAsync();
            }

            var command = new TreeCommand
            {
                Target = GetParam("target"),
                Filter = GetParam("filter"),
                VolumeId = VolumeId,
            };

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleMkdirViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS mkdir requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleMkdirAsync();
            }

            var target = GetParam("target");
            var name = GetParam("name");
            var path = DecodeHash(target);

            if (path == null || !IsAllowedPath(path))
            {
                return Json(ElFinderError("errAccess"));
            }

            var hasBatchDirs = Request.Form.ContainsKey("dirs[]");
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
                ? GetParams("dirs[]").Where(d => !string.IsNullOrWhiteSpace(d) && IsSafeName(d)).ToList()
                : null;

            var command = new MkdirCommand
            {
                Target = target,
                Name = uniqueName,
                Dirs = batchDirs,
                VolumeId = VolumeId,
            };

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleMkfileViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS mkfile requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleMkfileAsync();
            }

            var target = GetParam("target");
            var name = GetParam("name");
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

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandleRenameViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS rename requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleRenameAsync();
            }

            var target = GetParam("target");
            var name = GetParam("name");
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

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            return mappedError ?? JsonCqrs(response);
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
                }
            }

            return Json(new { removed = removed.Distinct().ToList() });
        }

        private async Task<IActionResult> HandleOpenViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS open requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleOpenAsync();
            }

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

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            return mappedError ?? JsonCqrs(response);
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
                var originalName = Path.GetFileName(file.FileName);
                if (string.IsNullOrWhiteSpace(originalName))
                {
                    continue;
                }

                var ext = Path.GetExtension(originalName).ToLowerInvariant();
                if (FileStorageConstants.DangerousFileExtensions.Contains(ext))
                {
                    return Json(ElFinderError("errUploadFile"));
                }

                var uniqueName = await GetUniqueNameAsync(path, originalName);

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
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS get requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandleGetAsync();
            }

            var command = new GetCommand
            {
                Target = GetParam("target"),
                VolumeId = VolumeId,
            };

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            return mappedError ?? JsonCqrs(response);
        }

        private async Task<IActionResult> HandlePutViaCqrsAsync()
        {
            var mediator = GetElFinderMediatorOrNull();
            if (mediator == null)
            {
                logger.LogWarning("elFinder CQRS put requested but MediatR.IMediator is not registered; falling back to legacy handler.");
                return await HandlePutAsync();
            }

            var command = new PutCommand
            {
                Target = GetParam("target"),
                Content = GetParam("content"),
                VolumeId = VolumeId,
            };

            var response = await mediator.Send(command);
            var mappedError = TranslateCqrsErrorToLegacy(this, response);
            return mappedError ?? JsonCqrs(response);
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
            var json = JsonSerializer.Serialize(response);
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

            var items = await storageContext.GetFilesAndDirectories(path);
            var fileObjects = items.Select(e => ToElFinderObject(e, EncodeHash(path))).ToList();

            // Build the cwd object for the directory being opened
            FileManagerEntry cwdEntry;
            try
            {
                cwdEntry = await storageContext.GetFileAsync(path);
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
                        var siblingItems = await storageContext.GetFilesAndDirectories(ancestorParent);
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
                        var cwdSiblings = await storageContext.GetFilesAndDirectories(cwdParent);
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

            var response = new Dictionary<string, object>
            {
                ["cwd"] = cwdObject,
                ["files"] = allFiles,
                ["options"] = BuildOptions(path),
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

            var items = await storageContext.GetFilesAndDirectories(path);
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
            var name = GetParam("name");
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
            var entry = await storageContext.CreateFolder(newPath.TrimStart('/'));

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
            var name = GetParam("name");
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
            var name = GetParam("name");
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
                entry = await storageContext.GetFileAsync(path);
            }
            catch
            {
                entry = null;
            }

            var isDir = entry?.IsDirectory ?? path.EndsWith("/");

            if (isDir)
            {
                await storageContext.MoveFolderAsync(path, newPath);
            }
            else
            {
                await storageContext.MoveFileAsync(path, newPath);
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

            foreach (var t in targets)
            {
                var path = DecodeHash(t);
                if (path == null || !IsAllowedPath(path))
                {
                    continue;
                }

                FileManagerEntry entry;
                try
                {
                    entry = await storageContext.GetFileAsync(path);
                }
                catch
                {
                    entry = null;
                }

                var isDir = entry?.IsDirectory ?? false;

                if (isDir)
                {
                    await storageContext.DeleteFolderAsync(path);
                }
                else
                {
                    await storageContext.DeleteFileAsync(path);
                }

                removed.Add(t);
            }

            return Json(new { removed });
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
                var fileName = Path.GetFileName(file.FileName);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
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
                entry = await storageContext.GetFileAsync(path);
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
                    entry = await storageContext.GetFileAsync(srcPath);
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
                        await storageContext.MoveFolderAsync(srcPath, newPath);
                    }
                    else
                    {
                        await storageContext.MoveFileAsync(srcPath, newPath);
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
                    var entry = await storageContext.GetFileAsync(path);
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
                            var e = await storageContext.GetFileAsync(fp);
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
                    var items = await storageContext.GetFilesAndDirectories(parent);
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
                var targetChildren = await storageContext.GetFilesAndDirectories(path);
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
            var desired = requestedName?.Trim();
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
                    var newEntry = await storageContext.GetFileAsync(destPath);
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
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return "/";
            }

            return "/" + string.Join("/", segments);
        }

        private static string GetParentPath(string path)
        {
            var trimmed = NormalizePath(path);
            if (string.IsNullOrEmpty(trimmed) || trimmed == "/")
            {
                return "/";
            }

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

            path = NormalizePath(path);

            // Block path traversal
            if (path.Contains(".."))
            {
                return false;
            }

            var normalised = NormalizePath(path);
            return normalised == RootPath || normalised.StartsWith(RootPath + "/");
        }

        private static bool IsSafeName(string name)
        {
            return !name.Contains('/') && !name.Contains('\\') && !name.Contains("..") && !name.Contains('\0');
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

        private Dictionary<string, object> BuildOptions(string path)
        {
            var blobBase = editorSettings.BlobPublicUrl.TrimEnd('/');
            var humanPath = NormalizePath(path).TrimStart('/');

            return new Dictionary<string, object>
            {
                ["path"] = humanPath,
                ["url"] = $"{blobBase}/{humanPath.TrimEnd('/')}/",
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

        private static string GetDisplayName(FileManagerEntry entry)
        {
            if (entry.IsDirectory)
            {
                return entry.Name ?? string.Empty;
            }

            var name = entry.Name ?? string.Empty;
            var ext = entry.Extension ?? string.Empty;

            if (!string.IsNullOrEmpty(ext) && !ext.StartsWith('.'))
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
    }
}
