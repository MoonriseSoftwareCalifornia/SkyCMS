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
    using System.Threading.Tasks;
    using Cosmos.BlobService;
    using Cosmos.BlobService.Models;
    using Cosmos.Common.Data;
    using Cosmos.Common.Features.Shared;
    using Cosmos.Common.Services.Caching;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using MimeTypes;
    using Sky.Cms.Models;
    using Sky.Editor.Services.EditorSettings;

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
        private const string VolumeId = "l1_";
        private const string RootPath = "/pub";

        private readonly IStorageContext storageContext;
        private readonly IEditorSettings editorSettings;
        private readonly ILogger<ElFinderConnectorController> logger;

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
        public ElFinderConnectorController(
            ApplicationDbContext dbContext,
            UserManager<IdentityUser> userManager,
            IMediator mediator,
            ICacheService<Layout> layoutCache,
            IStorageContext storageContext,
            IEditorSettings editorSettings,
            ILogger<ElFinderConnectorController> logger)
            : base(dbContext, userManager, mediator, layoutCache)
        {
            this.storageContext = storageContext;
            this.editorSettings = editorSettings;
            this.logger = logger;
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
                    "open" => await HandleOpenAsync(),
                    "tree" => await HandleTreeAsync(),
                    "ls" => await HandleLsAsync(),
                    "mkdir" => await HandleMkdirAsync(),
                    "mkfile" => await HandleMkfileAsync(),
                    "rename" => await HandleRenameAsync(),
                    "rm" => await HandleRmAsync(),
                    "upload" => await HandleUploadAsync(),
                    "get" => await HandleGetAsync(),
                    "put" => await HandlePutAsync(),
                    "paste" => await HandlePasteAsync(),
                    "tmb" => await HandleTmbAsync(),
                    "info" => await HandleInfoAsync(),
                    "size" => await HandleSizeAsync(),
                    "parents" => await HandleParentsAsync(),
                    _ => Json(ElFinderError("errUnknownCmd"))
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ElFinder connector error handling command '{Cmd}'", cmd);
                return Json(ElFinderError(ex.Message));
            }
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

            var response = new Dictionary<string, object>
            {
                ["cwd"] = cwdObject,
                ["files"] = fileObjects,
                ["api"] = "2.1",
                ["uplMaxSize"] = "64M",
                ["options"] = BuildOptions(path),
            };

            if (isInit)
            {
                // On init, also inject the root volume object so the sidebar tree renders
                var rootObject = SyntheticDirObject(RootPath, null, isRoot: true);
                var allFiles = new List<object> { rootObject };
                allFiles.AddRange(fileObjects);
                response["files"] = allFiles;
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

            var items = await storageContext.GetFilesAndDirectories(path);
            var list = items.ToDictionary(
                e => EncodeHash(e.Path),
                e => e.Name + (e.IsDirectory ? string.Empty : e.Extension));

            return Json(new { list });
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

            var newPath = path.TrimEnd('/') + "/" + name;
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
            if (FileManagerController.DangerousFileExtensions.Contains(ext))
            {
                return Json(ElFinderError("errUploadFile"));
            }

            var filePath = path.TrimEnd('/') + "/" + name;
            var meta = new FileUploadMetaData
            {
                UploadUid = Guid.NewGuid().ToString(),
                FileName = name,
                RelativePath = filePath.TrimStart('/'),
                ContentType = MimeTypeMap.GetMimeType(ext),
                ChunkIndex = 0,
                TotalChunks = 1,
                TotalFileSize = 0,
            };

            await storageContext.AppendBlob(new MemoryStream(Array.Empty<byte>()), meta);

            var entry = BuildSyntheticFileEntry(filePath, name, ext, 0);
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
            var newPath = parentPath.TrimEnd('/') + "/" + name;

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

            var newEntry = BuildSyntheticFileEntry(newPath, Path.GetFileNameWithoutExtension(name), Path.GetExtension(name), entry?.Size ?? 0, isDir);
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
                if (FileManagerController.DangerousFileExtensions.Contains(ext))
                {
                    return Json(ElFinderError("errUploadFile"));
                }

                var filePath = path.TrimEnd('/') + "/" + fileName;
                var meta = new FileUploadMetaData
                {
                    UploadUid = Guid.NewGuid().ToString(),
                    FileName = fileName,
                    RelativePath = filePath.TrimStart('/'),
                    ContentType = MimeTypeMap.GetMimeType(ext),
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

                var entry = BuildSyntheticFileEntry(filePath, Path.GetFileNameWithoutExtension(fileName), ext, file.Length);
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
                if (FileManagerController.ValidImageExtensions.Contains(ext))
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

            var tree = new List<object>();
            var current = path;

            while (!string.IsNullOrEmpty(current) && current != "/" && current.Length > 1)
            {
                var parent = GetParentPath(current);
                if (parent == current)
                {
                    break;
                }

                try
                {
                    var items = await storageContext.GetFilesAndDirectories(parent);
                    foreach (var item in items.Where(i => i.IsDirectory))
                    {
                        tree.Add(ToElFinderObject(item, EncodeHash(parent)));
                    }
                }
                catch
                {
                    break;
                }

                current = parent;

                if (current.TrimEnd('/') == RootPath.TrimEnd('/'))
                {
                    break;
                }
            }

            return await Task.FromResult(Json(new { tree }));
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────
        private static string EncodeHash(string path)
        {
            var bytes = Encoding.UTF8.GetBytes(path.TrimStart('/'));
            return VolumeId + Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private static string DecodeHash(string hash)
        {
            if (string.IsNullOrEmpty(hash) || !hash.StartsWith(VolumeId))
            {
                return null;
            }

            var encoded = hash.Substring(VolumeId.Length)
                .Replace('-', '+')
                .Replace('_', '/');

            var padding = encoded.Length % 4;
            if (padding > 0)
            {
                encoded += new string('=', 4 - padding);
            }

            try
            {
                var bytes = Convert.FromBase64String(encoded);
                return "/" + Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        private static string GetParentPath(string path)
        {
            var trimmed = path.TrimEnd('/');
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

            // Block path traversal
            if (path.Contains(".."))
            {
                return false;
            }

            var normalised = "/" + path.Trim('/');
            return normalised == RootPath || normalised.StartsWith(RootPath + "/");
        }

        private static bool IsSafeName(string name)
        {
            return !name.Contains('/') && !name.Contains('\\') && !name.Contains("..") && !name.Contains('\0');
        }

        private object ToElFinderObject(FileManagerEntry entry, string parentHash)
        {
            var fullPath = entry.Path.StartsWith("/") ? entry.Path : "/" + entry.Path;
            var hash = EncodeHash(fullPath);
            var displayName = entry.IsDirectory ? entry.Name : (entry.Name + entry.Extension);
            var mime = entry.IsDirectory ? "directory" : GetMimeType(entry.Extension);
            var ts = new DateTimeOffset(entry.ModifiedUtc == default ? DateTime.UtcNow : entry.ModifiedUtc, TimeSpan.Zero)
                         .ToUnixTimeSeconds();
            var isRoot = fullPath.TrimEnd('/') == RootPath;

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

            if (!isRoot && parentHash != null)
            {
                obj["phash"] = parentHash;
            }

            if (isRoot)
            {
                obj["volumeid"] = VolumeId;
                obj["dirs"] = 1;
            }

            if (!entry.IsDirectory)
            {
                var blobBase = editorSettings.BlobPublicUrl.TrimEnd('/');
                obj["url"] = $"{blobBase}/{fullPath.TrimStart('/')}";

                var ext = (entry.Extension ?? string.Empty).ToLowerInvariant();
                if (FileManagerController.ValidImageExtensions.Contains(ext))
                {
                    obj["tmb"] = $"/FileManager/GetImageThumbnail?target={Uri.EscapeDataString(fullPath)}&width=80&height=80";
                }
            }

            return obj;
        }

        private object SyntheticDirObject(string path, string parentHash, bool isRoot)
        {
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

            if (isRoot)
            {
                obj["volumeid"] = VolumeId;
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
            var humanPath = path.TrimStart('/');

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
                ["uploadMaxConnections"] = 3,
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
    }
}
