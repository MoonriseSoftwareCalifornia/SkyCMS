# Cosmos.* Projects — Overview

Purpose
- Documents the purpose and quick checks for all `Cosmos.*` projects (storage, connection strings, and Cloud-specific helpers).

Which projects
- Look for projects prefixed with `Cosmos.` (examples: `Cosmos.ConnectionStrings`, `Cosmos.BlobService`, `Cosmos.Common`, `Cosmos.EmailServices`).

Quick checks before editing
- Inspect `Cosmos.ConnectionStrings` for how connection strings are loaded and propagated.
- Search for client creation and singleton lifetimes; prefer reusing existing `CosmosClient` patterns rather than creating new clients per request.

Local development notes
- Use the Azure Cosmos DB Emulator for local testing. Default emulator endpoint:
  - `https://localhost:8081/` with the emulator key.
- When running locally, set the connection string to the emulator or supply a suitable test container.

Safety
- Changes to storage behavior can affect data integrity and tenant isolation. Ask for approval before modifying migrations, data models, or request-wide partitioning strategies.

Where to look
- Search for `Cosmos.ConnectionStrings`, `Cosmos.*` project folders, and helper classes in `Common`.
