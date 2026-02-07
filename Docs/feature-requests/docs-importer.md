# Feature Request: Docs Importer for SkyCMS

**Is your feature request related to a problem? Please describe.**
We maintain documentation in `Docs` as Markdown and publish it separately. We want to reuse that content inside SkyCMS without manual copy/paste or re-authoring. Today, there is no automated path to import and keep docs in sync with SkyCMS.

**Describe the solution you'd like**
Create a first-party docs importer that ingests the repository's `Docs` folder and syncs it into SkyCMS as structured content. The importer should be idempotent (create/update/skip based on changes), preserve folder hierarchy as navigation, upload assets and rewrite links, and publish to the correct tenant using existing tenant resolution patterns.

**Describe alternatives you've considered**
- Manual content migration per release (high overhead).
- Separate docs repository with custom sync scripts (more complexity).

**Additional context**
- Dependencies: content types for docs, content upsert/publish API or internal service, media upload API, tenant resolution via `IDynamicConfigurationProvider`.
- Open questions: which content types to use, ingestion surface (CLI/background job/GitHub action), and where to define canonical navigation if not the folder tree.
- Template strategy: use a dedicated "Docs Page" template created/maintained in SkyCMS (DB). The layout provides site chrome; the template contains a placeholder DIV with a marker attribute (e.g., `data-ccms-md="true"`) where the importer injects converted Markdown HTML. Nesting: Layout -> Docs Page Template -> Injected Markdown HTML.
- Acceptance criteria addition: importer replaces the `data-ccms-md="true"` placeholder with the converted Markdown HTML at render time (or during import) without affecting the rest of the template.
- Proposed decisions:
	- Template key/name: `docs-page` (title in SkyCMS templates) and used by the importer to resolve `TemplateId`.
	- Import system user: `DocsImport:UserId` is a dedicated service account in SkyCMS (non-human), used as `UserId` for Create/Save commands.
	- API key rotation: support multiple valid keys (primary + previous) or allow a short grace window (documented in config) to avoid downtime during rotations.
	- Link rewriting: default to API-side rewriting (for consistency), with an option to disable and let the importer handle it.

**Draft workflow & API (for implementation planning)**
- GitHub Action triggers on changes under `Docs/**` and `.skycms/**`.
- Action detects adds/modifies/deletes/renames via `git diff --name-status`.
- Action converts Markdown to HTML and upserts changed pages via the import API.
- Deletes and renames are sent to dedicated endpoints.
- A `.skycms/docs-import-map.json` tracks `sourcePath` -> `hash` (and optional article number) for idempotency.

Example GitHub Action (draft):
```yaml
name: Docs Import

on:
	push:
		paths:
			- "Docs/**"
			- ".skycms/**"

jobs:
	import-docs:
		runs-on: ubuntu-latest
		steps:
			- name: Checkout
				uses: actions/checkout@v4
				with:
					fetch-depth: 0

			- name: Install deps
				run: |
					npm install -g markdown-it

			- name: Detect changes
				id: diff
				run: |
					git diff --name-status ${{ github.sha }} ${{ github.sha }}^ > diff.txt
					cat diff.txt

			- name: Build & upsert docs
				env:
					SKYCMS_API_URL: ${{ secrets.SKYCMS_API_URL }}
					SKYCMS_API_KEY: ${{ secrets.SKYCMS_API_KEY }}
					SKYCMS_TENANT_HOST: ${{ secrets.SKYCMS_TENANT_HOST }}
				run: |
					node .skycms/scripts/import-docs.js diff.txt
```

Example API endpoints (draft):
- `PUT /_api/import/docs/items/{sourceKey}`
- `DELETE /_api/import/docs/items/{sourceKey}`
- `POST /_api/import/docs/rename`
- `POST /_api/import/docs/assets` (multipart form file upload)

Example upsert payload (draft):
```json
{
	"title": "Getting Started",
	"urlPath": "docs/getting-started",
	"html": "<h1>Getting Started</h1>...",
	"templateKey": "docs-page",
	"published": true,
	"source": {
		"path": "Docs/Getting-Started/index.md",
		"hash": "sha256:..."
	}
}
```

Configuration (draft):
- `DocsImport:ApiKey` (or `DocsImportApiKey`) for API auth.
- `DocsImport:UserId` (or `DocsImportUserId`) for the system user performing imports.
- Use appsettings.json, environment variables, or secrets (per standard SkyCMS config patterns).

Security & roles (draft):
- Use a dedicated SkyCMS service account with minimal roles required to create/update/delete pages.
- Store `DocsImport:ApiKey` only in secrets or environment variables (not checked into repo).
- Log import activity with sourceKey, userId, and request metadata (excluding secrets).

Failure modes & retries (draft):
- Importer should retry transient API failures (HTTP 429/502/503) with backoff.
- If an asset upload fails, skip page upsert and mark file as failed for next run.
- If a page upsert fails, record the error and continue with remaining files (non-blocking).
- Provide a summary report (counts of created/updated/deleted/skipped/failed).

Implementation readiness checklist (spec gaps to finalize):
- Content mapping: use `ArticleType.General` for docs.
- URL rules: default to `docs/<path>` with `index.md` trimmed; allow optional custom `UrlPath` override in front matter.
- Template resolution: template lookup uses title `docs-page`.
- Placeholder behavior: markdown HTML is injected during import (placeholder replaced at save time).
- Front matter: support `title`, `summary`, `tags`, `nav_order`, `published`, and `url_path`; front matter overrides file name.
- Navigation: derived from folder tree; front matter `nav_order` controls ordering; optional `title` override used for nav labels.
- Asset policy: upload local assets to `/pub/docs` (same root folder as docs); external URLs untouched.
- Link rewriting: rewrite relative `href`/`src` to docs UrlPath and `/pub/docs` asset URLs; do not rewrite absolute URLs, `#` anchors, or `mailto:`/`tel:`.
- Deletes and renames: deletes are soft deletes via `DeleteArticle`; renames use Save + TitleChange redirect logic.
- Auth & rate limits: add a rate limit policy (match deployment-style guardrails).
- Limits: cap HTML payload at 1 MB and assets at 25 MB.
- Observability: basic logs per file outcome and duration.
- Tests: controller auth + smoke tests for create/update/delete/rename/link rewrite/asset upload path rules.
