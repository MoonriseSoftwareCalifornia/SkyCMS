# Database Connection Builder Widget

## Purpose

The database connection builder is a JavaScript widget used by the `Connections/Create` and `Connections/Edit` views in `Sky.MultiTenant-Adminstrator`.

It helps administrators create, inspect, edit, and clear the `DbConn` value without manually typing a full connection string.

## Files

- UI markup: `Views/Connections/_DatabaseConnectionBuilder.cshtml`
- Launch points:
  - `Views/Connections/Create.cshtml`
  - `Views/Connections/Edit.cshtml`
- Client logic: `wwwroot/js/site.js`
- Unit tests: `JestTests/tests/unit/administrator/db-connection-builder.test.js`

## Supported Providers

The widget currently supports these FlexDb-compatible providers:

- Azure Cosmos DB
- SQL Server
- MySQL
- SQLite

## How It Is Wired

The Create and Edit views each contain:

- the raw `DbConn` input
- a `Build Connection String` button with `data-db-connection-builder`
- a `data-target-input` attribute that points to the target textbox id

The shared modal partial is rendered into both views. On page load, the widget scans for launch buttons and wires them to the shared modal.

## Markup Contract

The widget depends on specific ids and `data-*` attributes in the modal partial.

Important selectors include:

- Modal container: `#dbConnectionBuilderModal`
- Launch buttons: `[data-db-connection-builder]`
- Provider chooser buttons: `[data-provider-option]`
- Provider form containers: `[data-provider-form]`
- Validation message: `[data-db-builder-validation]`
- Unsupported-string warning: `[data-db-builder-unsupported]`
- Save button: `[data-db-builder-save]`
- Clear button: `[data-db-builder-clear]`

If those attributes are renamed, the JavaScript must be updated at the same time.

## User Flow

### New / Empty Connection String

If `DbConn` is empty when the modal opens:

1. The provider-selection screen is shown.
2. The user chooses a provider.
3. The provider-specific form is shown.
4. Save validates the required fields.
5. The widget builds the connection string and writes it back to `DbConn`.

### Existing Connection String

If `DbConn` already has a value when the modal opens:

1. The widget attempts to detect the provider.
2. The connection string is parsed into form fields.
3. The matching provider form is displayed.
4. On save, the updated string is written back to `DbConn`.

If the provider cannot be detected or parsed, the unsupported state is shown and the user can either:

- cancel and keep the original value
- clear the value and start over

### Clear Behavior

Clear does not immediately save.

Instead it:

1. resets the form state
2. returns the modal to provider selection
3. allows a different provider to be chosen

If the user then saves with no provider data, the widget shows a confirmation dialog and can write an empty string back to `DbConn`.

## Provider Detection Rules

The widget uses lightweight detection rules aligned with the supported formats:

- **Cosmos DB**: `AccountEndpoint=`
- **MySQL**: `uid=` or a MySQL-style combination using `Port=`, `Database=`, and `User ID=`
- **SQL Server**: `User ID=`, `Trusted_Connection`, or `Integrated Security`
- **SQLite**: `Data Source=` plus `:memory:`, `.db`, or `.sqlite`

## Parsing and Preservation of Extra Segments

The widget only exposes the required fields for each provider in the UI.

When editing an existing connection string, any segments not represented by the current form are preserved in `extraSegments` and appended again when saving.

Example:

- Original MySQL string: `Server=db;Port=3306;uid=user;pwd=secret;database=skycms;SslMode=Required;`
- The form edits the required fields.
- `SslMode=Required` is preserved and appended on save.

This behavior helps avoid discarding compatible provider settings that are not yet modeled by the v1 widget UI.

## Public JavaScript API

The widget exposes a small API for diagnostics and tests.

In the browser:

- `window.SkyDbConnectionBuilder`

In Jest:

- `module.exports`

Exposed members:

- `initializeDatabaseConnectionBuilder()`
- `detectProvider(connectionString)`
- `parseConnectionString(connectionString, provider)`
- `buildConnectionString(provider, fields, extraSegments)`
- `validateProviderFields(provider, fields)`

## Method Notes

### `initializeDatabaseConnectionBuilder()`
Wires the modal and buttons on the current page.

### `detectProvider(connectionString)`
Returns the widget provider key:

- `cosmos`
- `sqlserver`
- `mysql`
- `sqlite`
- `null` when no supported format is recognized

### `parseConnectionString(connectionString, provider)`
Returns an object shaped like:

```text
{
  fields: { ...provider specific values... },
  extraSegments: [ ...unmapped segments... ]
}
```

### `validateProviderFields(provider, fields)`
Returns an array of validation error messages. An empty array means validation passed.

### `buildConnectionString(provider, fields, extraSegments)`
Builds the normalized provider string and appends preserved extra segments.

## Server-Side Interaction

The widget only updates the `DbConn` textbox on the page.

The server-side Create/Edit actions still own persistence and final validation. The recent server updates allow `DbConn` to be saved as blank, which is required for the widget's clear-and-save flow.

Relevant files:

- `Models/ConnectionViewModel.cs`
- `Controllers/ConnectionsController.cs`

## Extending the Widget

To add another provider or more fields:

1. Add the form fields to `_DatabaseConnectionBuilder.cshtml`.
2. Update `providerLabels` in `site.js`.
3. Update:
   - `detectProvider`
   - `parseConnectionString`
   - `populateProviderFields`
   - `collectProviderFields`
   - `areAllFieldsBlank`
   - `validateProviderFields`
   - `buildConnectionString`
4. Add or update Jest tests.

## Limitations

Current v1 scope:

- required fields only per provider
- no advanced free-form editor inside the modal
- unsupported connection strings cannot be edited in place; they must be cleared first

## Suggested Debugging Tips

- Open browser dev tools and inspect `window.SkyDbConnectionBuilder`.
- Verify the modal markup ids and `data-*` attributes still match the script.
- If the wrong provider form opens, test the value with `detectProvider(...)` in the console.
- If a value seems to disappear, check whether it was treated as an unmapped segment or whether the provider changed after a clear.
