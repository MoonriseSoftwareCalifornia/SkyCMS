# SkyCMS .NET 10.0 Upgrade Tasks

## Overview

This document tracks the execution of the SkyCMS solution upgrade from .NET 9.0 to .NET 10.0 using an all-at-once approach. All 14 projects will be upgraded simultaneously in a single atomic operation, followed by comprehensive testing and a final commit.

**Progress**: 0/4 tasks complete (0%) ![0%](https://progress-bar.xyz/0)

---

## Tasks

### [▶] TASK-001: Verify prerequisites
**References**: Plan §Executive Summary, Plan §Migration Strategy §Prerequisites

- [▶] (1) Verify .NET 10.0 SDK installed on development machine
- [ ] (2) SDK version meets minimum requirements (**Verify**)
- [ ] (3) Check global.json compatibility (if present in repository)
- [ ] (4) Configuration files compatible with .NET 10.0 (**Verify**)

---

### [ ] TASK-002: Atomic framework and dependency upgrade with compilation fixes
**References**: Plan §Migration Strategy §All-At-Once Strategy Execution Principles, Plan §Project-by-Project Plans §Phase 0-5, Plan §Risk Management §Technology-Specific Risks, Plan §Success Criteria

- [ ] (1) Update target framework to net10.0 in all 14 project files per Plan §Project-by-Project Plans (AspNetCore.Identity.FlexDb, Cosmos.MicrosoftGraph, Cosmos.DynamicConfig, Cosmos.BlobService, Cosmos.Common, Cosmos.EmailServices, Sky.Shared.Razor, Sky.TestSetup, Sky.Cms.Api.Shared, Sky.Publisher, Sky.Editor, AspNetCore.Identity.FlexDb.Tests, Cosmos.Common.Tests, Sky.Tests)
- [ ] (2) All 14 project files updated to `<TargetFramework>net10.0</TargetFramework>` (**Verify**)
- [ ] (3) Update all 26 package references to net10.0-compatible versions per Plan §Project-by-Project Plans (focus: Microsoft.* packages to 10.0.5, EF Core 9.0.10→10.0.5)
- [ ] (4) All package references updated (**Verify**)
- [ ] (5) Remove incompatible package Microsoft.VisualStudio.Azure.Containers.Tools.Targets from Sky.Editor.csproj per Plan §Risk Management §Technology-Specific Risks
- [ ] (6) Incompatible package removed (**Verify**)
- [ ] (7) Run `dotnet restore SkyCMS.sln` to restore all dependencies
- [ ] (8) All dependencies restored successfully (**Verify**)
- [ ] (9) Run `dotnet build SkyCMS.sln --no-restore` to identify compilation errors
- [ ] (10) Build completed (errors expected at this stage) (**Verify**)
- [ ] (11) Fix all compilation errors systematically per Plan §Risk Management §Technology-Specific Risks (focus areas: TimeSpan API changes requiring double literals, GDI+/System.Drawing in Sky.Editor with 81 instances, binary compatibility issues, API signature changes)
- [ ] (12) Run `dotnet build SkyCMS.sln --no-restore` after fixes applied
- [ ] (13) Solution builds with 0 errors (**Verify**)

---

### [ ] TASK-003: Run full test suite and validate upgrade
**References**: Plan §Testing & Validation Strategy, Plan §Success Criteria §All Tests Pass

- [ ] (1) Run all test projects: `dotnet test SkyCMS.sln --no-build` (AspNetCore.Identity.FlexDb.Tests, Cosmos.Common.Tests, Sky.Tests)
- [ ] (2) Fix any test failures per Plan §Risk Management §Technology-Specific Risks (common issues: TimeSpan API changes, behavioral differences in .NET 10, test assertion updates)
- [ ] (3) Re-run all tests after fixes: `dotnet test SkyCMS.sln --no-build`
- [ ] (4) All tests pass with 0 failures (**Verify**)

---

### [ ] TASK-004: Final commit
**References**: Plan §Source Control Strategy §Commit Strategy

- [ ] (1) Commit all changes with message: "feat: Upgrade solution to .NET 10.0 - Update all 14 projects from net9.0 to net10.0 - Upgrade 26 NuGet packages to net10.0-compatible versions - Remove incompatible Microsoft.VisualStudio.Azure.Containers.Tools.Targets - Fix TimeSpan API and GDI+ compatibility - All 527 issues resolved, all tests passing"

---
