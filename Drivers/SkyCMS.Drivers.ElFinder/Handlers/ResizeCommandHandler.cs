using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MimeTypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SkyCMS.Drivers.ElFinder.Adapters;
using SkyCMS.Drivers.ElFinder.Commands;
using SkyCMS.Drivers.ElFinder.Responses;

namespace SkyCMS.Drivers.ElFinder.Handlers;

/// <summary>
/// Handles the "resize" command: resize, crop, or rotate an image using ImageSharp.
/// See Docs/commands/resize.md.
/// </summary>
public class ResizeCommandHandler : IRequestHandler<ResizeCommand, IElFinderResponse>
{
    private readonly IElFinderStorageAdapter _adapter;

    public ResizeCommandHandler(IElFinderStorageAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<IElFinderResponse> Handle(ResizeCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Target))
        {
            return ElFinderErrorResponse.InvalidParams("Target is required");
        }

        var sourcePath = _adapter.DecodePath(request.Target);
        if (sourcePath == null)
        {
            return ElFinderErrorResponse.InvalidParams("Invalid target hash");
        }

        if (!await _adapter.IsAccessibleAsync(sourcePath, cancellationToken))
        {
            return ElFinderErrorResponse.Access();
        }

        var stream = await _adapter.GetReadStreamAsync(sourcePath, cancellationToken);
        if (stream == null)
        {
            return ElFinderErrorResponse.NotFound();
        }

        using var outputStream = new MemoryStream();
        try
        {
            using (var image = await Image.LoadAsync(stream, cancellationToken))
            {
                ApplyTransform(image, request);

                var encoder = GetEncoder(sourcePath, request.Quality);
                await image.SaveAsync(outputStream, encoder, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            return ElFinderErrorResponse.Generic($"Image processing failed: {ex.Message}");
        }
        finally
        {
            await stream.DisposeAsync();
        }

        outputStream.Position = 0;

        // Save to copyName if provided, otherwise overwrite source.
        string destPath;
        if (!string.IsNullOrWhiteSpace(request.CopyName))
        {
            var parent = GetParentPath(sourcePath);
            destPath = parent.TrimEnd('/') + "/" + request.CopyName;
        }
        else
        {
            destPath = sourcePath;
        }

        var contentType = GetMimeType(destPath);
        var saved = await _adapter.UploadFileAsync(destPath, outputStream, contentType, cancellationToken);
        if (saved == null)
        {
            return ElFinderErrorResponse.Generic("Failed to save resized image");
        }

        var hash = _adapter.EncodePath(destPath);
        var parentPath = GetParentPath(destPath);
        var phash = _adapter.EncodePath(parentPath);

        var response = new ResizeResponse();
        response.Changed.Add(new ElFinderObject
        {
            Hash = hash,
            PHash = phash,
            Name = saved.Name ?? Path.GetFileName(destPath),
            Size = saved.Size,
            Mime = contentType,
            Ts = new DateTimeOffset(saved.Modified).ToUnixTimeSeconds(),
            Read = 1,
            Write = 1,
            Locked = 0,
        });

        return response;
    }

    private static void ApplyTransform(Image image, ResizeCommand request)
    {
        var mode = request.Mode ?? "resize";

        if (string.Equals(mode, "rotate", StringComparison.OrdinalIgnoreCase))
        {
            image.Mutate(ctx => ctx.Rotate(request.Degree));
            return;
        }

        if (string.Equals(mode, "crop", StringComparison.OrdinalIgnoreCase))
        {
            var cropRect = new Rectangle(request.X, request.Y, request.Width, request.Height);
            image.Mutate(ctx => ctx.Crop(cropRect));
            return;
        }

        // Default: resize
        if (request.Width > 0 && request.Height > 0)
        {
            image.Mutate(ctx => ctx.Resize(request.Width, request.Height));
        }
        else if (request.Width > 0)
        {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(request.Width, 0),
                Mode = ResizeMode.Max,
            }));
        }
        else if (request.Height > 0)
        {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(0, request.Height),
                Mode = ResizeMode.Max,
            }));
        }
    }

    private static SixLabors.ImageSharp.Formats.IImageEncoder GetEncoder(string path, int quality)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
            {
                Quality = quality is > 0 and <= 100 ? quality : 85,
            },
            ".png" => new SixLabors.ImageSharp.Formats.Png.PngEncoder(),
            ".gif" => new SixLabors.ImageSharp.Formats.Gif.GifEncoder(),
            ".webp" => new SixLabors.ImageSharp.Formats.Webp.WebpEncoder(),
            _ => new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 85 },
        };
    }

    private static string GetParentPath(string path)
    {
        var trimmed = path.TrimEnd('/');
        var slash = trimmed.LastIndexOf('/');
        return slash >= 0 ? trimmed[..(slash + 1)] : "/";
    }

    private static string GetMimeType(string fileName)
    {
        try
        {
            return MimeTypeMap.GetMimeType(Path.GetExtension(fileName));
        }
        catch
        {
            return "application/octet-stream";
        }
    }
}
