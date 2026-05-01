# Command: `resize`

**Status:** 📋 TODO — planned, not yet implemented  
**Priority:** Medium  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#resize

---

## Purpose

Performs server-side image manipulation: resize, crop, or rotate. Triggered from the image editor panel in the elFinder UI. The result overwrites the original file (or creates a copy — see `copyname`).

---

## HTTP

```
POST ?cmd=resize&target={hash}&mode={resize|crop|rotate}&width={w}&height={h}&x={x}&y={y}&degree={d}&quality={q}&copyname={name}
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `target` | Yes | Hash of the image file to edit |
| `mode` | Yes | `resize`, `crop`, or `rotate` |
| `width` | For resize/crop | Target width in pixels |
| `height` | For resize/crop | Target height in pixels |
| `x` | For crop | Crop X offset |
| `y` | For crop | Crop Y offset |
| `degree` | For rotate | Rotation degrees (90, 180, 270) |
| `quality` | No | JPEG quality 0–100 (default 100) |
| `copyname` | No | If set, save result as a new file with this name instead of overwriting |

---

## Expected response

```json
{
  "changed": [
    {
      "hash":   "l1_cHViL2ltYWdlcy9sb2dvLnBuZw",
      "phash":  "l1_cHViL2ltYWdlcw",
      "name":   "logo.png",
      "mime":   "image/png",
      "ts":     1714305000,
      "size":   38000,
      "read":   1,
      "write":  1,
      "locked": 0
    }
  ]
}
```

If `copyname` is set, use `added` (with new hash) instead of `changed`.

---

## Implementation notes

- Recommended library: `SixLabors.ImageSharp` (already MIT-licensed; check if present in solution).
- Flow: download blob → manipulate in-memory → upload result blob.
- `mode=rotate`: only 90° increments are needed for the elFinder UI (the degree input sends 90/180/270).
- `mode=resize`: preserve aspect ratio if only one dimension is non-zero.
- `mode=crop`: use `x`, `y`, `width`, `height` as rectangle parameters.
- Return `errResize` on image processing failure.

## Suggested implementation path

1. Confirm `SixLabors.ImageSharp` is available (or add to `Directory.Packages.props`).
2. Add `ResizeCommand : IRequest<IElFinderResponse>` with all parameters.
3. Add `ResizeCommandHandler` — download blob, apply transform, upload.
4. Add legacy fallback in controller.
5. Remove from `options.disabled`.
6. Add tests for each mode (resize/crop/rotate), copyname variant, unsupported MIME error.
