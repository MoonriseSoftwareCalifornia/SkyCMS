# Tenant Administration Web App Execution Plan

## Purpose
Create a separate, security-focused tenant administration web app for SkyCMS. The new app will use its own ASP.NET Core Identity boundary, its own DNS name, and the existing dynamic configuration provider for tenant registry access.

## Decisions Confirmed
- Separate web app, not an area inside `Sky.Editor`.
- Entra ID is the primary authentication method.
- A local break-glass admin account may be added later if required.
- Tenant secrets remain in the tenant/config database and rely on platform/database encryption at rest for now.
- Change history will be simple and lightweight.
- The admin app will have its own DNS name.

## Solution-Level Impact
### New project to add
- `TenantAdmin/Sky.TenantAdmin.csproj`

### Existing projects to modify
- `SkyCMS.sln`
- `Cosmos.ConnectionStrings/Cosmos.DynamicConfig.csproj`
- `Cosmos.ConnectionStrings/Connection.cs`
- `Cosmos.ConnectionStrings/DynamicConfigDbContext.cs`
- `Cosmos.ConnectionStrings/DynamicConfigurationProvider.cs`
- `Editor/Program.cs` only if a small compatibility change is required for shared tenant registry behavior
- `Tests/Sky.Tests.csproj` or a new tenant-admin test project if one is introduced

### Files expected in the new app
- `TenantAdmin/Program.cs`
- `TenantAdmin/appsettings.json`
- `TenantAdmin/appsettings.Development.json`
- `TenantAdmin/Areas/Identity/Pages/Account/Login.cshtml`
- `TenantAdmin/Areas/Identity/Pages/Account/Login.cshtml.cs`
- `TenantAdmin/Areas/Identity/Pages/Account/Logout.cshtml`
- `TenantAdmin/Areas/Identity/Pages/Account/Logout.cshtml.cs`
- `TenantAdmin/Pages/Index.cshtml`
- `TenantAdmin/Pages/Index.cshtml.cs`
- `TenantAdmin/Pages/Tenants/Index.cshtml`
- `TenantAdmin/Pages/Tenants/Index.cshtml.cs`
- `TenantAdmin/Pages/Tenants/Edit.cshtml`
- `TenantAdmin/Pages/Tenants/Edit.cshtml.cs`
- `TenantAdmin/Pages/Tenants/Create.cshtml`
- `TenantAdmin/Pages/Tenants/Create.cshtml.cs`
- `TenantAdmin/Pages/Tenants/Details.cshtml`
- `TenantAdmin/Pages/Tenants/Details.cshtml.cs`
- `TenantAdmin/Pages/Tenants/History.cshtml`
- `TenantAdmin/Pages/Tenants/History.cshtml.cs`
- `TenantAdmin/Services/ITenantRegistryService.cs`
- `TenantAdmin/Services/TenantRegistryService.cs`
- `TenantAdmin/Services/ITenantAuditService.cs`
- `TenantAdmin/Services/TenantAuditService.cs`
- `TenantAdmin/Data/TenantAdminDbContext.cs`
- `TenantAdmin/Data/Identity/` related Identity scaffolding as needed

## Step-by-Step Plan

### Step 1: Define the security boundary and solution shape
**Goal:** lock down what the new app owns and how it authenticates.

**Target files**
- `Docs/TenantAdminExecutionPlan.md`
- `SkyCMS.sln`
- `TenantAdmin/Sky.TenantAdmin.csproj`

**Work**
- Confirm the new app is a separate Razor Pages app.
- Confirm Entra ID as the primary login path.
- Confirm its own DNS name and cookie boundary.
- Add the new project to the solution.

**Done when**
- The solution contains a clearly named `Sky.TenantAdmin` project entry.
- The security boundary is documented and agreed.

### Step 2: Review the current tenant registry model
**Goal:** verify what the config database already stores and what must be added.

**Target files**
- `Cosmos.ConnectionStrings/Connection.cs`
- `Cosmos.ConnectionStrings/DynamicConfigDbContext.cs`
- `Cosmos.ConnectionStrings/DynamicConfigurationProvider.cs`

**Work**
- Inspect the existing `Connection` entity fields.
- Confirm read/write paths for tenant data.
- Identify where simple change history can be stored.

**Done when**
- The required tenant registry fields are mapped.
- Any missing lifecycle or audit requirements are recorded.

### Step 3: Scaffold the new Razor Pages admin app
**Goal:** create the new app shell and basic runtime pipeline.

**Target files**
- `TenantAdmin/Program.cs`
- `TenantAdmin/appsettings.json`
- `TenantAdmin/appsettings.Development.json`
- `TenantAdmin/Pages/Index.cshtml`
- `TenantAdmin/Pages/Index.cshtml.cs`
- `TenantAdmin/wwwroot/**`

