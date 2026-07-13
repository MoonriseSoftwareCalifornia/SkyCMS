namespace Cosmos.MultiTenant.Administrator.Controllers
{
    internal class TestResult
    {
        public bool IsDatabaseConnected { get; internal set; }
        public bool IsStorageConnected { get; internal set; }
        public string ErrorMessage { get; internal set; } = string.Empty;
    }
}