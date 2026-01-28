# SkyCMS Architecture (summary)

Short, focused overview to help contributors and AI agents understand the repository layout and design decisions.

- Top-level: a multi-project .NET solution (`SkyCMS.sln`) composed of several projects:
  - `Editor` / `Sky.Editor` — the web editor and UI, static assets under `Editor/wwwroot` and frontend package metadata in `Editor/package.json`.
  - `Sky.Api`, `Sky.Publisher`, `Sky.Cms.Api.Shared` — backend APIs, publisher, and shared server-side code.
  - `Cosmos.*` projects — Cosmos DB storage, connection-string wiring, and storage helpers (e.g., `Cosmos.ConnectionStrings`).
  - `Common`, `Scripts`, `InstallScripts` — utilities, deployment helpers, and tooling scripts.

- Why this structure:
  - Clear separation between the editor UI and server APIs reduces coupling and allows independent deployment.
  - Storage-specific code is grouped under `Cosmos.*` to centralize cloud-specific concerns and make it possible to swap implementations.
  - Central package/version control via `Directory.Packages.props` to keep dependency versions consistent across projects.

- Key data / request flow (common):
  1. Browser editor UI (`Editor/wwwroot`) issues API requests to `Sky.Api` / `Sky.Publisher`.
  2. Server-side handlers/controllers delegate work to services and repositories in shared projects (e.g., `Sky.Cms.Api.Shared`, `Common`).
  3. Persistence operations route through `Cosmos.*` projects which encapsulate Cosmos DB client configuration and queries.

- Important operational points:
  - Tenant / multi-tenant isolation is implemented early in middleware and by tenant-aware services (search `Program.cs` and `Boot/*` in `Editor`).
  - CI and releases are defined in `.github/workflows` (build, tests, NuGet push, Docker image pipelines). Do not change pipelines without approval.
  - Secrets and sensitive automation live in `UploadSecretsToGithubRepo.ps1` and CI settings — avoid editing these without explicit permission.

See the per-project READMEs for run and development commands.
