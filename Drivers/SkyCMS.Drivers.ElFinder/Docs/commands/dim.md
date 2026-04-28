# Command: `dim`

**Status:** 📋 TODO — planned, not yet implemented  
**Priority:** Low  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#dim

---

## Purpose

Returns the pixel **dimensions** (width × height) of an image or video file. Used by the client's info/properties panel and by the image editor to initialise the resize form.

---

## HTTP

```
GET/POST ?cmd=dim&target={hash}
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `target` | Yes | Hash of the image or video file |

---

## Example request

```
GET /elfinder/connector?cmd=dim&target=l1_cHViL2ltYWdlcy9sb2dvLnBuZw
```

---

## Expected response

```json
{
  "dim": "1920x1080"
}
```

The value is a string in `{width}x{height}` format.

---

## Implementation notes

- For images: read just enough of the file to decode the header (no need to fully download large blobs). `SixLabors.ImageSharp.Image.Identify()` can do this from a stream.
- For video: full decode is impractical server-side — return `errUsupportType` or omit for video.
- Download only the first N bytes for image header detection to keep latency low.
- Cache results where possible (blob metadata `x-ms-meta-dim`).

## Suggested implementation path

1. Add `DimCommand : IRequest<IElFinderResponse>` (target).
2. Add `DimCommandHandler` — open partial blob stream, call `Image.Identify()`, return `{w}x{h}`.
3. Add legacy fallback in controller.
4. Remove from `options.disabled`.
5. Add tests: known PNG dimensions, non-image file returns error or empty, large image (header-only read).
