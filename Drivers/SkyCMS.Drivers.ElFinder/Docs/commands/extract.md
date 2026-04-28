# Command: `extract`

**Status:** ⛔ Disabled — no server-side archive support  
**Min API version:** 2.1029  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#extract

---

## Purpose

Extracts an archive file (zip, tar.gz) into a target directory on the server.

---

## Why disabled

Counterpart to [`archive`](archive.md). Same reasons — no server-side archive tooling configured. Disabled via `options.archivers.extract: []` and `options.disabled`.

---

## Re-enabling

Not planned. If revisited, use `System.IO.Compression.ZipArchive` for ZIP extraction, streaming each entry directly to a blob upload.
