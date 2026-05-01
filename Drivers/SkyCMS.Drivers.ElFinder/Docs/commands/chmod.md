# Command: `chmod`

**Status:** ⛔ Disabled — not applicable to blob storage  
**Min API version:** 2.1  
**Official docs:** https://github.com/Studio-42/elFinder/wiki/Client-Server-API-2.1#chmod

---

## Purpose

Changes Unix-style file permissions (read/write/execute bits) on filesystem items.

---

## Why disabled

Azure Blob Storage has no concept of Unix permissions. Access control is managed via Azure RBAC and Shared Access Signatures, not per-file permission bits. Disabled via `options.disabled`.

---

## Re-enabling

Not applicable for blob storage. Would only be relevant if a local filesystem volume were added.
