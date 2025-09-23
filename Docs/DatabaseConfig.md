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

---

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

SQLite uses a file on disk. In containers, place the database on a persistent volume so it survives image updates. A common mount path is `/data/sqlite`.

Example connection string:

```json
{
    "ConnectionStrings": {
        "ApplicationDbContextConnection": "Data Source=/data/sqlite/skycms.db;"
    }
}
```

> Tip: For local Windows dev, you can use `Data Source=skycms.db;` (stored in the app’s working directory).

---

## Which database should I use?

Use what your team already knows when possible. Quick guidance:

| Provider          | Best for                                   | Pros                                                     | Considerations |
|-------------------|---------------------------------------------|----------------------------------------------------------|----------------|
| SQLite            | Single editor, lowest cost, quick start     | Zero setup, file-based, simplest deploy                  | Single-writer scenarios, ensure persistent volume in containers |
| MS SQL / Azure SQL| Teams, enterprise features, ecosystem tools | Familiar SQL, backups, point-in-time restore, tooling    | Requires managed instance/server; licensing/costs vary |
| MySQL             | LAMP stacks, cross-platform hosting         | Ubiquitous, cost-effective, broad hosting support        | Feature set differs from SQL Server; ensure provider parity |
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
    - Contains `Server=` or `Data Source=` → SQL Server or SQLite
    - Contains `server=` (lowercase) → MySQL
- Ensure the connection key is `ConnectionStrings:ApplicationDbContextConnection`.
- For TLS/SSL errors on dev SQL Server, set `TrustServerCertificate=True` (development only).
- For SQLite in containers, verify the file path points to a mounted, writable volume.
