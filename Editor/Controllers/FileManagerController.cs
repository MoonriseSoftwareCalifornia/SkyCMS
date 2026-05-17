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

    /// <summary>
    /// File manager controller.
    /// </summary>
    // [ResponseCache(NoStore = true)]
    [Authorize(Roles = "Administrators, Editors, Authors, Team Members")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class FileManagerController : BaseController
    {
        // Private fields
        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<IdentityUser> userManager;
        private readonly ArticleEditLogic articleLogic;
        private readonly IMediator articleQueries;
        private readonly string blobPublicAbsoluteUrl;
        private readonly IViewRenderService viewRenderService;
        private readonly ILogger<FileManagerController> logger;
        private readonly IStorageContext storageContext;
        private readonly IWebHostEnvironment hostEnvironment;
        private readonly IEditorSettings options;
        private readonly IMemoryCache memoryCache;
        private readonly IDynamicConfigurationProvider configProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileManagerController"/> class.
        /// </summary>
        /// <param name="options">Cosmos options.</param>
        /// <param name="logger">Logger service.</param>
        /// <param name="dbContext">Database context.</param>
        /// <param name="storageContext">Storage context.</param>
        /// <param name="userManager">User manager context.</param>
        /// <param name="articleLogic">Article logic.</param>
        /// <param name="mediator">Shared article queries mediator.</param>
        /// <param name="hostEnvironment">Host environment.</param>
        /// <param name="viewRenderService">View rendering service.</param>
        /// <param name="memoryCache">Memory cache for layout caching.</param>
        /// <param name="configProvider">Dynamic configuration provider for tenant-aware caching.</param>
        /// <param name="appMemoryCache">Application memory cache for short-lived lookups (e.g. deleted-article filtering).</param>
        [ActivatorUtilitiesConstructor]
        public FileManagerController(
            IEditorSettings options,
            ILogger<FileManagerController> logger,
            ApplicationDbContext dbContext,
            IStorageContext storageContext,
            UserManager<IdentityUser> userManager,
            ArticleEditLogic articleLogic,
            IMediator mediator,
            IWebHostEnvironment hostEnvironment,
            IViewRenderService viewRenderService,
            ICacheService<Layout> memoryCache,
            IDynamicConfigurationProvider configProvider,
            IMemoryCache appMemoryCache)
            : base(dbContext, userManager, mediator, memoryCache, configProvider)
        {
            this.options = options;
            this.logger = logger;
            this.storageContext = storageContext;

            this.hostEnvironment = hostEnvironment;
            this.userManager = userManager;
            this.articleLogic = articleLogic;
            this.articleQueries = mediator;
            this.dbContext = dbContext;

            var htmlUtilities = new HtmlUtilities();

            blobPublicAbsoluteUrl = options.BlobPublicUrl.TrimStart('/');

            this.viewRenderService = viewRenderService;
            this.memoryCache = appMemoryCache;
            this.configProvider = configProvider;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileManagerController"/> class.
        /// </summary>
        /// <param name="options">Cosmos options.</param>
        /// <param name="logger">Logger service.</param>
        /// <param name="dbContext">Database context.</param>
        /// <param name="storageContext">Storage context.</param>
        /// <param name="userManager">User manager context.</param>
        /// <param name="articleLogic">Article logic.</param>
        /// <param name="mediator">Shared article queries mediator.</param>
        /// <param name="hostEnvironment">Host environment.</param>
        /// <param name="viewRenderService">View rendering service.</param>
        /// <param name="layoutCache">Layout cache service.</param>
        public FileManagerController(
            IEditorSettings options,
            ILogger<FileManagerController> logger,
            ApplicationDbContext dbContext,
            IStorageContext storageContext,
            UserManager<IdentityUser> userManager,
            ArticleEditLogic articleLogic,
            IMediator mediator,
            IWebHostEnvironment hostEnvironment,
            IViewRenderService viewRenderService,
            ICacheService<Layout> layoutCache)
            : base(dbContext, userManager, mediator, layoutCache)
        {
            this.options = options;
            this.logger = logger;
            this.storageContext = storageContext;

            this.hostEnvironment = hostEnvironment;
            this.userManager = userManager;
            this.articleLogic = articleLogic;
            this.articleQueries = mediator;
            this.dbContext = dbContext;

            var htmlUtilities = new HtmlUtilities();

            blobPublicAbsoluteUrl = options.BlobPublicUrl.TrimStart('/');

            this.viewRenderService = viewRenderService;
            this.memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        /// <summary>
        /// Gets a list of valid editor extensions.
        /// </summary>
        public static string[] ValidEditorExtensions => FileStorageConstants.ValidEditorExtensions;

        /// <summary>
        /// Gets a list of valid image extensions.
        /// </summary>
        public static string[] ValidImageExtensions => FileStorageConstants.ValidImageExtensions;

        /// <summary>
        /// Gets the file extensions that are not allowed for upload due to security concerns.
        /// </summary>
        public static string[] DangerousFileExtensions => FileStorageConstants.DangerousFileExtensions;

        /// <summary>
        /// Fixes the path for the image asset array method.
        /// </summary>
        /// <param name="path">Path to fix.</param>
        /// <returns>fixed path.</returns>
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

            return "/" + path.TrimStart('/'); // just in case
        }

        /// <summary>
        /// Gets images for the design editor.
        /// ///. </summary>
        /// <param name="storageContext">Storage context.</param>
        /// <param name="path">Path to retrieve images.</param>
        /// <param name="exclude">Path to exclude images.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public static async Task<string[]> GetImageAssetArray(IStorageContext storageContext, string path, string exclude)
        {
            var blobs = await storageContext.GetFilesAndDirectories(path);

            if (!string.IsNullOrEmpty(exclude))
            {
                return blobs.Where(w => FileManagerController.ValidImageExtensions.Contains(Path.GetExtension(w.Name).ToLower()) && !w.Path.ToLower().StartsWith(exclude.TrimStart('/').ToLower())).Select(s => new
                {
                    src = FixPath(s.Path),
                }).ToList().Select(s => s.src).ToArray();
            }

            return blobs.Where(w => FileManagerController.ValidImageExtensions.Contains(Path.GetExtension(w.Name).ToLower())).Select(s => new
            {
                src = FixPath(s.Path),
            }).ToList().Select(s => s.src).ToArray();
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

            // GET FULL OR ABSOLUTE PATH
            //
            // List<FileManagerEntry> model = await _storageContext.GetFolderContents(target);
            IQueryable<FileManagerEntry> query;
            if (target.Trim('/') == "pub/articles")
            {
                var raw = await dbContext.ArticleCatalog
                    .Select(s => new { s.ArticleNumber, s.Title, s.Updated })
                    .ToListAsync();
                var model = raw.Select(s => new FileManagerEntry()
                {
                    Created = s.Updated.DateTime,
                    CreatedUtc = s.Updated.UtcDateTime,
                    Extension = string.Empty,
                    HasDirectories = true,
                    IsDirectory = true,
                    Modified = s.Updated.DateTime,
                    ModifiedUtc = s.Updated.UtcDateTime,
                    Name = s.Title,
                    Path = "/pub/articles/" + s.ArticleNumber,
                    DisplayPath = "/pub/articles/" + s.Title,
                    Size = 0
                });
                query = model.AsQueryable();
            }
            else if (target.Trim('/') == "pub/templates")
            {
                var raw = await dbContext.Templates
                    .Select(s => new { s.Id, s.Title })
                    .ToListAsync();
                var now = DateTimeOffset.UtcNow.DateTime;
                var model = raw.Select(s => new FileManagerEntry()
                {
                    Created = now,
                    CreatedUtc = now,
                    Extension = string.Empty,
                    HasDirectories = true,
                    IsDirectory = true,
                    Modified = now,
                    ModifiedUtc = now,
                    Name = s.Title,
                    Path = "/pub/templates/" + s.Id,
                    DisplayPath = "/pub/templates/" + s.Title,
                    Size = 0
                });
                query = model.AsQueryable();
            }
            else
            {
                var model = await storageContext.GetFilesAndDirectories(target);
                if (target.Trim('/').StartsWith("pub/articles", StringComparison.OrdinalIgnoreCase))
                {
                    var titleResolver = new PublicFileEntryTitleResolver(dbContext);
                    var tenantDomain = this.configProvider.GetTenantDomainNameFromRequest();
                    await titleResolver.FilterDeletedArticleEntriesAsync(model, this.memoryCache, tenantDomain);
                }

                query = model.AsQueryable();
            }

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

                if (this.options.UseModernFileExplorer)
                {
                    return View("~/Views/Shared/FileExplorer/IndexModern.cshtml", ddata);
                }

                return View(ddata);
            }

            var data = query.Skip(pageNo * pageSize).Take(pageSize).ToList();

            if (this.options.UseModernFileExplorer)
            {
                return View("~/Views/Shared/FileExplorer/IndexModern.cshtml", data);
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
                        await storageContext.MoveFolderAsync(item, dest);
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
                        await storageContext.MoveFileAsync(item, dest);
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

            var blobEndPoint = options.BlobPublicUrl.TrimEnd('/');
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

            string relativePath = UrlEncode($"{directory}/{fileName}");

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

            var blobName = UrlEncode(uploadName);

            var relativePath = UrlEncode(patchArray[0].TrimEnd('/'));

            if (!string.IsNullOrEmpty(patchArray[1]))
            {
                var dpath = Path.GetDirectoryName(patchArray[1]).Replace('\\', '/'); // Convert windows paths to unix style.
                var epath = UrlEncode(dpath);
                relativePath += "/" + UrlEncode(epath);
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
                    await storageContext.CreateFolder(folder);
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
            var basePath = UrlEncode(parts[0].TrimEnd('/'));
            var subDir = parts.Length > 1 ? parts[1].TrimStart('/') : string.Empty;

            string blobPath;
            if (!string.IsNullOrEmpty(subDir))
            {
                var dpath = Path.GetDirectoryName(subDir)?.Replace('\\', '/') ?? string.Empty;
                if (!string.IsNullOrEmpty(dpath))
                {
                    blobPath = $"{basePath}/{UrlEncode(dpath)}/{UrlEncode(fileName)}";
                }
                else
                {
                    blobPath = $"{basePath}/{UrlEncode(fileName)}";
                }
            }
            else
            {
                blobPath = $"{basePath}/{UrlEncode(fileName)}";
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
            var blobEndPoint = options.BlobPublicUrl.TrimEnd('/');
            var fileName = $"{Guid.NewGuid().ToString().ToLower()}{extension}";

            var image = await Image.LoadAsync(file.OpenReadStream());

            string relativePath = UrlEncode(directory + fileName);

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

            var serializer = new JsonSerializer();
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
        ///     Encodes a URL.
        /// </summary>
        /// <param name="path">URL path to encode.</param>
        /// <returns>Returns a URL Encoded string.</returns>
        /// <remarks>
        ///     For more information, see
        ///     <a
        ///         href="https://docs.microsoft.com/en-us/rest/api/storageservices/Naming-and-Referencing-Containers--Blobs--and-Metadata#blob-names">
        ///         documentation
        ///     </a>
        ///     .
        /// </remarks>
        public string UrlEncode(string path)
        {
            if (!ModelState.IsValid)
            {
                return string.Empty;
            }

            var parts = ParsePath(path);
            var urlEncodedParts = new List<string>();
            foreach (var part in parts)
            {
                urlEncodedParts.Add(HttpUtility.UrlEncode(part.Replace(" ", "-")).Replace("%40", "@"));
            }

            return TrimPathPart(string.Join('/', urlEncodedParts));
        }

        /// <summary>
        /// Creates a new file in a given folder.
        /// </summary>
        /// <param name="model">New file post model.</param>
        /// <returns>IActionResult。</returns>
        public async Task<IActionResult> NewFile(NewFileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!ValidEditorExtensions.Contains(Path.GetExtension(model.FileName).ToLower()))
            {
                return BadRequest("Invalid file extension.");
            }

            var relativePath = string.Join('/', ParsePath(model.ParentFolder, model.FileName));
            relativePath = UrlEncode(relativePath);

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

            var relativePath = string.Join('/', ParsePath(model.ParentFolder, model.FolderName));
            relativePath = UrlEncode(relativePath);

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

                var blob = await storageContext.GetFileAsync(path);

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
            entry.Name = UrlEncode(entry.Name);
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

            var relativePath = string.Join('/', ParsePath(entry.Path, entry.Name));
            relativePath = UrlEncode(relativePath);

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
                    await storageContext.DeleteFolderAsync(item.TrimEnd('/'));
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

                var dest = $"{model.BlobRootPath.TrimEnd('/')}/{UrlEncode(model.ToBlobName)}";

                // Skip move operation if source and destination are the same
                if (!target.Equals(dest, StringComparison.OrdinalIgnoreCase))
                {
                    await storageContext.MoveFileAsync(target, dest);
                }
            }

            return Ok();
        }

        /// <summary>
        ///     Parses out a path into a string array.
        /// </summary>
        /// <param name="pathParts">URL path as an arrayto parse out.</param>
        /// <returns>Processed path as an array.</returns>
        public string[] ParsePath(params string[] pathParts)
        {
            if (!ModelState.IsValid)
            {
                return new string[] { string.Empty };
            }

            if (pathParts == null)
            {
                return new string[] { };
            }

            var paths = new List<string>();

            foreach (var part in pathParts)
            {
                if (!string.IsNullOrEmpty(part))
                {
                    var split = part.Split("/");
                    foreach (var p in split)
                    {
                        if (!string.IsNullOrEmpty(p))
                        {
                            var path = TrimPathPart(p);
                            if (!string.IsNullOrEmpty(path))
                            {
                                paths.Add(path);
                            }
                        }
                    }
                }
            }

            return paths.ToArray();
        }

        /// <summary>
        ///     Trims leading and trailing slashes and white space from a path part.
        /// </summary>
        /// <param name="part">URL path part to trim.</param>
        /// <returns>Returns trimmed path.</returns>
        public string TrimPathPart(string part)
        {
            if (string.IsNullOrEmpty(part))
            {
                return string.Empty;
            }

            return part.Trim('/').Trim('\\').Trim();
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

                var filter = options.AllowedFileTypes.Split(',');
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

                var metaData = await storageContext.GetFileAsync(path);

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

            var filter = options.AllowedFileTypes.Split(',');
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

            if (string.IsNullOrEmpty(path) || path.Trim('/') == string.Empty)
            {
                return Unauthorized("Cannot upload here. Please select the 'pub' folder first, or sub-folder below that, then try again.");
            }

            // Validate against path traversal attempts
            if (path.Contains(".."))
            {
                return Unauthorized("Path traversal attempts are not allowed.");
            }

            // Get information about the chunk we are on.
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(metaData));

            var serializer = new JsonSerializer();
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
            if (fileMetaData.RelativePath?.Contains("..") == true || fileMetaData.FileName?.Contains("..") == true)
            {
                return Unauthorized("Path traversal attempts are not allowed.");
            }

            // Validate against dangerous file extensions
            var fileExtension = Path.GetExtension(fileMetaData.FileName).ToLower();
            if (DangerousFileExtensions.Contains(fileExtension))
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

            var blobName = UrlEncode(fileMetaData.FileName);
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
                    return Unauthorized("Must upload folders and files under /pub directory.");
                }

                part = $"{part}/{parts[i]}";
                await storageContext.CreateFolder(part.Trim('/'));
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
