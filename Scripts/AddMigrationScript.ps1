Add-Migration BlogEntities -Project Cosmos.Common -StartupProject Sky.Editor -Context ApplicationDbContext
Update-Database -Project Cosmos.Common -StartupProject Sky.Editor -Context ApplicationDbContext
