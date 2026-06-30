# wwwroot CDN Candidate List

Date: 2026-05-23
Scope: `Editor/wwwroot` assets
Sources: `Editor/libman.json`, `Editor/wwwroot-asset-audit.csv`

## How to use this list

- This list is for assets that can be served from well-known CDNs instead of keeping full copies in `wwwroot`.
- Keep version pinning strict. Do not use floating versions.
- Use SRI hashes for production pages when possible.
- For each row, test in these flows before removing local files: Editor content, GrapesJS builder, Blog create/edit, File Explorer, Login/Home pages.

## High-confidence CDN candidates

| Asset | Local path | Source refs | Size (MB) | Suggested CDN (pinned) | Confidence | Notes |
|---|---|---:|---:|---|---|---|
| Bootstrap | `wwwroot/lib/bootstrap` | 2 | 8.59 | `https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css` and `https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js` | High | Already partly CDN-loaded in some views. Consolidate to one strategy. |
| Luxon | `wwwroot/lib/luxon` | 5 | 1.89 | `https://cdn.jsdelivr.net/npm/luxon@3.5.0/build/global/luxon.min.js` | High | Used in table/list screens. |
| Tabulator | `wwwroot/lib/tabulator-tables` | 10 | 28.43 | `https://cdn.jsdelivr.net/npm/tabulator-tables@6.3.1/dist/js/tabulator.min.js` and `https://cdn.jsdelivr.net/npm/tabulator-tables@6.3.1/dist/css/tabulator_midnight.min.css` | High | Large size win if moved. |
| CropperJS | `wwwroot/lib/cropperjs` | 2 | 0.30 | `https://cdn.jsdelivr.net/npm/cropperjs@2.0.1/dist/cropper.min.js` and `https://cdn.jsdelivr.net/npm/cropperjs@2.0.1/dist/cropper.min.css` | High | CSS currently commented in one view; verify expected styling. |
| PicoCSS | `wwwroot/lib/picocss` | 2 | 18.45 | `https://cdn.jsdelivr.net/npm/@picocss/pico@2.1.1/css/pico.conditional.min.css` | High | Very large local footprint for one file use. |
| Popper | `wwwroot/lib/popper` | 1 | 1.30 | `https://cdn.jsdelivr.net/npm/@popperjs/core@2.11.8/dist/umd/popper.min.js` | Medium | Local path currently `/lib/popper/umd/popper.min.js`; confirm compatibility. |
| Tippy.js | `wwwroot/lib/tippy` | 1 | 1.26 | `https://cdn.jsdelivr.net/npm/tippy.js@6.3.7/dist/tippy.umd.min.js` | Medium | Verify plugins/themes if used. |
| SignalR client | `wwwroot/lib/signalr` | 0 | 2.69 | `https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js` | Medium | No direct refs in current source scan; still keep until runtime check. |
| jQuery Validate | `wwwroot/lib/jquery-validate` | 0 | 0.43 | `https://cdn.jsdelivr.net/npm/jquery-validation@1.21.0/dist/jquery.validate.min.js` | Medium | Usually pulled by partials; confirm indirect usage. |
| jQuery Validate Unobtrusive | `wwwroot/lib/jquery-validation-unobtrusive` | 0 | 0.02 | `https://cdn.jsdelivr.net/npm/jquery-validation-unobtrusive@4.0.0/dist/jquery.validate.unobtrusive.min.js` | Medium | Pair with jquery-validate if enabled by validation partials. |

## CDN possible, but verify before moving

| Asset | Local path | Source refs | Size (MB) | Suggested CDN | Confidence | Why lower confidence |
|---|---|---:|---:|---|---|---|
| Monaco Editor | `wwwroot/lib/monaco` | 6 | 42.48 | `https://cdn.jsdelivr.net/npm/monaco-editor@0.53.0/min/vs` | Medium | Worker paths, CSP, offline behavior, and plugin integrations can break. |
| elFinder | `wwwroot/lib/elfinder` | 3 | 4.66 | `https://cdn.jsdelivr.net/npm/elfinder@2.1.66` | Low | Project may rely on local package shape; also themed assets nearby. |
| CKEditor assets | `wwwroot/lib/ckeditor` | 5 | 37.27 | CKEditor CDN exists | Low | This repo appears to use custom/local integrations and plugins. |

## Keep local (not practical CDN targets)

| Asset | Local path | Reason |
|---|---|---|
| SkyCMS custom assets | `wwwroot/lib/cosmos` | Project-specific code, not a public CDN library. |
| GrapesJS UI package | `wwwroot/lib/grapesjsui` | Custom fork/integration assets and images. |
| SkyCMS Grapes plugins | `wwwroot/lib/grapesjs/skycms-grapes-plugins.js` | Project-specific plugin build output. |
| Monaco integration glue | `wwwroot/lib/monaco-editor-integration` | Local integration scripts. |
| Theme assets | `wwwroot/lib/elfinder-material-theme` | Custom/local theme package. |
| Project app assets | `wwwroot/css`, `wwwroot/js`, `wwwroot/images`, `wwwroot/ccms` | Site/editor assets, not third-party CDN libraries. |

## Candidate sequence (recommended order)

1. Move `luxon`, `tabulator-tables`, `filepond`, `cropperjs` to CDN behind a feature flag.
2. Move `picocss` and `bootstrap` (if not already consistently CDN).
3. Move `popper` and `tippy` after tooltip regression check.
4. Decide on `signalr` and validation libraries based on runtime logs.
5. Evaluate `monaco`, `elfinder`, and `ckeditor` last.

## Safe migration pattern

1. Add CDN URL with version pin.
2. Keep local file as fallback for one release window.
3. Monitor for script/css load failures.
4. Remove local files only after zero regressions in editor workflows.

Example fallback pattern:

```html
<script src="https://cdn.jsdelivr.net/npm/luxon@3.5.0/build/global/luxon.min.js" integrity="..." crossorigin="anonymous"></script>
<script>window.luxon || document.write('<script src="/lib/luxon/luxon.min.js"><\\/script>')</script>
```
