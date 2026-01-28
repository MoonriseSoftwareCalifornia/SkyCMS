---
title: SQLite Database Configuration
description: Configure SQLite file-based database for development and testing
keywords: SQLite, database, file-based, development, testing, configuration
audience: [developers]
---

# SQLite with SkyCMS

SQLite is a lightweight, file-based SQL database ideal for **development, testing, and small single-instance deployments**. No server required—the database is a single file on disk.

## When to use SQLite

✓ **Good for:**
- Local development
- Testing and prototyping
- Small single-instance deployments (low traffic)
- Docker containers where persistence isn't critical

✗ **Not recommended for:**
- Production with multiple application instances
- High concurrent load
- Distributed deployments
- Scenarios requiring strong multi-writer isolation

## Values you need

- **Database file path** (optional; defaults to current directory)

## Configure in SkyCMS

### Simple Configuration (default file location)

```json
{
  "ConnectionStrings": {
    "ApplicationDbContextConnection": "Data Source=skycms.db"
  }
}
```

This creates `skycms.db` in the current working directory.

### Custom File Path

```json
{
  "ConnectionStrings": {
    "ApplicationDbContextConnection": "Data Source=/app/data/skycms.db"
  }
}
```

### Docker Example

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app
COPY --from=builder /app/publish .

# Ensure data directory exists
RUN mkdir -p /app/data

ENV ConnectionStrings__ApplicationDbContextConnection="Data Source=/app/data/skycms.db"

ENTRYPOINT ["dotnet", "Sky.Editor.dll"]
```

### Environment Variables

```powershell
$env:ConnectionStrings__ApplicationDbContextConnection = "Data Source=/app/data/skycms.db"
```

## In-memory examples (ephemeral)

If you need a temporary, process-local database (typically for automated tests or short-lived development runs), use an in-memory SQLite connection string. Note these databases do not persist to disk and are lost when the process exits.

```json
{
  "ConnectionStrings": {
    "ApplicationDbContextConnection": "Data Source=:memory:"
  }
}
```

Shared in-memory databases (multiple connections in the same process) can use a URI-style filename with `mode=memory` and `cache=shared`:

```json
{
  "ConnectionStrings": {
    "ApplicationDbContextConnection": "Data Source=file:memdb1?mode=memory&cache=shared"
  }
}
```

## Best practices

- **Backup the .db file** regularly if using SQLite in production (small deployment).
- **Use persistent volumes** in Docker/Kubernetes to preserve the database file across container restarts.
- **Single instance only**: SQLite is not suitable for multi-instance deployments (horizontal scaling).
- **Monitor file size**: SQLite files can grow; ensure sufficient disk space.
- **No concurrent writes**: If you need high concurrency, migrate to a centralized database (SQL Server, MySQL, Cosmos DB).

**Note — In-memory usage:** Only use in-memory SQLite for testing and ephemeral development scenarios. In-memory databases do not persist across process or container restarts and are not suitable for any workload that requires durable storage.

## Tips and troubleshooting

- SQLite connection strings always start with `Data Source=`.
- If you see "database is locked" errors, reduce concurrent writers or switch to a centralized database.
- SQLite stores everything in a single file; make sure the application has read/write permissions to the directory.
- For development, you can inspect the SQLite database with tools like **DB Browser for SQLite** or **sqlitebrowser**.

## Migrating from SQLite to production database

When you outgrow SQLite:

1. Create a target database (Azure SQL, MySQL, or Cosmos DB).
2. Update the connection string in SkyCMS.
3. Run SkyCMS migrations: `dotnet ef database update` (Entity Framework handles schema creation).
4. SkyCMS will populate the new database on first run.
5. Optionally, export data from SQLite and import into the new database if manual migration is needed.
