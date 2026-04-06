# .NET 10.0 Upgrade Plan for SkyCMS

## Table of Contents
- [Executive Summary](#executive-summary)
- [Migration Strategy](#migration-strategy)
- [Detailed Dependency Analysis](#detailed-dependency-analysis)
- [Project-by-Project Plans](#project-by-project-plans)
- [Risk Management](#risk-management)
- [Testing & Validation Strategy](#testing--validation-strategy)
- [Complexity & Effort Assessment](#complexity--effort-assessment)
- [Source Control Strategy](#source-control-strategy)
- [Success Criteria](#success-criteria)


---

## Executive Summary

### Scenario Description
Upgrading SkyCMS solution from .NET 9.0 to .NET 10.0 (Long Term Support).

### Scope
- **Total Projects**: 14 projects
- **Current State**: All projects targeting net9.0
- **Target State**: All projects targeting net10.0

### Discovered Metrics
- **Total Issues**: 527 (Mandatory: 112, Potential: 410, Optional: 5)
- **Affected Files**: 115 files across the solution
- **Dependency Depth**: 7 levels
- **Incompatible Packages**: 4 packages require attention
- **Packages Requiring Upgrade**: 26 packages
- **Affected Technologies**: GDI+ / System.Drawing (81 issues)

### Complexity Classification
**Medium Solution** - Phase-based approach recommended

**Justification**:
- 14 projects (medium size)
- Clear 7-level dependency hierarchy with no circular dependencies
- 1 high-risk project (Sky.Editor: 227 issues, 47 mandatory)
- 4 incompatible NuGet packages to resolve
- Significant API compatibility issues across multiple projects

### Selected Strategy
**All-At-Once Strategy** - All projects upgraded simultaneously in a single coordinated operation.

**Rationale**:
- All projects currently on .NET 9.0 (homogeneous starting point)
- Clear dependency structure with no cycles
- All projects can be upgraded to net10.0
- Assessment shows all critical NuGet packages have net10.0-compatible versions available
- Medium solution size is manageable for atomic upgrade
- Faster completion time compared to incremental approach
- Eliminates multi-targeting complexity

### Expected Iterations
This plan uses phase-based organization for clarity, with one detailed iteration per logical phase:
- Phase 1: Foundation projects (Levels 0-1)
- Phase 2: Core libraries (Levels 2-3)
- Phase 3: Service layer (Level 4)
- Phase 4: API and application layer (Levels 5-6)
- Phase 5: Test projects (Level 7)

Note: Phases organize content for human understanding; actual execution will be atomic (all projects upgraded simultaneously).

### Critical Issues
- **Incompatible Packages**:
  - Microsoft.VisualStudio.Azure.Containers.Tools.Targets (incompatible with net10.0)
- **Deprecated Packages**:
  - Azure.Identity
  - Azure.Monitor.Query
  - SQLitePCLRaw.bundle_e_sqlcipher
- **High-Risk Project**: Sky.Editor (227 issues including 47 mandatory)
- **GDI+ Migration**: 81 issues related to System.Drawing - requires careful attention

---

## Migration Strategy

### Approach Selection

**Selected: All-At-Once Strategy**

All 14 projects in the solution will be upgraded simultaneously in a single coordinated operation. All project files will be updated to target net10.0, all package references will be updated, and the entire solution will be built and validated as one atomic unit.

### Justification

**Why All-At-Once is appropriate**:

1. **Homogeneous Starting Point**: All 14 projects currently target net9.0 - no mixed framework versions to reconcile
2. **Solution Size**: 14 projects is within the manageable range for atomic upgrade (< 30 projects)
3. **Clean Dependency Structure**: 7-level hierarchy with no circular dependencies
4. **Package Compatibility**: All critical NuGet packages have net10.0-compatible versions available
5. **Faster Completion**: Single upgrade operation vs. multiple incremental phases
6. **No Multi-Targeting**: Avoids complexity of maintaining multiple target frameworks
7. **Clear Validation**: Single build pass identifies all breaking changes at once

**Why NOT Incremental**:
- No .NET Framework projects requiring gradual migration
- No blocking package incompatibilities requiring phased approach
- Team can handle short-term solution-wide changes
- Clear dependency graph makes atomic upgrade safe

### All-At-Once Strategy Execution Principles

#### 1. Atomic Operation
- **All project files** updated in single batch
- **All package references** updated in single batch
- **Single restore** → **Single build** → **Fix all compilation errors** → **Verify**
- No intermediate states where some projects are net9.0 and others net10.0

#### 2. Dependency-Aware Validation
While updates are atomic, validation respects dependency order:
```
Update ALL → Restore → Build (foundation→apps) → Fix errors → Rebuild → Verify
```

#### 3. Bounded Build/Fix Cycle
The build-fix sequence is **explicitly bounded**:
- Build solution once to identify ALL compilation errors
- Fix ALL compilation errors in single pass (using breaking changes catalog)
- Rebuild to verify all fixes applied
- Success criteria: 0 errors, 0 warnings

This is **not a retry loop** - it's a single pass through: identify → fix → verify.

#### 4. Simultaneous Package Updates

All Microsoft.* packages move from version 9.x to 10.0.5 simultaneously:

| Package Category | Current | Target | Projects Affected |
|-----------------|---------|--------|-------------------|
| ASP.NET Core packages | 9.x | 10.0.5 | 7 projects |
| Entity Framework Core | 9.0.10 | 10.0.5 | 9 projects |
| Extensions packages | 9.x | 10.0.5 | 11 projects |
| System.* packages | 9.x | 10.0.5 | 3 projects |

#### 5. Risk Mitigation

**High-risk project handling** (Sky.Editor - 227 issues):
- Included in atomic upgrade (not separated)
- Breaking changes catalog provides guidance for expected issues
- Errors addressed systematically during build/fix phase
- Rollback: revert entire commit if critical blocking issues discovered

**Incompatible package handling**:
- Microsoft.VisualStudio.Azure.Containers.Tools.Targets: Remove before upgrade
- Deprecated packages: Update to recommended alternatives simultaneously

### Migration Order (Validation Sequence)

Though all projects update simultaneously, validation follows dependency order:

1. **Update Phase** (Atomic):
   - Update all 14 project files: `<TargetFramework>net10.0</TargetFramework>`
   - Update all package references to net10.0-compatible versions
   - Remove incompatible packages

2. **Build Phase** (Dependency-Ordered):
   - `dotnet restore` (entire solution)
   - `dotnet build SkyCMS.sln`
   - Compiler identifies breaking changes bottom-up (foundation → apps)

3. **Fix Phase** (Systematic):
   - Address all compilation errors using breaking changes catalog
   - Apply fixes across all affected files
   - Focus areas: API changes, System.Drawing migration, deprecated members

4. **Verification Phase**:
   - `dotnet build SkyCMS.sln` (expect 0 errors, 0 warnings)
   - Solution builds successfully

5. **Test Phase**:
   - Execute all test projects
   - Validate functionality preserved

### Parallel vs. Sequential Execution

**File Updates**: Sequential execution (one project file at a time for accuracy)

**Package Updates**: Can be batched per project (all packages in a project updated together)

**Build**: Single solution-wide build operation

**Error Fixes**: Systematic - address by category (API changes, deprecated types, etc.)

**Testing**: Sequential per test project

### Phase Definitions for Organization

While execution is atomic, content is organized into logical phases for clarity:

- **Phase 0**: Foundation libraries (AspNetCore.Identity.FlexDb, Cosmos.MicrosoftGraph)
- **Phase 1**: Configuration & Storage (Cosmos.DynamicConfig, Cosmos.BlobService)
- **Phase 2**: Core Platform (Cosmos.Common)
- **Phase 3**: Service Layer (Cosmos.EmailServices, Sky.Shared.Razor, Sky.TestSetup)
- **Phase 4**: Applications & APIs (Sky.Cms.Api.Shared, Sky.Publisher, Sky.Editor)
- **Phase 5**: Test Projects

**Important**: These phases are for **documentation and understanding**, not sequential execution. All projects in all phases are upgraded simultaneously.

---

## Detailed Dependency Analysis

### Dependency Graph Summary

The solution has a clear 7-level dependency hierarchy with **no circular dependencies**:

```
Level 0 (Foundation):
├── AspNetCore.Identity.FlexDb (10 issues, 1 mandatory)
└── Cosmos.MicrosoftGraph (31 issues, 12 mandatory)

Level 1:
├── AspNetCore.Identity.FlexDb.Tests (depends on: AspNetCore.Identity.FlexDb)
└── Cosmos.DynamicConfig (depends on: AspNetCore.Identity.FlexDb)

Level 2:
└── Cosmos.BlobService (depends on: Cosmos.DynamicConfig)

Level 3:
└── Cosmos.Common (depends on: AspNetCore.Identity.FlexDb, Cosmos.DynamicConfig, Cosmos.BlobService)

Level 4:
├── Cosmos.Common.Tests (depends on: Cosmos.Common)
├── Cosmos.EmailServices (depends on: Cosmos.Common, AspNetCore.Identity.FlexDb)
├── Sky.Shared.Razor (depends on: Cosmos.Common)
└── Sky.TestSetup (depends on: Cosmos.Common, AspNetCore.Identity.FlexDb, Cosmos.DynamicConfig, Cosmos.BlobService)

Level 5:
├── Sky.Cms.Api.Shared (depends on: Cosmos.Common, Cosmos.EmailServices)
└── Sky.Publisher (depends on: Sky.Shared.Razor, Cosmos.MicrosoftGraph, Cosmos.Common, Cosmos.EmailServices)

Level 6:
└── Sky.Editor (depends on: Sky.Shared.Razor, Cosmos.Common, Sky.Cms.Api.Shared, Cosmos.EmailServices)

Level 7:
└── Sky.Tests (depends on: Cosmos.Common, Sky.Editor, Cosmos.DynamicConfig, Cosmos.EmailServices, Sky.Publisher, Cosmos.BlobService)
```

### Project Groupings by Migration Phase

Per **All-At-Once Strategy**, all projects will be upgraded **simultaneously in a single atomic operation**. However, for organizational clarity and validation purposes, projects are grouped as follows:

#### Phase 0: Foundation Libraries (Level 0)
- **AspNetCore.Identity.FlexDb** - Identity framework for FlexDB (10 issues)
- **Cosmos.MicrosoftGraph** - Microsoft Graph integration (31 issues)

**Characteristics**: No project dependencies, highest reuse across solution

#### Phase 1: Configuration & Storage (Levels 1-2)
- **Cosmos.DynamicConfig** - Dynamic configuration provider (36 issues)
- **Cosmos.BlobService** - Blob storage service (14 issues)

**Characteristics**: Infrastructure services used by higher layers

#### Phase 2: Core Platform (Level 3)
- **Cosmos.Common** - Common utilities and shared code (28 issues, **20 mandatory**)

**Characteristics**: Central dependency hub - used by 8 other projects

#### Phase 3: Service Layer (Level 4)
- **Cosmos.EmailServices** - Email service abstraction (8 issues)
- **Sky.Shared.Razor** - Shared Razor components (2 issues)
- **Sky.TestSetup** - Test infrastructure (2 issues)

**Characteristics**: Reusable service components

#### Phase 4: Applications & APIs (Levels 5-6)
- **Sky.Cms.Api.Shared** - Shared API contracts (8 issues)
- **Sky.Publisher** - Publishing application (34 issues, 13 mandatory)
- **Sky.Editor** - Main editor application (**227 issues, 47 mandatory - HIGH RISK**)

**Characteristics**: Main application entry points

#### Phase 5: Test Projects (Levels 1, 4, 7)
- **AspNetCore.Identity.FlexDb.Tests** - Identity tests (5 issues)
- **Cosmos.Common.Tests** - Common library tests (37 issues)
- **Sky.Tests** - Main test suite (85 issues)

**Characteristics**: Validate other projects, upgraded last in sequence

### Critical Path

**All projects will be upgraded simultaneously**, but validation follows this critical path:

1. **Foundation** (AspNetCore.Identity.FlexDb, Cosmos.MicrosoftGraph) → Widely used, must build first
2. **Infrastructure** (Cosmos.DynamicConfig, Cosmos.BlobService) → Required by platform layer
3. **Platform** (Cosmos.Common) → Central hub with 8 dependents
4. **Services** → Build upon platform
5. **Applications** (Sky.Editor, Sky.Publisher) → Main deliverables
6. **Tests** → Final validation

### Dependency Constraints for All-At-Once Strategy

While all projects are updated atomically, the following constraints guide the execution:

- ✅ **All project files** updated to `<TargetFramework>net10.0</TargetFramework>` simultaneously
- ✅ **All package references** updated to net10.0-compatible versions simultaneously  
- ✅ **Build and validation** proceeds from foundation to applications (dependency order)
- ✅ **Breaking changes** addressed as discovered during build
- ✅ **Single commit** after all changes are validated

---

## Project-by-Project Plans

### Phase 0: Foundation Libraries

---

#### Project: AspNetCore.Identity.FlexDb

**Current State**:
- Target Framework: net9.0
- Project Type: ClassLibrary
- Dependencies: None (foundation library)
- Dependents: 5 projects (Cosmos.Common, Cosmos.DynamicConfig, AspNetCore.Identity.FlexDb.Tests, Sky.TestSetup, Cosmos.EmailServices)
- Total Issues: 10 (1 mandatory, 9 potential/optional)
- Lines of Code: ~moderate
- Risk Level: LOW

**Target State**:
- Target Framework: net10.0
- Updated Packages: 5 packages

**Migration Steps**:

1. **Prerequisites**: None (foundation library)

2. **Framework Update**:
   - Update `<TargetFramework>net10.0</TargetFramework>` in `AspNetCore.Identity.FlexDb.csproj`

3. **Package Updates**:

| Package | Current Version | Target Version | Reason |
|---------|----------------|----------------|--------|
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 9 | 10.0.5 | Framework compatibility |
| Microsoft.EntityFrameworkCore.Cosmos | 9.0.10 | 10.0.5 | Framework compatibility |
| Microsoft.EntityFrameworkCore.Sqlite.Core | 9.0.10 | 10.0.5 | Framework compatibility |
| Microsoft.EntityFrameworkCore.SqlServer | 9.0.10 | 10.0.5 | Framework compatibility |
| SQLitePCLRaw.bundle_e_sqlcipher | 2.1.11 | 2.1.11 | Keep current (deprecated but functional) |

4. **Expected Breaking Changes**:

   **TimeSpan API Changes** (4 occurrences):
   - `TimeSpan.FromSeconds(int)` → `TimeSpan.FromSeconds(double)`
   - `TimeSpan.FromMinutes(int)` → `TimeSpan.FromMinutes(double)`

   **Affected Files**:
   - `Strategies/SqlServerConfigurationStrategy.cs` (line 56)
   - `Stores/CosmosRoleStore.cs` (line 331)
   - `Stores/CosmosUserStore.cs` (line 871)
   - `Extensions/ServiceCollectionExtensions.cs` (line 48)

   **Fix**: Add explicit cast to `double`:
   ```csharp
   // Before
   TimeSpan.FromSeconds(30)
   // After
   TimeSpan.FromSeconds(30.0)
   ```

5. **Code Modifications**:
   - Update 4 TimeSpan method calls to use double literals
   - No other code changes expected

6. **Testing Strategy**:
   - Build AspNetCore.Identity.FlexDb project
   - Run AspNetCore.Identity.FlexDb.Tests
   - Validate identity operations (user/role management, claims)
   - Test cross-provider compatibility (Cosmos, SQLite, SQL Server)

7. **Validation Checklist**:
   - [ ] Project builds without errors
   - [ ] Project builds without warnings
   - [ ] AspNetCore.Identity.FlexDb.Tests pass
   - [ ] No package dependency conflicts
   - [ ] All database providers (Cosmos, SQLite, SQL Server) functional

---

#### Project: Cosmos.MicrosoftGraph

**Current State**:
- Target Framework: net9.0
- Project Type: ClassLibrary
- Dependencies: None (foundation library)
- Dependents: 1 project (Sky.Publisher)
- Total Issues: 31 (12 mandatory, 19 potential)
- Risk Level: MEDIUM

**Target State**:
- Target Framework: net10.0
- Updated Packages: Multiple Microsoft Graph and Azure packages

**Migration Steps**:

1. **Prerequisites**: None (foundation library)

2. **Framework Update**:
   - Update `<TargetFramework>net10.0</TargetFramework>` in `Cosmos.MicrosoftGraph.csproj`

3. **Package Updates**:

| Package | Current Version | Target Version | Reason |
|---------|----------------|----------------|--------|
| Azure.Identity | 1.17.0 | (deprecated - check for replacement) | Deprecated package |
| Azure.Monitor.Query | 1.7.1 | (deprecated - check for replacement) | Deprecated package |
| Microsoft.Graph.Beta | 5.78.0-preview | 5.78.0-preview | Compatible (no change) |

**Note**: Azure.Identity and Azure.Monitor.Query are deprecated. Verify if these packages have successors or if Azure SDK for .NET provides alternatives.

4. **Expected Breaking Changes**:
   - API compatibility issues (12 mandatory)
   - Microsoft Graph Beta API changes
   - Azure SDK behavioral changes
   - Review breaking changes documentation: https://go.microsoft.com/fwlink/?linkid=2262679

5. **Code Modifications**:
   - Update deprecated Azure.Identity usage (if replacement found)
   - Update Microsoft Graph API calls for .NET 10 compatibility
   - Address TimeSpan API changes if present
   - Review Azure SDK behavioral changes

6. **Testing Strategy**:
   - Build Cosmos.MicrosoftGraph project
   - Validate Microsoft Graph API connectivity
   - Test Azure authentication flows
   - Verify monitoring/query functionality

7. **Validation Checklist**:
   - [ ] Project builds without errors
   - [ ] Project builds without warnings
   - [ ] Deprecated packages addressed
   - [ ] Microsoft Graph API calls functional
   - [ ] Azure authentication works

---

### Phase 1: Configuration & Storage

---

#### Project: Cosmos.DynamicConfig

**Current State**:
- Target Framework: net9.0
- Project Type: ClassLibrary
- Dependencies: 1 (AspNetCore.Identity.FlexDb)
- Dependents: 5 projects (Cosmos.Common, Cosmos.BlobService, Sky.Tests, Sky.TestSetup, AspNetCore.Identity.FlexDb.Tests)
- Total Issues: 36 (3 mandatory, 33 potential)
- Risk Level: MEDIUM

**Target State**:
- Target Framework: net10.0
- Updated Packages: Multiple Microsoft packages

**Migration Steps**:

1. **Prerequisites**: 
   - AspNetCore.Identity.FlexDb upgraded to net10.0

2. **Framework Update**:
   - Update `<TargetFramework>net10.0</TargetFramework>` in `Cosmos.DynamicConfig.csproj`

3. **Package Updates**:

| Package | Current Version | Target Version | Reason |
|---------|----------------|----------------|--------|
| Microsoft.Extensions.Configuration | 9 | 10.0.5 | Framework compatibility |
| Microsoft.Extensions.Configuration.Abstractions | 9 | 10.0.5 | Framework compatibility |
| Microsoft.Extensions.Configuration.Binder | 9 | 10.0.5 | Framework compatibility |

4. **Expected Breaking Changes**:
   - Configuration API changes (3 mandatory issues)
   - Binary compatibility issues
   - Behavioral changes in configuration binding

5. **Code Modifications**:
   - Update configuration binding patterns
   - Address API signature changes
   - Review dependency injection registrations

6. **Testing Strategy**:
   - Build Cosmos.DynamicConfig project
   - Validate dynamic configuration loading
   - Test configuration binding
   - Verify multi-tenant configuration scenarios

7. **Validation Checklist**:
   - [ ] Project builds without errors
   - [ ] Project builds without warnings
   - [ ] Configuration loading works
   - [ ] Tenant resolution functional
   - [ ] No dependency conflicts with AspNetCore.Identity.FlexDb

---

#### Project: Cosmos.BlobService

**Current State**:
- Target Framework: net9.0
- Project Type: ClassLibrary
- Dependencies: 1 (Cosmos.DynamicConfig)
- Dependents: 3 projects (Cosmos.Common, Sky.Tests, Sky.TestSetup)
- Total Issues: 14 (3 mandatory, 11 potential)
- Risk Level: LOW

**Target State**:
- Target Framework: net10.0
- Updated Packages: Azure Storage packages

**Migration Steps**:

1. **Prerequisites**: 
   - Cosmos.DynamicConfig upgraded to net10.0

2. **Framework Update**:
   - Update `<TargetFramework>net10.0</TargetFramework>` in `Cosmos.BlobService.csproj`

3. **Package Updates**:

| Package | Current Version | Target Version | Reason |
|---------|----------------|----------------|--------|
| Azure.Storage.Files.Shares | 12.25.0 | 12.25.0 | Compatible (no change) |
| AWSSDK.S3 | 4.0.18.4 | 4.0.18.4 | Compatible (no change) |

4. **Expected Breaking Changes**:
   - Binary compatibility issues (3 mandatory)
   - Azure Storage API behavioral changes
   - AWS S3 SDK compatibility

5. **Code Modifications**:
   - Address binary compatibility issues
   - Update Azure Storage API usage
   - Review AWS S3 SDK patterns

6. **Testing Strategy**:
   - Build Cosmos.BlobService project
   - Test Azure Blob Storage operations
   - Test Azure File Shares operations
   - Verify AWS S3 integration

7. **Validation Checklist**:
   - [ ] Project builds without errors
   - [ ] Project builds without warnings
   - [ ] Azure Storage operations work
   - [ ] AWS S3 operations work
   - [ ] No dependency conflicts

---

[Details for remaining projects to be filled in next iteration]

### Phase 2: Core Platform

---

#### Project: Cosmos.Common

**Current State**:
- Target Framework: net9.0
- Project Type: ClassLibrary
- Dependencies: 3 (AspNetCore.Identity.FlexDb, Cosmos.DynamicConfig, Cosmos.BlobService)
- Dependents: 8 projects (Sky.Publisher, Sky.Editor, Sky.Tests, Sky.TestSetup, Sky.Cms.Api.Shared, Cosmos.EmailServices, Sky.Shared.Razor, Cosmos.Common.Tests)
- Total Issues: 28 (20 mandatory, 8 potential)
- Risk Level: MEDIUM-HIGH (central dependency hub)

**Target State**:
- Target Framework: net10.0
- Updated Packages: Multiple Microsoft packages

**Migration Steps**:

1. **Prerequisites**: 
   - AspNetCore.Identity.FlexDb upgraded to net10.0
   - Cosmos.DynamicConfig upgraded to net10.0
   - Cosmos.BlobService upgraded to net10.0

2. **Framework Update**:
   - Update `<TargetFramework>net10.0</TargetFramework>` in `Cosmos.Common.csproj`

3. **Package Updates**:

| Package | Current Version | Target Version | Reason |
|---------|----------------|----------------|--------|
| Microsoft.EntityFrameworkCore | 9.0.10 | 10.0.5 | Framework compatibility |
| Microsoft.Extensions.Caching.Memory | 9.0.10 | 10.0.5 | Framework compatibility |
| Microsoft.Extensions.Configuration.Json | 9 | 10.0.5 | Framework compatibility |
| Microsoft.Extensions.Logging.Abstractions | 9 | 10.0.5 | Framework compatibility |
| Azure.Communication.Email | 1.1.0 | 1.1.0 | Compatible (no change) |

4. **Expected Breaking Changes**:

   **High Mandatory Count** (20 mandatory issues):
   - Binary compatibility issues
   - API signature changes
   - Behavioral changes in EF Core
   - Configuration and logging API changes
   - TimeSpan API changes

   **Critical Areas**:
   - Entity Framework Core queries and migrations
   - Caching patterns
   - Configuration binding
   - Logging infrastructure

5. **Code Modifications**:
   - Update EF Core query patterns
   - Address binary compatibility issues
   - Update TimeSpan method calls
   - Review caching strategies for behavioral changes
   - Update logging patterns if needed

6. **Testing Strategy**:
   - Build Cosmos.Common project
   - Run Cosmos.Common.Tests (comprehensive test suite)
   - Validate database operations (all providers: Cosmos, SQLite, MySQL, SQL Server)
   - Test caching functionality
   - Verify email service integration
   - Test configuration loading

   **Critical**: This project has 8 dependents - thorough testing required

7. **Validation Checklist**:
   - [ ] Project builds without errors
   - [ ] Project builds without warnings
   - [ ] Cosmos.Common.Tests pass (all tests)
   - [ ] Database operations work (all providers)
   - [ ] Caching functionality preserved
   - [ ] No breaking changes for dependent projects
   - [ ] Performance acceptable

---

### Phase 3: Service Layer

---

#### Project: Cosmos.EmailServices

**Current State**:
- Target Framework: net9.0
- Project Type: ClassLibrary
- Dependencies: 2 (Cosmos.Common, AspNetCore.Identity.FlexDb)
- Dependents: 4 projects (Sky.Publisher, Sky.Editor, Sky.Tests, Sky.Cms.Api.Shared)
- Total Issues: 8 (2 mandatory, 6 potential)
- Risk Level: LOW

**Target State**:
- Target Framework: net10.0
- Updated Packages: SendGrid and email packages

**Migration Steps**:

1. **Prerequisites**: 
   - Cosmos.Common upgraded to net10.0
   - AspNetCore.Identity.FlexDb upgraded to net10.0

2. **Framework Update**:
   - Update `<TargetFramework>net10.0</TargetFramework>` in `Cosmos.EmailServices.csproj`

3. **Package Updates**:

| Package | Current Version | Target Version | Reason |
|---------|----------------|----------------|--------|
| SendGrid | 9.29.3 | 9.29.3 | Compatible (no change) |
| Azure.Communication.Email | 1.1.0 | 1.1.0 | Compatible (no change) |
| MailChimp.Net.V3 | 5.8.2 | 5.8.2 | Compatible (no change) |

4. **Expected Breaking Changes**:
   - Binary compatibility issues (2 mandatory)
   - Minimal code changes expected

5. **Code Modifications**:
   - Address binary compatibility issues
   - Update email service patterns if needed

6. **Testing Strategy**:
   - Build Cosmos.EmailServices project
   - Validate SendGrid integration
   - Test Azure Communication Email
   - Verify MailChimp integration
   - Test email template rendering (RazorLight)

7. **Validation Checklist**:
   - [ ] Project builds without errors
   - [ ] Project builds without warnings
   - [ ] Email sending functional
   - [ ] Template rendering works
   - [ ] No dependency conflicts

---

#### Project: Sky.Shared.Razor

**Current State**:
- Target Framework: net9.0
- Project Type: ClassLibrary
- Dependencies: 1 (Cosmos.Common)
- Dependents: 2 projects (Sky.Publisher, Sky.Editor)
- Total Issues: 2 (1 mandatory, 1 optional)
- Risk Level: LOW

**Target State**:
- Target Framework: net10.0
- Updated Packages: None (deprecated package noted)

**Migration Steps**:

1. **Prerequisites**: 
   - Cosmos.Common upgraded to net10.0

2. **Framework Update**:
   - Update `<TargetFramework>net10.0</TargetFramework>` in `Sky.Shared.Razor.csproj`

3. **Package Updates**:

| Package | Current Version | Target Version | Reason |
|---------|----------------|----------------|--------|
| (Deprecated package noted) | - | - | Check assessment for details |

4. **Expected Breaking Changes**:
   - Project target framework update (1 mandatory)
   - Minimal changes expected

5. **Code Modifications**:
   - Address deprecated package if present
   - Minimal code changes expected

6. **Testing Strategy**:
   - Build Sky.Shared.Razor project
   - Validate Razor components render
   - Test integration with Sky.Editor and Sky.Publisher

7. **Validation Checklist**:
   - [ ] Project builds without errors
   - [ ] Project builds without warnings
   - [ ] Razor components functional
   - [ ] No dependency conflicts

---

#### Project: Sky.TestSetup

**Current State**:
- Target Framework: net9.0
- Project Type: DotNetCoreApp
- Dependencies: 4 (Cosmos.Common, AspNetCore.Identity.FlexDb, Cosmos.DynamicConfig, Cosmos.BlobService)
- Dependents: None (test infrastructure)
- Total Issues: 2 (1 mandatory, 1 potential)
- Risk Level: LOW

**Target State**:
- Target Framework: net10.0
- Updated Packages: Test framework packages

**Migration Steps**:

1. **Prerequisites**: 
   - All dependencies upgraded to net10.0

2. **Framework Update**:
   - Update `<TargetFramework>net10.0</TargetFramework>` in `Sky.TestSetup.csproj`

3. **Package Updates**:

| Package | Current Version | Target Version | Reason |
|---------|----------------|----------------|--------|
| Microsoft.NET.Test.Sdk | 17.14.1 | 17.14.1 | Compatible (no change) |
| MSTest.TestAdapter | 4.1.0 | 4.1.0 | Compatible (no change) |
| MSTest.TestFramework | 4.1.0 | 4.1.0 | Compatible (no change) |

4. **Expected Breaking Changes**:
   - Project target framework update
   - Minimal test infrastructure changes

5. **Code Modifications**:
   - Address any test helper method compatibility
   - Update test initialization if needed

6. **Testing Strategy**:
   - Build Sky.TestSetup project
   - Validate test infrastructure setup
   - Ensure test projects can reference this

7. **Validation Checklist**:
   - [ ] Project builds without errors
   - [ ] Project builds without warnings
   - [ ] Test infrastructure functional
   - [ ] All dependencies compatible

---

[Remaining projects to be filled in next iteration]

### Phase 4: Applications & APIs

---

#### Project: Sky.Cms.Api.Shared

**Current State**:
- Target Framework: net9.0
- Project Type: AspNetCore
- Dependencies: 2 (Cosmos.Common, Cosmos.EmailServices)
- Dependents: 1 project (Sky.Editor)
- Total Issues: 8 (2 mandatory, 6 potential)
- Risk Level: LOW

**Target State**:
- Target Framework: net10.0
- Updated Packages: ASP.NET Core packages

**Migration Steps**:

1. **Prerequisites**: 
   - Cosmos.Common upgraded to net10.0
   - Cosmos.EmailServices upgraded to net10.0

2. **Framework Update**:
   - Update `<TargetFramework>net10.0</TargetFramework>` in `Sky.Cms.Api.Shared.csproj`

3. **Package Updates**:

| Package | Current Version | Target Version | Reason |
|---------|----------------|----------------|--------|
| Microsoft.AspNetCore.Authorization | 9 | 10.0.5 | Framework compatibility |

4. **Expected Breaking Changes**:
   - API compatibility issues (2 mandatory)
   - ASP.NET Core authorization changes
   - Behavioral changes in API patterns

5. **Code Modifications**:
   - Update authorization policies
   - Address API signature changes
   - Review middleware patterns

6. **Testing Strategy**:
   - Build Sky.Cms.Api.Shared project
   - Validate API contracts
   - Test authorization policies
   - Verify integration with Sky.Editor

7. **Validation Checklist**:
   - [ ] Project builds without errors
   - [ ] Project builds without warnings
   - [ ] API contracts preserved
   - [ ] Authorization functional
   - [ ] No breaking changes for Sky.Editor

---

#### Project: Sky.Publisher

**Current State**:
- Target Framework: net9.0
- Project Type: AspNetCore
- Dependencies: 4 (Sky.Shared.Razor, Cosmos.MicrosoftGraph, Cosmos.Common, Cosmos.EmailServices)
- Dependents: 1 project (Sky.Tests)
- Total Issues: 34 (13 mandatory, 21 potential)
- Risk Level: MEDIUM

**Target State**:
- Target Framework: net10.0
- Updated Packages: ASP.NET Core and Microsoft packages

**Migration Steps**:

1. **Prerequisites**: 
   - All dependencies upgraded to net10.0

2. **Framework Update**:
   - Update `<TargetFramework>net10.0</TargetFramework>` in `Sky.Publisher.csproj`

3. **Package Updates**:

| Package | Current Version | Target Version | Reason |
|---------|----------------|----------------|--------|
| Microsoft.AspNetCore.Mvc.NewtonsoftJson | 9 | 10.0.5 | Framework compatibility |
| Microsoft.EntityFrameworkCore.Tools | 9.0.10 | 10.0.5 | Framework compatibility |
| X.Web.Sitemap | 2.11.3 | 2.11.3 | Compatible (no change) |

4. **Expected Breaking Changes**:
   - Binary compatibility issues (13 mandatory)
   - ASP.NET Core MVC changes
   - Newtonsoft.Json serialization changes
   - EF Core tools updates
   - Microsoft Graph API changes (from Cosmos.MicrosoftGraph dependency)

5. **Code Modifications**:
   - Update ASP.NET Core MVC patterns
   - Address JSON serialization changes
   - Update EF Core migration patterns
   - Review Microsoft Graph integration
   - Update sitemap generation if needed

6. **Testing Strategy**:
   - Build Sky.Publisher project
   - Run Sky.Tests (publisher scenarios)
   - Test publishing workflows
   - Validate sitemap generation
   - Test Microsoft Graph integration
   - Verify email notifications

7. **Validation Checklist**:
   - [ ] Project builds without errors
   - [ ] Project builds without warnings
   - [ ] Publishing workflows functional
   - [ ] Sitemap generation works
   - [ ] Microsoft Graph integration preserved
   - [ ] No deployment pipeline issues

---

#### Project: Sky.Editor ⚠️ HIGH RISK

**Current State**:
- Target Framework: net9.0
- Project Type: AspNetCore (Razor Pages)
- Dependencies: 4 (Sky.Shared.Razor, Cosmos.Common, Sky.Cms.Api.Shared, Cosmos.EmailServices)
- Dependents: 1 project (Sky.Tests)
- Total Files: 655 files
- Total Issues: **227 (47 mandatory, 180 potential)**
- **Risk Level: HIGH**

**Target State**:
- Target Framework: net10.0
- Updated Packages: 5 packages (1 incompatible to remove)

**Migration Steps**:

1. **Prerequisites**: 
   - All dependencies upgraded to net10.0

2. **Framework Update**:
   - Update `<TargetFramework>net10.0</TargetFramework>` in `Sky.Editor.csproj`

3. **Package Updates**:

| Package | Current Version | Target Version | Reason |
|---------|----------------|----------------|--------|
| Microsoft.AspNetCore.Authentication.Google | 9 | 10.0.5 | Framework compatibility |
| Microsoft.AspNetCore.Authentication.MicrosoftAccount | 9 | 10.0.5 | Framework compatibility |
| Microsoft.AspNetCore.Mvc.NewtonsoftJson | 9 | 10.0.5 | Framework compatibility |
| Microsoft.EntityFrameworkCore.Tools | 9.0.10 | 10.0.5 | Framework compatibility |
| System.Drawing.Common | 9 | 10.0.5 | Framework compatibility |
| **Microsoft.VisualStudio.Azure.Containers.Tools.Targets** | 1.23.0 | **REMOVE** | **Incompatible with net10.0** |

**Critical**: Remove incompatible Docker tooling package

4. **Expected Breaking Changes**:

   **A. GDI+ / System.Drawing (81 issues - CRITICAL)**:

   - **Impact**: System.Drawing.Common is Windows-only as of .NET 6+
   - **Occurrences**: 81 instances in Sky.Editor
   - **Affected Areas**: Image manipulation, graphics rendering, image processing
   - **Mitigation**: Project already references SixLabors.ImageSharp (3.1.12) - preferred cross-platform alternative

   **Action Items**:
   - Audit all System.Drawing usage
   - Migrate to SixLabors.ImageSharp where possible
   - Keep System.Drawing.Common for Windows-specific scenarios (if applicable)
   - Test all image processing functionality

   **Reference**: https://go.microsoft.com/fwlink/?linkid=2341701

   **B. Binary Compatibility Issues (45 mandatory)**:

   - API signature changes across ASP.NET Core
   - Authentication middleware changes
   - Razor Pages runtime changes
   - Entity Framework Core query changes

   **C. Behavioral Changes (63 potential)**:

   - ASP.NET Core middleware ordering
   - Authentication/authorization behavior
   - Razor Pages rendering
   - JSON serialization (Newtonsoft.Json)

   **D. Source Compatibility (112 potential)**:

   - API deprecations
   - Method signature updates
   - Property access changes

   **E. Incompatible Package Removal**:

   - Docker tooling integration via Visual Studio SDK instead
   - Verify `docker-compose.yml` and container build still functional
   - Test containerization post-removal

5. **Code Modifications** (Systematic Approach):

   **Step 1: Remove Incompatible Package**
   ```xml
   <!-- Remove from Sky.Editor.csproj -->
   <PackageReference Include="Microsoft.VisualStudio.Azure.Containers.Tools.Targets" Version="1.23.0" />
   ```

   **Step 2: Address GDI+ Issues**
   - Review all files using `System.Drawing` namespace
   - Migrate image processing to SixLabors.ImageSharp
   - Test visual editor functionality thoroughly

   **Step 3: Fix Compilation Errors**
   - Address binary compatibility issues (45 mandatory)
   - Update API calls for ASP.NET Core 10
   - Fix authentication provider integration
   - Update Razor Pages patterns

   **Step 4: Review Behavioral Changes**
   - Test authentication flows (Google, Microsoft Account)
   - Verify Razor Pages rendering
   - Test JSON API responses (Newtonsoft.Json)
   - Validate middleware pipeline

6. **Testing Strategy** (Comprehensive):

   **Build Validation**:
   - `dotnet build Sky.Editor.csproj` (expect 0 errors)
   - Address all compilation errors systematically

   **Unit/Integration Tests**:
   - Run Sky.Tests (Editor scenarios)
   - Focus on high-risk areas (GDI+, authentication, image processing)

   **Functional Testing**:
   - Launch Sky.Editor application
   - Test visual editor functionality (iframe, autosave, content regions)
   - Test image upload and processing
   - Verify authentication (Google, Microsoft Account)
   - Test multi-tenant isolation (cookie domain, tenant resolution)
   - Validate rate limiting (contact-form policy)
   - Test antiforgery tokens

   **Container Testing**:
   - Build Docker image: `docker build -t skycms-editor .`
   - Run containerized application
   - Verify all functionality in container

   **Performance Testing**:
   - Compare response times to .NET 9 baseline
   - Validate no significant regressions

7. **Validation Checklist**:
   - [ ] Incompatible package removed
   - [ ] Project builds without errors
   - [ ] Project builds without warnings
   - [ ] All GDI+ usage audited and tested
   - [ ] Image processing functional (ImageSharp migration if needed)
   - [ ] Authentication providers work (Google, Microsoft Account)
   - [ ] Razor Pages render correctly
   - [ ] Visual editor functionality preserved
   - [ ] Multi-tenant features functional
   - [ ] Rate limiting works
   - [ ] Docker containerization functional
   - [ ] Sky.Tests pass (all editor scenarios)
   - [ ] No performance regressions
   - [ ] Cross-provider database compatibility (Cosmos, MySQL, SQLite, SQL Server)

**Special Considerations for Sky.Editor**:
- This is the highest-risk project in the upgrade
- Contains main Razor Pages application
- 655 files, 227 issues to address
- GDI+ migration is the primary complexity driver
- Incompatible Docker package requires careful validation
- Multi-tenant architecture must be preserved
- Consider feature branch testing before main upgrade
- Allocate extra testing time and resources

---

[Final phase to be filled in next iteration]

### Phase 5: Test Projects

---

#### Project: AspNetCore.Identity.FlexDb.Tests

**Current State**:
- Target Framework: net9.0
- Project Type: DotNetCoreApp (test project)
- Dependencies: 1 (AspNetCore.Identity.FlexDb)
- Dependents: None
- Total Issues: 5 (1 mandatory, 4 potential)
- Risk Level: LOW

**Target State**:
- Target Framework: net10.0
- Updated Packages: Test framework packages

**Migration Steps**:

1. **Prerequisites**: 
   - AspNetCore.Identity.FlexDb upgraded to net10.0

2. **Framework Update**:
   - Update `<TargetFramework>net10.0</TargetFramework>` in `AspNetCore.Identity.FlexDb.Tests.csproj`

3. **Package Updates**:

| Package | Current Version | Target Version | Reason |
|---------|----------------|----------------|--------|
| Microsoft.NET.Test.Sdk | 17.14.1 | 17.14.1 | Compatible (no change) |
| MSTest.TestAdapter | 4.1.0 | 4.1.0 | Compatible (no change) |
| MSTest.TestFramework | 4.1.0 | 4.1.0 | Compatible (no change) |
| coverlet.collector | 6.0.4 | 6.0.4 | Compatible (no change) |

4. **Expected Breaking Changes**:
   - Test framework compatibility (1 mandatory)
   - API changes from AspNetCore.Identity.FlexDb
   - Minimal test assertion updates

5. **Code Modifications**:
   - Update test assertions for .NET 10 behavior
   - Address any TimeSpan API usage in tests
   - Update test initialization if needed

6. **Testing Strategy**:
   - Build AspNetCore.Identity.FlexDb.Tests project
   - Run all tests: `dotnet test AspNetCore.Identity.FlexDb.Tests.csproj`
   - Validate identity operations across all database providers
   - Verify test coverage maintained

7. **Validation Checklist**:
   - [ ] Project builds without errors
   - [ ] All tests pass
   - [ ] Test coverage maintained
   - [ ] Cross-provider tests functional

---

#### Project: Cosmos.Common.Tests

**Current State**:
- Target Framework: net9.0
- Project Type: DotNetCoreApp (test project)
- Dependencies: 1 (Cosmos.Common)
- Dependents: None
- Total Issues: 37 (1 mandatory, 36 potential)
- Risk Level: MEDIUM

**Target State**:
- Target Framework: net10.0
- Updated Packages: Test framework and EF Core packages

**Migration Steps**:

1. **Prerequisites**: 
   - Cosmos.Common upgraded to net10.0

2. **Framework Update**:
   - Update `<TargetFramework>net10.0</TargetFramework>` in `Cosmos.Common.Tests.csproj`

3. **Package Updates**:

| Package | Current Version | Target Version | Reason |
|---------|----------------|----------------|--------|
| Microsoft.EntityFrameworkCore.InMemory | 9.0.10 | 10.0.5 | Framework compatibility |
| Microsoft.NET.Test.Sdk | 17.14.1 | 17.14.1 | Compatible (no change) |
| MSTest.TestAdapter | 4.1.0 | 4.1.0 | Compatible (no change) |
| MSTest.TestFramework | 4.1.0 | 4.1.0 | Compatible (no change) |

4. **Expected Breaking Changes**:
   - EF Core InMemory provider changes (1 mandatory)
   - API compatibility issues (36 potential)
   - Test assertions may need updates for .NET 10 behavior

5. **Code Modifications**:
   - Update EF Core InMemory test patterns
   - Address API compatibility issues
   - Update test assertions for behavioral changes
   - Review test data setup

6. **Testing Strategy**:
   - Build Cosmos.Common.Tests project
   - Run all tests: `dotnet test Cosmos.Common.Tests.csproj`
   - Validate database operations across providers
   - Test caching functionality
   - Verify configuration loading

7. **Validation Checklist**:
   - [ ] Project builds without errors
   - [ ] All tests pass
   - [ ] EF Core InMemory tests functional
   - [ ] Cross-provider compatibility validated
   - [ ] Test coverage maintained

---

#### Project: Sky.Tests

**Current State**:
- Target Framework: net9.0
- Project Type: DotNetCoreApp (test project)
- Dependencies: 6 (Cosmos.Common, Sky.Editor, Cosmos.DynamicConfig, Cosmos.EmailServices, Sky.Publisher, Cosmos.BlobService)
- Dependents: None
- Total Issues: 85 (5 mandatory, 80 potential)
- Risk Level: MEDIUM

**Target State**:
- Target Framework: net10.0
- Updated Packages: Test framework and dependencies

**Migration Steps**:

1. **Prerequisites**: 
   - **All projects upgraded to net10.0** (Sky.Tests is final project)

2. **Framework Update**:
   - Update `<TargetFramework>net10.0</TargetFramework>` in `Sky.Tests.csproj`

3. **Package Updates**:

| Package | Current Version | Target Version | Reason |
|---------|----------------|----------------|--------|
| Microsoft.EntityFrameworkCore.InMemory | 9.0.10 | 10.0.5 | Framework compatibility |
| Microsoft.AspNetCore.Mvc.Testing | (if present) | 10.0.5 | Framework compatibility |
| Moq | 4.20.72 | 4.20.72 | Compatible (no change) |
| Microsoft.NET.Test.Sdk | 17.14.1 | 17.14.1 | Compatible (no change) |
| MSTest.TestAdapter | 4.1.0 | 4.1.0 | Compatible (no change) |
| MSTest.TestFramework | 4.1.0 | 4.1.0 | Compatible (no change) |
| coverlet.msbuild | 6.0.4 | 6.0.4 | Compatible (no change) |

4. **Expected Breaking Changes**:
   - Test host compatibility (5 mandatory)
   - Integration test patterns for ASP.NET Core 10
   - EF Core InMemory provider changes
   - API compatibility issues from all dependencies (80 potential)
   - Moq compatibility with .NET 10 APIs

5. **Code Modifications**:
   - Update integration test setup (WebApplicationFactory)
   - Update test assertions for .NET 10 behavior
   - Address API compatibility issues from dependencies
   - Update test data initialization
   - Review test isolation and cleanup

6. **Testing Strategy**:
   - Build Sky.Tests project
   - Run all tests: `dotnet test Sky.Tests.csproj`
   - **Comprehensive validation** (tests entire solution):
     - Sky.Editor integration tests
     - Sky.Publisher integration tests
     - Multi-tenant scenarios
     - Authentication flows
     - Database operations (all providers)
     - Email service integration
     - Blob storage operations
     - Configuration loading

7. **Validation Checklist**:
   - [ ] Project builds without errors
   - [ ] All tests pass
   - [ ] Integration tests functional
   - [ ] Sky.Editor scenarios validated
   - [ ] Sky.Publisher scenarios validated
   - [ ] Multi-tenant features tested
   - [ ] Authentication flows verified
   - [ ] Database operations work (all providers)
   - [ ] Test coverage maintained
   - [ ] No test flakiness introduced

**Special Considerations**:
- Sky.Tests validates the entire solution end-to-end
- Final validation before upgrade completion
- Test failures here may indicate issues in any upgraded project
- Allocate time for comprehensive test execution
- Monitor test execution time (performance validation)

---

---

## Risk Management

### High-Risk Changes

| Project | Risk Level | Description | Mitigation |
|---------|-----------|-------------|------------|
| **Sky.Editor** | **HIGH** | 227 issues (47 mandatory) including 81 GDI+ breaking changes | Comprehensive testing, breaking changes catalog, consider ImageSharp migration |
| **Cosmos.Common** | **MEDIUM** | 28 issues (20 mandatory), used by 8 projects | Test thoroughly, impacts entire solution |
| **Sky.Tests** | **MEDIUM** | 85 issues - test infrastructure changes | May need test framework updates, pattern adjustments |
| **Cosmos.MicrosoftGraph** | **MEDIUM** | 31 issues (12 mandatory) | API compatibility verification needed |
| **Sky.Publisher** | **MEDIUM** | 34 issues (13 mandatory) | Build and deployment pipeline validation |

### Security Vulnerabilities

**No security vulnerabilities detected** in the assessment for package upgrades to net10.0.

However, note deprecated packages that should be addressed:
- **Azure.Identity** - deprecated, check for successor package
- **Azure.Monitor.Query** - deprecated, check for successor package  
- **SQLitePCLRaw.bundle_e_sqlcipher** - deprecated, evaluate alternatives

### Technology-Specific Risks

#### 1. System.Drawing / GDI+ (81 issues in Sky.Editor)

**Risk**: System.Drawing.Common is Windows-only as of .NET 6+ and has behavioral changes in .NET 10

**Impact**: 
- 81 occurrences in Sky.Editor project
- Affects image manipulation, graphics rendering
- May have cross-platform compatibility issues

**Mitigation**:
- Verify current usage is Windows-only or already abstracted
- Consider migration to SixLabors.ImageSharp (already referenced in project)
- System.Drawing.Common package will be updated to 10.0.5
- Test all image processing and rendering functionality
- Review breaking changes: https://go.microsoft.com/fwlink/?linkid=2341701

#### 2. Incompatible Package: Microsoft.VisualStudio.Azure.Containers.Tools.Targets

**Risk**: Package incompatible with net10.0

**Impact**: Docker tooling integration in Visual Studio

**Mitigation**:
- Remove package reference from Sky.Editor project
- Verify if newer compatible version exists
- Docker functionality may rely on SDK tooling instead
- Test container build pipeline after removal

#### 3. Binary Compatibility (45 mandatory issues in Sky.Editor)

**Risk**: API signature changes requiring code updates

**Impact**: Compilation failures, runtime behavior changes

**Mitigation**:
- Breaking changes catalog documents expected API changes
- Systematic error resolution during build phase
- Comprehensive testing after fixes applied

### Contingency Plans

#### If Build Fails with Critical Errors

**Option 1: Targeted Fixes**
- Address high-priority compilation errors first
- Use breaking changes documentation
- Apply fixes systematically by category

**Option 2: Package Version Adjustment**
- If specific package causes blocking issue
- Research compatible intermediate version
- Update plan with adjusted package version

**Option 3: Rollback**
- Revert all changes (single commit makes this clean)
- Research blocking issue
- Create focused fix
- Re-attempt atomic upgrade

#### If Tests Fail

**Option 1: Analyze Failures**
- Categorize test failures (API changes vs. behavior changes)
- Update test assertions for expected .NET 10 behavior
- Fix application code if tests reveal real issues

**Option 2: Defer Non-Critical Tests**
- Mark failing tests as Ignored with tracking issues
- Proceed with deployment if core functionality passes
- Address test issues in follow-up work

#### If Performance Degrades

**Option 1: Profile and Optimize**
- Use .NET 10 performance profilers
- Identify hot paths affected by framework changes
- Apply targeted optimizations

**Option 2: Enable Compatibility Features**
- Check for .NET 10 compatibility switches
- Enable legacy behavior where acceptable
- Plan migration to new patterns over time

### Rollback Strategy

**Simple Rollback** (All-At-Once advantage):
```bash
git reset --hard HEAD~1  # Revert single commit
git push --force-with-lease origin upgrade-to-NET10
```

**Criteria for Rollback**:
- Blocking compilation errors with no clear resolution path
- Critical runtime failures in core functionality
- Severe performance degradation (>30% regression)
- Test failures indicating fundamental incompatibility

**Recovery Path**:
1. Document specific blocking issue
2. Research resolution (Microsoft docs, GitHub issues, community)
3. Create targeted fix/workaround
4. Re-execute atomic upgrade with fix applied

---

## Testing & Validation Strategy

### Multi-Level Testing Approach

#### Phase-by-Phase Testing (Validation Sequence)

Though all projects are updated simultaneously (All-At-Once), validation proceeds dependency-order from foundation to applications:

**Foundation Validation** (Phase 0):
- Build AspNetCore.Identity.FlexDb → Run AspNetCore.Identity.FlexDb.Tests
- Build Cosmos.MicrosoftGraph → Validate Microsoft Graph integration
- **Success Criteria**: Both projects build, FlexDb tests pass

**Infrastructure Validation** (Phase 1):
- Build Cosmos.DynamicConfig, Cosmos.BlobService
- Test configuration loading and blob storage operations
- **Success Criteria**: Projects build, infrastructure functional

**Platform Validation** (Phase 2):
- Build Cosmos.Common → Run Cosmos.Common.Tests
- **Critical**: Validate database operations across all providers (Cosmos, SQLite, MySQL, SQL Server)
- **Success Criteria**: Cosmos.Common builds, all tests pass, no breaking changes for dependents

**Service Validation** (Phase 3):
- Build Cosmos.EmailServices, Sky.Shared.Razor, Sky.TestSetup
- Test email services and Razor components
- **Success Criteria**: All projects build, service operations functional

**Application Validation** (Phase 4):
- Build Sky.Cms.Api.Shared, Sky.Publisher, **Sky.Editor**
- **Focus**: Sky.Editor comprehensive testing (high-risk project)
- Test containerization (Docker build after removing incompatible package)
- **Success Criteria**: All applications build, Sky.Editor functional

**Integration Validation** (Phase 5):
- Build and run all test projects
- **Sky.Tests**: Comprehensive end-to-end validation
- **Success Criteria**: All tests pass, solution fully validated

### Smoke Tests (Quick Validation)

After atomic upgrade completion, execute quick smoke tests:

1. **Build Verification**:
   ```bash
   dotnet restore SkyCMS.sln
   dotnet build SkyCMS.sln --no-restore
   ```
   **Expected**: 0 errors, 0 warnings

2. **Test Execution**:
   ```bash
   dotnet test SkyCMS.sln --no-build
   ```
   **Expected**: All tests pass

3. **Application Launch** (Sky.Editor):
   ```bash
   dotnet run --project Editor/Sky.Editor.csproj
   ```
   **Expected**: Application starts, responds to requests

4. **Container Build**:
   ```bash
   docker build -t skycms-editor -f Editor/Dockerfile .
   ```
   **Expected**: Image builds successfully (verify Docker tooling works without incompatible package)

### Comprehensive Validation

#### Database Operations (Critical for Multi-Tenant)

Test across all supported providers:

**Cosmos DB**:
- Connection established
- Entity queries functional
- Multi-tenant isolation preserved
- Identity operations work

**SQL Server**:
- Connection established (retry logic functional)
- EF migrations compatible
- Identity operations work

**MySQL** (Pomelo.EntityFrameworkCore.MySql):
- Connection established
- Provider-specific queries functional
- No breaking changes

**SQLite**:
- In-memory and file-based operations
- Test scenarios functional
- FlexDb cross-provider compatibility

**Validation Checklist**:
- [ ] All providers connect successfully
- [ ] Queries execute without errors
- [ ] Multi-tenant isolation functional
- [ ] Identity operations work (all providers)
- [ ] No provider-specific breaking changes

#### ASP.NET Core Features

**Authentication & Authorization**:
- [ ] Google authentication functional
- [ ] Microsoft Account authentication functional
- [ ] Cookie domain isolation (multi-tenant)
- [ ] Antiforgery tokens scoped per tenant
- [ ] Authorization policies enforced

**Razor Pages**:
- [ ] Pages render correctly
- [ ] Visual editor iframe functional
- [ ] Autosave functionality works
- [ ] Content regions editable
- [ ] Client-side JavaScript functional

**API Functionality**:
- [ ] API endpoints respond
- [ ] JSON serialization works (Newtonsoft.Json)
- [ ] Rate limiting functional (contact-form policy: 3 req/5min production, 20 req/1min development)
- [ ] CORS policies enforced

**Middleware Pipeline**:
- [ ] DomainMiddleware establishes tenant context
- [ ] IDynamicConfigurationProvider resolves tenant (x-origin-hostname priority, fallback to Host header)
- [ ] Middleware ordering preserved
- [ ] Exception handling functional

#### Image Processing (GDI+ Migration)

**Critical Testing** (81 issues in Sky.Editor):

- [ ] Image upload functional
- [ ] Image resizing works
- [ ] Image format conversion functional
- [ ] Graphics rendering (if applicable)
- [ ] SixLabors.ImageSharp integration (if migrated from System.Drawing)
- [ ] No Windows-specific dependencies breaking cross-platform compatibility

**Test Scenarios**:
- Upload various image formats (JPEG, PNG, GIF, WebP)
- Resize images (thumbnail generation)
- Format conversion
- Verify visual output matches .NET 9 behavior

#### Performance Validation

**Baseline Comparison** (compare to .NET 9):

- [ ] Application startup time (no significant regression)
- [ ] Request response time (no >10% degradation)
- [ ] Database query performance (no regressions)
- [ ] Memory usage (monitor for increases)
- [ ] Test execution time (acceptable range)

**Metrics to Monitor**:
- Application startup: < X seconds (define baseline)
- API response time: < Y ms (define baseline)
- Test suite execution: < Z minutes (define baseline)

#### Security Validation

- [ ] No new security vulnerabilities introduced
- [ ] Deprecated packages addressed (Azure.Identity, Azure.Monitor.Query, SQLitePCLRaw.bundle_e_sqlcipher)
- [ ] Authentication flows secure
- [ ] Multi-tenant isolation maintained (no data leakage)
- [ ] HTTPS enforcement preserved
- [ ] Antiforgery protection functional

### Test Failure Handling

**If Tests Fail**:

1. **Categorize Failures**:
   - Compilation errors → Address systematically using breaking changes catalog
   - Test assertion failures → Update for .NET 10 behavior
   - Runtime errors → Investigate and fix root cause

2. **Prioritize Fixes**:
   - P0: Blocking compilation errors
   - P1: Critical functionality failures (auth, database, multi-tenant)
   - P2: Non-critical functionality failures
   - P3: Test infrastructure issues

3. **Fix Strategy**:
   - Address P0 errors first (prevent further testing)
   - Fix P1 issues before proceeding to deployment
   - P2/P3 can be deferred with tracking issues if non-blocking

4. **Re-Validation**:
   - After fixes applied, rebuild and re-run full test suite
   - Verify no regressions introduced by fixes
   - Update plan with any new learnings

### Test Coverage Goals

- [ ] Unit tests: 100% execution, >90% pass rate (investigate failures)
- [ ] Integration tests: 100% execution, >95% pass rate
- [ ] End-to-end tests: All critical paths validated
- [ ] Cross-provider tests: All database providers validated
- [ ] Multi-tenant scenarios: Isolation and resolution tested

### Documentation of Test Results

For each test phase, document:
- Test execution time
- Pass/fail counts
- Any failures and resolutions
- Performance metrics
- Any behavioral changes observed

---

## Complexity & Effort Assessment

### Overall Complexity: **MEDIUM**

**Factors**:
- 14 projects (manageable size)
- 527 total issues (significant but addressed systematically)
- 1 high-complexity project (Sky.Editor)
- Clear dependency structure (no cycles)
- Well-defined package upgrade path

### Per-Project Complexity

| Project | Complexity | Issues | Mandatory | Dependencies | Risk Factors |
|---------|-----------|--------|-----------|--------------|--------------|
| **Sky.Editor** | **HIGH** | 227 | 47 | 4 | GDI+ migration (81), incompatible package, 655 files |
| **Sky.Tests** | **MEDIUM** | 85 | 5 | 6 | Test framework updates, many dependencies |
| **Cosmos.Common.Tests** | **MEDIUM** | 37 | 1 | 1 | Test project, moderate issue count |
| **Cosmos.DynamicConfig** | **MEDIUM** | 36 | 3 | 1 | Used by 5 projects |
| **Sky.Publisher** | **MEDIUM** | 34 | 13 | 4 | Application project, deployment complexity |
| **Cosmos.MicrosoftGraph** | **MEDIUM** | 31 | 12 | 0 | Microsoft Graph API changes |
| **Cosmos.Common** | **MEDIUM** | 28 | 20 | 3 | High mandatory count, 8 dependents |
| **Cosmos.BlobService** | **LOW** | 14 | 3 | 1 | Focused scope |
| **AspNetCore.Identity.FlexDb** | **LOW** | 10 | 1 | 0 | Foundation library |
| **Sky.Cms.Api.Shared** | **LOW** | 8 | 2 | 2 | API contracts |
| **Cosmos.EmailServices** | **LOW** | 8 | 2 | 2 | Service layer |
| **AspNetCore.Identity.FlexDb.Tests** | **LOW** | 5 | 1 | 1 | Small test project |
| **Sky.Shared.Razor** | **LOW** | 2 | 1 | 1 | Simple Razor components |
| **Sky.TestSetup** | **LOW** | 2 | 1 | 4 | Test infrastructure |

### Phase Complexity Assessment

Following dependency-ordered validation sequence:

#### Phase 0: Foundation Libraries
**Complexity**: LOW to MEDIUM
- AspNetCore.Identity.FlexDb: Low (10 issues, 1 mandatory)
- Cosmos.MicrosoftGraph: Medium (31 issues, 12 mandatory)
- Combined effort: Moderate, clear API changes

#### Phase 1: Configuration & Storage
**Complexity**: MEDIUM
- Cosmos.DynamicConfig: Medium (36 issues, used by 5 projects)
- Cosmos.BlobService: Low (14 issues)
- Combined effort: Moderate, infrastructure focus

#### Phase 2: Core Platform
**Complexity**: MEDIUM-HIGH
- Cosmos.Common: Medium-High (20 mandatory issues, 8 dependents)
- Critical central dependency
- High impact if errors occur

#### Phase 3: Service Layer
**Complexity**: LOW
- Cosmos.EmailServices: Low (8 issues)
- Sky.Shared.Razor: Low (2 issues)
- Sky.TestSetup: Low (2 issues)
- Combined effort: Low, straightforward upgrades

#### Phase 4: Applications & APIs
**Complexity**: HIGH
- Sky.Cms.Api.Shared: Low (8 issues)
- Sky.Publisher: Medium (34 issues, 13 mandatory)
- **Sky.Editor: HIGH** (227 issues, 47 mandatory, GDI+ migration)
- Combined effort: High, dominated by Sky.Editor

#### Phase 5: Test Projects
**Complexity**: MEDIUM
- AspNetCore.Identity.FlexDb.Tests: Low (5 issues)
- Cosmos.Common.Tests: Medium (37 issues)
- Sky.Tests: Medium (85 issues)
- Combined effort: Moderate, test framework adjustments

### Resource Requirements

**Skills Required**:
- .NET 9 → .NET 10 migration experience
- ASP.NET Core Razor Pages expertise (for Sky.Editor)
- Entity Framework Core knowledge
- System.Drawing / image processing (for GDI+ migration)
- Docker / container tooling (for incompatible package removal)
- Test framework experience (MSTest, test project updates)

**Team Composition Recommendation**:
- 1 senior .NET developer (lead migration, high-risk areas)
- 1 QA engineer (test execution and validation)
- Access to DevOps engineer (container tooling verification)

**Parallel Work Capacity**:
- All project files can be updated in parallel (atomic operation)
- Build errors addressed systematically (sequential by category)
- Testing can be parallelized across test projects

### Effort Distribution

**Estimated effort distribution** (relative complexity, not time):

- **Project File Updates**: 10% - Straightforward, automated
- **Package Reference Updates**: 10% - Straightforward, version bumps
- **Build Error Resolution**: 40% - Systematic, guided by breaking changes
  - Sky.Editor GDI+ issues: 20%
  - Other API compatibility: 15%
  - Package incompatibility: 5%
- **Testing & Validation**: 30% - Comprehensive test suite execution
- **Incompatible Package Resolution**: 5% - Docker tooling verification
- **Documentation & Review**: 5% - Plan updates, PR review

### Success Indicators

**Low Risk Projects** (should complete quickly):
- AspNetCore.Identity.FlexDb
- Cosmos.BlobService
- Sky.Shared.Razor
- Sky.TestSetup
- Sky.Cms.Api.Shared

**Medium Risk Projects** (require attention):
- Cosmos.Common (high mandatory count)
- Cosmos.DynamicConfig (many dependents)
- Cosmos.MicrosoftGraph (API changes)
- Sky.Publisher (application complexity)

**High Risk Project** (requires focused effort):
- Sky.Editor (GDI+ migration, 227 issues, incompatible package)

---

## Source Control Strategy

### Branch Strategy

**Branches**:
- **Source Branch**: `main` (starting point)
- **Upgrade Branch**: `upgrade-to-NET10` (all changes committed here)
- **Merge Target**: `main` (after validation complete)

**Branch Protection**:
- Upgrade branch already created and checked out
- All upgrade changes committed to `upgrade-to-NET10`
- No direct commits to `main` during upgrade

### Commit Strategy (All-At-Once Approach)

**Single Commit Approach** (Recommended):

Following All-At-Once strategy principles, prefer a single atomic commit after all changes validated:

**Commit Workflow**:
1. Update all 14 project files (`<TargetFramework>net10.0</TargetFramework>`)
2. Update all package references (26 packages)
3. Remove incompatible package (Microsoft.VisualStudio.Azure.Containers.Tools.Targets)
4. Address all compilation errors
5. Fix all breaking changes
6. Run all tests and validate
7. **Single commit**: `git commit -m "feat: Upgrade solution to .NET 10.0"`

**Advantages**:
- Clean atomic operation
- Simple rollback if issues discovered (single `git reset`)
- Clear history (one commit = one upgrade)
- Matches All-At-Once execution model

**Alternative: Phased Commits** (If Needed):

If single commit becomes unwieldy, use phase-based commits:

1. `git commit -m "feat: Update project files to net10.0"`
   - All `<TargetFramework>` updates

2. `git commit -m "feat: Update package references to net10.0-compatible versions"`
   - All package version updates
   - Remove incompatible package

3. `git commit -m "fix: Address compilation errors from .NET 10 upgrade"`
   - All breaking change fixes
   - TimeSpan API updates
   - GDI+ migration (if applicable)
   - Other API compatibility fixes

4. `git commit -m "fix: Update tests for .NET 10 compatibility"`
   - Test assertion updates
   - Test framework adjustments

**Use phased commits only if**:
- Need to checkpoint progress during extended debugging
- Want to isolate specific types of changes for review
- Single commit becomes too large (>1000 files changed)

### Commit Message Format

Follow conventional commits format:

```
<type>: <description>

<body>

<footer>
```

**Types**:
- `feat`: Framework/package version updates
- `fix`: Breaking change fixes, compilation error fixes
- `test`: Test updates
- `chore`: Tooling, configuration updates

**Example Commit Messages**:

**Single Atomic Commit**:
```
feat: Upgrade solution to .NET 10.0

- Update all 14 projects from net9.0 to net10.0
- Upgrade 26 NuGet packages to net10.0-compatible versions
- Remove incompatible Microsoft.VisualStudio.Azure.Containers.Tools.Targets package
- Fix TimeSpan API compatibility (double parameter required)
- Address GDI+ breaking changes in Sky.Editor
- Update test assertions for .NET 10 behavior
- All tests passing (527 issues resolved)

BREAKING CHANGE: Minimum supported framework is now .NET 10.0

Closes #<issue-number>
```

**Phased Commit Example 1**:
```
feat: Update project target frameworks to net10.0

Update all 14 project files:
- AspNetCore.Identity.FlexDb
- Cosmos.MicrosoftGraph
- Cosmos.DynamicConfig
- Cosmos.BlobService
- Cosmos.Common
- Cosmos.EmailServices
- Sky.Shared.Razor
- Sky.TestSetup
- Sky.Cms.Api.Shared
- Sky.Publisher
- Sky.Editor
- AspNetCore.Identity.FlexDb.Tests
- Cosmos.Common.Tests
- Sky.Tests

Part of .NET 10.0 upgrade initiative.
```

**Phased Commit Example 2**:
```
fix: Address .NET 10 breaking changes

- TimeSpan API: Use double literals (FromSeconds/FromMinutes)
- Remove incompatible Docker tooling package from Sky.Editor
- Update EF Core query patterns for net10.0
- Fix API signature changes in Cosmos.Common
- Address GDI+ compatibility in Sky.Editor (81 instances)

Part of .NET 10.0 upgrade initiative.
```

### Review and Merge Process

**Pull Request Requirements**:

Create PR from `upgrade-to-NET10` to `main`:

**PR Title**: `Upgrade SkyCMS to .NET 10.0`

**PR Description Template**:
```markdown
## Upgrade Summary
Upgrade entire SkyCMS solution from .NET 9.0 to .NET 10.0 (LTS)

## Scope
- **Projects Updated**: 14 projects
- **Packages Updated**: 26 packages
- **Issues Resolved**: 527 (112 mandatory, 410 potential, 5 optional)
- **Strategy**: All-At-Once (atomic upgrade)

## Key Changes
- [ ] All project files updated to `net10.0`
- [ ] All Microsoft packages upgraded to 10.0.5
- [ ] Incompatible Docker tooling package removed
- [ ] TimeSpan API compatibility fixes applied
- [ ] GDI+ breaking changes addressed (Sky.Editor)
- [ ] Test assertions updated for .NET 10 behavior

## Testing Performed
- [x] Solution builds without errors
- [x] Solution builds without warnings
- [x] All unit tests pass (AspNetCore.Identity.FlexDb.Tests, Cosmos.Common.Tests)
- [x] All integration tests pass (Sky.Tests)
- [x] Cross-provider database validation (Cosmos, SQL Server, MySQL, SQLite)
- [x] Authentication flows tested (Google, Microsoft Account)
- [x] Multi-tenant features validated
- [x] Docker containerization functional
- [x] Performance baseline acceptable

## Breaking Changes
- Minimum supported framework: .NET 10.0
- System.Drawing usage updated for Windows-only compatibility
- Docker tooling now relies on SDK instead of incompatible package

## Migration Notes
- No database migrations required
- No configuration changes required
- Docker compose files unchanged
- CI/CD pipelines may need .NET 10 SDK updates

## Rollback Plan
Single commit (or few commits) makes rollback straightforward:
`git reset --hard <commit-before-upgrade>`

## Related Issues
Closes #<issue-number>

## Checklist
- [x] All projects build successfully
- [x] All tests pass
- [x] Breaking changes documented
- [x] Performance validated
- [x] Security implications reviewed
- [x] Documentation updated (if applicable)
```

**Review Checklist**:
- [ ] All project files updated consistently
- [ ] Package versions correct (10.0.5 for Microsoft packages)
- [ ] Breaking changes addressed comprehensively
- [ ] No hard-coded .NET 9 references remaining
- [ ] Test coverage maintained
- [ ] CI/CD pipeline considerations noted
- [ ] Deployment plan reviewed

**Merge Criteria**:
- [ ] All automated checks pass (build, tests, linting)
- [ ] Code review approved by senior .NET developer
- [ ] QA validation complete
- [ ] Performance validated (no significant regressions)
- [ ] Documentation updated
- [ ] Stakeholders informed

### Post-Merge Actions

1. **Tag Release**:
   ```bash
   git tag -a v1.0.0-net10 -m "Upgrade to .NET 10.0"
   git push origin v1.0.0-net10
   ```

2. **Update CI/CD**:
   - Update GitHub Actions workflows to use .NET 10 SDK
   - Update Docker base images to .NET 10
   - Verify deployment pipelines functional

3. **Monitor Deployment**:
   - Watch application logs for errors
   - Monitor performance metrics
   - Validate production functionality

4. **Communication**:
   - Notify team of successful upgrade
   - Document any lessons learned
   - Update project README with .NET 10 requirement

### Rollback Procedure

**If Critical Issues Discovered Post-Merge**:

```bash
# Create rollback branch
git checkout -b rollback-net10 main

# Revert the upgrade commit(s)
git revert <upgrade-commit-sha>

# Or hard reset if revert is complex
git reset --hard <commit-before-upgrade>
git push --force-with-lease origin rollback-net10

# Create PR to merge rollback
# Investigate issues
# Re-attempt upgrade with fixes
```

**Rollback Criteria**:
- Blocking production issues
- Critical performance regressions (>30%)
- Data integrity concerns
- Security vulnerabilities introduced

---

## Success Criteria

### Technical Criteria

#### All Projects Migrated
- [x] **14/14 projects** target `net10.0`
- [x] No projects remaining on `net9.0`
- [x] All project files use `<TargetFramework>net10.0</TargetFramework>`

**Verification**:
```bash
grep -r "TargetFramework>net9.0" --include="*.csproj" .
# Expected output: (no matches)
```

#### All Packages Updated
- [x] **26 packages** upgraded to net10.0-compatible versions
- [x] All Microsoft.* packages at version 10.0.5
- [x] Incompatible package removed (Microsoft.VisualStudio.Azure.Containers.Tools.Targets)
- [x] Deprecated packages addressed (Azure.Identity, Azure.Monitor.Query, SQLitePCLRaw.bundle_e_sqlcipher)

**Verification**:
```bash
# No Microsoft packages at version 9.x
grep -r 'Microsoft.*" Version="9' --include="*.csproj" .
# Expected output: (no matches)

# Incompatible package removed
grep -r 'Microsoft.VisualStudio.Azure.Containers.Tools.Targets' --include="*.csproj" .
# Expected output: (no matches)
```

#### Solution Builds Successfully
- [x] `dotnet restore SkyCMS.sln` succeeds
- [x] `dotnet build SkyCMS.sln` succeeds with **0 errors**
- [x] `dotnet build SkyCMS.sln` produces **0 warnings** (or acceptable warnings documented)

**Verification**:
```bash
dotnet clean SkyCMS.sln
dotnet restore SkyCMS.sln
dotnet build SkyCMS.sln --no-restore
# Expected: Build succeeded. 0 Error(s). 0 Warning(s).
```

#### All Tests Pass
- [x] **AspNetCore.Identity.FlexDb.Tests**: All tests pass
- [x] **Cosmos.Common.Tests**: All tests pass
- [x] **Sky.Tests**: All tests pass
- [x] Test coverage maintained (no tests skipped or removed)

**Verification**:
```bash
dotnet test SkyCMS.sln --no-build
# Expected: Test Run Successful. Total tests: X, Passed: X, Failed: 0, Skipped: 0
```

#### No Package Dependency Conflicts
- [x] `dotnet restore` resolves all dependencies without conflicts
- [x] No version mismatch warnings
- [x] All transitive dependencies compatible with net10.0

**Verification**:
```bash
dotnet list package --include-transitive | grep -i "warning\|conflict"
# Expected output: (no matches)
```

#### No Security Vulnerabilities
- [x] No new security vulnerabilities introduced
- [x] Deprecated packages with security implications addressed
- [x] `dotnet list package --vulnerable` shows no vulnerabilities

**Verification**:
```bash
dotnet list package --vulnerable
# Expected: No vulnerable packages found
```

### Quality Criteria

#### Code Quality Maintained
- [x] StyleCop analyzer rules pass (or acceptable suppressions documented)
- [x] No new code analysis warnings introduced
- [x] Existing code quality standards maintained

**Verification**:
```bash
# Build with full analyzer output
dotnet build SkyCMS.sln -p:EnforceCodeStyleInBuild=true
# Expected: No new SA* warnings (StyleCop) or other analyzer warnings
```

#### Test Coverage Maintained
- [x] Unit test coverage ≥ baseline (pre-upgrade)
- [x] Integration test coverage ≥ baseline
- [x] Critical paths validated (authentication, database, multi-tenant)

**Verification**:
```bash
dotnet test SkyCMS.sln --collect:"XPlat Code Coverage"
# Compare coverage report to baseline
```

#### Documentation Updated
- [x] README.md reflects .NET 10.0 requirement
- [x] Breaking changes documented (if public library)
- [x] Upgrade plan document complete
- [x] Lessons learned captured

### Process Criteria

#### All-At-Once Strategy Followed
- [x] All projects upgraded simultaneously (atomic operation)
- [x] No intermediate multi-targeting states
- [x] Single coordinated build/fix/verify cycle completed
- [x] Dependency-ordered validation sequence followed

#### Breaking Changes Addressed
- [x] **TimeSpan API**: All `FromSeconds`/`FromMinutes` calls updated to use `double`
- [x] **GDI+ Migration**: Sky.Editor System.Drawing usage validated/migrated (81 instances)
- [x] **Incompatible Package**: Docker tooling package removed, containerization verified
- [x] **EF Core Changes**: Database operations validated across all providers
- [x] **ASP.NET Core Changes**: Authentication, Razor Pages, middleware functional

#### Cross-Provider Compatibility (FlexDb Architecture)
- [x] **Cosmos DB**: Connection, queries, identity operations functional
- [x] **SQL Server**: Connection, migrations, identity operations functional
- [x] **MySQL**: Pomelo provider functional, queries work
- [x] **SQLite**: In-memory and file-based operations functional
- [x] No provider-specific breaking changes

### Functional Criteria

#### Sky.Editor (High-Risk Project)
- [x] Application builds and runs
- [x] Razor Pages render correctly
- [x] Visual editor functional (iframe, autosave, content regions)
- [x] Image upload/processing works (GDI+ migration successful)
- [x] Authentication providers functional (Google, Microsoft Account)
- [x] Multi-tenant features work (cookie domain, tenant resolution)
- [x] Rate limiting functional (contact-form policy)
- [x] Docker containerization works (incompatible package removal verified)

#### Sky.Publisher
- [x] Application builds and runs
- [x] Publishing workflows functional
- [x] Sitemap generation works
- [x] Microsoft Graph integration preserved

#### Sky.Cms.Api.Shared
- [x] API contracts preserved
- [x] Authorization policies functional

#### Multi-Tenant Architecture Preserved
- [x] **IDynamicConfigurationProvider**: Tenant resolution functional
- [x] **Header Priority**: x-origin-hostname preferred over Host header
- [x] **Cookie Isolation**: CookieDomain claims enforced
- [x] **DomainMiddleware**: Tenant context established early
- [x] **Settings Queries**: Filtered by tenant domain
- [x] **Antiforgery Tokens**: Scoped per HttpContext (per-tenant)
- [x] **IApplicationDbContext**: Multi-tenant data isolation functional

#### Performance Acceptable
- [x] Application startup time ≤ baseline + 10%
- [x] API response time ≤ baseline + 10%
- [x] Database query performance ≥ baseline - 10%
- [x] Test suite execution time acceptable
- [x] No memory leaks detected

**Verification**:
- Collect baseline metrics from .NET 9.0
- Run performance benchmarks on .NET 10.0
- Compare and document results

### Deployment Criteria

#### CI/CD Pipeline Updated
- [x] GitHub Actions workflows use .NET 10 SDK
- [x] Docker base images updated to .NET 10
- [x] NuGet packaging updated (if publishing packages)
- [x] All automated checks pass

**Verification**:
- Review `.github/workflows/*.yml` for SDK version
- Review Dockerfile for base image version
- Trigger CI build and validate success

#### Container Build Successful
- [x] Docker image builds: `docker build -t skycms-editor -f Editor/Dockerfile .`
- [x] Container runs: `docker run -p 8080:80 skycms-editor`
- [x] Application responds to requests in container
- [x] docker-compose.yml functional

**Verification**:
```bash
docker build -t skycms-editor -f Editor/Dockerfile .
docker run -d -p 8080:80 --name test-editor skycms-editor
curl http://localhost:8080
# Expected: HTTP 200 response
docker stop test-editor && docker rm test-editor
```

### Acceptance Criteria Summary

**The .NET 10.0 upgrade is COMPLETE when**:

✅ **Technical**:
- All 14 projects target net10.0
- All 26 packages updated
- Solution builds with 0 errors, 0 warnings
- All tests pass
- No security vulnerabilities

✅ **Quality**:
- Code quality maintained
- Test coverage maintained
- Documentation updated

✅ **Functional**:
- Sky.Editor fully functional (high-risk project validated)
- Sky.Publisher functional
- Multi-tenant architecture preserved
- All database providers functional
- Authentication flows work
- Performance acceptable

✅ **Process**:
- All-At-Once strategy followed
- Breaking changes addressed
- Cross-provider compatibility validated

✅ **Deployment**:
- CI/CD pipeline updated
- Docker containerization functional
- Ready for production deployment

### Sign-Off Checklist

**Before declaring upgrade complete**:

- [ ] All technical criteria met
- [ ] All quality criteria met
- [ ] All functional criteria met
- [ ] All process criteria met
- [ ] All deployment criteria met
- [ ] Senior .NET developer review complete
- [ ] QA validation complete
- [ ] Stakeholder approval obtained
- [ ] Production deployment plan reviewed
- [ ] Rollback plan documented and understood

### Final Verification Command

**One-Step Verification**:
```bash
#!/bin/bash
# verify-net10-upgrade.sh

echo "=== .NET 10.0 Upgrade Verification ==="

echo "1. Checking project target frameworks..."
if grep -r "TargetFramework>net9.0" --include="*.csproj" .; then
    echo "❌ FAIL: Projects still targeting net9.0"
    exit 1
else
    echo "✅ PASS: All projects on net10.0"
fi

echo "2. Restoring packages..."
dotnet restore SkyCMS.sln || { echo "❌ FAIL: Restore failed"; exit 1; }
echo "✅ PASS: Restore successful"

echo "3. Building solution..."
dotnet build SkyCMS.sln --no-restore || { echo "❌ FAIL: Build failed"; exit 1; }
echo "✅ PASS: Build successful"

echo "4. Running tests..."
dotnet test SkyCMS.sln --no-build || { echo "❌ FAIL: Tests failed"; exit 1; }
echo "✅ PASS: All tests passed"

echo "5. Checking for vulnerabilities..."
dotnet list package --vulnerable | grep -i "has the following vulnerable" && { echo "❌ FAIL: Vulnerabilities found"; exit 1; }
echo "✅ PASS: No vulnerabilities"

echo ""
echo "🎉 SUCCESS: .NET 10.0 upgrade complete and verified!"
echo ""
```

**Usage**:
```bash
chmod +x verify-net10-upgrade.sh
./verify-net10-upgrade.sh
```

---

## Plan Complete

This plan provides comprehensive guidance for upgrading SkyCMS from .NET 9.0 to .NET 10.0 using the All-At-Once strategy. All 14 projects will be upgraded simultaneously in a single coordinated operation, with validation proceeding in dependency order from foundation libraries to applications and tests.

**Key Highlights**:
- **Strategy**: All-At-Once (atomic upgrade)
- **Scope**: 14 projects, 527 issues, 26 package updates
- **High-Risk Area**: Sky.Editor (227 issues including 81 GDI+ instances)
- **Critical**: Incompatible Docker tooling package removal
- **Validation**: Multi-level testing with cross-provider database validation

**Ready for Execution Stage**: This plan can now be transformed into actionable tasks for systematic execution.
