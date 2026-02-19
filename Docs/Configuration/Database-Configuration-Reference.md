---
title: Database Configuration Reference
description: Complete reference for database configuration with Cosmos DB, SQL Server, MySQL, and SQLite
keywords: database, configuration-reference, connection-strings, EF-Core, providers
audience: [developers, administrators]
version: 2.0
last_updated: "2026-01-03"
stage: stable
read_time: 7
---

# Database Configuration

SkyCMS supports Azure Cosmos DB, MS SQL (including Azure SQL), MySQL, and SQLite to store users, settings, page edits, and other CMS data. Sky automatically selects the correct EF Core provider based on the connection string configured under the key:

`ConnectionStrings:ApplicationDbContextConnection`

Use the following structure in `appsettings.json` (or environment variables/secrets):

```json
{
    "ConnectionStrings": {
        "ApplicationDbContextConnection": "<your-connection-string>"
    }
}
```

Jump to:

- [Database Configuration](#database-configuration)
  - [Azure Cosmos DB](#azure-cosmos-db)
  - [MS SQL (and Azure SQL)](#ms-sql-and-azure-sql)
  - [MySQL](#mysql)
  - [SQLite](#sqlite)
  - [Which database should I use?](#which-database-should-i-use)
  - [Security and secrets](#security-and-secrets)
  - [Troubleshooting](#troubleshooting)
  - [See Also](#see-also)

---

## When to use this
- You need exact connection string formats and provider-specific guidance for SkyCMS databases.
- You’re wiring production or CI/CD and want authoritative reference values.

## Why this matters
- Provider detection hinges on connection string shape; mistakes block startup or migrations.
- Correct strings and secrets handling reduce downtime and security risk.

## Key takeaways
- Use `ApplicationDbContextConnection` for all providers; the string determines EF provider.
- Keep secrets out of source; prefer env vars/secret stores.
- SQLite is dev/test only; use managed SQL/MySQL/Cosmos for production.

## Prerequisites
- Chosen database provider and a reachable instance; credentials with create/read/write.
- Ability to set env vars or appsettings securely.

## Quick path
1. Copy the provider format below and fill in your values.
2. Set `ConnectionStrings__ApplicationDbContextConnection` (env) or appsettings.
3. Start the app and verify provider loads; run migrations if applicable.

## Azure Cosmos DB

TL;DR connection string format:

```json
{
    "ConnectionStrings": {
        "ApplicationDbContextConnection": "AccountEndpoint=https://{account}.documents.azure.com:443/;AccountKey={key};Database={database};"
    }
}
```

How to find values in the Azure portal:

1. Open [Azure Portal](https://portal.azure.com) → search for “Cosmos DB” → select your account
2. Account name: shown on the Overview page and in the URI (e.g., `https://<account>.documents.azure.com`)
3. Account key: Cosmos DB → Keys → Primary Key (or use the Primary Connection String)
4. Database name: Cosmos DB → Data Explorer → pick your database

> Note: Store keys securely (e.g., Azure Key Vault). Rotate keys regularly.

---

## MS SQL (and Azure SQL)

Common Azure SQL format:

```json
{
    "ConnectionStrings": {
        "ApplicationDbContextConnection": "Server=tcp:{server}.database.windows.net,1433;Initial Catalog={database};Persist Security Info=False;User ID={user};Password={password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    }
}
```

Local development example (SQL Server):

```json
{
    "ConnectionStrings": {
        "ApplicationDbContextConnection": "Server=localhost;Database=SkyCMS;Trusted_Connection=True;TrustServerCertificate=True;"
    }
}
```

---

## MySQL

Typical format (default port 3306):

```json
{
    "ConnectionStrings": {
        "ApplicationDbContextConnection": "Server={server};Port=3306;Uid={user};Pwd={password};Database={database};"
    }
}
```

---

## SQLite

SQLite is ideal for development, testing, or small single-instance deployments:

```json
{
    "ConnectionStrings": {
        "ApplicationDbContextConnection": "Data Source=skycms.db"
    }
}
```

With custom path:

```json
{
    "ConnectionStrings": {
        "ApplicationDbContextConnection": "Data Source=/app/data/skycms.db"
    }
}
```

> Note: SQLite is a file-based database. For production deployments with multiple instances, use a centralized database (SQL Server, MySQL, or Cosmos DB).

---

## Which database should I use?

Use what your team already knows when possible. Quick guidance:

| Provider          | Best for                                   | Pros                                                     | Considerations |
|-------------------|---------------------------------------------|----------------------------------------------------------|----------------|
| MS SQL / Azure SQL| Teams, enterprise features, ecosystem tools | Familiar SQL, backups, point-in-time restore, tooling    | Requires managed instance/server; licensing/costs vary |
| MySQL             | LAMP stacks, cross-platform hosting         | Ubiquitous, cost-effective, broad hosting support        | Feature set differs from SQL Server; ensure provider parity |
| SQLite            | Development, testing, small single-instance | Zero configuration, serverless, file-based, portable     | Not suitable for multi-instance production deployments |
| Azure Cosmos DB   | Global scale, low-latency, elastic workloads| Serverless scale, global distribution, NoSQL flexibility | Different query model; cost control and partitioning strategy |

---

## Security and secrets

- Do not commit secrets to source control.
- Prefer environment variables, ASP.NET Core User Secrets (for local dev), or Azure Key Vault.
- Limit privileges on database credentials; rotate keys/passwords regularly.

---

## Troubleshooting

- Provider detection is based on the connection string:
    - Contains `AccountEndpoint` → Cosmos DB
    - Contains `Data Source=` with `.db` file → SQLite
    - Contains `Server=` or `Data Source=` → SQL Server
    - Contains `server=` (lowercase) → MySQL
- Ensure the connection key is `ConnectionStrings:ApplicationDbContextConnection`.
- For TLS/SSL errors on dev SQL Server, set `TrustServerCertificate=True` (development only).

---

## See Also

- **[Database Overview](./Database-Overview.md)** - Compare database options and when to use each
- **[Storage Configuration](./Storage-Overview.md)** - Configure cloud storage for static assets
- **[Configuration Overview](./README.md)** - Index of all configuration documentation
- **[LEARNING_PATHS: DevOps](../LEARNING_PATHS.md#devops--system-administrator)** - Database setup for DevOps professionals
- **[Troubleshooting Guide](../Troubleshooting.md)** - Common database issues and solutions
- **[AspNetCore.Identity.FlexDb](../Components/AspNetCore.Identity.FlexDb.md)** - Multi-database identity framework
- **[Main Documentation Hub](../index.md)** - Browse all documentation
