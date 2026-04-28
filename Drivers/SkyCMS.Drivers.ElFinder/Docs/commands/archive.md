# Command: `archive`

**Status:** ⛔ Disabled — no server-side archive support  
**Min API version:** 2.1029  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#archive

---

## Purpose

Creates a compressed archive (zip, tar.gz, etc.) from selected files/directories on the server.

---

## Why disabled

Server-side archive creation requires significant CPU and memory, and blob storage has no atomic multi-file bundling primitive. The client is already disabled from showing this option via `options.archivers.create: []` and `options.disabled`.

---

## Re-enabling

Not planned. If needed in the future:
- Use `System.IO.Compression.ZipArchive` with streaming to avoid memory pressure.
- Stream the ZIP directly to a temporary blob then return a download URL.
- See [`zipdl`](zipdl.md) as an alternative (client-side batch download).
