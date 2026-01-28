# Sky.Api — Developer README

Purpose
- Backend API project providing public and internal HTTP endpoints used by the editor and public sites.

Quick start (local)
```powershell
dotnet build SkyCMS.sln
dotnet run --project Sky.Api
```

Where to look
- Entry point and middleware: `Sky.Api/Program.cs`.
- Controllers: `Sky.Api/Controllers`.
- Shared DTOs and features used by other projects: `Sky.Cms.Api.Shared`.

Tests
- Run all tests or target the solution tests:
```powershell
dotnet test SkyCMS.sln
```

Notes & conventions
- Keep controllers thin; delegate business logic to services and handlers in shared projects.
- Respect analyzer rules (`stylecop.json`) and repository conventions in `Directory.Packages.props`.
- Ask before changing CI, `Directory.Packages.props`, or global package versions.