**Work**
- Create the project and wire up Razor Pages.
- Add static files, layout, and basic navigation.
- Set the app name and placeholder landing page.

**Done when**
- The app builds and serves a home page locally.

### Step 4: Add isolated admin authentication
**Goal:** make the admin app sign in separately from `Sky.Editor`.

**Target files**
- `TenantAdmin/Program.cs`
- `TenantAdmin/Areas/Identity/Pages/Account/Login.cshtml`
- `TenantAdmin/Areas/Identity/Pages/Account/Login.cshtml.cs`
- `TenantAdmin/Areas/Identity/Pages/Account/Logout.cshtml`
- `TenantAdmin/Areas/Identity/Pages/Account/Logout.cshtml.cs`
- `TenantAdmin/Data/Identity/**`

**Work**
- Configure ASP.NET Core Identity in the new app.
- Set Entra ID as the primary sign-in option.
- Add authorization policies for admin-only pages.
- Reserve a local fallback account pattern only if later needed.

**Done when**
- The admin app has its own login flow and cookie.

### Step 5: Build the tenant registry service layer
**Goal:** encapsulate tenant CRUD and validation logic behind services.

**Target files**
- `TenantAdmin/Services/ITenantRegistryService.cs`
- `TenantAdmin/Services/TenantRegistryService.cs`
- `TenantAdmin/Services/ITenantAuditService.cs`
- `TenantAdmin/Services/TenantAuditService.cs`
- `TenantAdmin/Data/TenantAdminDbContext.cs`
- `Cosmos.ConnectionStrings/Connection.cs`

**Work**
- Add methods for list, create, edit, disable, and validation operations.
- Keep secret storage aligned with DB/platform encryption-at-rest only.
- Add a minimal audit/history write path.

**Done when**
- Registry operations are available through services rather than page code.

### Step 6: Add tenant management Razor Pages
**Goal:** provide the admin UI for registry operations.

**Target files**
- `TenantAdmin/Pages/Tenants/Index.cshtml`
- `TenantAdmin/Pages/Tenants/Index.cshtml.cs`
- `TenantAdmin/Pages/Tenants/Create.cshtml`
- `TenantAdmin/Pages/Tenants/Create.cshtml.cs`
- `TenantAdmin/Pages/Tenants/Edit.cshtml`
- `TenantAdmin/Pages/Tenants/Edit.cshtml.cs`
- `TenantAdmin/Pages/Tenants/Details.cshtml`
- `TenantAdmin/Pages/Tenants/Details.cshtml.cs`
- `TenantAdmin/Pages/Tenants/History.cshtml`
- `TenantAdmin/Pages/Tenants/History.cshtml.cs`

**Work**
- Build list/detail/edit/create screens.
- Add anti-forgery and confirmation flows.
- Add a simple history page for changes.

**Done when**
- An admin can manage tenants end-to-end from Razor Pages.

### Step 7: Integrate with the shared dynamic configuration provider
**Goal:** keep `Sky.Editor` reading tenant data from the same registry.

**Target files**
- `Cosmos.ConnectionStrings/DynamicConfigurationProvider.cs`
- `Cosmos.ConnectionStrings/DynamicConfigDbContext.cs`
- `Editor/Program.cs` only if needed

**Work**
- Reuse the existing config DB access pattern.
- Keep the editor tenant resolution path stable.
- Add cache invalidation or reload behavior only if the registry changes require it.

**Done when**
- The editor still resolves tenant connections without breaking changes.

### Step 8: Add validation and regression tests
**Goal:** verify admin auth, registry operations, and editor compatibility.

**Target files**
- `Tests/Sky.Tests.csproj` or a new tenant admin test project
- `Tests/DynamicConfig/**`
- `Tests/Controllers/**` or new tenant-admin tests
- `TenantAdmin/**` test coverage as needed

**Work**
- Add tests for auth, CRUD, history, and tenant validation.
- Add regression tests for the editor's tenant lookup behavior.
- Validate the solution builds cleanly.

**Done when**
- Relevant tests pass and the solution builds.

## Progress Tracker
- [ ] Step 1: Define the security boundary and solution shape
- [ ] Step 2: Review the current tenant registry model
- [ ] Step 3: Scaffold the new Razor Pages admin app
- [ ] Step 4: Add isolated admin authentication
- [ ] Step 5: Build the tenant registry service layer
- [ ] Step 6: Add tenant management Razor Pages
- [ ] Step 7: Integrate with the shared dynamic configuration provider
- [ ] Step 8: Add validation and regression tests

## Notes
- Keep the admin app isolated from tenant content hosting.
- Prefer Razor Pages for the admin UI to match the existing workspace direction.
- Keep the change set minimal and security-first.
