# Copilot Instructions

## General Guidelines
- Follow existing project patterns and be concise in changes.
- Preserve compatibility across MS SQL, MySQL, SQLite, and Azure Cosmos DB for AspNetCore.Identity.FlexDb changes; avoid provider-specific EF behavior unless guarded by provider-aware strategies and tests.
- When updating EF queries, preserve cross-provider compatibility for SQLite, MySQL, and MS SQL; avoid provider-specific behavior.
- All LINQ queries throughout the application must support Cosmos DB as a database provider. Cosmos DB does not support joins between different entity types (cross-container joins). Queries must be rewritten to avoid Join operations — use sequential queries with client-side correlation instead.
- All LINQ queries used with Entity Framework must avoid inline casts (e.g., `(int)SomeEnum.Value`) inside lambda/predicate expressions. The Cosmos DB EF Core provider cannot translate inline casts within expression trees. Always pre-compute enum-to-int conversions (and similar casts) into local variables before the query, then reference those variables in the predicate. This applies solution-wide to maintain cross-provider compatibility (MS SQL, MySQL, SQLite, and Cosmos DB).
- In this codebase, ArticleNumber is a Cosmos DB partition key; query patterns should preserve equality filtering on ArticleNumber for efficient cross-provider-compatible access.
- Provide clear in-chat progress updates in small steps while working so the user can tell the task is still active.
- Prefer vendor-neutral AI naming (e.g., AiSettings, AiProxyController) instead of Copilot-branded names in the codebase.
- In multi-tenant mode, background scheduler logic must not resolve ApplicationDbContext from DI/HTTP request context; iterate tenant connections and run per-tenant using explicit connection-based DbContext creation.

## Content Management Guidelines
- For elFinder article paths, update Name to article title only when listing direct children under `/pub/articles`; for deeper paths like `/pub/articles/{id}/sub-folder`, keep Name unchanged and only adjust DisplayPath.

## Testing Guidelines
- Create richer Copilot integration tests with varied request types; a single simple hello-world style test is not sufficient.
- Workflow file names should describe when and why tests run, not only speed labels, to improve maintainability and recall.

## Architecture Overview
- The SkyCMS multi-tenant architecture utilizes the following components:
  - **IDynamicConfigurationProvider** (singleton) for tenant resolution via headers (x-origin-hostname priority over Host header).
  - **Per-request scoped services** that inject the provider to get the current tenant.
  - **Cookie isolation** with CookieDomain claims in Sky.Editor.
  - **Early middleware** (DomainMiddleware) to establish tenant context.
  - **Settings queries** filtered by tenant domain.
  - **Rate limiter policy** "contact-form" already configured (3 req/5min in production, 20 req/1min in development).
  - **Antiforgery tokens** automatically scoped per HttpContext (per-tenant).

## Planned Improvement Initiative
- There is an ongoing architecture-hardening initiative to narrow unnecessary `public` visibility in class libraries.
- Preferred execution model: dedicated branch and one library-focused PR at a time.
- First candidate library: `Cosmos.MicrosoftGraph`; treat `Cosmos.ConnectionStrings` and `Common/Cosmos.Common` as later/high-coupling phases.
- Keep changes behavior-preserving and visibility-focused; do not mix unrelated refactors.
- Validate with solution build and relevant tests before merge.

---

## Purpose
Provide concise, actionable guidance for AI coding agents working in this repository so they can be immediately productive.

## Big Picture (short)
- Multi-tenant ASP.NET Core solution centered in `SkyCMS.sln` and `Program.cs`.
- Frontend/editor bundle lives in `Sky.Editor` (see `Sky.Editor.csproj` and `Editor/wwwroot`).
- Backend APIs and publisher code are in `Sky.Api`, `Sky.Publisher`, and `Sky.Cms.Api.Shared`.
- Persistence and cloud integrations are split into `Cosmos.*` projects — expect Cosmos DB patterns and connection-string wiring there.
- Central package/version management via `Directory.Packages.props`.

