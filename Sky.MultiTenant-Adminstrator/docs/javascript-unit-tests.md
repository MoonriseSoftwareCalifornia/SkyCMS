# Running JavaScript Unit Tests for the Database Connection Builder

## Purpose

This document explains how to run the JavaScript unit tests that cover the database connection builder widget used by `Sky.MultiTenant-Adminstrator`.

## Test Workspace

The JavaScript tests live in the existing repository-level Jest workspace:

- Jest workspace: `JestTests`
- Test file: `JestTests/tests/unit/administrator/db-connection-builder.test.js`

The widget code under test lives here:

- `Sky.MultiTenant-Adminstrator/wwwroot/js/site.js`

## One-Time Setup

Before running Jest tests on a machine that has not restored the test workspace yet, install the existing test dependencies.

From the repository root:

```powershell
Set-Location .\JestTests
npm ci
```

This restores the packages defined in `JestTests/package-lock.json`.

## Run Only the Database Connection Builder Tests

This is the recommended command while working on the widget:

```powershell
Set-Location .\JestTests
npx jest tests/unit/administrator/db-connection-builder.test.js --runInBand
```

Why this command is preferred:

- it runs only the new widget test file
- it avoids unrelated existing Jest suites
- it gives fast feedback while editing `site.js`

## Run the Broader Jest Unit Test Set

If you want to run the Jest unit suite configured by the workspace scripts:

```powershell
Set-Location .\JestTests
npm test
```

Important note:

- `npm test` uses the workspace script configured in `JestTests/package.json`
- that script can include unrelated unit suites in the repository
- some unrelated suites may fail even when the database connection builder tests pass

Because of that, use the targeted `npx jest ...db-connection-builder.test.js` command for widget-focused validation.

## What the Widget Tests Cover

Current coverage includes:

- provider detection
- parsing connection strings into form fields
- preserving extra unmapped connection string segments
- rebuilding normalized connection strings
- provider-specific validation rules
- populating the correct modal form when editing an existing value
- saving a generated connection string back into `DbConn`
- clearing an existing value and saving an empty string

## How the Tests Work

The tests use:

- Jest
- jsdom
- a lightweight Bootstrap modal stub
- a test DOM that mirrors the widget's required modal structure

The widget exports a small API from `site.js` through `module.exports`, which allows the tests to call helper functions directly.

## Typical Developer Workflow

1. Edit `Sky.MultiTenant-Adminstrator/wwwroot/js/site.js`
2. If needed, update `Views/Connections/_DatabaseConnectionBuilder.cshtml`
3. Run the targeted widget suite:

```powershell
Set-Location .\JestTests
npx jest tests/unit/administrator/db-connection-builder.test.js --runInBand
```

4. Run a solution build:

```powershell
dotnet build .\SkyCMS.sln
```

## Adding More Tests

When extending the widget, prefer adding tests for both:

- pure helper logic
- DOM interaction behavior

Good candidates for additional tests:

- SQL Server authentication mode toggling
- unsupported connection string behavior
- Cosmos DB parsing with `AccessToken`
- preservation of additional provider-specific segments
- changing provider after Clear

## Troubleshooting

### `'jest' is not recognized`
The Jest workspace dependencies have not been restored yet.

Run:

```powershell
Set-Location .\JestTests
npm ci
```

### Targeted test passes, but `npm test` fails
That usually means another existing Jest suite failed outside the widget area.

To validate only this widget, run:

```powershell
Set-Location .\JestTests
npx jest tests/unit/administrator/db-connection-builder.test.js --runInBand
```

### DOM-related failures after markup changes
The tests depend on the same ids and `data-*` selectors used by the widget.

If modal markup changes, update both:

- `Views/Connections/_DatabaseConnectionBuilder.cshtml`
- `JestTests/tests/unit/administrator/db-connection-builder.test.js`

## Reference Files

- `JestTests/package.json`
- `JestTests/tests/setup/setup.js`
- `JestTests/tests/unit/administrator/db-connection-builder.test.js`
- `Sky.MultiTenant-Adminstrator/wwwroot/js/site.js`
- `Sky.MultiTenant-Adminstrator/Views/Connections/_DatabaseConnectionBuilder.cshtml`
