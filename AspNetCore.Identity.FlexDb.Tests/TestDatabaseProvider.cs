namespace AspNetCore.Identity.CosmosDb.Tests.Net9
{
    /// <summary>
    /// Supported database providers for testing
    /// </summary>
    public enum DatabaseProvider
    {
        CosmosDb,
        SqlServer,
        MySql,
        Sqlite
    }

    /// <summary>
    /// Holds information about a test database provider
    /// </summary>
    public class TestDatabaseProvider
    {
        public DatabaseProvider Provider { get; set; }
        public string ConnectionString { get; set; }
        public string DisplayName { get; set; }

        public TestDatabaseProvider(DatabaseProvider provider, string connectionString)
        {
            Provider = provider;
            ConnectionString = connectionString;
            DisplayName = provider.ToString();
        }

        public override string ToString() => DisplayName;
    }
}