## Key, discoverable patterns (use these exactly)
- Tenant resolution: use `IDynamicConfigurationProvider` and do NOT rely on raw `Host` — prefer the provider (see `DomainMiddleware`).
- Per-request services are tenant-scoped; register as scoped and get tenant info from the provider.
- For multi-tenant features, create a scoped service that consumes `IApplicationDbContext` to load tenant-specific settings per request, then inject that service into controllers. Multi-tenant isolation for tenant-scoped data access is considered fully handled by `IApplicationDbContext`, so tests should not add extra tenant isolation assertions beyond that layer.
- Cookie isolation: `CookieDomain` claim controls editor cookies (affects `Sky.Editor`).
- Rate limiting: the policy named `contact-form` is defined and used; preserve its configuration when touching endpoints.
- Secret handling/CI: `UploadSecretsToGithubRepo.ps1` and `.github/workflows/*` contain CI/secret conventions — don't duplicate secrets in code.

## Developer workflows (concrete commands)
- Build solution: `dotnet build SkyCMS.sln`
- Run tests: `dotnet test SkyCMS.sln` or target test projects under `*/Tests/*` (e.g., `AspNetCore.Identity.FlexDb.Tests`).
- Run locally (API/web): `dotnet run --project Sky.Editor` or run `Program.cs` project via `launchSettings.json` in your IDE.
- Docker: compose files live at the repo root (`docker-compose.yml`, `docker-compose.override.yml`) and CI uses `.github/workflows/docker-image.yml`.
- Migrations / DB setup: see `AddMigrationScript.ps1` and `UploadSecretsToGithubRepo.ps1` for migration and secret upload patterns.

## Integration points & external dependencies
- Cosmos DB: `Cosmos.*` projects hold storage logic and connection strings; look for `Cosmos.ConnectionStrings` and use existing client patterns.
- Cloud deploy & CDN: Cloudflare secrets and deployment are referenced in `.github/CLOUDFLARE_SECRETS_SETUP.md` and `.github/workflows/deploy-docs-cloudflare.yml`.
- NuGet & packaging: CI publishes using `.github/workflows/NuGetPush.yml` — respect versions in `Directory.Packages.props`.

## Conventions to follow when editing
- Preserve DI registrations and lifetime scopes; prefer adding new scoped services over changing lifetimes.
- Tenant-aware data access must flow through `IDynamicConfigurationProvider` — search for that symbol when in doubt.
- Keep changes minimal and focused: fix root cause when practical, but avoid sweeping refactors without test coverage.
- Respect analyzer and formatting configuration in `stylecop.json` files (e.g., `Editor/stylecop.json`). Run `dotnet build` to surface StyleCop analyzer warnings (SA*). Use `dotnet format` or IDE formatting to fix style issues and avoid introducing new `SA*` warnings; if a rule needs suppression or adjustment, propose it and request approval.

## Useful places to inspect (quick grep targets)
- `Program.cs` — app startup and middleware ordering
- `DomainMiddleware` and `IDynamicConfigurationProvider` — tenant resolution
- `Sky.Editor` (project root) — editor UX and cookie domain usage
- `Cosmos.*` folders — storage patterns and connection strings
- `.github/workflows` — CI checks, build/test/deploy pipelines

## When adding features or fixes
- Add tests in the matching test project (follow existing test project patterns under `*/Tests/*`).
- Update `Directory.Packages.props` only if you need a new package version globally — prefer local package references otherwise.

## If uncertain, search for these files first
- `Program.cs`, `Directory.Packages.props`, `docker-compose.yml`, `launchSettings.json`, `UploadSecretsToGithubRepo.ps1`, `AddMigrationScript.ps1`, `Sky.Editor.csproj`, `SkyCMS.sln`.

## Editor Functionality
- When rewiring visual editor iframe/autosave save paths, editable content regions should route to `parent.saveEditorRegion` rather than `SavePageProperties`.
- For the blog banner image widget, ensure that banner image changes save immediately on upload or delete rather than waiting for a manual save.
- Prefer DRY implementation by sharing article display-path/title filtering logic between `FileManagerController.FilterEntries` and `VsCodeController.GetFilesList` rather than maintaining parallel logic.