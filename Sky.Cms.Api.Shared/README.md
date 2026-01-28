# Sky.Cms.Api.Shared — Developer README

Purpose
- Shared models, DTOs, and feature code used across API projects (contracts and handler code that is reused by `Sky.Api` and other services).

Quick start (local)
```powershell
dotnet build SkyCMS.sln
```

Where to look
- Shared models and DTOs: `Sky.Cms.Api.Shared/Models`.
- Features and handlers: `Sky.Cms.Api.Shared/Features`.

Tests
- This project contains types used across multiple projects; run solution tests with:
```powershell
dotnet test SkyCMS.sln
```

Notes & conventions
- Keep shared contracts stable; changing DTOs can require coordinated client updates.
- Avoid adding heavy dependencies to shared projects — prefer lightweight, well-tested helpers.
- When adding new shared features, include unit tests and document intended consumers in this README.
