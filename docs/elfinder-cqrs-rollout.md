# elFinder CQRS Rollout Guide

## Status: **Complete — all commands on CQRS in all environments**

All 15 elFinder commands are now routed through CQRS handlers in production.
The legacy code path is preserved as a silent fallback (triggered only if
MediatR cannot be resolved from the DI container).

---

## Configuration Reference

```json
"ElFinder": {
  "Cqrs": {
    "Enabled": true
  }
}
```

Per-command overrides are still supported for targeted rollback:

```json
"ElFinder": {
  "Cqrs": {
    "Enabled": true,
    "Commands": {
      "rm": false
    }
  }
}
```

| Key | Type | Effect |
|-----|------|--------|
| `ElFinder:Cqrs:Enabled` | `bool` | Global switch. `true` = all commands use CQRS unless overridden. |
| `ElFinder:Cqrs:Commands:<cmd>` | `bool` | Per-command override. Takes precedence over the global flag. |

### Evaluation order

1. Per-command config key (`ElFinder:Cqrs:Commands:<cmd>`) — highest priority.
2. Global `ElFinder:Cqrs:Enabled` flag.
3. Query-string flags (`__cqrs=1` or `__cqrs_<cmd>=1`) — for ad-hoc testing.
4. Default: **legacy path**.

If MediatR cannot be resolved from DI, every CQRS path silently falls back to
the legacy implementation — misconfiguration is safe.

---

## Rollout History

### Phase 1 — Read-only commands ✅
`tree`, `ls`, `size`, `tmb`, `info`, `parents` — enabled in development first.

### Phase 2 — `open` + `get` ✅
Read commands with more complex response shapes validated.

### Phase 3 — Non-destructive write commands ✅
`mkdir`, `mkfile`, `upload`, `put`, `paste` — enabled in development, then production.

### Phase 4 — Destructive write commands ✅
`rename`, `rm` — enabled after Phase 3 stability confirmed.

### Phase 5 — Production global flip ✅
`ElFinder:Cqrs:Enabled: true` in `appsettings.json`. All commands on CQRS.

---

## Ad-hoc Testing

Append query-string flags to any elFinder connector request to force/override routing:

| Flag | Effect |
|------|--------|
| `?__cqrs=1` | All commands use CQRS (no-op when already globally enabled) |
| `?__cqrs_<cmd>=1` | Force a specific command to CQRS |
| `?__cqrs_<cmd>=0` | Not supported — use config `Commands:<cmd>: false` to disable |

---

## Handler Inventory

| elFinder Command | CQRS Handler | Production |
|------------------|-------------|------------|
| `open` | `OpenCommandHandler` | ✅ on |
| `tree` | `TreeCommandHandler` | ✅ on |
| `ls` | `LsCommandHandler` | ✅ on |
| `mkdir` | `MkdirCommandHandler` | ✅ on |
| `mkfile` | `MkfileCommandHandler` | ✅ on |
| `rename` | `RenameCommandHandler` | ✅ on |
| `rm` | `RmCommandHandler` | ✅ on |
| `upload` | `UploadCommandHandler` | ✅ on |
| `get` | `GetCommandHandler` | ✅ on |
| `put` | `PutCommandHandler` | ✅ on |
| `paste` | `PasteCommandHandler` | ✅ on |
| `tmb` | `TmbCommandHandler` | ✅ on |
| `info` | `InfoCommandHandler` | ✅ on |
| `size` | `SizeCommandHandler` | ✅ on |
| `parents` | `ParentsCommandHandler` | ✅ on |

---

## Rollback

To revert a single command to the legacy path without redeployment:

```json
"ElFinder": {
  "Cqrs": {
    "Enabled": true,
    "Commands": {
      "rm": false
    }
  }
}
```

To revert all commands globally:

```json
"ElFinder": {
  "Cqrs": {
    "Enabled": false
  }
}
```

Restart the application after any config change. No code deployment required.
